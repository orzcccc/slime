using UnityEngine;
using UnityEngine.UI;

namespace SlimeBlindBox.UI
{
    public sealed class MainUiReferences : MonoBehaviour
    {
        public Text playerText;
        public Text resourceText;
        public Text boxText;
        public Text timerText;
        public Text messageText;
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
        public Text inventoryPageText;
        public Button dictionaryTab;
        public Button guildTab;
        public Button gmAddOne;
        public Button gmAddEight;
        public Button gmFill;
        public Button gmClear;
        public Button gmAdvance;
        public Image selectedBoxImage;
        public Text selectedBoxName;
        public Text selectedBoxCount;
        public Text boxOpenHint;
        public Button selectedBoxOpenButton;
    }
}
