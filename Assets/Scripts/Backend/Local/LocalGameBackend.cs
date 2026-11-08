using System;
using System.Collections.Generic;
using System.Globalization;

namespace SlimeBlindBox.Backend.Local
{
    /// <summary>
    /// 开发阶段的客户端本地后端：在本机计算盲盒产出、随机抽取和库存，并写入本地存档。
    /// 规则与 Docs/Systems 下的系统文档一致。正式版由 PlayFab + Steam 权威后端替换，
    /// 本类为纯 C#，核心规则可直接迁移到 PlayFab CloudScript（C# Azure Functions）。
    /// 每个修改操作都在存档副本上执行，保存成功后才提交，失败时内存数据保持不变。
    /// </summary>
    public sealed class LocalGameBackend : IGameBackend
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly IPlayerDataStore store;
        private readonly IClock clock;
        private readonly IRandomSource random;
        private readonly string playerId;
        private readonly string defaultNickname;
        private readonly Action<Exception> exceptionLogger;
        private PlayerSaveData current;

        public LocalGameBackend(
            IPlayerDataStore store,
            IClock clock,
            IRandomSource random,
            bool debugToolsEnabled,
            string playerId,
            string defaultNickname,
            Action<Exception> exceptionLogger = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.playerId = playerId;
            this.defaultNickname = defaultNickname;
            this.exceptionLogger = exceptionLogger;
            SupportsDebugTools = debugToolsEnabled;
        }

        public bool SupportsDebugTools { get; }

        public void Login(Action<LoginResponse> onSuccess, Action<BackendError> onError)
        {
            try
            {
                if (GameBalanceConfig.Validate().Count > 0)
                    throw new BackendException(BackendErrorCodes.ConfigInvalid, BackendMessages.ConfigInvalid);

                var now = clock.UtcNow;
                PlayerSaveData loaded;
                try
                {
                    loaded = store.Load();
                }
                catch (Exception exception)
                {
                    exceptionLogger?.Invoke(exception);
                    throw new BackendException(BackendErrorCodes.LoadFailed, BackendMessages.LoadFailed);
                }

                var working = loaded ?? CreateNewPlayer(now);
                if (working.schemaVersion > PlayerSaveData.CurrentSchemaVersion)
                    throw new BackendException(BackendErrorCodes.SaveVersionTooNew, BackendMessages.SaveVersionTooNew);
                Migrate(working);
                Normalize(working);
                working.lastLoginAtUnixMs = ToUnixMs(now);
                Persist(working);
                onSuccess?.Invoke(new LoginResponse { user = BuildUser(working) });
            }
            catch (Exception exception)
            {
                Fail(exception, onError);
            }
        }

        public void GetGameState(Action<GameStateDto> onSuccess, Action<BackendError> onError)
        {
            Execute(data =>
            {
                var now = clock.UtcNow;
                SettleBoxes(data, now);
                return BuildGameState(data, now);
            }, onSuccess, onError);
        }

        public void ClaimBoxes(Action<ClaimBoxesResponse> onSuccess, Action<BackendError> onError)
        {
            Execute(data =>
            {
                var now = clock.UtcNow;
                SettleBoxes(data, now);
                var claimed = data.pendingBoxCount;
                if (claimed <= 0) throw new BackendException(BackendErrorCodes.NoPendingBoxes, BackendMessages.NoPendingBoxes);

                // 品质在产出时已确定，领取只是把凝聚台上看到的盲盒原样放进仓库。
                foreach (var key in data.pendingBoxRarities) GetOrCreateBoxStack(data, key).quantity += 1;
                data.pendingBoxRarities.Clear();
                data.pendingBoxCount = 0;
                data.lastBoxAtUnixMs = ToUnixMs(now);
                return new ClaimBoxesResponse { claimed = claimed, game_state = BuildGameState(data, now) };
            }, onSuccess, onError);
        }

        public void OpenBox(string boxRarityKey, Action<OpenBoxResponse> onSuccess, Action<BackendError> onError)
        {
            Execute(data =>
            {
                var now = clock.UtcNow;
                SettleBoxes(data, now);
                var box = GameBalanceConfig.FindBoxRarity(boxRarityKey);
                if (box == null) throw new BackendException(BackendErrorCodes.UnknownBoxRarity, BackendMessages.UnknownBoxRarity);

                var stack = FindBoxStack(data, box.Key);
                if (stack == null || stack.quantity <= 0)
                    throw new BackendException(BackendErrorCodes.BoxStockEmpty, string.Format(BackendMessages.BoxStockEmptyFormat, box.Name));

                var slime = PickSlime(box.Key);
                stack.quantity -= 1;
                if (data.totalBoxesOpened < int.MaxValue) data.totalBoxesOpened += 1;
                GrantSlime(data, slime.Id, 1, now);
                var rarity = GameBalanceConfig.FindSlimeRarity(slime.RarityKey);
                return new OpenBoxResponse
                {
                    slime_id = slime.Id,
                    name = slime.Name,
                    rarity = rarity.Name,
                    rarity_key = rarity.Key,
                    rarity_color = rarity.Color,
                    box_rarity_key = box.Key,
                    box_rarity = box.Name,
                    game_state = BuildGameState(data, now),
                };
            }, onSuccess, onError);
        }

        public void GetInventory(Action<InventoryResponse> onSuccess, Action<BackendError> onError)
        {
            Query(data =>
            {
                var items = new List<InventoryItemDto>();
                foreach (var record in SortedSlimes(data))
                {
                    if (record.quantity <= 0) continue;
                    var slime = GameBalanceConfig.FindSlime(record.slimeId);
                    if (slime == null) continue;
                    var rarity = GameBalanceConfig.FindSlimeRarity(slime.RarityKey);
                    items.Add(new InventoryItemDto
                    {
                        slime_id = slime.Id,
                        name = slime.Name,
                        rarity = rarity.Name,
                        rarity_key = rarity.Key,
                        rarity_color = rarity.Color,
                        base_cp = slime.BaseCp,
                        quantity = record.quantity,
                        level = record.level,
                        experience = record.experience,
                        next_level_experience = GameBalanceConfig.ExperienceForLevel(record.level),
                        unit_effective_cp = GameBalanceConfig.UnitEffectiveCp(slime.BaseCp, record.level),
                        effective_cp = GameBalanceConfig.EffectiveCp(slime.BaseCp, record.level, record.quantity),
                        level_cp_multiplier = (float)GameBalanceConfig.LevelCpMultiplier(record.level),
                        first_obtained_at = FormatTime(FromUnixMs(record.firstObtainedAtUnixMs)),
                    });
                }
                return new InventoryResponse { inventory = items.ToArray() };
            }, onSuccess, onError);
        }

        public void GetDictionary(Action<DictionaryResponse> onSuccess, Action<BackendError> onError)
        {
            Query(data =>
            {
                var entries = new List<DictionaryItemDto>();
                var discovered = 0;
                foreach (var slime in GameBalanceConfig.Slimes)
                {
                    var rarity = GameBalanceConfig.FindSlimeRarity(slime.RarityKey);
                    var record = FindSlimeRecord(data, slime.Id);
                    if (record != null) discovered++;
                    entries.Add(new DictionaryItemDto
                    {
                        slime_id = slime.Id,
                        name = slime.Name,
                        rarity = rarity.Name,
                        rarity_key = rarity.Key,
                        rarity_color = rarity.Color,
                        base_cp = slime.BaseCp,
                        discovered = record != null ? 1 : 0,
                        first_obtained_at = record != null ? FormatTime(FromUnixMs(record.firstObtainedAtUnixMs)) : null,
                    });
                }
                entries.Sort((left, right) => left.slime_id.CompareTo(right.slime_id));
                return new DictionaryResponse
                {
                    entries = entries.ToArray(),
                    discovered = discovered,
                    total = entries.Count,
                    completion = entries.Count > 0 ? discovered / (float)entries.Count : 0f,
                };
            }, onSuccess, onError);
        }

        public void DebugModifyBoxes(DebugBoxAction action, int amount, Action<GameStateDto> onSuccess, Action<BackendError> onError)
        {
            if (!SupportsDebugTools)
            {
                onError?.Invoke(new BackendError(BackendErrorCodes.DebugToolsDisabled, BackendMessages.DebugToolsDisabled));
                return;
            }

            Execute(data =>
            {
                var now = clock.UtcNow;
                SettleBoxes(data, now);
                switch (action)
                {
                    case DebugBoxAction.Add:
                        if (amount < GameBalanceConfig.DebugAddMinimum || amount > GameBalanceConfig.DebugAddMaximum)
                            throw new BackendException(BackendErrorCodes.InvalidArgument,
                                string.Format(BackendMessages.DebugAddRangeFormat, GameBalanceConfig.DebugAddMinimum, GameBalanceConfig.DebugAddMaximum));
                        AddPendingBoxes(data, Math.Min(amount, data.boxCapacity - data.pendingBoxCount));
                        break;
                    case DebugBoxAction.Fill:
                        AddPendingBoxes(data, data.boxCapacity - data.pendingBoxCount);
                        break;
                    case DebugBoxAction.Clear:
                        data.pendingBoxRarities.Clear();
                        data.pendingBoxCount = 0;
                        data.lastBoxAtUnixMs = ToUnixMs(now);
                        break;
                    case DebugBoxAction.AdvanceHours:
                        if (amount < GameBalanceConfig.DebugAdvanceMinimumHours || amount > GameBalanceConfig.DebugAdvanceMaximumHours)
                            throw new BackendException(BackendErrorCodes.InvalidArgument,
                                string.Format(BackendMessages.DebugAdvanceRangeFormat, GameBalanceConfig.DebugAdvanceMinimumHours, GameBalanceConfig.DebugAdvanceMaximumHours));
                        data.lastBoxAtUnixMs -= (long)TimeSpan.FromHours(amount).TotalMilliseconds;
                        break;
                    default:
                        throw new BackendException(BackendErrorCodes.InvalidArgument, BackendMessages.Internal);
                }
                SettleBoxes(data, now);
                return BuildGameState(data, now);
            }, onSuccess, onError);
        }

        private void Execute<T>(Func<PlayerSaveData, T> mutation, Action<T> onSuccess, Action<BackendError> onError)
        {
            try
            {
                var working = RequireLogin().Clone();
                var result = mutation(working);
                Persist(working);
                onSuccess?.Invoke(result);
            }
            catch (Exception exception)
            {
                Fail(exception, onError);
            }
        }

        private void Query<T>(Func<PlayerSaveData, T> query, Action<T> onSuccess, Action<BackendError> onError)
        {
            try
            {
                var result = query(RequireLogin());
                onSuccess?.Invoke(result);
            }
            catch (Exception exception)
            {
                Fail(exception, onError);
            }
        }

        private PlayerSaveData RequireLogin()
        {
            if (current == null) throw new BackendException(BackendErrorCodes.NotLoggedIn, BackendMessages.NotLoggedIn);
            return current;
        }

        private void Persist(PlayerSaveData working)
        {
            try
            {
                store.Save(working);
            }
            catch (Exception exception)
            {
                exceptionLogger?.Invoke(exception);
                throw new BackendException(BackendErrorCodes.SaveFailed, BackendMessages.SaveFailed);
            }
            current = working;
        }

        private void Fail(Exception exception, Action<BackendError> onError)
        {
            if (exception is BackendException backendException)
            {
                onError?.Invoke(new BackendError(backendException.Code, backendException.Message));
                return;
            }
            exceptionLogger?.Invoke(exception);
            onError?.Invoke(new BackendError(BackendErrorCodes.Internal, BackendMessages.Internal));
        }

        private PlayerSaveData CreateNewPlayer(DateTime now)
        {
            var nowMs = ToUnixMs(now);
            var data = new PlayerSaveData
            {
                playerId = playerId,
                nickname = defaultNickname,
                createdAtUnixMs = nowMs,
                boxCapacity = GameBalanceConfig.DefaultBoxCapacity,
                lastBoxAtUnixMs = nowMs,
            };
            foreach (var box in GameBalanceConfig.BoxRarities) data.boxInventory.Add(new BoxStackSave { key = box.Key });
            AddPendingBoxes(data, GameBalanceConfig.InitialPendingBoxCount);
            return data;
        }

        /// <summary>按存档版本逐级迁移，保证已有玩家数据不需要删档。</summary>
        private static void Migrate(PlayerSaveData data)
        {
            if (data.schemaVersion < 2)
            {
                // v1 没有记录开箱次数。目前每次开箱恰好获得 1 只史莱姆且史莱姆不会被消耗，
                // 因此用持有史莱姆总数作为迁移初值。
                long opened = 0;
                if (data.slimes != null)
                    foreach (var record in data.slimes)
                        if (record != null && record.quantity > 0) opened += record.quantity;
                data.totalBoxesOpened = (int)Math.Min(int.MaxValue, opened);
            }
        }

        /// <summary>修复存档中的异常值（负数、空列表、重复条目），保证后续逻辑不会因脏数据出错。</summary>
        private void Normalize(PlayerSaveData data)
        {
            if (string.IsNullOrEmpty(data.playerId)) data.playerId = playerId;
            if (string.IsNullOrEmpty(data.nickname)) data.nickname = defaultNickname;
            data.totalBoxesOpened = Math.Max(0, data.totalBoxesOpened);
            if (data.boxCapacity <= 0) data.boxCapacity = GameBalanceConfig.DefaultBoxCapacity;
            data.pendingBoxCount = Math.Max(0, Math.Min(data.boxCapacity, data.pendingBoxCount));
            // 待领取品质与数量对齐：保留有效记录，旧存档缺少的品质按领取概率补齐，多余记录丢弃。
            var pendingRarities = new List<string>();
            foreach (var key in data.pendingBoxRarities ?? new List<string>())
                if (pendingRarities.Count < data.pendingBoxCount && GameBalanceConfig.FindBoxRarity(key) != null) pendingRarities.Add(key);
            var missingRarities = data.pendingBoxCount - pendingRarities.Count;
            data.pendingBoxRarities = pendingRarities;
            AddPendingBoxes(data, missingRarities);

            var stacks = new Dictionary<string, BoxStackSave>();
            foreach (var stack in data.boxInventory ?? new List<BoxStackSave>())
            {
                if (stack == null || string.IsNullOrEmpty(stack.key)) continue;
                var quantity = Math.Max(0, stack.quantity);
                if (stacks.TryGetValue(stack.key, out var existing)) existing.quantity += quantity;
                else stacks[stack.key] = new BoxStackSave { key = stack.key, quantity = quantity };
            }
            data.boxInventory = new List<BoxStackSave>();
            foreach (var box in GameBalanceConfig.BoxRarities)
            {
                if (!stacks.TryGetValue(box.Key, out var stack)) stack = new BoxStackSave { key = box.Key };
                data.boxInventory.Add(stack);
            }

            var slimes = new Dictionary<int, SlimeSave>();
            foreach (var record in data.slimes ?? new List<SlimeSave>())
            {
                if (record == null) continue;
                var quantity = Math.Max(0, record.quantity);
                if (slimes.TryGetValue(record.slimeId, out var existing))
                {
                    existing.quantity += quantity;
                    existing.level = Math.Max(existing.level, record.level);
                    continue;
                }
                var copy = record.Clone();
                copy.quantity = quantity;
                copy.level = Math.Max(1, copy.level);
                copy.experience = Math.Max(0, copy.experience);
                slimes[record.slimeId] = copy;
            }
            data.slimes = new List<SlimeSave>(slimes.Values);
            data.schemaVersion = PlayerSaveData.CurrentSchemaVersion;
        }

        /// <summary>按当前时间结算挂机产出：每个周期 1 个，达到容量后停止并把计时基准推进到当前时间。</summary>
        private void SettleBoxes(PlayerSaveData data, DateTime now)
        {
            var nowMs = ToUnixMs(now);
            // 系统时间被调回过去时，计时基准不能停留在未来，否则产出会一直停滞。
            if (data.lastBoxAtUnixMs > nowMs) data.lastBoxAtUnixMs = nowMs;
            if (data.pendingBoxCount >= data.boxCapacity)
            {
                data.lastBoxAtUnixMs = nowMs;
                return;
            }

            var intervalMs = (long)GameBalanceConfig.BoxGenerationInterval.TotalMilliseconds;
            var generated = (nowMs - data.lastBoxAtUnixMs) / intervalMs;
            var accepted = (int)Math.Min(generated, data.boxCapacity - data.pendingBoxCount);
            if (accepted <= 0) return;
            AddPendingBoxes(data, accepted);
            data.lastBoxAtUnixMs += accepted * intervalMs;
            if (data.pendingBoxCount >= data.boxCapacity) data.lastBoxAtUnixMs = nowMs;
        }

        /// <summary>新增待领取盲盒：品质在产出时按领取权重确定，凝聚台据此显示外观，领取时不再重抽。</summary>
        private void AddPendingBoxes(PlayerSaveData data, int count)
        {
            for (var index = 0; index < count; index++) data.pendingBoxRarities.Add(PickBoxRarity().Key);
            data.pendingBoxCount = data.pendingBoxRarities.Count;
        }

        private GameStateDto BuildGameState(PlayerSaveData data, DateTime now)
        {
            var interval = GameBalanceConfig.BoxGenerationInterval;
            var boxes = new BoxInventoryItemDto[GameBalanceConfig.BoxRarities.Count];
            for (var index = 0; index < boxes.Length; index++)
            {
                var box = GameBalanceConfig.BoxRarities[index];
                boxes[index] = new BoxInventoryItemDto
                {
                    key = box.Key,
                    name = box.Name,
                    color = box.Color,
                    quantity = FindBoxStack(data, box.Key)?.quantity ?? 0,
                };
            }

            return new GameStateDto
            {
                box_count = data.pendingBoxCount,
                pending_boxes = data.pendingBoxRarities.ToArray(),
                box_capacity = data.boxCapacity,
                box_interval_seconds = (int)interval.TotalSeconds,
                total_cp = CalculateTotalCp(data),
                server_time = FormatTime(now),
                next_box_at = data.pendingBoxCount >= data.boxCapacity
                    ? null
                    : FormatTime(FromUnixMs(data.lastBoxAtUnixMs) + interval),
                total_boxes_opened = data.totalBoxesOpened,
                box_inventory = boxes,
            };
        }

        private static UserDto BuildUser(PlayerSaveData data) => new UserDto
        {
            player_id = data.playerId,
            nickname = data.nickname,
            total_cp = CalculateTotalCp(data),
        };

        private static int CalculateTotalCp(PlayerSaveData data)
        {
            long total = 0;
            foreach (var record in data.slimes)
            {
                if (record.quantity <= 0) continue;
                var slime = GameBalanceConfig.FindSlime(record.slimeId);
                if (slime != null) total += GameBalanceConfig.EffectiveCp(slime.BaseCp, record.level, record.quantity);
            }
            return (int)Math.Min(int.MaxValue, total);
        }

        private BoxRarityDefinition PickBoxRarity()
        {
            var roll = random.Next(GameBalanceConfig.BoxWeightTotal);
            foreach (var box in GameBalanceConfig.BoxRarities)
            {
                roll -= box.Weight;
                if (roll < 0) return box;
            }
            throw new BackendException(BackendErrorCodes.ConfigInvalid, BackendMessages.ConfigInvalid);
        }

        private SlimeDefinition PickSlime(string boxRarityKey)
        {
            var candidates = GameBalanceConfig.Slimes;
            var weights = new int[candidates.Count];
            long total = 0;
            for (var index = 0; index < candidates.Count; index++)
            {
                var slime = candidates[index];
                weights[index] = slime.DropWeight > 0
                    ? GameBalanceConfig.AdjustedSlimeWeight(slime.DropWeight, slime.RarityKey, boxRarityKey)
                    : 0;
                total += weights[index];
            }
            if (total <= 0 || total > int.MaxValue)
                throw new BackendException(BackendErrorCodes.ConfigInvalid, BackendMessages.ConfigInvalid);

            var roll = random.Next((int)total);
            for (var index = 0; index < candidates.Count; index++)
            {
                roll -= weights[index];
                if (roll < 0) return candidates[index];
            }
            throw new BackendException(BackendErrorCodes.ConfigInvalid, BackendMessages.ConfigInvalid);
        }

        private static void GrantSlime(PlayerSaveData data, int slimeId, int quantity, DateTime now)
        {
            var record = FindSlimeRecord(data, slimeId);
            if (record == null)
            {
                record = new SlimeSave { slimeId = slimeId, level = 1, firstObtainedAtUnixMs = ToUnixMs(now) };
                data.slimes.Add(record);
            }
            record.quantity += quantity;
        }

        private static BoxStackSave FindBoxStack(PlayerSaveData data, string key)
        {
            foreach (var stack in data.boxInventory) if (stack.key == key) return stack;
            return null;
        }

        private static BoxStackSave GetOrCreateBoxStack(PlayerSaveData data, string key)
        {
            var stack = FindBoxStack(data, key);
            if (stack != null) return stack;
            stack = new BoxStackSave { key = key };
            data.boxInventory.Add(stack);
            return stack;
        }

        private static SlimeSave FindSlimeRecord(PlayerSaveData data, int slimeId)
        {
            foreach (var record in data.slimes) if (record.slimeId == slimeId) return record;
            return null;
        }

        private static List<SlimeSave> SortedSlimes(PlayerSaveData data)
        {
            var sorted = new List<SlimeSave>(data.slimes);
            sorted.Sort((left, right) => left.slimeId.CompareTo(right.slimeId));
            return sorted;
        }

        private static long ToUnixMs(DateTime time) => (time.ToUniversalTime() - UnixEpoch).Ticks / TimeSpan.TicksPerMillisecond;

        private static DateTime FromUnixMs(long milliseconds) => UnixEpoch.AddTicks(milliseconds * TimeSpan.TicksPerMillisecond);

        private static string FormatTime(DateTime time) =>
            time.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }
}
