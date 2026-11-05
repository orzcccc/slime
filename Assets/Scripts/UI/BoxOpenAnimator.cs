using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    internal sealed class BoxOpenAnimator : MonoBehaviour
    {
        private RectTransform target;
        private Image image;

        public void Initialize(Image boxImage)
        {
            image = boxImage;
            target = boxImage.rectTransform;
        }

        public IEnumerator Play(Color rarityColor)
        {
            if (target == null) yield break;
            var originalScale = target.localScale;
            var originalRotation = target.localRotation;
            for (var index = 0; index < 18; index++)
            {
                var progress = index / 17f;
                target.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(progress * Mathf.PI * 8) * (18 * (1 - progress)));
                target.localScale = originalScale * (1 + Mathf.Sin(progress * Mathf.PI) * .25f);
                if (image != null) image.color = Color.Lerp(rarityColor, Color.white, Mathf.PingPong(progress * 4, 1));
                yield return new WaitForSecondsRealtime(.035f);
            }
            target.localScale = originalScale;
            target.localRotation = originalRotation;
            if (image != null) image.color = Color.white;
        }
    }
}
