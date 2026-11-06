using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.UI
{
    public sealed class MainUiReferences : MonoBehaviour
    {
        [Header("顶栏")]
        public TMP_Text resourceText;
        public Button slimeModeButton;
        public Image slimeModeImage;
        public TMP_Text slimeModeText;
        public Button boxModeButton;
        public Image boxModeImage;
        public TMP_Text boxModeText;

        [Header("岛屿")]
        public GameObject homePage;
        public RectTransform slimeArea;
        public Image slimeImage;
        public GameObject boxShowcase;
        public TMP_Text timerText;

        [Header("凝聚台")]
        public TMP_Text boxText;
        public Transform boxSlotContent;
        public Slider productionBar;
        public Button claimButton;

        [Header("底部功能坞")]
        public Button boxTab;
        public GameObject warehouseBadge;
        public TMP_Text warehouseBadgeText;
        public Button inventoryTab;
        public Button dictionaryTab;
        public Button settingsButton;
        public TMP_Text messageText;

        [Header("功能页")]
        public GameObject contentArea;
        public GameObject inventoryPage;
        public GameObject boxPage;
        public GameObject dictionaryPage;
        public Transform boxInventoryContent;
        public Transform inventoryContent;
        public Transform boxQualityContent;
        public Transform dictionaryContent;
        public Button inventoryPreviousPage;
        public Button inventoryNextPage;
        public TMP_Text inventoryPageText;
        public Image selectedBoxImage;
        public TMP_Text selectedBoxName;
        public TMP_Text selectedBoxCount;
        public TMP_Text boxOpenHint;
        public Button selectedBoxOpenButton;

        [Header("开发者工具（仅开发构建）")]
        public GameObject gmPanel;
        public Button gmAddOne;
        public Button gmAddEight;
        public Button gmFill;
        public Button gmClear;
        public Button gmAdvance;
    }
}
