using System;
using System.Collections.Generic;
using SlimeBlindBox.App;
using SlimeBlindBox.Backend;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.UI
{
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
