using System;
using System.Globalization;
using SlimeBlindBox.Backend;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.UI
{
    /// <summary>史莱姆详情（B03）：只负责展示，切换与跳转由 InventoryPopupView 处理。</summary>
    public sealed class SlimeDetailView : MonoBehaviour
    {
        public Button backButton;
        public Button previousButton;
        public Button nextButton;
        public Button dictionaryButton;
        public Button islandButton;
        public Image ribbonImage;
        public TMP_Text rarityText;
        public Image slimeImage;
        public TMP_Text nameText;
        public TMP_Text ownedText;
        public TMP_Text levelText;
        public Slider levelBar;
        public TMP_Text levelBarText;
        public TMP_Text cpText;
        public TMP_Text totalText;

        public void Show(InventoryItemDto item, bool hasPrevious, bool hasNext)
        {
            var rarityColor = RarityStyle.Parse(item.rarity_color);
            ribbonImage.color = rarityColor;
            rarityText.text = item.rarity;
            rarityText.color = RarityStyle.TextOn(rarityColor);
            slimeImage.sprite = SlimeArtCatalog.Slime(item.slime_id);
            nameText.text = item.name;
            ownedText.text = string.Format(InventoryUiText.OwnedFormat, item.quantity, FormatDate(item.first_obtained_at));
            levelText.text = string.Format(InventoryUiText.LevelFormat, item.level);
            // 经验来源待定，经验条固定为锁定态。
            levelBar.value = 0f;
            levelBarText.text = InventoryUiText.LevelLocked;
            cpText.text = string.Format(InventoryUiText.CpFormat, item.base_cp, item.unit_effective_cp, item.level, item.level_cp_multiplier);
            totalText.gameObject.SetActive(item.quantity > 1);
            totalText.text = string.Format(InventoryUiText.TotalFormat, item.effective_cp, item.quantity);
            previousButton.interactable = hasPrevious;
            nextButton.interactable = hasNext;
            // 岛上展示系统尚未实现，按钮保持禁用。
            islandButton.interactable = false;
        }

        private static string FormatDate(string isoTime)
        {
            if (string.IsNullOrEmpty(isoTime)) return InventoryUiText.UnknownDate;
            return DateTime.TryParse(isoTime, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var time)
                ? time.ToLocalTime().ToString(InventoryUiText.DateFormat, CultureInfo.InvariantCulture)
                : InventoryUiText.UnknownDate;
        }
    }
}
