using System;
using SlimeBlindBox.App;
using SlimeBlindBox.Backend;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.UI
{
    /// <summary>
    /// M05/M06/M07 开箱：抖动→光束→结果。动画至少 0.8 秒；请求未返回时显示“开箱中…”，
    /// 响应较慢时允许返回仓库，待请求结束后才允许重试，避免重复消耗盲盒。
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
        public GameObject actionBar;
        public Button closeButton;
        public Button backButton;
        public Button againButton;
        public TMP_Text againText;

        private SlimeGameController controller;
        private Action<BoxOpeningExit> closed;
        private string key;
        private float startedAt;
        private bool resultReady;
        private bool waiting;
        private int requestVersion;
        private OpenBoxResponse result;

        public bool IsOpening => gameObject.activeInHierarchy && (waiting || resultReady);

        public void Initialize(SlimeGameController gameController, TMP_FontAsset font,
            Action<BoxOpeningExit> onClosed)
        {
            controller = gameController;
            closed = onClosed;
            foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.font = font;
            backButton.onClick.AddListener(() => Close(BoxOpeningExit.Warehouse));
            closeButton.onClick.AddListener(() => Close(BoxOpeningExit.Warehouse));
            againButton.onClick.AddListener(() =>
            {
                if (waiting || controller.IsBusy) return;
                if (controller.GameState?.box_inventory != null)
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
            if (waiting) return;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            key = boxKey;
            result = null;
            resultReady = false;
            waiting = true;
            var version = ++requestVersion;
            startedAt = Time.unscaledTime;
            resultPanel.SetActive(false);
            openingState.SetActive(true);
            actionBar.SetActive(false);
            closeButton.gameObject.SetActive(false);
            boxImage.gameObject.SetActive(true);
            boxImage.sprite = BoxVisuals.Sprite(key);
            boxImage.color = Color.white;
            ResetBoxPose();
            rays.color = BoxVisuals.Color(key);
            glow.color = BoxVisuals.Color(key);
            statusText.text = BoxOpeningUiText.Opening;
            againButton.interactable = false;
            // 先置等待状态，再发请求；本地后端可能在调用栈内同步返回。
            if (!controller.OpenBox(key,
                response => { if (version == requestVersion) OnOpened(response); },
                error => { if (version == requestVersion) OnFailed(error); }))
            {
                waiting = false;
                ShowRetry(MainUiText.Processing);
            }
        }

        private void OnEnable()
        {
            if (controller != null) controller.StateChanged += RefreshActions;
        }

        private void OnDisable()
        {
            if (controller != null) controller.StateChanged -= RefreshActions;
            requestVersion++;
            waiting = false;
            resultReady = false;
        }

        private void RefreshActions()
        {
            againButton.interactable = !waiting && !resultReady && !controller.IsBusy;
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
            ShowRetry(error?.message ?? BoxOpeningUiText.Timeout);
        }

        private void Update()
        {
            if (!gameObject.activeSelf) return;
            if (Input.GetKeyDown(KeyCode.Escape) && closeButton.gameObject.activeSelf)
            {
                Close(BoxOpeningExit.Warehouse);
                return;
            }
            if (waiting || resultReady)
            {
                var elapsed = Time.unscaledTime - startedAt;
                var t = Mathf.Clamp01(elapsed / MinimumAnimationSeconds);
                boxImage.rectTransform.localScale = Vector3.one * (1 + .04f * Mathf.Sin(t * 40f));
                boxImage.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 55f) * 6f * (1 - t));
                rays.rectTransform.localRotation = Quaternion.Euler(0, 0, Time.unscaledTime * 12f);
                rays.color = new Color(rays.color.r, rays.color.g, rays.color.b, .55f + .45f * Mathf.PingPong(Time.unscaledTime * 2f, 1f));
                if (elapsed > TimeoutSeconds && !resultReady && !actionBar.activeSelf)
                    ShowRetry(BoxOpeningUiText.WaitingForResponse);
            }
            if (resultReady && !resultPanel.activeSelf && Time.unscaledTime - startedAt >= MinimumAnimationSeconds) ShowResult();
        }

        private void ShowRetry(string message)
        {
            statusText.text = message;
            actionBar.SetActive(true);
            closeButton.gameObject.SetActive(true);
            againText.text = BoxOpeningUiText.Retry;
            ResetBoxPose();
            RefreshActions();
        }

        private void ResetBoxPose()
        {
            boxImage.rectTransform.localScale = Vector3.one;
            boxImage.rectTransform.localRotation = Quaternion.identity;
        }

        private void ShowResult()
        {
            resultReady = false;
            openingState.SetActive(false);
            resultPanel.SetActive(true);
            actionBar.SetActive(true);
            closeButton.gameObject.SetActive(true);
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
            ring.gameObject.SetActive(high);
            sparkle.gameObject.SetActive(high);
            glow.color = rarityColor;
            rays.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, .75f);
            RefreshActions();
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
