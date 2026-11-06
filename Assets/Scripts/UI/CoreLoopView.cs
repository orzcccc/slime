using System;
using SlimeBlindBox.App;
using SlimeBlindBox.Backend;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.UI
{
    public sealed class CoreLoopView : MonoBehaviour
    {
        private const string DisplayModeKey = "mainui.display.mode";
        private const int DisplayModeSlime = 0;
        private const int DisplayModeBox = 1;
        private const int InventoryPageSize = 8;
        private const int BadgeMaximum = 99;
        private const int DefaultBoxIntervalSeconds = 3 * 60 * 60;

        private static readonly Color TextColor = new Color32(59, 47, 58, 255);
        private static readonly Color ModeSelectedTextColor = Color.white;
        private static readonly Color AccentDarkColor = new Color32(35, 122, 88, 255);
        private static readonly Color PanelAltColor = new Color32(31, 29, 55, 255);

        private static TMP_FontAsset runtimeFont;
        private SlimeGameController controller;
        private MainUiReferences uiRefs;
        private GameObject[] pages;
        private BoxSlotItemView slotPrefab;
        private GameObject inventoryItemPrefab;
        private Sprite[] inventorySlimeSprites;
        private Sprite[] boxQualitySprites;
        private BoxOpenAnimator boxAnimator;
        private DateTime nextBoxAt;
        private int boxIntervalSeconds = DefaultBoxIntervalSeconds;
        private int lastShownSecond = -1;
        private bool isUiReady;
        private string selectedBoxKey = "WHITE";
        private Color selectedBoxColor = Color.white;
        private int inventoryPageIndex;

        private void Start()
        {
            controller = GetComponent<SlimeGameController>();
            if (!BuildUi()) return;
            isUiReady = true;
            controller.StateChanged += Render;
            Render();
        }

        private void OnDestroy()
        {
            if (controller != null) controller.StateChanged -= Render;
        }

        private void Update()
        {
            if (!isUiReady) return;
            var state = controller.GameState;
            if (state == null || string.IsNullOrEmpty(state.next_box_at) || nextBoxAt == default) return;

            var remaining = nextBoxAt.ToUniversalTime() - DateTime.UtcNow;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            var totalSeconds = (int)remaining.TotalSeconds;
            if (totalSeconds == lastShownSecond) return;
            lastShownSecond = totalSeconds;
            uiRefs.timerText.text = string.Format(MainUiText.NextBoxFormat, (int)remaining.TotalHours, remaining.Minutes, remaining.Seconds);
            uiRefs.productionBar.value = 1f - Mathf.Clamp01(totalSeconds / (float)boxIntervalSeconds);
        }

        private bool BuildUi()
        {
            runtimeFont = ChineseTmpFontProvider.GetFont();
            EnsureEventSystem();
            var prefab = Resources.Load<GameObject>("UI/MainUI");
            if (prefab == null)
            {
                Debug.LogError("缺少 Resources/UI/MainUI.prefab。");
                enabled = false;
                return false;
            }

            var instance = Instantiate(prefab, transform, false);
            uiRefs = instance.GetComponent<MainUiReferences>();
            if (uiRefs == null)
            {
                Debug.LogError("MainUI.prefab 缺少 MainUiReferences 组件。");
                enabled = false;
                return false;
            }

            foreach (var text in instance.GetComponentsInChildren<TMP_Text>(true))
                if (runtimeFont != null) text.font = runtimeFont;

            slotPrefab = Resources.Load<BoxSlotItemView>("UI/BoxSlotItem");
            inventoryItemPrefab = Resources.Load<GameObject>("UI/InventoryItem");
            inventorySlimeSprites = Resources.LoadAll<Sprite>("PixelAdventure/SlimeIdle");
            boxQualitySprites = new[]
            {
                Resources.Load<Sprite>("icon/宝箱1"),
                Resources.Load<Sprite>("icon/宝箱2"),
                Resources.Load<Sprite>("icon/宝箱3"),
                Resources.Load<Sprite>("icon/宝箱4"),
                Resources.Load<Sprite>("icon/宝箱5"),
            };
            pages = new[] { uiRefs.inventoryPage, uiRefs.boxPage, uiRefs.dictionaryPage };

            uiRefs.claimButton.onClick.AddListener(controller.ClaimBoxes);
            uiRefs.boxTab.onClick.AddListener(() => TogglePage(uiRefs.boxPage));
            uiRefs.inventoryTab.onClick.AddListener(() => TogglePage(uiRefs.inventoryPage));
            uiRefs.dictionaryTab.onClick.AddListener(() => TogglePage(uiRefs.dictionaryPage));
            uiRefs.slimeModeButton.onClick.AddListener(() => SetDisplayMode(DisplayModeSlime));
            uiRefs.boxModeButton.onClick.AddListener(() => SetDisplayMode(DisplayModeBox));
            uiRefs.inventoryPreviousPage.onClick.AddListener(() => ChangeInventoryPage(-1));
            uiRefs.inventoryNextPage.onClick.AddListener(() => ChangeInventoryPage(1));
            uiRefs.selectedBoxOpenButton.onClick.AddListener(OpenSelectedBox);
            BindDeveloperTools();

            boxAnimator = uiRefs.selectedBoxImage.gameObject.AddComponent<BoxOpenAnimator>();
            boxAnimator.Initialize(uiRefs.selectedBoxImage);
            InitializeSettings(instance.transform);
            SetDisplayMode(PlayerPrefs.GetInt(DisplayModeKey, DisplayModeSlime), false);
            ShowHome();
            return true;
        }

        private void BindDeveloperTools()
        {
            // GM 只在 Editor 和 Development Build 中出现，Release 构建不显示也不绑定。
            var available = Debug.isDebugBuild && controller.SupportsDebugTools;
            uiRefs.gmPanel.SetActive(available);
            if (!available) return;
            uiRefs.gmAddOne.onClick.AddListener(() => controller.DebugModifyBoxes(DebugBoxAction.Add, 1));
            uiRefs.gmAddEight.onClick.AddListener(() => controller.DebugModifyBoxes(DebugBoxAction.Add, 8));
            uiRefs.gmFill.onClick.AddListener(() => controller.DebugModifyBoxes(DebugBoxAction.Fill, 0));
            uiRefs.gmClear.onClick.AddListener(() => controller.DebugModifyBoxes(DebugBoxAction.Clear, 0));
            uiRefs.gmAdvance.onClick.AddListener(() => controller.DebugModifyBoxes(DebugBoxAction.AdvanceHours, 3));
        }

        private void InitializeSettings(Transform uiRoot)
        {
            var prefab = Resources.Load<GameObject>("UI/GameSettings");
            if (prefab == null)
            {
                Debug.LogError("缺少 Resources/UI/GameSettings.prefab。");
                uiRefs.settingsButton.interactable = false;
                return;
            }

            var instance = Instantiate(prefab, uiRoot, false);
            var settingsView = instance.GetComponent<GameSettingsView>();
            if (settingsView == null)
            {
                Debug.LogError("GameSettings.prefab 缺少 GameSettingsView 组件。");
                Destroy(instance);
                uiRefs.settingsButton.interactable = false;
                return;
            }

            settingsView.Initialize(runtimeFont);
            instance.SetActive(false);
            uiRefs.settingsButton.onClick.AddListener(settingsView.Show);
        }

        private void SetDisplayMode(int mode, bool save = true)
        {
            var showSlime = mode != DisplayModeBox;
            uiRefs.slimeArea.gameObject.SetActive(showSlime);
            uiRefs.boxShowcase.SetActive(!showSlime);
            uiRefs.slimeModeImage.color = showSlime ? Color.white : Color.clear;
            uiRefs.boxModeImage.color = showSlime ? Color.clear : Color.white;
            uiRefs.slimeModeText.color = showSlime ? ModeSelectedTextColor : TextColor;
            uiRefs.boxModeText.color = showSlime ? TextColor : ModeSelectedTextColor;
            if (!save) return;
            PlayerPrefs.SetInt(DisplayModeKey, showSlime ? DisplayModeSlime : DisplayModeBox);
            PlayerPrefs.Save();
        }

        private void TogglePage(GameObject target)
        {
            if (target.activeSelf) ShowHome();
            else ShowPage(target);
        }

        private void ShowHome() => ShowPage(null);

        private void ShowPage(GameObject target)
        {
            foreach (var page in pages) if (page != null) page.SetActive(page == target);
            uiRefs.contentArea.SetActive(target != null);
            uiRefs.homePage.SetActive(target == null);
        }

        private void Render()
        {
            if (!isUiReady) return;
            var user = controller.User;
            var state = controller.GameState;

            var totalCp = state?.total_cp ?? user?.total_cp ?? 0;
            uiRefs.resourceText.text = string.Format(MainUiText.TotalCpFormat, totalCp);
            uiRefs.messageText.text = controller.IsBusy ? MainUiText.Processing : controller.LastMessage;
            RenderPlatform(state);
            RenderWarehouseBadge(state);
            RenderInventory();
            RenderDictionary();
            RenderBoxInventory();
        }

        private void RenderPlatform(GameStateDto state)
        {
            uiRefs.boxText.text = state == null
                ? MainUiText.PendingUnknown
                : string.Format(MainUiText.PendingFormat, state.box_count, state.box_capacity);
            uiRefs.claimButton.interactable = !controller.IsBusy && state != null && state.box_count > 0;
            RenderSlots(state?.box_count ?? 0, state?.box_capacity ?? 0);

            boxIntervalSeconds = state != null && state.box_interval_seconds > 0 ? state.box_interval_seconds : DefaultBoxIntervalSeconds;
            nextBoxAt = default;
            lastShownSecond = -1;
            if (state == null) return;
            if (string.IsNullOrEmpty(state.next_box_at))
            {
                uiRefs.timerText.text = MainUiText.PlatformFull;
                uiRefs.productionBar.value = 1f;
                return;
            }
            if (!DateTime.TryParse(state.next_box_at, null, System.Globalization.DateTimeStyles.RoundtripKind, out nextBoxAt))
                uiRefs.timerText.text = MainUiText.SyncingTime;
        }

        private void RenderSlots(int filled, int capacity)
        {
            var content = uiRefs.boxSlotContent;
            if (slotPrefab == null || content == null) return;
            for (var index = content.childCount; index < capacity; index++) Instantiate(slotPrefab, content, false);
            for (var index = 0; index < content.childCount; index++)
            {
                var slot = content.GetChild(index);
                slot.gameObject.SetActive(index < capacity);
                if (index < capacity) slot.GetComponent<BoxSlotItemView>().SetFilled(index < filled);
            }
        }

        private void RenderWarehouseBadge(GameStateDto state)
        {
            var total = 0;
            if (state?.box_inventory != null) foreach (var box in state.box_inventory) total += box.quantity;
            uiRefs.warehouseBadge.SetActive(total > 0);
            uiRefs.warehouseBadgeText.text = total > BadgeMaximum ? MainUiText.BadgeOverflow : total.ToString();
        }

        private void RenderBoxInventory()
        {
            if (uiRefs.boxQualityContent == null) return;
            var boxQualityContent = uiRefs.boxQualityContent;
            Clear(boxQualityContent);
            var boxes = controller.GameState?.box_inventory;
            if (boxes == null) return;
            foreach (var box in boxes)
            {
                var key = box.key;
                var button = Button(boxQualityContent, $"{box.name}\n× {box.quantity}", () => SelectBox(key));
                button.interactable = !controller.IsBusy;
                var icon = new GameObject("BoxIcon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                icon.transform.SetParent(button.transform, false);
                icon.sprite = GetBoxSprite(key); icon.preserveAspect = true; icon.raycastTarget = false;
                var iconRect = icon.rectTransform;
                iconRect.anchorMin = new Vector2(.04f, .12f); iconRect.anchorMax = new Vector2(.35f, .88f);
                iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
                if (ColorUtility.TryParseHtmlString(box.color, out var color))
                {
                    button.GetComponent<Image>().color = new Color(color.r, color.g, color.b, key == selectedBoxKey ? 1f : .28f);
                    var label = button.GetComponentInChildren<TMP_Text>();
                    if (label != null)
                    {
                        label.rectTransform.anchorMin = new Vector2(.34f, 0);
                        label.rectTransform.anchorMax = Vector2.one;
                        label.rectTransform.offsetMin = new Vector2(4, 3);
                        label.rectTransform.offsetMax = new Vector2(-4, -3);
                        label.color = color;
                        var outline = label.gameObject.AddComponent<Outline>();
                        outline.effectColor = key == "WHITE" ? new Color32(90, 75, 85, 255) : new Color(0, 0, 0, .65f);
                        outline.effectDistance = new Vector2(1, -1);
                    }
                }
            }
            SelectBox(selectedBoxKey, false);
        }

        private void SelectBox(string key, bool updateMessage = true)
        {
            var box = Array.Find(controller.GameState?.box_inventory, item => item.key == key);
            if (box == null) return;
            selectedBoxKey = key;
            if (!ColorUtility.TryParseHtmlString(box.color, out selectedBoxColor)) selectedBoxColor = Color.white;
            uiRefs.selectedBoxName.text = string.Format(MainUiText.BoxNameFormat, box.name);
            uiRefs.selectedBoxCount.text = string.Format(MainUiText.BoxOwnedFormat, box.quantity);
            uiRefs.selectedBoxImage.sprite = GetBoxSprite(key);
            uiRefs.selectedBoxImage.color = Color.white;
            uiRefs.selectedBoxImage.preserveAspect = true;
            uiRefs.selectedBoxOpenButton.interactable = !controller.IsBusy && box.quantity > 0;
            if (updateMessage) uiRefs.boxOpenHint.text = box.quantity > 0 ? MainUiText.BoxOpenHint : MainUiText.BoxStockEmpty;
            if (updateMessage) RenderBoxInventory();
        }

        private Sprite GetBoxSprite(string key)
        {
            if (boxQualitySprites == null || boxQualitySprites.Length < 5) return null;
            switch (key)
            {
                case "GREEN": return boxQualitySprites[1];
                case "BLUE": return boxQualitySprites[2];
                case "PURPLE": return boxQualitySprites[3];
                case "RED": return boxQualitySprites[4];
                default: return boxQualitySprites[0];
            }
        }

        private void OpenSelectedBox()
        {
            if (boxAnimator != null) StartCoroutine(PlayBoxAndOpen());
            else controller.OpenBox(selectedBoxKey);
        }

        private System.Collections.IEnumerator PlayBoxAndOpen()
        {
            uiRefs.selectedBoxOpenButton.interactable = false;
            uiRefs.boxOpenHint.text = MainUiText.BoxOpening;
            yield return boxAnimator.Play(selectedBoxColor);
            controller.OpenBox(selectedBoxKey);
        }

        private void RenderInventory()
        {
            var inventoryContent = uiRefs.inventoryContent;
            if (inventoryContent == null) return;
            Clear(inventoryContent);
            if (controller.Inventory.Length == 0) CreateText(inventoryContent, MainUiText.EmptyInventory, 18, TextAlignmentOptions.Center);
            var totalPages = Mathf.Max(1, Mathf.CeilToInt(controller.Inventory.Length / (float)InventoryPageSize));
            inventoryPageIndex = Mathf.Clamp(inventoryPageIndex, 0, totalPages - 1);
            uiRefs.inventoryPageText.text = string.Format(MainUiText.PageFormat, inventoryPageIndex + 1, totalPages);
            uiRefs.inventoryPreviousPage.interactable = inventoryPageIndex > 0;
            uiRefs.inventoryNextPage.interactable = inventoryPageIndex < totalPages - 1;
            var start = inventoryPageIndex * InventoryPageSize;
            var end = Mathf.Min(start + InventoryPageSize, controller.Inventory.Length);
            for (var itemIndex = start; itemIndex < end; itemIndex++)
            {
                var item = controller.Inventory[itemIndex];
                var row = inventoryItemPrefab != null ? Instantiate(inventoryItemPrefab, inventoryContent) : Panel(inventoryContent, $"Slime_{item.slime_id}", PanelAltColor);
                row.name = $"Slime_{item.slime_id}";
                foreach (var text in row.GetComponentsInChildren<TMP_Text>(true)) if (runtimeFont != null) text.font = runtimeFont;
                var view = row.GetComponent<InventoryItemView>();
                if (view == null) continue;
                view.nameText.text = $"{item.name} [{item.rarity}]";
                view.detailText.text = $"数量 × {item.quantity}\nLv.{item.level}  CP {item.effective_cp}";
                ApplyRarity(view.nameText, item.rarity_key, item.rarity_color);
                if (inventorySlimeSprites.Length > 0) view.icon.sprite = inventorySlimeSprites[Mathf.Abs(item.slime_id) % inventorySlimeSprites.Length];
                if (ColorUtility.TryParseHtmlString(item.rarity_color, out var iconColor)) view.icon.color = Color.Lerp(Color.white, iconColor, .3f);
            }
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(inventoryContent as RectTransform);
            var scrollRect = inventoryContent.GetComponentInParent<ScrollRect>();
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        }

        private void ChangeInventoryPage(int delta)
        {
            inventoryPageIndex += delta;
            RenderInventory();
        }

        private void RenderDictionary()
        {
            var dictionaryContent = uiRefs.dictionaryContent;
            if (dictionaryContent == null) return;
            Clear(dictionaryContent);
            var data = controller.Dictionary;
            if (data == null) return;
            CreateText(dictionaryContent, $"收集进度：{data.discovered}/{data.total}（{data.completion:P0}）", 20, TextAlignmentOptions.Center);
            foreach (var item in data.entries)
            {
                var label = CreateText(dictionaryContent, item.discovered == 1 ? $"已发现  {item.name} [{item.rarity}]" : $"未发现  ??? [{item.rarity}]", 17, TextAlignmentOptions.MidlineLeft);
                ApplyRarity(label, item.rarity_key, item.rarity_color);
            }
        }

        private static GameObject Panel(Transform parent, string name, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static TMP_Text CreateText(Transform parent, string value, int size, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            if (runtimeFont != null) text.font = runtimeFont;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = TextColor;
            text.raycastTarget = false;
            textObject.GetComponent<LayoutElement>().preferredHeight = size + 14;
            return text;
        }

        private static void ApplyRarity(TMP_Text label, string rarityKey, string rarityColor)
        {
            if (label == null || string.IsNullOrEmpty(rarityKey)) return;
            label.gameObject.AddComponent<RarityTextEffect>().Initialize(label, rarityKey, rarityColor);
        }

        private static Button Button(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var buttonObject = Panel(parent, label, AccentDarkColor);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(action);
            var text = CreateText(buttonObject.transform, label, 19, TextAlignmentOptions.Center);
            text.color = Color.white;
            buttonObject.AddComponent<LayoutElement>().preferredWidth = 130;
            return button;
        }

        private static void Clear(Transform target)
        {
            for (var i = target.childCount - 1; i >= 0; i--) Destroy(target.GetChild(i).gameObject);
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
