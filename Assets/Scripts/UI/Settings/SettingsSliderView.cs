using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    /// <summary>
    /// 音量滑条（G13）：常态 / 禁用。根节点为轨道（Selectable，桌面窗口不会把它当拖拽区）；
    /// fill 为左对齐的九宫格填充条，handle 为滑块。拖动时触发 ValueChanged，松手时触发 Released。
    /// </summary>
    public sealed class SettingsSliderView : Selectable, IDragHandler, IInitializePotentialDragHandler
    {
        private const float DisabledAlpha = 0.45f;

        public RectTransform fillRect;
        public RectTransform handleRect;
        public Graphic trackGraphic;
        public Graphic fillGraphic;
        public Graphic handleGraphic;

        private bool dragging;

        public event Action<float> ValueChanged;
        public event Action<float> Released;

        public float Value { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
        }

        public void SetValue(float value)
        {
            Value = Mathf.Clamp01(value);
            Layout();
        }

        public void SetEnabledState(bool enabled)
        {
            interactable = enabled;
            var alpha = enabled ? 1f : DisabledAlpha;
            SetAlpha(trackGraphic, alpha);
            SetAlpha(fillGraphic, alpha);
            SetAlpha(handleGraphic, alpha);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (!IsInteractable() || eventData.button != PointerEventData.InputButton.Left) return;
            dragging = true;
            UpdateFromPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragging && IsInteractable()) UpdateFromPointer(eventData);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            if (!dragging) return;
            dragging = false;
            Released?.Invoke(Value);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        private void UpdateFromPointer(PointerEventData eventData)
        {
            var track = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position, eventData.pressEventCamera, out var local)) return;
            var rect = track.rect;
            var value = rect.width > 0f ? Mathf.Clamp01((local.x - rect.xMin) / rect.width) : 0f;
            if (Mathf.Approximately(value, Value)) return;
            Value = value;
            Layout();
            ValueChanged?.Invoke(Value);
        }

        private void Layout()
        {
            var rect = ((RectTransform)transform).rect;
            if (fillRect != null)
            {
                var minimum = fillRect.rect.height;
                fillRect.gameObject.SetActive(Value > 0.001f);
                fillRect.sizeDelta = new Vector2(Mathf.Max(minimum, rect.width * Value), fillRect.sizeDelta.y);
            }
            if (handleRect != null)
                handleRect.anchoredPosition = new Vector2((Value - 0.5f) * rect.width, handleRect.anchoredPosition.y);
        }
    }
}

