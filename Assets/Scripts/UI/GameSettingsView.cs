using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    /// <summary>
    /// 游戏设置弹窗。游戏固定为 800×800 桌面岛屿窗口，不提供显示模式和分辨率切换。
    /// </summary>
    public sealed class GameSettingsView : MonoBehaviour
    {
        public Button closeButton;
        public Button exitButton;
        public Text statusText;

        public void Initialize(Font font)
        {
            foreach (var text in GetComponentsInChildren<Text>(true))
                if (font != null) text.font = font;

            if (statusText != null) statusText.text = "按住岛屿可拖动游戏到桌面任意位置";
            closeButton.onClick.AddListener(Hide);
            exitButton.onClick.AddListener(ExitGame);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void Hide() => gameObject.SetActive(false);

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
