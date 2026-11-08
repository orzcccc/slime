using System;
using System.Collections.Generic;
using SlimeBlindBox.App;
using SlimeBlindBox.Backend;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.UI
{
    /// <summary>关闭背包后要回到的界面。</summary>
    public enum InventoryExit
    {
        Home,
        Dictionary,
        BoxPage,
    }

    /// <summary>
    /// 背包弹窗：B01 列表、排序下拉、B02 品质筛选层和 B03 史莱姆详情。
    /// 只展示 SlimeGameController 已从后端读取的数据，排序与筛选只影响显示顺序。
    /// </summary>
    public sealed class InventoryPopupView : MonoBehaviour, IScrollHandler
    {
        private const int PageSize = 8;

        private enum StateAction
        {
            None,
            Retry,
            OpenBoxes,
            ResetFilter,
        }

        [Header("面板")]
        [SerializeField] private Button closeButton;

        [Header("列表 B01")]
        [SerializeField] private GameObject listPage;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private Button sortButton;
        [SerializeField] private TMP_Text sortText;
        [SerializeField] private Button filterButton;
        [SerializeField] private TMP_Text filterText;
        [SerializeField] private RectTransform cardGrid;
        [SerializeField] private InventoryItemView cardPrefab;
        [SerializeField] private GameObject statePanel;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private Button stateButton;
        [SerializeField] private TMP_Text stateButtonText;
        [SerializeField] private Button previousPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private TMP_Text pageText;

        [Header("排序下拉")]
        [SerializeField] private GameObject sortLayer;
        [SerializeField] private Button sortMask;
        [SerializeField] private RectTransform sortOptionList;

        [Header("品质筛选层 B02")]
        [SerializeField] private GameObject filterLayer;
        [SerializeField] private Button filterMask;
        [SerializeField] private RectTransform filterOptionGrid;
        [SerializeField] private Button filterResetButton;
        [SerializeField] private Button filterConfirmButton;
        [SerializeField] private QualityOptionItemView optionPrefab;

        [Header("详情 B03")]
        [SerializeField] private SlimeDetailView detailView;

        private readonly List<InventoryItemDto> visible = new List<InventoryItemDto>();
        private readonly List<InventoryItemView> cards = new List<InventoryItemView>();
        private readonly List<QualityOptionItemView> sortOptions = new List<QualityOptionItemView>();
        private readonly List<QualityOptionItemView> filterOptions = new List<QualityOptionItemView>();
        private readonly List<string> filterOptionKeys = new List<string>();
        private readonly HashSet<string> appliedFilter = new HashSet<string>();
        private readonly HashSet<string> pendingFilter = new HashSet<string>();

        private SlimeGameController controller;
        private TMP_FontAsset font;
        private Action<InventoryExit> closed;
        private InventorySortMode sortMode = InventorySortMode.Quality;
        private StateAction stateAction;
        private int pageIndex;
        private int detailIndex = -1;
        private int detailSlimeId;

        private bool LayerOpen => sortLayer.activeSelf || filterLayer.activeSelf;
        private bool DetailOpen => detailView.gameObject.activeSelf;

        public void Initialize(SlimeGameController gameController, TMP_FontAsset runtimeFont, Action<InventoryExit> onClosed)
        {
            controller = gameController;
            font = runtimeFont;
            closed = onClosed;
            ApplyFont(transform);

            for (var slot = 0; slot < PageSize; slot++)
            {
                var card = Instantiate(cardPrefab, cardGrid, false);
                ApplyFont(card.transform);
                var cardSlot = slot;
                card.button.onClick.AddListener(() => OpenDetail(pageIndex * PageSize + cardSlot));
                cards.Add(card);
            }

            foreach (var mode in InventoryListQuery.SortModes)
            {
                var option = CreateOption(sortOptionList);
                option.Bind(InventoryListQuery.SortLabel(mode), false, Color.white);
                var selected = mode;
                option.button.onClick.AddListener(() => SelectSort(selected));
                sortOptions.Add(option);
            }

            // 品质档位来自数值配置；接入 PlayFab 后改为云端下发的品质表。
            foreach (var rarity in GameBalanceConfig.SlimeRarities)
            {
                var option = CreateOption(filterOptionGrid);
                option.Bind(rarity.Name, true, RarityStyle.Parse(rarity.Color));
                var key = rarity.Key;
                option.button.onClick.AddListener(() => TogglePendingFilter(key));
                filterOptions.Add(option);
                filterOptionKeys.Add(key);
            }

            closeButton.onClick.AddListener(() => Close(InventoryExit.Home));
            sortButton.onClick.AddListener(OpenSort);
            filterButton.onClick.AddListener(OpenFilter);
            sortMask.onClick.AddListener(CloseLayers);
            filterMask.onClick.AddListener(CloseLayers);
            filterResetButton.onClick.AddListener(ResetPendingFilter);
            filterConfirmButton.onClick.AddListener(ApplyFilter);
            previousPageButton.onClick.AddListener(() => ChangePage(-1));
            nextPageButton.onClick.AddListener(() => ChangePage(1));
            stateButton.onClick.AddListener(RunStateAction);
            detailView.backButton.onClick.AddListener(CloseDetail);
            detailView.previousButton.onClick.AddListener(() => StepDetail(-1));
            detailView.nextButton.onClick.AddListener(() => StepDetail(1));
            detailView.dictionaryButton.onClick.AddListener(() => Close(InventoryExit.Dictionary));
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            pageIndex = 0;
            CloseLayers();
            CloseDetail();
            Render();
        }

        /// <summary>从图鉴进入对应史莱姆详情，清除可能隐藏目标的品质筛选。</summary>
        public void ShowSlime(int slimeId)
        {
            appliedFilter.Clear();
            pendingFilter.Clear();
            Render();
            var index = visible.FindIndex(item => item.slime_id == slimeId);
            if (index < 0) return;
            pageIndex = index / PageSize;
            Render();
            OpenDetail(index);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!listPage.activeSelf || LayerOpen) return;
            if (eventData.scrollDelta.y < 0f) ChangePage(1);
            else if (eventData.scrollDelta.y > 0f) ChangePage(-1);
        }

        private void OnEnable()
        {
            if (controller != null) controller.StateChanged += Render;
        }

        private void OnDisable()
        {
            if (controller != null) controller.StateChanged -= Render;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (LayerOpen) CloseLayers();
                else if (DetailOpen) CloseDetail();
                else Close(InventoryExit.Home);
                return;
            }
            if (!DetailOpen || LayerOpen) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) StepDetail(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow)) StepDetail(1);
        }

        private void Close(InventoryExit exit)
        {
            CloseLayers();
            CloseDetail();
            gameObject.SetActive(false);
            closed?.Invoke(exit);
        }

        private void Render()
        {
            if (controller == null) return;
            var inventory = controller.Inventory;
            InventoryListQuery.Apply(inventory, sortMode, appliedFilter, visible);
            RenderSummary(inventory);
            sortText.text = string.Format(InventoryUiText.SortFormat, InventoryListQuery.SortLabel(sortMode));
            filterText.text = FilterLabel();

            var totalPages = Mathf.Max(1, Mathf.CeilToInt(visible.Count / (float)PageSize));
            pageIndex = Mathf.Clamp(pageIndex, 0, totalPages - 1);
            for (var slot = 0; slot < cards.Count; slot++)
            {
                var index = pageIndex * PageSize + slot;
                var show = index < visible.Count;
                cards[slot].gameObject.SetActive(show);
                if (show) cards[slot].Bind(visible[index]);
            }
            pageText.text = string.Format(InventoryUiText.PageFormat, pageIndex + 1, totalPages);
            previousPageButton.interactable = pageIndex > 0;
            nextPageButton.interactable = pageIndex < totalPages - 1;
            RenderState(inventory.Length);
            if (DetailOpen) RefreshDetail();
        }

        private void RenderSummary(InventoryItemDto[] inventory)
        {
            var total = 0;
            var cpSum = 0;
            foreach (var item in inventory)
            {
                total += item.quantity;
                cpSum += item.effective_cp;
            }
            var totalCp = controller.GameState?.total_cp ?? cpSum;
            summaryText.text = string.Format(InventoryUiText.SummaryFormat, inventory.Length, total, totalCp);
        }

        private void RenderState(int ownedKinds)
        {
            string message;
            if (!controller.InventoryLoaded)
            {
                message = controller.IsBusy ? InventoryUiText.Loading : InventoryUiText.LoadFailed;
                stateAction = controller.IsBusy ? StateAction.None : StateAction.Retry;
            }
            else if (ownedKinds == 0)
            {
                message = InventoryUiText.EmptyBag;
                stateAction = StateAction.OpenBoxes;
            }
            else if (visible.Count == 0)
            {
                message = InventoryUiText.NoMatch;
                stateAction = StateAction.ResetFilter;
            }
            else
            {
                statePanel.SetActive(false);
                return;
            }

            statePanel.SetActive(true);
            stateText.text = message;
            stateButton.gameObject.SetActive(stateAction != StateAction.None);
            stateButtonText.text = stateAction == StateAction.Retry ? InventoryUiText.Retry
                : stateAction == StateAction.OpenBoxes ? InventoryUiText.EmptyBagAction
                : InventoryUiText.NoMatchAction;
        }

        private void RunStateAction()
        {
            switch (stateAction)
            {
                case StateAction.Retry:
                    controller.RetryInventory();
                    break;
                case StateAction.OpenBoxes:
                    Close(InventoryExit.BoxPage);
                    break;
                case StateAction.ResetFilter:
                    appliedFilter.Clear();
                    pageIndex = 0;
                    Render();
                    break;
            }
        }

        private string FilterLabel()
        {
            if (appliedFilter.Count == 0) return InventoryUiText.FilterAll;
            if (appliedFilter.Count > 1) return string.Format(InventoryUiText.FilterManyFormat, appliedFilter.Count);
            foreach (var key in appliedFilter)
                return string.Format(InventoryUiText.FilterOneFormat, GameBalanceConfig.FindSlimeRarity(key)?.Name ?? key);
            return InventoryUiText.FilterAll;
        }

        private void ChangePage(int delta)
        {
            var totalPages = Mathf.Max(1, Mathf.CeilToInt(visible.Count / (float)PageSize));
            var target = Mathf.Clamp(pageIndex + delta, 0, totalPages - 1);
            if (target == pageIndex) return;
            pageIndex = target;
            Render();
        }

        private void OpenSort()
        {
            CloseLayers();
            sortLayer.SetActive(true);
            for (var i = 0; i < sortOptions.Count; i++) sortOptions[i].SetSelected(InventoryListQuery.SortModes[i] == sortMode);
        }

        private void SelectSort(InventorySortMode mode)
        {
            sortMode = mode;
            pageIndex = 0;
            CloseLayers();
            Render();
        }

        private void OpenFilter()
        {
            CloseLayers();
            pendingFilter.Clear();
            pendingFilter.UnionWith(appliedFilter);
            filterLayer.SetActive(true);
            RefreshFilterOptions();
        }

        private void TogglePendingFilter(string key)
        {
            if (!pendingFilter.Remove(key)) pendingFilter.Add(key);
            RefreshFilterOptions();
        }

        private void ResetPendingFilter()
        {
            pendingFilter.Clear();
            RefreshFilterOptions();
        }

        private void ApplyFilter()
        {
            appliedFilter.Clear();
            appliedFilter.UnionWith(pendingFilter);
            pageIndex = 0;
            CloseLayers();
            Render();
        }

        private void RefreshFilterOptions()
        {
            for (var i = 0; i < filterOptions.Count; i++) filterOptions[i].SetSelected(pendingFilter.Contains(filterOptionKeys[i]));
        }

        private void CloseLayers()
        {
            sortLayer.SetActive(false);
            filterLayer.SetActive(false);
        }

        private void OpenDetail(int index)
        {
            if (index < 0 || index >= visible.Count) return;
            CloseLayers();
            detailIndex = index;
            detailSlimeId = visible[index].slime_id;
            listPage.SetActive(false);
            detailView.gameObject.SetActive(true);
            ShowDetailItem();
        }

        private void StepDetail(int delta)
        {
            var target = Mathf.Clamp(detailIndex + delta, 0, visible.Count - 1);
            if (target == detailIndex || target < 0) return;
            detailIndex = target;
            detailSlimeId = visible[target].slime_id;
            ShowDetailItem();
        }

        /// <summary>数据刷新后按 slime_id 重新定位详情条目；该史莱姆已不在列表中时回到列表。</summary>
        private void RefreshDetail()
        {
            detailIndex = visible.FindIndex(item => item.slime_id == detailSlimeId);
            if (detailIndex < 0) CloseDetail();
            else ShowDetailItem();
        }

        private void ShowDetailItem()
        {
            detailView.Show(visible[detailIndex], detailIndex > 0, detailIndex < visible.Count - 1);
        }

        private void CloseDetail()
        {
            detailIndex = -1;
            detailView.gameObject.SetActive(false);
            listPage.SetActive(true);
        }

        private QualityOptionItemView CreateOption(Transform parent)
        {
            var option = Instantiate(optionPrefab, parent, false);
            ApplyFont(option.transform);
            return option;
        }

        private void ApplyFont(Transform root)
        {
            if (font == null) return;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.font = font;
        }
    }
}
