using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.UI
{
    internal sealed class RarityTextEffect : MonoBehaviour
    {
        private TMP_Text label;
        private string rarityKey;
        private Color baseColor;

        public void Initialize(TMP_Text target, string key, string hexColor)
        {
            label = target;
            rarityKey = key;
            if (!ColorUtility.TryParseHtmlString(hexColor, out baseColor)) baseColor = Color.white;
            label.color = baseColor;
            if (key == "DEMON_KING" || key == "WAR_GOD")
            {
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = key == "DEMON_KING" ? Color.black : new Color32(255, 119, 36, 255);
                outline.effectDistance = new Vector2(2, -2);
            }
        }

        private void Update()
        {
            if (label == null) return;
            var phase = Mathf.PingPong(Time.unscaledTime * 1.4f, 1f);
            if (rarityKey == "SUPREME")
                label.color = Color.Lerp(new Color32(80, 224, 255, 255), new Color32(214, 103, 255, 255), phase);
            else if (rarityKey == "DEMON_GOD")
                label.color = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * .18f, 1f), .75f, 1f);
            else if (rarityKey == "ARTIFACT" || rarityKey == "WAR_GOD")
                label.color = Color.Lerp(baseColor, Color.white, phase * .45f);
        }
    }
}
