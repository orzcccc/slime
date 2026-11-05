using TMPro;
using UnityEngine;

namespace SlimeBlindBox.UI
{
    internal static class ChineseTmpFontProvider
    {
        private const string ProjectFontAssetPath = "Fonts/KeinannMaruPOP SDF";
        private static TMP_FontAsset cachedFont;

        public static TMP_FontAsset GetFont()
        {
            if (cachedFont != null) return cachedFont;

            cachedFont = Resources.Load<TMP_FontAsset>(ProjectFontAssetPath);
            if (cachedFont == null)
            {
                Debug.LogError($"缺少 TMP 字体资产 Resources/{ProjectFontAssetPath}。");
            }

            return cachedFont;
        }
    }
}
