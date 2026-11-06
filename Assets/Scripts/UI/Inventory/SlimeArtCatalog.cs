using System.Collections.Generic;
using UnityEngine;

namespace SlimeBlindBox.UI
{
    /// <summary>
    /// 史莱姆立绘与品质卡框的资源映射：卡框按品质键、立绘按 slime_id 命名，
    /// 位于 Resources/Sprites/Bag/。加载结果缓存，缺图时回退到普通品质资源。
    /// </summary>
    public static class SlimeArtCatalog
    {
        private const string CardPathPrefix = "Sprites/Bag/Cards/Card_";
        private const string SlimePathPrefix = "Sprites/Bag/Slimes/Slime_";
        private const string FallbackRarityKey = "COMMON";
        private const int FallbackSlimeId = 202;

        private static readonly Dictionary<string, Sprite> Cards = new Dictionary<string, Sprite>();
        private static readonly Dictionary<int, Sprite> Slimes = new Dictionary<int, Sprite>();

        public static Sprite Card(string rarityKey)
        {
            var key = string.IsNullOrEmpty(rarityKey) ? FallbackRarityKey : rarityKey;
            if (Cards.TryGetValue(key, out var sprite)) return sprite;
            sprite = Resources.Load<Sprite>(CardPathPrefix + key);
            if (sprite == null && key != FallbackRarityKey)
            {
                Debug.LogWarning("缺少品质卡框：" + key);
                sprite = Card(FallbackRarityKey);
            }
            Cards[key] = sprite;
            return sprite;
        }

        public static Sprite Slime(int slimeId)
        {
            if (Slimes.TryGetValue(slimeId, out var sprite)) return sprite;
            sprite = Resources.Load<Sprite>(SlimePathPrefix + slimeId);
            if (sprite == null && slimeId != FallbackSlimeId)
            {
                Debug.LogWarning("缺少史莱姆立绘：" + slimeId);
                sprite = Slime(FallbackSlimeId);
            }
            Slimes[slimeId] = sprite;
            return sprite;
        }
    }
}
