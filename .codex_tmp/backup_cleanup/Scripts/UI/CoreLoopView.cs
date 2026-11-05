using System;
using SlimeBlindBox.App;
using SlimeBlindBox.Networking;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    public sealed class CoreLoopView : MonoBehaviour
    {
        private static readonly Color BackgroundColor = new Color32(20, 18, 38, 255);
        private static readonly Color PanelColor = new Color32(42, 38, 72, 255);
        private static readonly Color PanelAltColor = new Color32(31, 29, 55, 255);
        private static readonly Color AccentColor = new Color32(83, 209, 138, 255);
        private static readonly Color AccentDarkColor = new Color32(35, 122, 88, 255);
        private static readonly Color BorderColor = new Color32(119, 100, 170, 255);
        private static readonly Color TextColor = new Color32(240, 238, 255, 255);
        private static readonly Color MutedTextColor = new Color32(183, 176, 211, 255);
        private static Font runtimeFont;
        private SlimeGameController controller;
        private Text playerText;
        private Text boxText;
        private Text timerText;
        private Text messageText;
        private Text resourceText;
        private Transform inventoryContent;
        private Transform leaderboardContent;
        private Transform dictionaryContent;
        private Transform guildContent;
        private Transform boxInventoryContent;
        private Transform equipmentContent;
        private Transform boxQualityContent;
        private GameObject cultivatePanel;
        private GameObject inventoryPanel;
        private GameObject inventorySlimePanel;
        private GameObject inventoryEquipmentPanel;
        private GameObject boxPanel;
        private GameObject dictionaryPanel;
        private GameObject guildPanel;
        private GameObject leaderboardPanel;
        private Button openButton;
        private DateTime nextBoxAt;
        private bool isUiReady;
        private MainUiReferences uiRefs;
        private string selectedBoxKey = "WHITE";
        private Color selectedBoxColor = Color.white;
        private BoxOpenAnimator boxAnimator;
        private Sprite[] boxQualitySprites;
        private const int InventoryPageSize = 8;
        private int inventoryPageIndex;

        private void Start()
        {
            controller = GetComponent<SlimeGameController>();
            BuildUi();
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
            if (!isUiReady || controller?.GameState == null || timerText == null) return;
            if (string.IsNullOrEmpty(controller.GameState.next_box_at))
            {
                timerText.text = "培养皿已满";
                return;
            }
            if (nextBoxAt == default)
            {
                timerText.text = "正在同步时间……";
                return;
            }

            var remaining = nextBoxAt.ToUniversalTime() - DateTime.UtcNow;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            timerText.text = $"下一个盲盒：{remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        }

        private void BuildUi()
        {
            runtimeFont = ChineseTmpFontProvider.GetFont();
            EnsureEventSystem();
            var prefab = Resources.Load<GameObject>("UI/MainUI");
            if (prefab == null)
            {
                Debug.LogError("缺少 Resources/UI/MainUI.prefab，请通过 Slime/UI/Rebuild Main UI Prefab 重新生成。");
                enabled = false;
                return;
            }

            var instance = Instantiate(prefab, transform, false);
            var refs = instance.GetComponent<MainUiReferences>();
            if (refs == null)
            {
                Debug.LogError("MainUI.prefab 缺少 MainUiReferences 组件。");
                enabled = false;
                return;
            }

            foreach (var text in instance.GetComponentsInChildren<Text>(true))
                if (runtimeFont != null) text.font = runtimeFont;

            playerText = refs.playerText;
            resourceText = refs.resourceText;
            boxText = refs.boxText;
            timerText = refs.timerText;
            messageText = refs.messageText;
            openButton = refs.openButton;
            openButton.gameObject.SetActive(false);
            inventoryContent = refs.inventoryContent;
            dictionaryContent = refs.dictionaryContent;
            guildContent = refs.guildContent;
            boxInventoryContent = refs.boxInventoryContent;
            equipmentContent = refs.equipmentContent;
            boxQualityContent = refs.boxQualityContent;
            leaderboardContent = refs.leaderboardContent;
            cultivatePanel = refs.homePage;
            inventoryPanel = refs.inventoryPage;
            inventorySlimePanel = refs.inventorySlimePanel;
            inventoryEquipmentPanel = refs.inventoryEquipmentPanel;
            boxPanel = refs.boxPage;
            dictionaryPanel = refs.dictionaryPage;
            guildPanel = refs.guildPage;
            leaderboardPanel = refs.leaderboardPage;
            uiRefs = refs;

            refs.claimButton.onClick.AddListener(controller.ClaimBoxes);
            refs.homeTab.onClick.AddListener(() => ShowTab(cultivatePanel));
            refs.inventoryTab.onClick.AddListener(() => ShowTab(inventoryPanel));
            refs.boxTab.onClick.AddListener(() => ShowTab(boxPanel));
            refs.inventorySlimeTab.onClick.AddListener(() => ShowInventoryTab(inventorySlimePanel));
            refs.inventoryEquipmentTab.onClick.AddListener(() => ShowInventoryTab(inventoryEquipmentPanel));
            refs.inventoryPreviousPage.onClick.AddListener(() => ChangeInventoryPage(-1));
            refs.inventoryNextPage.onClick.AddListener(() => ChangeInventoryPage(1));
            refs.dictionaryTab.onClick.AddListener(() => ShowTab(dictionaryPanel));
            refs.guildTab.onClick.AddListener(() => ShowTab(guildPanel));
            refs.leaderboardTab.onClick.AddListener(() => ShowTab(leaderboardPanel));
            refs.gmAddOne.onClick.AddListener(() => controller.GmBoxes("add", 1));
            refs.gmAddEight.onClick.AddListener(() => controller.GmBoxes("add", 8));
            refs.gmFill.onClick.AddListener(() => controller.GmBoxes("fill", 0));
            refs.gmClear.onClick.AddListener(() => controller.GmBoxes("clear", 0));
            refs.gmAdvance.onClick.AddListener(() => controller.GmBoxes("advance", 3));
            refs.selectedBoxOpenButton.onClick.AddListener(OpenSelectedBox);
            boxAnimator = refs.selectedBoxImage.gameObject.AddComponent<BoxOpenAnimator>();
            boxAnimator.Initialize(refs.selectedBoxImage);
            boxQualitySprites = new[]
            {
                Resources.Load<Sprite>("icon/宝箱1"),
                Resources.Load<Sprite>("icon/宝箱2"),
                Resources.Load<Sprite>("icon/宝箱3"),
                Resources.Load<Sprite>("icon/宝箱4"),
                Resources.Load<Sprite>("icon/宝箱5"),
            };
            InitializeSettings(instance.transform, refs);

            var frames = Resources.LoadAll<Sprite>("PixelAdventure/SlimeIdle");
            if (refs.slimeImage != null && frames.Length > 0)
            {
                var animator = refs.slimeImage.gameObject.AddComponent<PixelSlimeAnimator>();
                animator.Initialize(refs.slimeImage, frames);
            }
            ShowTab(cultivatePanel);
            ShowInventoryTab(inventorySlimePanel);
        }

        private void InitializeSettings(Transform uiRoot, MainUiReferences refs)
        {
            var prefab = Resources.Load<GameObject>("UI/GameSettings");
            if (prefab == null || refs.settingsButton == null)
            {
                Debug.LogError("缺少 Resources/UI/GameSettings.prefab 或设置按钮引用，请重新生成 Main UI Prefab。");
                return;
            }

            var instance = Instantiate(prefab, uiRoot, false);
            var settingsView = instance.GetComponent<GameSettingsView>();
            if (settingsView == null)
            {
                Debug.LogError("GameSettings.prefab 缺少 GameSettingsView 组件。");
                Destroy(instance);
                return;
            }

            settingsView.Initialize(runtimeFont);
            instance.SetActive(false);
            refs.settingsButton.onClick.AddListener(settingsView.Show);
        }

        private void Render()
        {
            if (!isUiReady || controller == null || playerText == null || boxText == null
                || messageText == null || openButton == null || inventoryContent == null
                || leaderboardContent == null || dictionaryContent == null || guildContent == null)
            {
                return;
            }

            var user = controller.User;
            var state = controller.GameState;
            playerText.text = user == null ? "开发账号登录中……" : $"玩家：{user.nickname}";
            if (resourceText != null && user != null)
                resourceText.text = $"CP  {state?.total_cp ?? user.total_cp}    残渣  {state?.residue ?? user.residue}";
            boxText.text = state == null ? "盲盒：--" : $"盲盒：{state.box_count} / {state.box_capacity}";
            messageText.text = controller.IsBusy ? "处理中……" : controller.LastMessage;
            openButton.interactable = !controller.IsBusy && state != null && state.box_count > 0;
            nextBoxAt = default;
            if (state != null && !string.IsNullOrEmpty(state.next_box_at))
            {
                DateTime.TryParse(
                    state.next_box_at,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out nextBoxAt);
            }
            RenderInventory();
            RenderLeaderboard();
            RenderDictionary();
            RenderGuild();
            RenderBoxInventory();
        }

        private void RenderBoxInventory()
        {
            if (boxQualityContent == null || uiRefs == null) return;
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
                    var label = button.GetComponentInChildren<Text>();
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
            if (box == null || uiRefs == null) return;
            selectedBoxKey = key;
            if (!ColorUtility.TryParseHtmlString(box.color, out selectedBoxColor)) selectedBoxColor = Color.white;
            uiRefs.selectedBoxName.text = $"{box.name}盲盒";
            uiRefs.selectedBoxCount.text = $"持有 {box.quantity} 个";
            uiRefs.selectedBoxImage.sprite = GetBoxSprite(key);
            uiRefs.selectedBoxImage.color = Color.white;
            uiRefs.selectedBoxImage.preserveAspect = true;
            uiRefs.selectedBoxOpenButton.interactable = !controller.IsBusy && box.quantity > 0;
            if (updateMessage) uiRefs.boxOpenHint.text = box.quantity > 0 ? "点击开启按钮打开 1 个" : "该品质盲盒库存为空";
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
            uiRefs.boxOpenHint.text = "盲盒开启中……";
            yield return boxAnimator.Play(selectedBoxColor);
            controller.OpenBox(selectedBoxKey);
        }

        private void RenderInventory()
        {
            Clear(inventoryContent);
            if (controller.Inventory.Length == 0) CreateText(inventoryContent, "暂无史莱姆", 18, TextAnchor.MiddleCenter);
            var totalPages = Mathf.Max(1, Mathf.CeilToInt(controller.Inventory.Length / (float)InventoryPageSize));
            inventoryPageIndex = Mathf.Clamp(inventoryPageIndex, 0, totalPages - 1);
            if (uiRefs != null)
            {
                uiRefs.inventoryPageText.text = $"{inventoryPageIndex + 1} / {totalPages}";
                uiRefs.inventoryPreviousPage.interactable = inventoryPageIndex > 0;
                uiRefs.inventoryNextPage.interactable = inventoryPageIndex < totalPages - 1;
            }
            var start = inventoryPageIndex * InventoryPageSize;
            var end = Mathf.Min(start + InventoryPageSize, controller.Inventory.Length);
            for (var itemIndex = start; itemIndex < end; itemIndex++)
            {
                var item = controller.Inventory[itemIndex];
                var template = Resources.Load<GameObject>("UI/InventoryItem");
                var row = template != null ? Instantiate(template, inventoryContent) : Panel(inventoryContent, $"Slime_{item.slime_id}", PanelAltColor);
                row.name = $"Slime_{item.slime_id}";
                foreach (var text in row.GetComponentsInChildren<Text>(true)) if (runtimeFont != null) text.font = runtimeFont;
                var view = row.GetComponent<InventoryItemView>();
                if (view == null) continue;
                view.nameText.text = $"{item.name} [{item.rarity}]";
                view.detailText.text = $"数量 × {item.quantity}\nLv.{item.level}  CP {item.effective_cp}";
                ApplyRarity(view.nameText, item.rarity_key, item.rarity_color);
                var slimeFrames = Resources.LoadAll<Sprite>("PixelAdventure/SlimeIdle");
                if (slimeFrames.Length > 0) view.icon.sprite = slimeFrames[Mathf.Abs(item.slime_id) % slimeFrames.Length];
                if (ColorUtility.TryParseHtmlString(item.rarity_color, out var iconColor)) view.icon.color = Color.Lerp(Color.white, iconColor, .3f);
                var capturedId = item.slime_id;
                view.mergeButton.onClick.AddListener(() => controller.Merge(capturedId));
                view.mergeButton.interactable = !controller.IsBusy && item.quantity >= 5;
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
            Clear(dictionaryContent);
            var data = controller.Dictionary;
            if (data == null) return;
            CreateText(dictionaryContent, $"收集进度：{data.discovered}/{data.total}（{data.completion:P0}）", 20, TextAnchor.MiddleCenter);
            foreach (var item in data.entries)
            {
                var label = CreateText(dictionaryContent, item.discovered == 1 ? $"已发现  {item.name} [{item.rarity}]" : $"未发现  ??? [{item.rarity}]", 17, TextAnchor.MiddleLeft);
                ApplyRarity(label, item.rarity_key, item.rarity_color);
            }
        }

        private void RenderGuild()
        {
            Clear(guildContent);
            var data = controller.Guild;
            if (data == null || !data.has_guild)
            {
                CreateText(guildContent, "尚未加入公会", 20, TextAnchor.MiddleCenter);
                Button(guildContent, "创建个人公会", controller.CreateGuild);
                return;
            }
            CreateText(guildContent, $"{data.guild.name}  资金 {data.guild.funds}  公会币 {data.guild_coins}", 20, TextAnchor.MiddleCenter);
            CreateText(guildContent, $"魔力节点 Lv.{data.guild.magic_level}  扩容 Lv.{data.guild.capacity_level}", 17, TextAnchor.MiddleCenter);
            Button(guildContent, "升级魔力节点", () => controller.UpgradeGuild("magic"));
            Button(guildContent, "升级扩容", () => controller.UpgradeGuild("capacity"));
            if (data.boss != null)
            {
                CreateText(guildContent, $"史莱姆王饥饿度：{data.boss.current_hunger}/{data.boss.max_hunger}", 18, TextAnchor.MiddleCenter);
                Button(guildContent, "今日投喂", controller.FeedBoss);
            }
            foreach (var item in controller.Inventory)
            {
                var id = item.slime_id;
                Button(guildContent, $"捐献 1 只 {item.name}", () => controller.Donate(id));
            }
        }

        private void RenderLeaderboard()
        {
            Clear(leaderboardContent);
            if (controller.Leaderboard.Length == 0) CreateText(leaderboardContent, "暂无排行数据", 18, TextAnchor.MiddleCenter);
            for (var i = 0; i < controller.Leaderboard.Length; i++)
            {
                var item = controller.Leaderboard[i];
                CreateText(leaderboardContent, $"{i + 1}. {item.nickname}    {item.total_cp} CP", 17, TextAnchor.MiddleLeft);
            }
        }

        private static Transform CreateList(Transform parent, string title)
        {
            var panel = Panel(parent, title, PanelColor);
            panel.AddComponent<LayoutElement>().flexibleWidth = 1;
            var vertical = panel.AddComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(12, 12, 12, 12);
            vertical.spacing = 6;
            CreateText(panel.transform, title, 23, TextAnchor.MiddleCenter);
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(panel.transform, false);
            content.GetComponent<VerticalLayoutGroup>().spacing = 5;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content.transform;
        }

        private static GameObject CreatePage(Transform parent, string name)
        {
            var page = Panel(parent, name, PanelColor);
            page.AddComponent<LayoutElement>().flexibleHeight = 1;
            var layout = page.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 6;
            return page;
        }

        private void ShowTab(GameObject target)
        {
            var pages = new[] { cultivatePanel, inventoryPanel, boxPanel, dictionaryPanel, guildPanel, leaderboardPanel };
            foreach (var page in pages) if (page != null) page.SetActive(page == target);
        }

        private void ShowInventoryTab(GameObject target)
        {
            if (inventorySlimePanel != null) inventorySlimePanel.SetActive(target == inventorySlimePanel);
            if (inventoryEquipmentPanel != null) inventoryEquipmentPanel.SetActive(target == inventoryEquipmentPanel);
        }

        private static GameObject Panel(Transform parent, string name, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = BorderColor;
            outline.effectDistance = new Vector2(3, -3);
            return panel;
        }

        private static Text CreateText(Transform parent, string value, int size, TextAnchor alignment)
        {
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            if (runtimeFont != null) text.font = runtimeFont;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = TextColor;
            textObject.GetComponent<LayoutElement>().preferredHeight = size + 14;
            return text;
        }

        private static void ApplyRarity(Text label, string rarityKey, string rarityColor)
        {
            if (label == null || string.IsNullOrEmpty(rarityKey)) return;
            label.gameObject.AddComponent<RarityTextEffect>().Initialize(label, rarityKey, rarityColor);
        }

        private static Button Button(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var buttonObject = Panel(parent, label, AccentDarkColor);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            var colors = button.colors;
            colors.normalColor = AccentDarkColor;
            colors.highlightedColor = AccentColor;
            colors.pressedColor = new Color32(24, 87, 67, 255);
            colors.disabledColor = new Color32(65, 62, 82, 255);
            colors.colorMultiplier = 1;
            button.colors = colors;
            button.onClick.AddListener(action);
            CreateText(buttonObject.transform, label, 19, TextAnchor.MiddleCenter);
            buttonObject.AddComponent<LayoutElement>().preferredWidth = 130;
            return button;
        }

        private static void CreateSlimeShowcase(Transform parent)
        {
            var showcase = Panel(parent, "SlimeShowcase", PanelAltColor);
            showcase.AddComponent<LayoutElement>().preferredHeight = 180;
            var label = CreateText(showcase.transform, "史莱姆培养皿", 24, TextAnchor.UpperCenter);
            Stretch(label.rectTransform);

            var sprites = Resources.LoadAll<Sprite>("PixelAdventure/SlimeIdle");
            if (sprites == null || sprites.Length == 0)
            {
                CreateText(showcase.transform, "Pixel Adventure 2 Slime", 20, TextAnchor.MiddleCenter).color = MutedTextColor;
                return;
            }

            var slime = new GameObject("AnimatedSlime", typeof(RectTransform), typeof(Image), typeof(PixelSlimeAnimator));
            slime.transform.SetParent(showcase.transform, false);
            var rect = slime.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(176, 120);
            rect.anchoredPosition = new Vector2(0, -12);
            var image = slime.GetComponent<Image>();
            image.preserveAspect = true;
            image.color = Color.white;
            slime.GetComponent<PixelSlimeAnimator>().Initialize(image, sprites);
        }

        private static void Clear(Transform target)
        {
            for (var i = target.childCount - 1; i >= 0; i--) Destroy(target.GetChild(i).gameObject);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
