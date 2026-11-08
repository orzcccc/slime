using System;
using SlimeBlindBox.Backend;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.UI
{
    /// <summary>图鉴卡片：已发现显示立绘/名称/编号，未发现显示灰色剪影和 ???。</summary>
    public sealed class DictionaryEntryView : MonoBehaviour
    {
        public Button button;
        public Image frameImage;
        public Image slimeImage;
        public TMP_Text rarityText;
        public TMP_Text numberText;
        public TMP_Text nameText;
        public GameObject newDot;
        private int slimeId;

        public void Bind(DictionaryItemDto item, Action<int> onOpen)
        {
            slimeId = item.slime_id;
            var discovered = item.discovered == 1;
            frameImage.sprite = SlimeArtCatalog.Card(item.rarity_key);
            slimeImage.sprite = SlimeArtCatalog.Slime(item.slime_id);
            slimeImage.color = discovered ? Color.white : new Color(.45f, .47f, .55f, .85f);
            rarityText.text = item.rarity;
            rarityText.color = RarityStyle.TextOn(RarityStyle.Parse(item.rarity_color));
            numberText.text = "No." + item.number.ToString("00");
            nameText.text = discovered ? item.name : BoxOpeningUiText.UnknownName;
            // 新点亮红点：首次获得后尚未看过详情时由 DictionaryView 控制。
            newDot.SetActive(false);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onOpen(slimeId));
        }
    }
}
