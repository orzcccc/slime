using UnityEngine;

namespace SlimeBlindBox.UI
{
    [ExecuteAlways]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void OnEnable()
        {
            rectTransform = transform as RectTransform;
            Apply();
        }

        private void Update()
        {
            if (lastSafeArea != Screen.safeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
                Apply();
        }

        private void Apply()
        {
            if (rectTransform == null || Screen.width <= 0 || Screen.height <= 0) return;
            var safeArea = Screen.safeArea;
            rectTransform.anchorMin = new Vector2(safeArea.xMin / Screen.width, safeArea.yMin / Screen.height);
            rectTransform.anchorMax = new Vector2(safeArea.xMax / Screen.width, safeArea.yMax / Screen.height);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
