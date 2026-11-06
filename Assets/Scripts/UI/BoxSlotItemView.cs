using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    /// <summary>主界面凝聚台的单个待领取槽位：空槽或已产出的未揭示盲盒。</summary>
    public sealed class BoxSlotItemView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private Sprite emptySprite;
        [SerializeField] private Sprite filledSprite;

        public void SetFilled(bool filled)
        {
            background.sprite = filled ? filledSprite : emptySprite;
            icon.gameObject.SetActive(filled);
        }
    }
}
