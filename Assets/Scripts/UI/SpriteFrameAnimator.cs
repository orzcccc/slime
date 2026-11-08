using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    /// <summary>按固定帧率循环播放一组序列帧，用于岛屿植物摇摆等装饰动画。节点隐藏时自动暂停。</summary>
    public sealed class SpriteFrameAnimator : MonoBehaviour
    {
        [SerializeField] private Image target;
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float framesPerSecond = 30f;

        private float startTime;
        private int shownFrame = -1;

        private void OnEnable()
        {
            startTime = Time.unscaledTime;
            shownFrame = -1;
        }

        private void Update()
        {
            if (target == null || frames == null || frames.Length == 0) return;
            var index = (int)((Time.unscaledTime - startTime) * framesPerSecond) % frames.Length;
            if (index == shownFrame) return;
            shownFrame = index;
            target.sprite = frames[index];
        }
    }
}
