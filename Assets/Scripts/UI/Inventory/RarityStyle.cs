using UnityEngine;

namespace SlimeBlindBox.UI
{
    /// <summary>品质色解析与可读文字色。品质色由后端随数据下发。</summary>
    public static class RarityStyle
    {
        /// <summary>神器（阶级 7）及以上的卡片使用动态描边。</summary>
        public const int GlowMinimumRank = 7;

        private static readonly Color DarkText = new Color32(43, 39, 58, 255);

        public static Color Parse(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.white;
        }

        /// <summary>浅色底上用深色字，深色底上用白字。</summary>
        public static Color TextOn(Color background)
        {
            var luminance = background.r * .299f + background.g * .587f + background.b * .114f;
            return luminance > .72f ? DarkText : Color.white;
        }
    }
}
