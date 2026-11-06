using SlimeBlindBox.Backend;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.UI
{
    /// <summary>背包卡片：品质卡框、品质名、岛上展示标记、数量角标、立绘、名称与等级 CP。</summary>
    public sealed class InventoryItemView : MonoBehaviour
    {
        public Button button;
        public Image frameImage;
        public RarityFrameGlow frameGlow;
        public TMP_Text rarityText;
        public GameObject islandBadge;
        public TMP_Text countText;
        public Image slimeImage;
        public TMP_Text nameText;
        public TMP_Text detailText;

        public void Bind(InventoryItemDto item)
        {
            var rarityColor = RarityStyle.Parse(item.rarity_color);
            frameImage.sprite = SlimeArtCatalog.Card(item.rarity_key);
            slimeImage.sprite = SlimeArtCatalog.Slime(item.slime_id);
            rarityText.text = item.rarity;
            rarityText.color = RarityStyle.TextOn(rarityColor);
            // 岛上展示功能尚未实现，标记暂不显示，见 Docs/Systems/背包与物品界面.md。
            islandBadge.SetActive(false);
            countText.text = string.Format(InventoryUiText.CountFormat, item.quantity);
            nameText.text = item.name;
            detailText.text = string.Format(InventoryUiText.CardStatsFormat, item.level, item.unit_effective_cp);
            if (InventoryListQuery.RarityRank(item.rarity_key) >= RarityStyle.GlowMinimumRank) frameGlow.Play(rarityColor);
            else frameGlow.Stop();
        }
    }
}
