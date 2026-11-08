using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    /// <summary>
    /// 岛上史莱姆的序列帧动画：移动时循环“弹跳”；静止时循环“呼吸眨眼”，每隔若干轮插播一次“睡觉醒来”。
    /// 停止移动时先跳完当前这一下再回到待机，避免在半空中切换。睡觉期间 <see cref="IsSleeping"/> 为 true，闲逛逻辑据此原地停下。
    /// </summary>
    public sealed class SlimeAnimator : MonoBehaviour
    {
        [SerializeField] private Image target;
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] sleepFrames;
        [SerializeField] private Sprite[] hopFrames;
        [SerializeField] private float framesPerSecond = 30f;
        [Tooltip("两次睡觉之间播放呼吸动画的轮数范围（含两端）")]
        [SerializeField] private Vector2Int idleLoopsBetweenSleeps = new Vector2Int(4, 8);

        private Sprite[] clip;
        private float clipStart;
        private int shownFrame = -1;
        private int idleLoopsLeft;
        private bool moving;

        public bool IsSleeping => clip != null && clip == sleepFrames && clip.Length > 0;

        /// <summary>由闲逛逻辑每帧调用。开始移动立即切到弹跳；停止移动在当前弹跳结束后回到待机。</summary>
        public void SetMoving(bool value)
        {
            if (value == moving) return;
            moving = value;
            if (moving && HasFrames(hopFrames) && clip != hopFrames) Play(hopFrames);
        }

        private void OnEnable()
        {
            moving = false;
            PlayIdle();
        }

        private void Update()
        {
            if (target == null || !HasFrames(clip)) return;
            var elapsed = (int)((Time.unscaledTime - clipStart) * framesPerSecond);
            if (elapsed >= clip.Length)
            {
                NextClip();
                elapsed = 0;
            }
            if (elapsed == shownFrame) return;
            shownFrame = elapsed;
            target.sprite = clip[elapsed];
        }

        private void NextClip()
        {
            if (moving && HasFrames(hopFrames)) Play(hopFrames);
            else if (clip == hopFrames || IsSleeping) PlayIdle();
            else if (--idleLoopsLeft <= 0 && HasFrames(sleepFrames)) Play(sleepFrames);
            else Play(idleFrames);
        }

        private void PlayIdle()
        {
            idleLoopsLeft = Random.Range(idleLoopsBetweenSleeps.x, idleLoopsBetweenSleeps.y + 1);
            Play(idleFrames);
        }

        private void Play(Sprite[] frames)
        {
            clip = frames;
            clipStart = Time.unscaledTime;
            shownFrame = -1;
        }

        private static bool HasFrames(Sprite[] frames) => frames != null && frames.Length > 0;
    }
}
