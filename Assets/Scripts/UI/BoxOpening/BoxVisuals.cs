using System.Collections.Generic;
using SlimeBlindBox.Backend;
using UnityEngine;

namespace SlimeBlindBox.UI
{
    /// <summary>盲盒品质的显示资源（宝箱、底色、名称）与排序，加载结果缓存。</summary>
    public static class BoxVisuals
    {
        private const string SpritePrefix = "Sprites/MainPage/Props/Box_";
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        public static Sprite Sprite(string key)
        {
            if (string.IsNullOrEmpty(key)) key = "WHITE";
            if (Sprites.TryGetValue(key, out var sprite)) return sprite;
            sprite = Resources.Load<Sprite>(SpritePrefix + key);
            if (sprite == null)
            {
                Debug.LogWarning("缺少盲盒品质外观：" + key);
                if (key != "WHITE") sprite = Sprite("WHITE");
            }
            Sprites[key] = sprite;
            return sprite;
        }

        public static Color Color(string key)
        {
            var box = GameBalanceConfig.FindBoxRarity(key);
            return box != null && ColorUtility.TryParseHtmlString(box.Color, out var color) ? color : UnityEngine.Color.white;
        }

        public static string Name(string key) => GameBalanceConfig.FindBoxRarity(key)?.Name ?? "未知";

        public static int Rank(string key)
        {
            for (var i = 0; i < GameBalanceConfig.BoxRarities.Count; i++)
                if (GameBalanceConfig.BoxRarities[i].Key == key) return i;
            return -1;
        }
    }

    /// <summary>简单复用列表：Begin 后 Get 复用已有条目，End 隐藏多余条目。用于固定 8 格 / 12 格界面。</summary>
    public sealed class Corpus<T> where T : MonoBehaviour
    {
        private readonly T template;
        private readonly Transform parent;
        private readonly List<T> items = new List<T>();
        private int used;

        public Corpus(T template, Transform parent)
        {
            this.template = template;
            this.parent = parent;
        }

        public void Begin() { used = 0; }
        public T Get()
        {
            if (used >= items.Count)
            {
                var item = Object.Instantiate(template, parent, false);
                items.Add(item);
            }
            var result = items[used++];
            result.gameObject.SetActive(true);
            return result;
        }
        public void End()
        {
            for (var i = used; i < items.Count; i++) items[i].gameObject.SetActive(false);
        }
    }
}
