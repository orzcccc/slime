using System;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    /// <summary>
    /// 开关（G12）：开 / 关 / 禁用三态。根节点 Image 为关闭态轨道并承载 Button，
    /// onTrack 为开启态轨道，knob 在两端之间移动。禁用时整体变淡且不可点击。
    /// </summary>
    public sealed class ToggleSwitchView : MonoBehaviour
    {
        private const float DisabledAlpha = 0.45f;

        public Button button;
        public Image trackImage;
        public Image onTrackImage;
        public Image knobImage;
        public float knobTravel = 22f;

        private bool bound;

        public event Action<bool> ValueChanged;

        public bool IsOn { get; private set; }

        public bool Interactable
        {
            get => button != null && button.interactable;
            set
            {
                Bind();
                if (button != null) button.interactable = value;
                var alpha = value ? 1f : DisabledAlpha;
                SetAlpha(trackImage, alpha);
                SetAlpha(onTrackImage, alpha);
                SetAlpha(knobImage, alpha);
            }
        }

        private void Awake() => Bind();

        public void SetIsOn(bool value)
        {
            Bind();
            IsOn = value;
            if (onTrackImage != null) onTrackImage.gameObject.SetActive(value);
            if (knobImage != null)
            {
                var knob = knobImage.rectTransform;
                knob.anchoredPosition = new Vector2(value ? knobTravel : -knobTravel, knob.anchoredPosition.y);
            }
        }

        private void Bind()
        {
            if (bound || button == null) return;
            bound = true;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                SetIsOn(!IsOn);
                ValueChanged?.Invoke(IsOn);
            });
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}

