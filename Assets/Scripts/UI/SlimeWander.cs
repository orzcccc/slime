using UnityEngine;

namespace SlimeBlindBox.UI
{
    /// <summary>
    /// 岛上史莱姆在活动区域内随机闲逛：走到随机目标点后停留一会儿再换目标。
    /// 只在移动时上下颠动；睡觉动画播放期间原地不动。
    /// </summary>
    public sealed class SlimeWander : MonoBehaviour
    {
        [SerializeField] private RectTransform area;
        [SerializeField] private SlimeIdleAnimator idleAnimator;
        [SerializeField] private float speed = 40f;
        [SerializeField] private Vector2 idleSeconds = new Vector2(1.5f, 4f);
        [SerializeField] private float bobHeight = 4f;
        [SerializeField] private float bobFrequency = 2.2f;

        private RectTransform self;
        private Vector2 basePosition;
        private Vector2 target;
        private float idleUntil;

        private void Awake()
        {
            self = (RectTransform)transform;
            basePosition = self.anchoredPosition;
            target = basePosition;
        }

        private void OnEnable()
        {
            idleUntil = Time.unscaledTime + Random.Range(idleSeconds.x, idleSeconds.y);
        }

        private void Update()
        {
            if (area == null) return;
            var now = Time.unscaledTime;
            if (idleAnimator != null && idleAnimator.IsSleeping)
            {
                idleUntil = Mathf.Max(idleUntil, now + idleSeconds.x);
                self.anchoredPosition = basePosition;
                return;
            }
            if (now >= idleUntil && basePosition == target) target = PickTarget();
            var moving = basePosition != target;
            if (moving)
            {
                basePosition = Vector2.MoveTowards(basePosition, target, speed * Time.unscaledDeltaTime);
                if (basePosition == target) idleUntil = now + Random.Range(idleSeconds.x, idleSeconds.y);
            }
            var bob = moving ? Mathf.Abs(Mathf.Sin(now * bobFrequency)) * bobHeight : 0f;
            self.anchoredPosition = basePosition + new Vector2(0f, bob);
        }

        private Vector2 PickTarget()
        {
            var rect = area.rect;
            var half = self.rect.size * 0.5f;
            var x = Random.Range(rect.xMin + half.x, Mathf.Max(rect.xMin + half.x, rect.xMax - half.x));
            var y = Random.Range(rect.yMin + half.y, Mathf.Max(rect.yMin + half.y, rect.yMax - half.y));
            return new Vector2(x, y);
        }
    }
}
