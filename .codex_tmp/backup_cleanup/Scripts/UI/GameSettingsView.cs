using System;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    public sealed class GameSettingsView : MonoBehaviour
    {
        public Dropdown displayModeDropdown;
        public Dropdown resolutionDropdown;
        public Button applyButton;
        public Button closeButton;
        public Button exitButton;
        public Text statusText;

        private Vector2Int[] resolutions;

        public void Initialize(Font font)
        {
            foreach (var text in GetComponentsInChildren<Text>(true))
                if (font != null) text.font = font;

            resolutions = GameSettingsService.CommonResolutions;
            displayModeDropdown.ClearOptions();
            displayModeDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "全屏",
                "无边框",
                "窗口化",
            });

            resolutionDropdown.ClearOptions();
            var labels = new System.Collections.Generic.List<string>();
            foreach (var resolution in resolutions) labels.Add($"{resolution.x} × {resolution.y}");
            resolutionDropdown.AddOptions(labels);

            displayModeDropdown.value = (int)GameSettingsService.CurrentMode;
            resolutionDropdown.value = FindResolution(GameSettingsService.CurrentResolution);
            displayModeDropdown.RefreshShownValue();
            resolutionDropdown.RefreshShownValue();
            statusText.text = "修改后点击应用";

            applyButton.onClick.AddListener(Apply);
            closeButton.onClick.AddListener(Hide);
            exitButton.onClick.AddListener(ExitGame);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void Hide() => gameObject.SetActive(false);

        private void Apply()
        {
            var resolutionIndex = Mathf.Clamp(resolutionDropdown.value, 0, resolutions.Length - 1);
            var mode = (GameDisplayMode)Mathf.Clamp(displayModeDropdown.value, 0, 2);
            GameSettingsService.Apply(resolutions[resolutionIndex], mode);
            statusText.text = $"已应用：{resolutions[resolutionIndex].x} × {resolutions[resolutionIndex].y} / {ModeName(mode)}";
        }

        private int FindResolution(Vector2Int current)
        {
            for (var i = 0; i < resolutions.Length; i++)
                if (resolutions[i] == current) return i;
            return Array.IndexOf(resolutions, new Vector2Int(1920, 1080));
        }

        private static string ModeName(GameDisplayMode mode)
        {
            switch (mode)
            {
                case GameDisplayMode.Fullscreen: return "全屏";
                case GameDisplayMode.Windowed: return "窗口化";
                default: return "无边框";
            }
        }

        private static void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
