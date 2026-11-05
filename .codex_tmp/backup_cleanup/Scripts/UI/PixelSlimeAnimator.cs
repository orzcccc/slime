using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    internal sealed class PixelSlimeAnimator : MonoBehaviour
    {
        private Image image;
        private Sprite[] frames;
        private int frameIndex;
        private float elapsed;

        public void Initialize(Image target, Sprite[] animationFrames)
        {
            image = target;
            frames = animationFrames;
            if (frames != null && frames.Length > 0) image.sprite = frames[0];
        }

        private void Update()
        {
            if (image == null || frames == null || frames.Length == 0) return;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < 0.1f) return;
            elapsed = 0;
            frameIndex = (frameIndex + 1) % frames.Length;
            image.sprite = frames[frameIndex];
        }
    }
}
