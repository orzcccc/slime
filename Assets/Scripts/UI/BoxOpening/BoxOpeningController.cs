using System;
using SlimeBlindBox.App;
using SlimeBlindBox.Backend;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.UI
{
    /// <summary>盲盒仓库、领取结果和开箱页的协调器；图鉴由 DictionaryView 独立管理与控制器共享。</summary>
    public sealed class BoxOpeningController : MonoBehaviour
    {
        [SerializeField] private BoxWarehouseView warehouse;
        [SerializeField] private ClaimResultView claimResult;
        [SerializeField] private BoxOpeningStageView opening;

        private SlimeGameController controller;
        private Action homeCallback;

        public void Initialize(SlimeGameController gameController, TMP_FontAsset font,
            BoxWarehouseRow rowPrefab, Transform rowList, ClaimResultTile tilePrefab, Transform tileGrid,
            Action onHome)
        {
            controller = gameController;
            homeCallback = onHome;
            warehouse.Initialize(controller, font, OnWarehouseClosed);
            var row = Instantiate(rowPrefab, rowList, false);
            row.gameObject.SetActive(false);
            claimResult.Initialize(controller, font, tilePrefab, tileGrid, OnClaimClosed);
            opening.Initialize(controller, font, OnRequestOpen, OnOpeningClosed);
        }

        public void ShowWarehouse(string preferredKey = null)
        {
            warehouse.Show(preferredKey);
        }

        public void ShowClaim(ClaimBoxesResponse response)
        {
            claimResult.Show(response);
        }

        private void OnWarehouseClosed(BoxWarehouseExit exit)
        {
            switch (exit)
            {
                case BoxWarehouseExit.OpenBox:
                    opening.StartOpen(warehouse.SelectedKey);
                    break;
                case BoxWarehouseExit.Island:
                    homeCallback?.Invoke();
                    break;
                default:
                    homeCallback?.Invoke();
                    break;
            }
        }

        private void OnClaimClosed(ClaimResultExit exit)
        {
            if (exit == ClaimResultExit.OpenHighest)
            {
                var key = claimResult.HighestBoxKey ?? "WHITE";
                opening.StartOpen(key);
            }
            else homeCallback?.Invoke();
        }

        private void OnOpeningClosed(BoxOpeningExit exit)
        {
            ShowWarehouse();
        }

        private void OnRequestOpen(string key)
        {
            if (!controller.OpenBox(key, opening.OnOpened, opening.OnFailed)) opening.OnFailed(null);
        }
    }
}
