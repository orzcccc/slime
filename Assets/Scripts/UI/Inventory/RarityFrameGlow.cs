using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    /// <summary>高品质卡片的动态描边：卡框后方略大一圈的品质色光边按固定周期呼吸。</summary>
    public sealed class RarityFrameGlow : MonoBehaviour
    {
        [SerializeField] private Image glowImage;
        [SerializeField] private float period = 1.6f;
        [SerializeField] private float minAlpha = .3f;
        [SerializeField] private float maxAlpha = .95f;

        private Color baseColor = Color.white;

        public void Play(Color color)
        {
            baseColor = color;
            glowImage.enabled = true;
            enabled = true;
            Apply();
        }

        public void Stop()
        {
            glowImage.enabled = false;
            enabled = false;
        }

        private void Update() => Apply();

        private void Apply()
        {
            var wave = .5f + .5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / period);
            var color = baseColor;
            color.a = Mathf.Lerp(minAlpha, maxAlpha, wave);
            glowImage.color = color;
        }
    }
}
