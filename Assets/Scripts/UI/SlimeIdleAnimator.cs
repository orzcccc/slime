using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    /// <summary>
    /// 岛上史莱姆的待机动画：循环播放“呼吸眨眼”，每隔若干轮插播一次“睡觉醒来”，播完回到呼吸。
    /// 睡觉期间 <see cref="IsSleeping"/> 为 true，供闲逛逻辑原地停下。
    /// </summary>
    public sealed class SlimeIdleAnimator : MonoBehaviour
    {
        [SerializeField] private Image target;
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] sleepFrames;
        [SerializeField] private float framesPerSecond = 30f;
        [Tooltip("两次睡觉之间播放呼吸动画的轮数范围（含两端）")]
        [SerializeField] private Vector2Int idleLoopsBetweenSleeps = new Vector2Int(4, 8);

        private Sprite[] clip;
        private float clipStart;
        private int shownFrame = -1;
        private int idleLoopsLeft;

        public bool IsSleeping => clip == sleepFrames && clip != null && clip.Length > 0;

        private void OnEnable()
        {
            PlayIdle();
        }

        private void Update()
        {
            if (target == null || clip == null || clip.Length == 0) return;
            var elapsed = (int)((Time.unscaledTime - clipStart) * framesPerSecond);
            if (elapsed >= clip.Length)
            {
                if (IsSleeping) PlayIdle();
                else if (--idleLoopsLeft <= 0 && sleepFrames != null && sleepFrames.Length > 0) Play(sleepFrames);
                else Play(idleFrames);
                elapsed = 0;
            }
            if (elapsed == shownFrame) return;
            shownFrame = elapsed;
            target.sprite = clip[elapsed];
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
    }
}
