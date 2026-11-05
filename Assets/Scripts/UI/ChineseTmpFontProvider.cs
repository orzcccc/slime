using UnityEngine;

namespace SlimeBlindBox.UI
{
    internal static class ChineseTmpFontProvider
    {
        private const string ProjectFontPath = "Fonts/KeinannMaruPOP";

        private static readonly string[] FallbackOsFonts =
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

            cachedFont = Resources.Load<Font>(ProjectFontPath);
            if (cachedFont != null) return cachedFont;

            Debug.LogError($"缺少项目字体 Resources/{ProjectFontPath}，临时改用系统中文字体。");
            cachedFont = Font.CreateDynamicFontFromOSFont(FallbackOsFonts, 32);
            if (cachedFont == null)
            {
                Debug.LogError("未找到支持中文的系统字体（Microsoft YaHei、SimHei 或 SimSun）。");
            }

            return cachedFont;
        }
    }
}
