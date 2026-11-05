using System;
using System.Collections;
using SlimeBlindBox.Networking;
using UnityEngine;

namespace SlimeBlindBox.App
{
    public sealed class SlimeGameController : MonoBehaviour
    {
        [SerializeField] private SlimeApiClient apiClient;
        [SerializeField] private string developmentPlayerId = "local-player";

        public UserDto User { get; private set; }
        public GameStateDto GameState { get; private set; }
        public InventoryItemDto[] Inventory { get; private set; } = Array.Empty<InventoryItemDto>();
        public DictionaryResponse Dictionary { get; private set; }
        public GuildStateResponse Guild { get; private set; }
        public bool IsBusy { get; private set; }
        public string LastMessage { get; private set; } = "正在启动……";

        public event Action StateChanged;

        private IEnumerator Start()
        {
            if (apiClient == null) apiClient = GetComponent<SlimeApiClient>();
            yield return Run(apiClient.Login(
                $"dev:{developmentPlayerId}",
                response =>
                {
                    User = response.user;
                    LastMessage = $"欢迎回来，{User.nickname}";
                },
                SetError));
            if (apiClient.IsLoggedIn) yield return RefreshAll();
        }

        public void OpenBox(string boxRarityKey = "WHITE")
        {
            if (IsBusy || GameState == null) return;
            StartCoroutine(Run(apiClient.OpenBox(boxRarityKey,
                response =>
                {
                    GameState = response.game_state;
                    LastMessage = $"获得 {response.name}（{response.rarity}）";
                    StartCoroutine(RefreshInventory());
                },
                SetError)));
        }

        public void ClaimBoxes()
        {
            if (IsBusy || GameState == null || GameState.box_count <= 0) return;
            StartCoroutine(Run(apiClient.ClaimBoxes(response =>
            {
                GameState = response.game_state;
                LastMessage = $"已领取 {response.claimed} 个盲盒到仓库";
            }, SetError)));
        }

        public void CreateGuild() => StartCoroutine(Run(apiClient.CreateGuild("我的史莱姆公会", _ => { LastMessage = "公会创建成功"; StartCoroutine(RefreshAll()); }, SetError)));
        public void Donate(int slimeId) => StartCoroutine(Run(apiClient.Donate(slimeId, 1, _ => { LastMessage = "捐献成功"; StartCoroutine(RefreshAll()); }, SetError)));
        public void UpgradeGuild(string type) => StartCoroutine(Run(apiClient.UpgradeGuild(type, _ => { LastMessage = "科技升级成功"; StartCoroutine(RefreshAll()); }, SetError)));
        public void FeedBoss() => StartCoroutine(Run(apiClient.FeedBoss(value => { LastMessage = $"投喂造成 {value.damage} 点效果"; StartCoroutine(RefreshAll()); }, SetError)));
        public void GmBoxes(string action, int amount) => StartCoroutine(Run(apiClient.GmBoxes(action, amount, value => { GameState = value; LastMessage = "GM 操作成功"; }, SetError)));

        public IEnumerator RefreshAll()
        {
            yield return Run(apiClient.GetGameState(value => GameState = value, SetError));
            yield return RefreshInventory();
            yield return Run(apiClient.GetDictionary(value => Dictionary = value, SetError));
            yield return Run(apiClient.GetGuild(value => Guild = value, SetError));
        }

        private IEnumerator RefreshInventory()
        {
            yield return Run(apiClient.GetInventory(value => Inventory = value.inventory, SetError));
        }

        private IEnumerator Run(IEnumerator operation)
        {
            IsBusy = true;
            StateChanged?.Invoke();
            yield return operation;
            IsBusy = false;
            StateChanged?.Invoke();
        }

        private void SetError(string error)
        {
            LastMessage = error;
            StateChanged?.Invoke();
        }
    }
}
