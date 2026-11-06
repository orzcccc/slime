using SlimeBlindBox.App;
using SlimeBlindBox.Desktop;
using UnityEngine;

namespace SlimeBlindBox.UI
{
    public static class CoreLoopBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateApplication()
        {
            if (Object.FindObjectOfType<SlimeGameController>() != null) return;

            var root = new GameObject("SlimeBlindBoxApp");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<SlimeGameController>();
            root.AddComponent<CoreLoopView>();
            root.AddComponent<DesktopWindowController>();
        }
    }
}
