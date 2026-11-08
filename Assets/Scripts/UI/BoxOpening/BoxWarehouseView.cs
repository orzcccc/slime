using System;
using System.Collections.Generic;
using SlimeBlindBox.App;
using SlimeBlindBox.Backend;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.UI
{
    /// <summary>M04 盲盒仓库：五种品质从高到低排列，单选，数量为 0 的置灰；一次打开 1 个。</summary>
    public sealed class BoxWarehouseView : MonoBehaviour
    {
        public Button closeButton;
        public RectTransform rowList;
        public BoxWarehouseRow rowPrefab;
        public TMP_Text selectedText;
        public Button openButton;
        public Button probabilityButton;
        public GameObject probabilityLayer;
        public Button probabilityClose;
        public RectTransform probabilityContent;

        private readonly List<BoxWarehouseRow> rows = new List<BoxWarehouseRow>();
        private SlimeGameController controller;
        private TMP_FontAsset font;
        private Action<BoxWarehouseExit> closed;
        private string selectedKey;

        public string SelectedKey => selectedKey;

        public void Initialize(SlimeGameController gameController, TMP_FontAsset runtimeFont, Action<BoxWarehouseExit> onClosed)
        {
            controller = gameController;
            font = runtimeFont;
            closed = onClosed;
            foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.font = font;
            closeButton.onClick.AddListener(() => Close(BoxWarehouseExit.Home));
            openButton.onClick.AddListener(OpenSelected);
            probabilityButton.onClick.AddListener(() => ShowProbability());
            probabilityClose.onClick.AddListener(() => probabilityLayer.SetActive(false));
            gameObject.SetActive(false);
        }

        public void Show(string preferredKey = null)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            probabilityLayer.SetActive(false);
            Render(preferredKey);
        }

        private void Render(string preferredKey)
        {
            var inventory = controller.GameState?.box_inventory;
            var ordered = new List<BoxInventoryItemDto>();
            if (inventory != null)
            {
                for (var i = GameBalanceConfig.BoxRarities.Count - 1; i >= 0; i--)
                {
                    var def = GameBalanceConfig.BoxRarities[i];
                    var item = Array.Find(inventory, b => b != null && b.key == def.Key);
                    ordered.Add(item ?? new BoxInventoryItemDto { key = def.Key, name = def.Name, color = def.Color, quantity = 0 });
                }
            }
            while (rows.Count < ordered.Count)
            {
                var row = Instantiate(rowPrefab, rowList, false);
                foreach (var text in row.GetComponentsInChildren<TMP_Text>(true)) text.font = font;
                rows.Add(row);
            }
            for (var i = 0; i < rows.Count; i++)
            {
                var show = i < ordered.Count;
                rows[i].gameObject.SetActive(show);
                if (!show) continue;
                var item = ordered[i];
                var active = item.quantity > 0;
                var key = item.key;
                rows[i].Bind(item, active, () =>
                {
                    selectedKey = key;
                    RenderRows(ordered);
                    RefreshSelected();
                });
                rows[i].transform.SetSiblingIndex(i);
            }
            var preferred = Array.Find(ordered.ToArray(), b => b.quantity > 0 && b.key == preferredKey);
            if (preferred == null) preferred = ordered.Find(b => b.quantity > 0);
            selectedKey = preferred?.key;
            RenderRows(ordered);
            RefreshSelected();
        }

        private List<BoxInventoryItemDto> current;
        private void RenderRows(List<BoxInventoryItemDto> ordered)
        {
            current = ordered;
            for (var i = 0; i < rows.Count && i < ordered.Count; i++) rows[i].SetSelected(ordered[i].key == selectedKey);
        }

        private void RefreshSelected()
        {
            var item = current?.Find(b => b.key == selectedKey);
            var hasStock = item != null && item.quantity > 0;
            selectedText.text = item != null && hasStock
                ? string.Format(BoxOpeningUiText.SelectedFormat, item.name + "盲盒", string.Format(BoxOpeningUiText.QuantityFormat, item.quantity))
                : BoxOpeningUiText.GoIsland;
            openButton.interactable = hasStock && !controller.IsBusy;
            openButton.GetComponentInChildren<TMP_Text>().text = hasStock ? BoxOpeningUiText.OpenOne : BoxOpeningUiText.GoIsland;
        }

        private void OpenSelected()
        {
            var item = current?.Find(b => b.key == selectedKey);
            if (item == null || item.quantity <= 0)
            {
                Close(BoxWarehouseExit.Island);
                return;
            }
            closed?.Invoke(BoxWarehouseExit.OpenBox);
        }

        private void ShowProbability()
        {
            for (var i = probabilityContent.childCount - 1; i >= 0; i--) Destroy(probabilityContent.GetChild(i).gameObject);
            var header = ProbabilityLine(BoxOpeningUiText.ProbabilityHeader, 15, true);
            foreach (var box in GameBalanceConfig.BoxRarities)
            {
                var title = ProbabilityLine(box.Name + "盲盒", 14, true);
                var parts = new List<string>();
                var probabilities = GameBalanceConfig.RarityProbabilities(box.Key);
                for (var i = 0; i < GameBalanceConfig.SlimeRarities.Count; i++)
                    if (probabilities[i] >= .00005) parts.Add(GameBalanceConfig.SlimeRarities[i].Name + " " + probabilities[i].ToString("P1"));
                ProbabilityLine(string.Join(" · ", parts), 12, false);
            }
            ProbabilityLine(BoxOpeningUiText.ProbabilityNote, 11, false, "#9fb3db");
            probabilityLayer.SetActive(true);
        }

        private TMP_Text ProbabilityLine(string value, int size, bool bold, string color = "#eef1ff")
        {
            var go = new GameObject("Line", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(probabilityContent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = RarityStyle.Parse(color);
            text.enableWordWrapping = true;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            go.GetComponent<LayoutElement>().preferredHeight = size + 8;
            return text;
        }

        private void Close(BoxWarehouseExit exit)
        {
            gameObject.SetActive(false);
            closed?.Invoke(exit);
        }
    }

    public enum BoxWarehouseExit
    {
        Home,
        Island,
        OpenBox,
    }

    public sealed class BoxWarehouseRow : MonoBehaviour
    {
        public Button button;
        public Image background;
        public Image icon;
        public TMP_Text nameText;
        public TMP_Text boosterText;
        public TMP_Text quantityText;
        public GameObject selectedBorder;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private Sprite disabledSprite;
        [SerializeField] private Sprite starOn;
        [SerializeField] private Sprite starOff;
        [SerializeField] private Image[] starImages;

        public void Bind(BoxInventoryItemDto item, bool active, Action onClick)
        {
            icon.sprite = BoxVisuals.Sprite(item.key);
            icon.color = active ? Color.white : new Color(1, 1, 1, .35f);
            nameText.text = item.name + "盲盒";
            nameText.color = active ? Color.white : new Color(.7f, .75f, .85f, .55f);
            quantityText.text = item.quantity > 9999 ? BoxOpeningUiText.QuantityCap : string.Format(BoxOpeningUiText.QuantityFormat, item.quantity);
            quantityText.color = active ? new Color32(201, 214, 240, 255) : new Color(1, 1, 1, .3f);
            var stars = BoxVisuals.Rank(item.key);
            boosterText.text = stars <= 0 ? BoxOpeningUiText.BoosterPrefix + " " + BoxOpeningUiText.BoosterNone : BoxOpeningUiText.BoosterPrefix;
            for (var i = 0; i < starImages.Length; i++)
            {
                starImages[i].gameObject.SetActive(i < stars);
                starImages[i].sprite = i < stars ? starOn : starOff;
                starImages[i].color = active ? Color.white : new Color(1, 1, 1, .35f);
            }
            background.sprite = active ? normalSprite : disabledSprite;
            button.interactable = active;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
            selectedBorder.SetActive(false);
        }

        public void SetSelected(bool selected)
        {
            background.sprite = selected ? selectedSprite : (button.interactable ? normalSprite : disabledSprite);
            selectedBorder.SetActive(selected);
        }
    }
}
