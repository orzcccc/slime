using System;
using System.Globalization;
using SlimeBlindBox.Backend;
using SlimeBlindBox.UI;
using UnityEngine;

namespace SlimeBlindBox.App
{
    /// <summary>
    /// 游戏状态控制器：通过 IGameBackend 读取和修改玩家数据，并通知界面刷新。
    /// 不直接修改任何玩家数据，所有改动都经由后端完成。
    /// </summary>
    public sealed class SlimeGameController : MonoBehaviour
    {
        private const float StateRefreshRetrySeconds = 5f;

        private IGameBackend backend;
        private int pendingRequests;
        private DateTime nextBoxAtUtc = DateTime.MaxValue;
        private float nextRefreshAllowedAt;

        public UserDto User { get; private set; }
        public GameStateDto GameState { get; private set; }
        public InventoryItemDto[] Inventory { get; private set; } = Array.Empty<InventoryItemDto>();
        /// <summary>背包是否至少成功读取过一次。为 false 且不在请求中时，表示读取失败，界面应提示重试。</summary>
        public bool InventoryLoaded { get; private set; }
        public DictionaryResponse Dictionary { get; private set; }
        public bool IsBusy => pendingRequests > 0;
        public bool IsLoggedIn => User != null;
        public bool SupportsDebugTools => backend != null && backend.SupportsDebugTools;
        public string LastMessage { get; private set; } = MainUiText.Starting;

        public event Action StateChanged;

        private void Awake()
        {
            backend = GameBackendFactory.Create();
        }

        private void Start()
        {
            Connect();
        }

        private void Connect()
        {
            Begin();
            backend.Login(response =>
            {
                User = response.user;
                LastMessage = string.Format(MainUiText.WelcomeFormat, User.nickname);
                End();
                RefreshAll();
            }, Fail);
        }

        private void Update()
        {
            // 到达下一个盲盒的产出时间后自动同步一次状态，让待领取数量及时更新。
            if (!IsLoggedIn || IsBusy || DateTime.UtcNow < nextBoxAtUtc || Time.unscaledTime < nextRefreshAllowedAt) return;
            nextRefreshAllowedAt = Time.unscaledTime + StateRefreshRetrySeconds;
            RefreshGameState();
        }

        /// <summary>打开一个盲盒。成功回调在状态与收藏刷新完成后触发；失败不扣除仓库盲盒。</summary>
        public bool OpenBox(string boxRarityKey, Action<OpenBoxResponse> onSuccess = null, Action<BackendError> onError = null)
        {
            if (IsBusy || GameState == null) return false;
            Begin();
            backend.OpenBox(boxRarityKey, response =>
            {
                SetGameState(response.game_state);
                LastMessage = string.Format(MainUiText.ObtainedFormat, response.name, response.rarity);
                End();
                RefreshCollections();
                onSuccess?.Invoke(response);
            }, error =>
            {
                Fail(error);
                onError?.Invoke(error);
            });
            return true;
        }

        /// <summary>领取凝聚台上的全部盲盒。claimed_boxes 与领取前槽位顺序一致。</summary>
        public bool ClaimBoxes(Action<ClaimBoxesResponse> onSuccess = null, Action<BackendError> onError = null)
        {
            if (IsBusy || GameState == null || GameState.box_count <= 0) return false;
            Begin();
            backend.ClaimBoxes(response =>
            {
                SetGameState(response.game_state);
                LastMessage = string.Format(MainUiText.ClaimedFormat, response.claimed);
                End();
                onSuccess?.Invoke(response);
            }, error =>
            {
                Fail(error);
                onError?.Invoke(error);
            });
            return true;
        }

        public void DebugModifyBoxes(DebugBoxAction action, int amount)
        {
            if (IsBusy || !SupportsDebugTools) return;
            Begin();
            backend.DebugModifyBoxes(action, amount, state =>
            {
                SetGameState(state);
                LastMessage = MainUiText.DebugDone;
                End();
            }, Fail);
        }

        public void RefreshAll()
        {
            RefreshGameState();
            RefreshCollections();
        }

        /// <summary>背包读取失败后的重试：未登录时重新登录，否则只重新读取背包与图鉴。</summary>
        public void RetryInventory()
        {
            if (IsBusy) return;
            if (IsLoggedIn) RefreshCollections();
            else Connect();
        }

        private void RefreshGameState()
        {
            Begin();
            backend.GetGameState(state =>
            {
                SetGameState(state);
                End();
            }, Fail);
        }

        private void RefreshCollections()
        {
            Begin();
            backend.GetInventory(response =>
            {
                Inventory = response.inventory ?? Array.Empty<InventoryItemDto>();
                InventoryLoaded = true;
                End();
            }, Fail);
            Begin();
            backend.GetDictionary(response =>
            {
                Dictionary = response;
                End();
            }, Fail);
        }

        private void SetGameState(GameStateDto state)
        {
            GameState = state;
            nextBoxAtUtc = DateTime.MaxValue;
            if (state != null && !string.IsNullOrEmpty(state.next_box_at)
                && DateTime.TryParse(state.next_box_at, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
                nextBoxAtUtc = parsed;
        }

        private void Begin()
        {
            pendingRequests++;
            StateChanged?.Invoke();
        }

        private void End()
        {
            pendingRequests = Math.Max(0, pendingRequests - 1);
            StateChanged?.Invoke();
        }

        private void Fail(BackendError error)
        {
            Debug.LogWarning("后端请求失败：" + error.code);
            LastMessage = error.message;
            End();
        }
    }
}
