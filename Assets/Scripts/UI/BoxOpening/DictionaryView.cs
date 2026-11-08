using System;
using System.Collections.Generic;
using SlimeBlindBox.App;
using SlimeBlindBox.Backend;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.UI
{
    /// <summary>T01/T02 图鉴：收集进度、全部/已收集/未收集分页、4×3 卡片和条目详情。</summary>
    public sealed class DictionaryView : MonoBehaviour
    {
        private const int PageSize = 12;

        public Button closeButton;
        public GameObject listPage;
        public TMP_Text collectedText;
        public Slider progress;
        public TMP_Text percentText;
        public Button allTab;
        public Button collectedTab;
        public Button missingTab;
        public RectTransform grid;
        public DictionaryEntryView entryPrefab;
        public GameObject emptyState;
        public TMP_Text emptyText;
        public Button previousButton;
        public Button nextButton;
        public TMP_Text pageText;
        public GameObject detailPage;
        public Button backButton;
        public Button previousEntryButton;
        public Button nextEntryButton;
        public Image ribbon;
        public TMP_Text rarityText;
        public Image slimeImage;
        public TMP_Text nameText;
        public TMP_Text numberText;
        public TMP_Text firstObtainedText;
        public TMP_Text baseCpText;
        public TMP_Text quantityText;
        public TMP_Text probabilityText;
        public TMP_Text descriptionText;
        public Button bagButton;

        private enum Filter { All, Collected, Missing }

        private readonly List<DictionaryItemDto> visible = new List<DictionaryItemDto>();
        private readonly List<DictionaryEntryView> entries = new List<DictionaryEntryView>();
        private DictionaryItemDto[] data;
        private SlimeGameController controller;
        private TMP_FontAsset font;
        private Action<DictionaryExit> closed;
        private Filter filter = Filter.All;
        private int page;
        private int detailId = -1;

        public void Initialize(SlimeGameController gameController, TMP_FontAsset runtimeFont, Action<DictionaryExit> onClosed)
        {
            controller = gameController;
            font = runtimeFont;
            closed = onClosed;
            foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.font = font;
            for (var i = 0; i < PageSize; i++)
            {
                var card = Instantiate(entryPrefab, grid, false);
                foreach (var text in card.GetComponentsInChildren<TMP_Text>(true)) text.font = font;
                entries.Add(card);
            }
            closeButton.onClick.AddListener(() => Close(DictionaryExit.Home));
            allTab.onClick.AddListener(() => SetFilter(Filter.All));
            collectedTab.onClick.AddListener(() => SetFilter(Filter.Collected));
            missingTab.onClick.AddListener(() => SetFilter(Filter.Missing));
            previousButton.onClick.AddListener(() => ChangePage(-1));
            nextButton.onClick.AddListener(() => ChangePage(1));
            backButton.onClick.AddListener(CloseDetail);
            previousEntryButton.onClick.AddListener(() => StepDetail(-1));
            nextEntryButton.onClick.AddListener(() => StepDetail(1));
            bagButton.onClick.AddListener(() => Close(DictionaryExit.Bag));
            detailPage.SetActive(false);
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (controller != null) controller.StateChanged += Refresh;
        }

        private void OnDisable()
        {
            if (controller != null) controller.StateChanged -= Refresh;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            page = 0;
            filter = Filter.All;
            detailPage.SetActive(false);
            listPage.SetActive(true);
            Refresh();
        }

        private void Refresh()
        {
            data = controller.Dictionary?.entries;
            if (data == null)
            {
                emptyState.SetActive(true);
                emptyText.text = controller.IsBusy ? BoxOpeningUiText.Loading : BoxOpeningUiText.LoadFailed;
                return;
            }
            var total = data.Length;
            var collected = 0;
            foreach (var item in data) if (item.discovered == 1) collected++;
            collectedText.text = string.Format(BoxOpeningUiText.CollectedFormat, collected, total);
            progress.value = total > 0 ? collected / (float)total : 0f;
            percentText.text = string.Format(BoxOpeningUiText.CompletionFormat, progress.value);
            visible.Clear();
            foreach (var item in data)
                if (filter == Filter.All || (filter == Filter.Collected && item.discovered == 1) || (filter == Filter.Missing && item.discovered != 1))
                    visible.Add(item);
            var pages = Mathf.Max(1, Mathf.CeilToInt(visible.Count / (float)PageSize));
            page = Mathf.Clamp(page, 0, pages - 1);
            pageText.text = string.Format(BoxOpeningUiText.PageFormat, page + 1, pages);
            previousButton.interactable = page > 0;
            nextButton.interactable = page < pages - 1;
            emptyState.SetActive(visible.Count == 0);
            emptyText.text = BoxOpeningUiText.NotFound;
            for (var i = 0; i < entries.Count; i++)
            {
                var idx = page * PageSize + i;
                var show = idx < visible.Count;
                entries[i].gameObject.SetActive(show);
                if (show) entries[i].Bind(visible[idx], OpenDetail);
            }
            if (detailId >= 0) ShowDetail();
        }

        private void SetFilter(Filter next)
        {
            filter = next;
            page = 0;
            Refresh();
        }

        private void ChangePage(int delta)
        {
            page += delta;
            Refresh();
        }

        private void OpenDetail(int id)
        {
            detailId = id;
            foreach (var entry in entries) entry.newDot.SetActive(false);
            listPage.SetActive(false);
            detailPage.SetActive(true);
            ShowDetail();
        }

        private void ShowDetail()
        {
            if (data == null) return;
            var index = Array.FindIndex(data, d => d.slime_id == detailId);
            if (index < 0) return;
            var item = data[index];
            var color = RarityStyle.Parse(item.rarity_color);
            ribbon.color = color;
            rarityText.text = item.rarity;
            rarityText.color = RarityStyle.TextOn(color);
            slimeImage.sprite = SlimeArtCatalog.Slime(item.slime_id);
            slimeImage.color = item.discovered == 1 ? Color.white : new Color(.45f, .47f, .55f, .85f);
            nameText.text = item.discovered == 1 ? item.name : BoxOpeningUiText.UnknownName;
            numberText.text = "No." + item.number.ToString("00");
            firstObtainedText.text = item.discovered == 1
                ? string.Format(BoxOpeningUiText.FirstObtainedTimeFormat, FormatDate(item.first_obtained_at))
                : BoxOpeningUiText.EntryMissing;
            baseCpText.text = string.Format(BoxOpeningUiText.BaseCpFormat, item.base_cp);
            quantityText.text = string.Format(BoxOpeningUiText.QuantityRowFormat, item.quantity);
            probabilityText.text = string.Format(BoxOpeningUiText.BaseProbabilityFormat, item.base_probability);
            descriptionText.text = item.description ?? string.Empty;
            bagButton.interactable = item.discovered == 1 && item.quantity > 0;
            previousEntryButton.interactable = index > 0;
            nextEntryButton.interactable = index < data.Length - 1;
        }

        private void StepDetail(int delta)
        {
            if (data == null) return;
            var index = Mathf.Clamp(Array.FindIndex(data, d => d.slime_id == detailId) + delta, 0, data.Length - 1);
            detailId = data[index].slime_id;
            ShowDetail();
        }

        private void CloseDetail()
        {
            detailId = -1;
            detailPage.SetActive(false);
            listPage.SetActive(true);
            Refresh();
        }

        private void Close(DictionaryExit exit)
        {
            gameObject.SetActive(false);
            closed?.Invoke(exit);
        }

        private static string FormatDate(string iso)
        {
            return DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t)
                ? t.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "--";
        }
    }

    public enum DictionaryExit
    {
        Home,
        Bag,
    }
}
