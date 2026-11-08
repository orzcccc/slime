using System;
using System.Collections.Generic;
using SlimeBlindBox.App;
using SlimeBlindBox.Backend;
using SlimeBlindBox.Desktop;
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
        private const int BadgeMaximum = 99;
        private const int DefaultBoxIntervalSeconds = 3 * 60 * 60;

        private static readonly Color TextColor = new Color32(59, 47, 58, 255);
        private static readonly Color ModeSelectedTextColor = Color.white;
        private static readonly Color ModeUnselectedTextColor = new Color32(197, 207, 240, 255);
        private static readonly Color AccentDarkColor = new Color32(35, 122, 88, 255);

        private static TMP_FontAsset runtimeFont;
        private SlimeGameController controller;
        private MainUiReferences uiRefs;
        private GameObject[] pages;
        private BoxSlotItemView slotPrefab;
        /// <summary>盲盒品质外观，按品质键从 Resources/Sprites/MainPage/Props/Box_&lt;KEY&gt; 加载并缓存。</summary>
        private readonly Dictionary<string, Sprite> boxQualitySprites = new Dictionary<string, Sprite>();
        private const string BoxSpritePathPrefix = "Sprites/MainPage/Props/Box_";
        private InventoryPopupView inventoryView;
        private GameSettingsView settingsView;
        private BoxOpenAnimator boxAnimator;
        private bool isOpeningBox;
        private DateTime nextBoxAt;
        private int boxIntervalSeconds = DefaultBoxIntervalSeconds;
        private int lastShownSecond = -1;
        private bool isUiReady;
        private string selectedBoxKey = "WHITE";
        private Color selectedBoxColor = Color.white;

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
            pages = new[] { uiRefs.boxPage, uiRefs.dictionaryPage };

            uiRefs.claimButton.onClick.AddListener(controller.ClaimBoxes);
            uiRefs.boxTab.onClick.AddListener(() => TogglePage(uiRefs.boxPage));
            uiRefs.dictionaryTab.onClick.AddListener(() => TogglePage(uiRefs.dictionaryPage));
            uiRefs.slimeModeButton.onClick.AddListener(() => SetDisplayMode(DisplayModeSlime));
            uiRefs.boxModeButton.onClick.AddListener(() => SetDisplayMode(DisplayModeBox));
            uiRefs.selectedBoxOpenButton.onClick.AddListener(OpenSelectedBox);
            BindDeveloperTools();

            boxAnimator = uiRefs.selectedBoxImage.gameObject.AddComponent<BoxOpenAnimator>();
            boxAnimator.Initialize(uiRefs.selectedBoxImage);
            InitializeInventory(instance.transform);
            InitializeSettings(instance.transform);
            SetDisplayMode(PlayerPrefs.GetInt(DisplayModeKey, DisplayModeSlime), false);
            ShowHome();
            return true;
        }

        private void BindDeveloperTools()
        {
            // GM 是否可用由后端决定（GameBackendFactory.DeveloperToolsEnabled），正式打包的游戏同样适用。
            var available = controller.SupportsDebugTools;
            uiRefs.gmPanel.SetActive(available);
            if (!available) return;
            uiRefs.gmAddOne.onClick.AddListener(() => controller.DebugModifyBoxes(DebugBoxAction.Add, 1));
            uiRefs.gmAddEight.onClick.AddListener(() => controller.DebugModifyBoxes(DebugBoxAction.Add, 8));
            uiRefs.gmFill.onClick.AddListener(() => controller.DebugModifyBoxes(DebugBoxAction.Fill, 0));
            uiRefs.gmClear.onClick.AddListener(() => controller.DebugModifyBoxes(DebugBoxAction.Clear, 0));
            uiRefs.gmAdvance.onClick.AddListener(() => controller.DebugModifyBoxes(DebugBoxAction.AdvanceHours, 3));
        }

        private void InitializeInventory(Transform uiRoot)
        {
            var prefab = Resources.Load<InventoryPopupView>("UI/InventoryPopup");
            if (prefab == null)
            {
                Debug.LogError("缺少 Resources/UI/InventoryPopup.prefab 或其 InventoryPopupView 组件。");
                uiRefs.inventoryTab.interactable = false;
                return;
            }

            inventoryView = Instantiate(prefab, uiRoot, false);
            inventoryView.Initialize(controller, runtimeFont, OnInventoryClosed);
            inventoryView.gameObject.SetActive(false);
            uiRefs.inventoryTab.onClick.AddListener(OpenInventory);
        }

        private void OpenInventory()
        {
            ShowHome();
            SetHudVisible(false);
            inventoryView.Show();
        }

        private void OnInventoryClosed(InventoryExit exit)
        {
            SetHudVisible(true);
            switch (exit)
            {
                case InventoryExit.Dictionary:
                    ShowPage(uiRefs.dictionaryPage);
                    break;
                case InventoryExit.BoxPage:
                    ShowPage(uiRefs.boxPage);
                    break;
                default:
                    ShowHome();
                    break;
            }
        }

        /// <summary>背包面板占据窗口中央，打开期间隐藏岛屿、顶栏、底栏和消息条。</summary>
        private void SetHudVisible(bool visible)
        {
            uiRefs.homePage.SetActive(visible);
            uiRefs.topBar.SetActive(visible);
            uiRefs.bottomBar.SetActive(visible);
            uiRefs.messageText.gameObject.SetActive(visible);
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
            settingsView = instance.GetComponent<GameSettingsView>();
            if (settingsView == null)
            {
                Debug.LogError("GameSettings.prefab 缺少 GameSettingsView 组件。");
                Destroy(instance);
                uiRefs.settingsButton.interactable = false;
                return;
            }

            instance.SetActive(false);
            settingsView.Initialize(controller, runtimeFont, () => isOpeningBox, OnSettingsOpened, OnSettingsClosed,
                GetComponent<DesktopTrayController>());
            uiRefs.settingsButton.onClick.AddListener(() => settingsView.Show());
        }

        /// <summary>设置面板与背包同尺寸，打开期间隐藏岛屿和顶栏、底栏；托盘打开设置时先收起背包。</summary>
        private void OnSettingsOpened()
        {
            if (inventoryView != null && inventoryView.gameObject.activeSelf) inventoryView.gameObject.SetActive(false);
            ShowHome();
            SetHudVisible(false);
        }

        private void OnSettingsClosed() => SetHudVisible(true);

        private void SetDisplayMode(int mode, bool save = true)
        {
            var showSlime = mode != DisplayModeBox;
            uiRefs.slimeArea.gameObject.SetActive(showSlime);
            uiRefs.boxShowcase.SetActive(!showSlime);
            uiRefs.slimeModeImage.color = showSlime ? Color.white : Color.clear;
            uiRefs.boxModeImage.color = showSlime ? Color.clear : Color.white;
            uiRefs.slimeModeText.color = showSlime ? ModeSelectedTextColor : ModeUnselectedTextColor;
            uiRefs.boxModeText.color = showSlime ? ModeUnselectedTextColor : ModeSelectedTextColor;
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
            RenderDictionary();
            RenderBoxInventory();
        }

        private void RenderPlatform(GameStateDto state)
        {
            uiRefs.boxText.text = state == null
                ? MainUiText.PendingUnknown
                : string.Format(MainUiText.PendingFormat, state.box_count, state.box_capacity);
            uiRefs.claimButton.interactable = !controller.IsBusy && state != null && state.box_count > 0;
            RenderSlots(state);

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

        /// <summary>凝聚台槽位：已产出的盲盒显示对应品质的宝箱和品质底色，其余为空槽。</summary>
        private void RenderSlots(GameStateDto state)
        {
            var content = uiRefs.boxSlotContent;
            if (slotPrefab == null || content == null) return;
            var filled = state?.box_count ?? 0;
            var capacity = state?.box_capacity ?? 0;
            var pending = state?.pending_boxes;
            for (var index = content.childCount; index < capacity; index++) Instantiate(slotPrefab, content, false);
            for (var index = 0; index < content.childCount; index++)
            {
                var slot = content.GetChild(index);
                slot.gameObject.SetActive(index < capacity);
                if (index >= capacity) continue;
                var view = slot.GetComponent<BoxSlotItemView>();
                if (index >= filled)
                {
                    view.SetEmpty();
                    continue;
                }
                var key = pending != null && index < pending.Length ? pending[index] : null;
                view.SetFilled(GetBoxSprite(key), GetBoxColor(state, key));
            }
        }

        /// <summary>盲盒品质色取后端下发的仓库条目颜色；找不到时用白色。</summary>
        private static Color GetBoxColor(GameStateDto state, string key)
        {
            if (state?.box_inventory == null || string.IsNullOrEmpty(key)) return Color.white;
            foreach (var box in state.box_inventory)
                if (box.key == key && ColorUtility.TryParseHtmlString(box.color, out var color)) return color;
            return Color.white;
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
            if (string.IsNullOrEmpty(key)) key = "WHITE";
            if (boxQualitySprites.TryGetValue(key, out var sprite)) return sprite;
            sprite = Resources.Load<Sprite>(BoxSpritePathPrefix + key);
            if (sprite == null)
            {
                Debug.LogWarning("缺少盲盒品质外观：" + key);
                if (key != "WHITE") sprite = GetBoxSprite("WHITE");
            }
            boxQualitySprites[key] = sprite;
            return sprite;
        }

        private void OpenSelectedBox()
        {
            if (boxAnimator != null) StartCoroutine(PlayBoxAndOpen());
            else controller.OpenBox(selectedBoxKey);
        }

        private System.Collections.IEnumerator PlayBoxAndOpen()
        {
            isOpeningBox = true;
            uiRefs.selectedBoxOpenButton.interactable = false;
            uiRefs.boxOpenHint.text = MainUiText.BoxOpening;
            try
            {
                yield return boxAnimator.Play(selectedBoxColor, LocalSettings.FastOpenAnimation);
                controller.OpenBox(selectedBoxKey);
            }
            finally
            {
                isOpeningBox = false;
            }
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
