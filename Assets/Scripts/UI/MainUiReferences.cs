using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.UI
{
    public sealed class MainUiReferences : MonoBehaviour
    {
        public TMP_Text playerText;
        public TMP_Text resourceText;
        public TMP_Text boxText;
        public TMP_Text timerText;
        public TMP_Text messageText;
        public Button openButton;
        public Button claimButton;
        public Button settingsButton;
        public Image slimeImage;
        public Transform boxInventoryContent;
        public Transform inventoryContent;
        public Transform boxQualityContent;
        public Transform dictionaryContent;
        public Transform guildContent;
        public GameObject homePage;
        public GameObject inventoryPage;
        public GameObject boxPage;
        public GameObject dictionaryPage;
        public GameObject guildPage;
        public Button homeTab;
        public Button inventoryTab;
        public Button boxTab;
        public Button inventoryPreviousPage;
        public Button inventoryNextPage;
        public TMP_Text inventoryPageText;
        public Button dictionaryTab;
        public Button guildTab;
        public Button gmAddOne;
        public Button gmAddEight;
        public Button gmFill;
        public Button gmClear;
        public Button gmAdvance;
        public Image selectedBoxImage;
        public TMP_Text selectedBoxName;
        public TMP_Text selectedBoxCount;
        public TMP_Text boxOpenHint;
        public Button selectedBoxOpenButton;
    }
}
