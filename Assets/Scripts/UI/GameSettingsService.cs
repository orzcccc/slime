using System;
using UnityEngine;

namespace SlimeBlindBox.UI
{
    public enum GameDisplayMode
    {
        Fullscreen = 0,
        Borderless = 1,
        Windowed = 2,
    }

    public static class GameSettingsService
    {
        private const string InitializedKey = "settings.display.initialized";
        private const string WidthKey = "settings.display.width";
        private const string HeightKey = "settings.display.height";
        private const string ModeKey = "settings.display.mode";

        public static readonly Vector2Int[] CommonResolutions =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1366, 768),
            new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440),
            new Vector2Int(3840, 2160),
        };

        public static Vector2Int CurrentResolution => new Vector2Int(
            PlayerPrefs.GetInt(WidthKey, 1920),
            PlayerPrefs.GetInt(HeightKey, 1080));

        public static GameDisplayMode CurrentMode => (GameDisplayMode)Mathf.Clamp(
            PlayerPrefs.GetInt(ModeKey, (int)GameDisplayMode.Borderless),
            0,
            Enum.GetValues(typeof(GameDisplayMode)).Length - 1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnStartup()
        {
            EnsureDefaults();
            Apply(CurrentResolution, CurrentMode, false);
        }

        public static void EnsureDefaults()
        {
            if (PlayerPrefs.GetInt(InitializedKey, 0) == 1) return;
            PlayerPrefs.SetInt(WidthKey, 1920);
            PlayerPrefs.SetInt(HeightKey, 1080);
            PlayerPrefs.SetInt(ModeKey, (int)GameDisplayMode.Borderless);
            PlayerPrefs.SetInt(InitializedKey, 1);
            PlayerPrefs.Save();
        }

        public static void Apply(Vector2Int resolution, GameDisplayMode mode, bool save = true)
        {
            resolution.x = Mathf.Max(640, resolution.x);
            resolution.y = Mathf.Max(360, resolution.y);
            Screen.SetResolution(resolution.x, resolution.y, ToUnityMode(mode));
            if (!save) return;

            PlayerPrefs.SetInt(WidthKey, resolution.x);
            PlayerPrefs.SetInt(HeightKey, resolution.y);
            PlayerPrefs.SetInt(ModeKey, (int)mode);
            PlayerPrefs.SetInt(InitializedKey, 1);
            PlayerPrefs.Save();
        }

        private static FullScreenMode ToUnityMode(GameDisplayMode mode)
        {
            switch (mode)
            {
                case GameDisplayMode.Fullscreen:
                    return FullScreenMode.ExclusiveFullScreen;
                case GameDisplayMode.Windowed:
                    return FullScreenMode.Windowed;
                default:
                    return FullScreenMode.FullScreenWindow;
            }
        }
    }
}
