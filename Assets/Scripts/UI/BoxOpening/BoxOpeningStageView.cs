using System;
using System.Collections.Generic;
using SlimeBlindBox.App;
using SlimeBlindBox.Backend;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.UI
{
    /// <summary>
    /// M05/M06/M07 开箱：抖动→光束→结果。动画至少 0.8 秒；请求未返回时显示“开箱中…”，
    /// 超过 5 秒显示重试，失败后盲盒仍保留在仓库中。
    /// </summary>
    public sealed class BoxOpeningStageView : MonoBehaviour
    {
        private const float MinimumAnimationSeconds = .8f;
        private const float TimeoutSeconds = 5f;

        public GameObject openingState;
        public Image boxImage;
        public Image rays;
        public Image glow;
        public Image ring;
        public Image sparkle;
        public TMP_Text statusText;
        public GameObject resultPanel;
        public Image resultRibbon;
        public TMP_Text resultRarityText;
        public Image resultSlime;
        public GameObject resultGlow;
        public TMP_Text resultName;
        public TMP_Text resultNewText;
        public TMP_Text resultCp;
        public TMP_Text resultCpChange;
        public GameObject tagNew;
        public Button backButton;
        public Button againButton;
        public TMP_Text againText;

        private SlimeGameController controller;
        private Action<BoxOpeningExit> closed;
        private Action<string> requestOpen;
        private string key;
        private float startedAt;
        private bool resultReady;
        private bool waiting;
        private OpenBoxResponse result;

        public void Initialize(SlimeGameController gameController, TMP_FontAsset font,
            Action<string> onRequestOpen, Action<BoxOpeningExit> onClosed)
        {
            controller = gameController;
            closed = onClosed;
            requestOpen = onRequestOpen;
            foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.font = font;
            backButton.onClick.AddListener(() => Close(BoxOpeningExit.Warehouse));
            againButton.onClick.AddListener(() =>
            {
                if (result != null && controller.GameState.box_inventory != null)
                {
                    var stack = Array.Find(controller.GameState.box_inventory, b => b.key == key);
                    if (stack != null && stack.quantity > 0)
                    {
                        StartOpen(key);
                        return;
                    }
                }
                Close(BoxOpeningExit.Warehouse);
            });
            gameObject.SetActive(false);
        }

        public void StartOpen(string boxKey)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            key = boxKey;
            result = null;
            resultReady = false;
            waiting = false;
            startedAt = Time.unscaledTime;
            resultPanel.SetActive(false);
            openingState.SetActive(true);
            boxImage.gameObject.SetActive(true);
            boxImage.sprite = BoxVisuals.Sprite(key);
            boxImage.color = Color.white;
            rays.color = BoxVisuals.Color(key);
            glow.color = BoxVisuals.Color(key);
            statusText.text = BoxOpeningUiText.Opening;
            againButton.interactable = false;
            if (requestOpen == null) return;
            if (!controller.OpenBox(key, OnOpened, OnFailed))
            {
                ShowRetry();
                return;
            }
            waiting = true;
        }

        public void OnOpened(OpenBoxResponse response)
        {
            result = response;
            resultReady = true;
            waiting = false;
            if (Time.unscaledTime - startedAt >= MinimumAnimationSeconds) ShowResult();
        }

        public void OnFailed(BackendError error)
        {
            resultReady = false;
            waiting = false;
            statusText.text = error?.message ?? BoxOpeningUiText.Timeout;
            ShowRetry();
        }

        private void Update()
        {
            if (!gameObject.activeSelf) return;
            if (waiting)
            {
                var t = (Time.unscaledTime - startedAt) / MinimumAnimationSeconds;
                boxImage.rectTransform.localScale = Vector3.one * (1 + .04f * Mathf.Sin(t * 40f));
                boxImage.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 55f) * 6f * (1 - t));
                rays.rectTransform.localRotation = Quaternion.Euler(0, 0, Time.unscaledTime * 12f);
                rays.color = new Color(rays.color.r, rays.color.g, rays.color.b, .55f + .45f * Mathf.PingPong(Time.unscaledTime * 2f, 1f));
                if (Time.unscaledTime - startedAt > TimeoutSeconds && !resultReady) ShowRetry();
            }
            if (resultReady && !resultPanel.activeSelf && Time.unscaledTime - startedAt >= MinimumAnimationSeconds) ShowResult();
        }

        private void ShowRetry()
        {
            waiting = false;
            statusText.text = BoxOpeningUiText.Timeout;
            againButton.interactable = true;
            againText.text = BoxOpeningUiText.Retry;
        }

        private void ShowResult()
        {
            resultReady = false;
            openingState.SetActive(false);
            resultPanel.SetActive(true);
            var rarityColor = RarityStyle.Parse(result.rarity_color);
            var high = InventoryListQuery.RarityRank(result.rarity_key) >= 7;
            resultRibbon.color = rarityColor;
            resultRarityText.text = result.rarity;
            resultRarityText.color = RarityStyle.TextOn(rarityColor);
            resultSlime.sprite = SlimeArtCatalog.Slime(result.slime_id);
            resultName.text = result.name;
            tagNew.SetActive(result.is_new);
            resultNewText.text = result.is_new ? BoxOpeningUiText.FirstObtained : string.Format(BoxOpeningUiText.RepeatObtainedFormat, result.quantity);
            resultCp.text = string.Format(BoxOpeningUiText.CpFormat, result.unit_effective_cp);
            resultCpChange.text = string.Format(BoxOpeningUiText.CpChangeFormat, result.total_cp_before, result.game_state.total_cp);
            resultGlow.SetActive(high);
            glow.color = rarityColor;
            rays.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, .75f);
            againButton.interactable = true;
            var stack = Array.Find(result.game_state.box_inventory, b => b.key == key);
            againText.text = stack != null && stack.quantity > 0 ? BoxOpeningUiText.OpenAgainFormat : BoxOpeningUiText.ChangeBox;
        }

        private void Close(BoxOpeningExit exit)
        {
            gameObject.SetActive(false);
            waiting = false;
            closed?.Invoke(exit);
        }
    }

    public enum BoxOpeningExit
    {
        Warehouse,
    }
}
