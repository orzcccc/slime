using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    /// <summary>主界面凝聚台的单个待领取槽位：空槽，或显示对应品质宝箱与品质底色的已产出盲盒。</summary>
    public sealed class BoxSlotItemView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private Sprite emptySprite;
        [SerializeField] private Sprite filledSprite;
        [Tooltip("品质色混入槽位底板的比例，0 为不着色，1 为纯品质色")]
        [SerializeField, Range(0f, 1f)] private float rarityTint = 0.75f;

        public void SetEmpty()
        {
            background.sprite = emptySprite;
            background.color = Color.white;
            icon.gameObject.SetActive(false);
        }

        public void SetFilled(Sprite boxSprite, Color rarityColor)
        {
            background.sprite = filledSprite;
            background.color = Color.Lerp(Color.white, rarityColor, rarityTint);
            icon.sprite = boxSprite;
            icon.gameObject.SetActive(boxSprite != null);
        }
    }
}
