using System;
using SlimeBlindBox.App;
using SlimeBlindBox.Backend;
using UnityEngine;
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

        public bool IsOpening => opening.IsOpening;

        public void Initialize(SlimeGameController gameController, TMP_FontAsset font, Action onHome)
        {
            controller = gameController;
            homeCallback = onHome;
            warehouse.Initialize(controller, font, OnWarehouseClosed);
            claimResult.Initialize(font, OnClaimClosed);
            opening.Initialize(controller, font, OnOpeningClosed);
        }

        public void ShowWarehouse(string preferredKey = null)
        {
            ShowPage(warehouse.gameObject);
            warehouse.Show(preferredKey);
        }

        public void ShowClaim(ClaimBoxesResponse response)
        {
            ShowPage(claimResult.gameObject);
            claimResult.Show(response);
        }

        private void ShowPage(GameObject page)
        {
            warehouse.gameObject.SetActive(false);
            claimResult.gameObject.SetActive(false);
            opening.gameObject.SetActive(false);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            page.SetActive(true);
        }

        private void ShowOpening(string key)
        {
            ShowPage(opening.gameObject);
            opening.StartOpen(key);
        }

        private void ReturnHome()
        {
            gameObject.SetActive(false);
            homeCallback?.Invoke();
        }

        private void OnWarehouseClosed(BoxWarehouseExit exit)
        {
            switch (exit)
            {
                case BoxWarehouseExit.OpenBox:
                    ShowOpening(warehouse.SelectedKey);
                    break;
                case BoxWarehouseExit.Island:
                    ReturnHome();
                    break;
                default:
                    ReturnHome();
                    break;
            }
        }

        private void OnClaimClosed(ClaimResultExit exit)
        {
            if (exit == ClaimResultExit.OpenHighest)
            {
                var key = claimResult.HighestBoxKey;
                if (key != null) ShowOpening(key);
                else ShowWarehouse();
            }
            else ReturnHome();
        }

        private void OnOpeningClosed(BoxOpeningExit exit)
        {
            ShowWarehouse();
        }

    }
}
