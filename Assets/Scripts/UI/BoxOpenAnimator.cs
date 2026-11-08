using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    internal sealed class BoxOpenAnimator : MonoBehaviour
    {
        private const int FullFrames = 18;
        /// <summary>“快速”开箱动画的帧数。高稀有度揭示特效（M07）实现后不受该选项影响。</summary>
        private const int FastFrames = 8;
        private const float FrameSeconds = .035f;

        private RectTransform target;
        private Image image;

        public void Initialize(Image boxImage)
        {
            image = boxImage;
            target = boxImage.rectTransform;
        }

        public IEnumerator Play(Color rarityColor, bool fast = false)
        {
            if (target == null) yield break;
            var originalScale = target.localScale;
            var originalRotation = target.localRotation;
            var frames = fast ? FastFrames : FullFrames;
            for (var index = 0; index < frames; index++)
            {
                var progress = index / (frames - 1f);
                target.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(progress * Mathf.PI * 8) * (18 * (1 - progress)));
                target.localScale = originalScale * (1 + Mathf.Sin(progress * Mathf.PI) * .25f);
                if (image != null) image.color = Color.Lerp(rarityColor, Color.white, Mathf.PingPong(progress * 4, 1));
                yield return new WaitForSecondsRealtime(FrameSeconds);
            }
            target.localScale = originalScale;
            target.localRotation = originalRotation;
            if (image != null) image.color = Color.white;
        }
    }
}
