using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    [RequireComponent(typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter))]
    public sealed class AdaptiveGridLayout : MonoBehaviour
    {
        [SerializeField] private float minimumCellWidth = 230f;
        [SerializeField] private float cellHeight = 150f;
        [SerializeField] private int maximumColumns = 4;

        private RectTransform rectTransform;
        private GridLayoutGroup grid;
        private float lastWidth = -1f;

        public void Initialize(float minWidth, float height, int maxColumns)
        {
            minimumCellWidth = minWidth;
            cellHeight = height;
            maximumColumns = maxColumns;
            CacheComponents();
            Apply();
        }

        private void Awake()
        {
            CacheComponents();
            Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled) return;
            Apply();
        }

        private void CacheComponents()
        {
            rectTransform = GetComponent<RectTransform>();
            grid = GetComponent<GridLayoutGroup>();
            var fitter = GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void Apply()
        {
            if (rectTransform == null || grid == null) CacheComponents();
            var width = rectTransform.rect.width;
            if (width <= 0f || Mathf.Approximately(width, lastWidth)) return;
            lastWidth = width;

            var usableWidth = Mathf.Max(minimumCellWidth, width - grid.padding.horizontal);
            var columns = Mathf.Clamp(
                Mathf.FloorToInt((usableWidth + grid.spacing.x) / (minimumCellWidth + grid.spacing.x)),
                1,
                maximumColumns);
            var cellWidth = (usableWidth - grid.spacing.x * (columns - 1)) / columns;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(cellWidth, cellHeight);
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }
    }
}
