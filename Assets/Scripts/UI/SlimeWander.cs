using UnityEngine;

namespace SlimeBlindBox.UI
{
    /// <summary>
    /// 岛上史莱姆在活动区域内随机闲逛：走到随机目标点后停留一会儿再换目标。
    /// 移动与静止状态交给 <see cref="SlimeAnimator"/> 切换弹跳 / 待机动画；睡觉动画播放期间原地不动。
    /// </summary>
    public sealed class SlimeWander : MonoBehaviour
    {
        [SerializeField] private RectTransform area;
        [SerializeField] private SlimeAnimator animator;
        [SerializeField] private float speed = 40f;
        [SerializeField] private Vector2 idleSeconds = new Vector2(1.5f, 4f);

        private RectTransform self;
        private Vector2 position;
        private Vector2 target;
        private float idleUntil;

        private void Awake()
        {
            self = (RectTransform)transform;
            position = self.anchoredPosition;
            target = position;
        }

        private void OnEnable()
        {
            idleUntil = Time.unscaledTime + Random.Range(idleSeconds.x, idleSeconds.y);
        }

        private void Update()
        {
            if (area == null) return;
            var now = Time.unscaledTime;
            if (animator != null && animator.IsSleeping)
            {
                idleUntil = Mathf.Max(idleUntil, now + idleSeconds.x);
                return;
            }
            if (now >= idleUntil && position == target) target = PickTarget();
            var moving = position != target;
            if (moving)
            {
                position = Vector2.MoveTowards(position, target, speed * Time.unscaledDeltaTime);
                if (position == target) idleUntil = now + Random.Range(idleSeconds.x, idleSeconds.y);
                self.anchoredPosition = position;
            }
            if (animator != null) animator.SetMoving(moving);
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
