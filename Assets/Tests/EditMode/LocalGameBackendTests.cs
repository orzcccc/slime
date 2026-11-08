using System;
using System.Collections.Generic;
using NUnit.Framework;
using SlimeBlindBox.Backend;
using SlimeBlindBox.Backend.Local;

namespace SlimeBlindBox.Backend.Tests
{
    public sealed class LocalGameBackendTests
    {
        private static readonly DateTime StartTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private sealed class ManualClock : IClock
        {
            public DateTime UtcNow { get; set; } = StartTime;
        }

        /// <summary>按顺序返回预设值，用尽后返回 0，便于构造确定性结果。</summary>
        private sealed class ScriptedRandom : IRandomSource
        {
            private readonly Queue<int> values = new Queue<int>();
            public void Enqueue(params int[] next) { foreach (var value in next) values.Enqueue(value); }
            public int Next(int maxExclusive) => values.Count > 0 ? Math.Min(values.Dequeue(), maxExclusive - 1) : 0;
        }

        private sealed class FailingStore : IPlayerDataStore
        {
            public readonly InMemoryPlayerDataStore Inner = new InMemoryPlayerDataStore();
            public bool FailSaves;
            public PlayerSaveData Load() => Inner.Load();
            public void Save(PlayerSaveData data)
            {
                if (FailSaves) throw new System.IO.IOException("disk full");
                Inner.Save(data);
            }
        }

        private ManualClock clock;
        private ScriptedRandom random;
        private FailingStore store;
        private LocalGameBackend backend;

        [SetUp]
        public void SetUp()
        {
            clock = new ManualClock();
            random = new ScriptedRandom();
            store = new FailingStore();
            backend = CreateBackend(true);
            Login(backend);
        }

        private LocalGameBackend CreateBackend(bool debugTools) =>
            new LocalGameBackend(store, clock, random, debugTools, "tester", "测试玩家");

        private static void Login(IGameBackend target)
        {
            BackendError error = null;
            target.Login(_ => { }, e => error = e);
            Assert.IsNull(error, error?.message);
        }

        private static T Ok<T>(Action<Action<T>, Action<BackendError>> call)
        {
            T result = default;
            BackendError error = null;
            call(value => result = value, e => error = e);
            Assert.IsNull(error, error?.code);
            return result;
        }

        private static BackendError Err<T>(Action<Action<T>, Action<BackendError>> call)
        {
            BackendError error = null;
            var succeeded = false;
            call(_ => succeeded = true, e => error = e);
            Assert.IsFalse(succeeded, "预期失败但操作成功");
            Assert.IsNotNull(error);
            return error;
        }

        private GameStateDto State() => Ok<GameStateDto>(backend.GetGameState);

        private static int Stock(GameStateDto state, string key) => Array.Find(state.box_inventory, box => box.key == key).quantity;

        [Test]
        public void NewPlayerStartsWithOnePendingBox()
        {
            var state = State();
            Assert.AreEqual(1, state.box_count);
            Assert.AreEqual(8, state.box_capacity);
            Assert.AreEqual(3 * 60 * 60, state.box_interval_seconds);
            Assert.AreEqual(5, state.box_inventory.Length);
            Assert.IsNotNull(state.next_box_at);
        }

        [Test]
        public void OfflineProductionAccumulatesAndCapsAtCapacity()
        {
            clock.UtcNow = StartTime.AddHours(7);
            Assert.AreEqual(3, State().box_count);

            clock.UtcNow = StartTime.AddHours(48);
            var full = State();
            Assert.AreEqual(8, full.box_count);
            Assert.IsNull(full.next_box_at);
        }

        [Test]
        public void PartialProgressKeepsRemainderTowardsNextBox()
        {
            clock.UtcNow = StartTime.AddHours(4);
            var state = State();
            Assert.AreEqual(2, state.box_count);
            Assert.AreEqual("2026-01-01T06:00:00.000Z", state.next_box_at);
        }

        [Test]
        public void ClaimMovesAllPendingBoxesIntoWarehouse()
        {
            clock.UtcNow = StartTime.AddHours(6);
            var response = Ok<ClaimBoxesResponse>(backend.ClaimBoxes);
            Assert.AreEqual(3, response.claimed);
            Assert.AreEqual(0, response.game_state.box_count);
            var total = 0;
            foreach (var box in response.game_state.box_inventory) total += box.quantity;
            Assert.AreEqual(3, total);
            Assert.AreEqual("2026-01-01T09:00:00.000Z", response.game_state.next_box_at);
        }

        [Test]
        public void ClaimWithoutPendingBoxesFailsWithoutChanges()
        {
            Ok<ClaimBoxesResponse>(backend.ClaimBoxes);
            var saves = store.Inner.SaveCount;
            var error = Err<ClaimBoxesResponse>(backend.ClaimBoxes);
            Assert.AreEqual(BackendErrorCodes.NoPendingBoxes, error.code);
            Assert.AreEqual(saves, store.Inner.SaveCount);
        }

        [Test]
        public void OpenBoxConsumesStockAndGrantsSlime()
        {
            // 新玩家初始的 1 个待领取盲盒在创建时以随机数 0 抽中白色。
            Ok<ClaimBoxesResponse>(backend.ClaimBoxes);
            random.Enqueue(0); // 开箱：权重第一位的劣质史莱姆
            var opened = Ok<OpenBoxResponse>((ok, fail) => backend.OpenBox("WHITE", ok, fail));
            Assert.AreEqual(201, opened.slime_id);
            Assert.AreEqual("劣质", opened.rarity);
            Assert.AreEqual(0, Stock(opened.game_state, "WHITE"));
            Assert.AreEqual(5, opened.game_state.total_cp);

            var inventory = Ok<InventoryResponse>(backend.GetInventory);
            Assert.AreEqual(1, inventory.inventory.Length);
            Assert.AreEqual(1, inventory.inventory[0].quantity);
            Assert.AreEqual(5, inventory.inventory[0].unit_effective_cp);
            Assert.AreEqual(5, inventory.inventory[0].effective_cp);
            Assert.AreEqual(1f, inventory.inventory[0].level_cp_multiplier);

            var dictionary = Ok<DictionaryResponse>(backend.GetDictionary);
            Assert.AreEqual(12, dictionary.total);
            Assert.AreEqual(1, dictionary.discovered);
        }

        [Test]
        public void OpenBoxWithEmptyStockFails()
        {
            BackendError error = null;
            backend.OpenBox("RED", _ => Assert.Fail(), e => error = e);
            Assert.AreEqual(BackendErrorCodes.BoxStockEmpty, error.code);
            Assert.AreEqual("仓库中没有红色盲盒", error.message);
        }

        [Test]
        public void OpenBoxWithUnknownRarityFails()
        {
            BackendError error = null;
            backend.OpenBox("GOLD", _ => Assert.Fail(), e => error = e);
            Assert.AreEqual(BackendErrorCodes.UnknownBoxRarity, error.code);
        }

        [Test]
        public void ProgressPersistsAcrossSessions()
        {
            Ok<GameStateDto>((ok, fail) => backend.DebugModifyBoxes(DebugBoxAction.Clear, 0, ok, fail));
            random.Enqueue(9999); // 产出：红色盲盒
            Ok<GameStateDto>((ok, fail) => backend.DebugModifyBoxes(DebugBoxAction.Add, 1, ok, fail));
            Ok<ClaimBoxesResponse>(backend.ClaimBoxes);

            var reloaded = CreateBackend(true);
            Login(reloaded);
            var state = Ok<GameStateDto>(reloaded.GetGameState);
            Assert.AreEqual(1, Stock(state, "RED"));
            Assert.AreEqual(0, state.box_count);
        }

        [Test]
        public void SaveFailureLeavesStateUnchanged()
        {
            store.FailSaves = true;
            var error = Err<ClaimBoxesResponse>(backend.ClaimBoxes);
            Assert.AreEqual(BackendErrorCodes.SaveFailed, error.code);

            store.FailSaves = false;
            Assert.AreEqual(1, State().box_count);
        }

        [Test]
        public void ClockRollbackDoesNotStallProduction()
        {
            clock.UtcNow = StartTime.AddHours(-10);
            Assert.AreEqual(1, State().box_count);
            clock.UtcNow = StartTime.AddHours(-7);
            Assert.AreEqual(2, State().box_count);
        }

        [Test]
        public void DebugToolsRespectRangesAndCapacity()
        {
            Assert.AreEqual(8, Ok<GameStateDto>((ok, fail) => backend.DebugModifyBoxes(DebugBoxAction.Add, 100, ok, fail)).box_count);
            Assert.AreEqual(0, Ok<GameStateDto>((ok, fail) => backend.DebugModifyBoxes(DebugBoxAction.Clear, 0, ok, fail)).box_count);
            Assert.AreEqual(1, Ok<GameStateDto>((ok, fail) => backend.DebugModifyBoxes(DebugBoxAction.AdvanceHours, 3, ok, fail)).box_count);

            BackendError error = null;
            backend.DebugModifyBoxes(DebugBoxAction.Add, 101, _ => Assert.Fail(), e => error = e);
            Assert.AreEqual(BackendErrorCodes.InvalidArgument, error.code);
        }

        [Test]
        public void DebugToolsUnavailableWhenDisabled()
        {
            var release = CreateBackend(false);
            Login(release);
            Assert.IsFalse(release.SupportsDebugTools);
            BackendError error = null;
            release.DebugModifyBoxes(DebugBoxAction.Fill, 0, _ => Assert.Fail(), e => error = e);
            Assert.AreEqual(BackendErrorCodes.DebugToolsDisabled, error.code);
        }

        [Test]
        public void CorruptedSaveValuesAreRepaired()
        {
            var broken = store.Inner.Load();
            broken.pendingBoxCount = -5;
            broken.boxCapacity = 0;
            broken.slimes.Add(new SlimeSave { slimeId = 205, quantity = -3, level = 0 });
            broken.boxInventory = null;
            store.Inner.Save(broken);

            var reloaded = CreateBackend(true);
            Login(reloaded);
            var state = Ok<GameStateDto>(reloaded.GetGameState);
            Assert.AreEqual(0, state.box_count);
            Assert.AreEqual(8, state.box_capacity);
            Assert.AreEqual(5, state.box_inventory.Length);
            Assert.AreEqual(0, state.total_cp);
        }

        [Test]
        public void NewerSaveVersionIsRejected()
        {
            var future = store.Inner.Load();
            future.schemaVersion = PlayerSaveData.CurrentSchemaVersion + 1;
            store.Inner.Save(future);
            BackendError error = null;
            CreateBackend(true).Login(_ => Assert.Fail(), e => error = e);
            Assert.AreEqual(BackendErrorCodes.SaveVersionTooNew, error.code);
        }

        [Test]
        public void PendingBoxRaritiesAreFixedAtProductionAndClaimedAsShown()
        {
            Ok<GameStateDto>((ok, fail) => backend.DebugModifyBoxes(DebugBoxAction.Clear, 0, ok, fail));
            random.Enqueue(9999, 0, 5000); // 红、白、绿
            var state = Ok<GameStateDto>((ok, fail) => backend.DebugModifyBoxes(DebugBoxAction.Add, 3, ok, fail));
            CollectionAssert.AreEqual(new[] { "RED", "WHITE", "GREEN" }, state.pending_boxes);
            random.Enqueue(7500, 7500, 7500); // 重新读取或领取都不应重抽品质
            CollectionAssert.AreEqual(state.pending_boxes, State().pending_boxes);

            var claimed = Ok<ClaimBoxesResponse>(backend.ClaimBoxes).game_state;
            Assert.AreEqual(1, Stock(claimed, "RED"));
            Assert.AreEqual(1, Stock(claimed, "WHITE"));
            Assert.AreEqual(1, Stock(claimed, "GREEN"));
            Assert.AreEqual(0, Stock(claimed, "BLUE"));
            Assert.AreEqual(0, claimed.pending_boxes.Length);
        }

        [Test]
        public void OfflineProductionRollsRarityForEachNewBox()
        {
            random.Enqueue(7500, 9000); // 蓝、紫
            clock.UtcNow = StartTime.AddHours(6);
            var state = State();
            Assert.AreEqual(3, state.box_count);
            CollectionAssert.AreEqual(new[] { "WHITE", "BLUE", "PURPLE" }, state.pending_boxes);
        }

        [Test]
        public void MissingOrInvalidPendingRaritiesAreRepairedOnLoad()
        {
            var legacy = store.Inner.Load();
            legacy.pendingBoxCount = 3;
            legacy.pendingBoxRarities = new List<string> { "BLUE", "GOLD" };
            store.Inner.Save(legacy);

            random.Enqueue(9999, 9999);
            var reloaded = CreateBackend(true);
            Login(reloaded);
            var state = Ok<GameStateDto>(reloaded.GetGameState);
            Assert.AreEqual(3, state.box_count);
            CollectionAssert.AreEqual(new[] { "BLUE", "RED", "RED" }, state.pending_boxes);
        }

        [Test]
        public void OperationsRequireLogin()
        {
            BackendError error = null;
            CreateBackend(true).GetGameState(_ => Assert.Fail(), e => error = e);
            Assert.AreEqual(BackendErrorCodes.NotLoggedIn, error.code);
        }
    }

    public sealed class GameBalanceConfigTests
    {
        [Test]
        public void ConfigurationIsValid()
        {
            CollectionAssert.IsEmpty(GameBalanceConfig.Validate());
        }

        [Test]
        public void TwelveRaritiesInOrder()
        {
            var names = new List<string>();
            foreach (var rarity in GameBalanceConfig.SlimeRarities) names.Add(rarity.Name);
            CollectionAssert.AreEqual(new[] { "劣质", "普通", "优秀", "精良", "极品", "史诗", "传说", "神器", "魔王", "战神", "至尊", "魔神" }, names);
        }

        [Test]
        public void BoxRarityWeightsMatchDesign()
        {
            var weights = new List<int>();
            foreach (var box in GameBalanceConfig.BoxRarities) weights.Add(box.Weight);
            CollectionAssert.AreEqual(new[] { 4500, 3000, 1500, 800, 200 }, weights);
        }

        [Test]
        public void RareBoxesFavourHighRarities()
        {
            Assert.Greater(GameBalanceConfig.AdjustedSlimeWeight(1000, "LEGENDARY", "RED"), GameBalanceConfig.AdjustedSlimeWeight(1000, "LEGENDARY", "WHITE"));
            Assert.Less(GameBalanceConfig.AdjustedSlimeWeight(1000, "POOR", "RED"), GameBalanceConfig.AdjustedSlimeWeight(1000, "POOR", "WHITE"));
            Assert.AreEqual(1000, GameBalanceConfig.AdjustedSlimeWeight(1000, "UNCOMMON", "RED"));
        }

        [Test]
        public void EffectiveCpFollowsLogarithmicGrowth()
        {
            Assert.AreEqual(100, GameBalanceConfig.EffectiveCp(100, 1, 1));
            Assert.AreEqual(125, GameBalanceConfig.EffectiveCp(100, 2, 1));
            Assert.AreEqual(300, GameBalanceConfig.EffectiveCp(100, 4, 2));
        }

        [Test]
        public void UnitEffectiveCpAndMultiplierMatchTotal()
        {
            Assert.AreEqual(1.25, GameBalanceConfig.LevelCpMultiplier(2), 1e-9);
            Assert.AreEqual(1000, GameBalanceConfig.UnitEffectiveCp(800, 2));
            Assert.AreEqual(3000, GameBalanceConfig.EffectiveCp(800, 2, 3));
            Assert.AreEqual(1.0, GameBalanceConfig.LevelCpMultiplier(0), 1e-9);
        }
    }
}
