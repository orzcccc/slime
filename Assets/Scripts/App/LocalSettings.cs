using System;
using UnityEngine;

namespace SlimeBlindBox.App
{
    /// <summary>
    /// 本机玩家偏好（窗口、声音、提醒、开箱动画）。只影响本机表现，不涉及玩家资产，存在 PlayerPrefs 中。
    /// 读取时校验取值，缺失或损坏的数据回退到默认值；写入失败返回 false，由界面回滚并提示。
    /// 开机自启以注册表为准，不在这里保存。
    /// </summary>
    public static class LocalSettings
    {
        public static readonly int[] ScalePercents = { 100, 125, 150, 200 };

        public const float DefaultSfxVolume = 0.8f;
        public const float DefaultBgmVolume = 0.6f;

        private const string AlwaysOnTopKey = "settings.window.topmost";
        private const string LockPositionKey = "settings.window.lock";
        private const string ScaleKey = "settings.window.scale";
        private const string SfxEnabledKey = "settings.audio.sfx.enabled";
        private const string SfxVolumeKey = "settings.audio.sfx.volume";
        private const string BgmEnabledKey = "settings.audio.bgm.enabled";
        private const string BgmVolumeKey = "settings.audio.bgm.volume";
        private const string BoxFullReminderKey = "settings.notify.boxfull";
        private const string FastOpenKey = "settings.anim.fastopen";

        /// <summary>任一设置写入成功后触发。</summary>
        public static event Action Changed;

        public static bool AlwaysOnTop => GetBool(AlwaysOnTopKey, false);
        public static bool LockPosition => GetBool(LockPositionKey, false);
        public static bool SfxEnabled => GetBool(SfxEnabledKey, true);
        public static float SfxVolume => GetVolume(SfxVolumeKey, DefaultSfxVolume);
        public static bool BgmEnabled => GetBool(BgmEnabledKey, true);
        public static float BgmVolume => GetVolume(BgmVolumeKey, DefaultBgmVolume);
        public static bool BoxFullReminder => GetBool(BoxFullReminderKey, true);
        public static bool FastOpenAnimation => GetBool(FastOpenKey, false);

        /// <summary>玩家选择的缩放百分比；从未选择或数据无效时返回 0，表示跟随系统缩放。</summary>
        public static int SavedScalePercent
        {
            get
            {
                var value = PlayerPrefs.GetInt(ScaleKey, 0);
                return Array.IndexOf(ScalePercents, value) >= 0 ? value : 0;
            }
        }

        /// <summary>取与系统缩放（DPI / 96）最接近的一档。</summary>
        public static int NearestScalePercent(float systemScale)
        {
            var target = systemScale * 100f;
            var best = ScalePercents[0];
            foreach (var percent in ScalePercents)
                if (Mathf.Abs(percent - target) < Mathf.Abs(best - target)) best = percent;
            return best;
        }

        public static bool SetAlwaysOnTop(bool value) => SetInt(AlwaysOnTopKey, value ? 1 : 0);
        public static bool SetLockPosition(bool value) => SetInt(LockPositionKey, value ? 1 : 0);
        public static bool SetScalePercent(int value) => Array.IndexOf(ScalePercents, value) >= 0 && SetInt(ScaleKey, value);
        public static bool SetSfxEnabled(bool value) => SetInt(SfxEnabledKey, value ? 1 : 0);
        public static bool SetBgmEnabled(bool value) => SetInt(BgmEnabledKey, value ? 1 : 0);
        public static bool SetBoxFullReminder(bool value) => SetInt(BoxFullReminderKey, value ? 1 : 0);
        public static bool SetFastOpenAnimation(bool value) => SetInt(FastOpenKey, value ? 1 : 0);
        public static bool SetSfxVolume(float value) => SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
        public static bool SetBgmVolume(float value) => SetFloat(BgmVolumeKey, Mathf.Clamp01(value));

        private static bool GetBool(string key, bool fallback)
        {
            var value = PlayerPrefs.GetInt(key, fallback ? 1 : 0);
            return value == 0 || value == 1 ? value == 1 : fallback;
        }

        private static float GetVolume(string key, float fallback)
        {
            var value = PlayerPrefs.GetFloat(key, fallback);
            return float.IsNaN(value) || value < 0f || value > 1f ? fallback : value;
        }

        private static bool SetInt(string key, int value) => Write(() => PlayerPrefs.SetInt(key, value));

        private static bool SetFloat(string key, float value) => Write(() => PlayerPrefs.SetFloat(key, value));

        private static bool Write(Action write)
        {
            try
            {
                write();
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("保存本机设置失败：" + exception.Message);
                return false;
            }
            Changed?.Invoke();
            return true;
        }
    }
}

