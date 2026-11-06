using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.UI
{
    /// <summary>筛选层的品质选项与排序下拉的选项：未选中 / 选中两态。</summary>
    public sealed class QualityOptionItemView : MonoBehaviour
    {
        public Button button;
        public Image background;
        public Image chipImage;
        public TMP_Text labelText;
        public GameObject checkMark;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite selectedSprite;

        public void Bind(string label, bool showChip, Color chipColor)
        {
            labelText.text = label;
            chipImage.gameObject.SetActive(showChip);
            chipImage.color = chipColor;
        }

        public void SetSelected(bool selected)
        {
            background.sprite = selected ? selectedSprite : normalSprite;
            checkMark.SetActive(selected);
        }
    }
}
