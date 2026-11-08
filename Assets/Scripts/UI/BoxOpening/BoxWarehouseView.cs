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
        [SerializeField] private TMP_Text probabilityLinePrefab;

        private readonly List<BoxWarehouseRow> rows = new List<BoxWarehouseRow>();
        private readonly List<TMP_Text> probabilityLines = new List<TMP_Text>();
        private int usedProbabilityLines;
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

        private void OnEnable()
        {
            if (controller != null) controller.StateChanged += RefreshStock;
        }

        private void OnDisable()
        {
            if (controller != null) controller.StateChanged -= RefreshStock;
        }

        private void RefreshStock() => Render(selectedKey);

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (probabilityLayer.activeSelf) probabilityLayer.SetActive(false);
            else Close(BoxWarehouseExit.Home);
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
            openButton.interactable = !hasStock || !controller.IsBusy;
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
            if (!controller.IsBusy) Close(BoxWarehouseExit.OpenBox);
        }

        private void ShowProbability()
        {
            usedProbabilityLines = 0;
            ProbabilityLine(BoxOpeningUiText.ProbabilityHeader, 15, true);
            foreach (var box in GameBalanceConfig.BoxRarities)
            {
                ProbabilityLine(box.Name + "盲盒", 14, true);
                var parts = new List<string>();
                var probabilities = GameBalanceConfig.RarityProbabilities(box.Key);
                for (var i = 0; i < GameBalanceConfig.SlimeRarities.Count; i++)
                    if (probabilities[i] > 0) parts.Add(GameBalanceConfig.SlimeRarities[i].Name + " " + probabilities[i].ToString("0.######%"));
                ProbabilityLine(string.Join(" · ", parts), 12, false);
            }
            ProbabilityLine(BoxOpeningUiText.ProbabilityNote, 11, false, "#9fb3db");
            for (var i = usedProbabilityLines; i < probabilityLines.Count; i++) probabilityLines[i].gameObject.SetActive(false);
            probabilityLayer.SetActive(true);
        }

        private TMP_Text ProbabilityLine(string value, int size, bool bold, string color = "#eef1ff")
        {
            if (usedProbabilityLines >= probabilityLines.Count)
                probabilityLines.Add(Instantiate(probabilityLinePrefab, probabilityContent, false));
            var text = probabilityLines[usedProbabilityLines++];
            text.gameObject.SetActive(true);
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = RarityStyle.Parse(color);
            text.enableWordWrapping = true;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
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
}
