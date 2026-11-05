using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    public sealed class ResponsiveMainUi : MonoBehaviour
    {
        [SerializeField] private RectTransform homeHero;
        [SerializeField] private RectTransform homeSide;
        [SerializeField] private HorizontalLayoutGroup bottomNavigation;
        [SerializeField] private CanvasScaler canvasScaler;

        private int lastWidth;
        private int lastHeight;

        public void Initialize(RectTransform hero, RectTransform side, HorizontalLayoutGroup navigation)
        {
            homeHero = hero;
            homeSide = side;
            bottomNavigation = navigation;
            canvasScaler = GetComponentInParent<CanvasScaler>();
            Apply();
        }

        private void Update()
        {
            if (lastWidth != Screen.width || lastHeight != Screen.height) Apply();
        }

        private void Apply()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            var aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.78f;
            var compact = aspect < 1.45f || Screen.width < 900;
            if (canvasScaler != null)
                canvasScaler.matchWidthOrHeight = aspect >= 1.7f ? .5f : aspect >= 1.45f ? .65f : .8f;

            if (homeHero != null && homeSide != null)
            {
                if (compact)
                {
                    SetAnchors(homeHero, new Vector2(.04f, .43f), new Vector2(.96f, .96f));
                    SetAnchors(homeSide, new Vector2(.04f, .03f), new Vector2(.96f, .40f));
                }
                else
                {
                    SetAnchors(homeHero, new Vector2(.04f, .08f), new Vector2(.64f, .96f));
                    SetAnchors(homeSide, new Vector2(.67f, .03f), new Vector2(.96f, .96f));
                }
            }

            if (bottomNavigation != null)
            {
                bottomNavigation.spacing = compact ? 4 : 10;
                bottomNavigation.padding = compact ? new RectOffset(8, 8, 8, 8) : new RectOffset(25, 25, 9, 9);
            }
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
