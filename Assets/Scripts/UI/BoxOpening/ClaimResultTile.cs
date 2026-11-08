using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    /// <summary>领取卡片保留根点击区，仅切换正反面图片，跳过时立即结束翻面动画。</summary>
    public sealed class ClaimResultTile : MonoBehaviour
    {
        public Button button;
        public Image backImage;
        public Image frontImage;
        public Image flashImage;
        public Sprite emptySprite;
        public string RarityKey { get; private set; }

        public void Bind(Sprite front, Color color, string key, Action onClick)
        {
            StopAllCoroutines();
            RarityKey = key;
            frontImage.sprite = front;
            frontImage.color = Color.white;
            frontImage.gameObject.SetActive(false);
            backImage.sprite = emptySprite;
            backImage.enabled = true;
            backImage.color = Color.white;
            flashImage.color = color;
            flashImage.gameObject.SetActive(false);
            transform.localScale = Vector3.one;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
            gameObject.SetActive(true);
        }

        public void Reveal() => StartCoroutine(Flip());

        public void RevealInstant()
        {
            StopAllCoroutines();
            transform.localScale = Vector3.one;
            // 背面位于根物体，保留 Graphic 供按钮命中，仅清除其颜色。
            backImage.color = Color.clear;
            frontImage.gameObject.SetActive(true);
        }

        private IEnumerator Flip()
        {
            const float duration = .3f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var progress = elapsed / duration;
                transform.localScale = new Vector3(Mathf.Abs(1 - 2 * progress), 1, 1);
                if (progress >= .5f)
                {
                    backImage.color = Color.clear;
                    frontImage.gameObject.SetActive(true);
                }
                yield return null;
            }
            transform.localScale = Vector3.one;
            backImage.color = Color.clear;
            frontImage.gameObject.SetActive(true);
        }
    }
}
