using UnityEngine;

namespace SlimeBlindBox.UI
{
    internal static class ChineseTmpFontProvider
    {
        private static readonly string[] PreferredFonts =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "SimHei",
            "SimSun"
        };

        private static Font cachedFont;

        public static Font GetFont()
        {
            if (cachedFont != null) return cachedFont;

            cachedFont = Font.CreateDynamicFontFromOSFont(PreferredFonts, 32);
            if (cachedFont == null)
            {
                Debug.LogError("未找到支持中文的系统字体（Microsoft YaHei、SimHei 或 SimSun）。");
            }

            return cachedFont;
        }
    }
}
