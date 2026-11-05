using SlimeBlindBox.Desktop;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SlimeBlindBox.EditorTools
{
    /// <summary>
    /// 桌面岛屿窗口所需的 Player 设置。项目加载时和每次构建前自动校正，避免被手动改回全屏配置。
    /// </summary>
    [InitializeOnLoad]
    public sealed class DesktopWindowPlayerSettings : IPreprocessBuildWithReport
    {
        static DesktopWindowPlayerSettings()
        {
            EditorApplication.delayCall += () => Apply(false);
        }

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => Apply(false);

        [MenuItem("Slime/Desktop/Apply Desktop Window Player Settings")]
        private static void ApplyFromMenu() => Apply(true);

        private static void Apply(bool log)
        {
            var changed = false;
            changed |= Set(PlayerSettings.defaultScreenWidth, DesktopWindowController.WindowWidth, v => PlayerSettings.defaultScreenWidth = v);
            changed |= Set(PlayerSettings.defaultScreenHeight, DesktopWindowController.WindowHeight, v => PlayerSettings.defaultScreenHeight = v);
            changed |= Set(PlayerSettings.fullScreenMode, FullScreenMode.Windowed, v => PlayerSettings.fullScreenMode = v);
            changed |= Set(PlayerSettings.defaultIsNativeResolution, false, v => PlayerSettings.defaultIsNativeResolution = v);
            changed |= Set(PlayerSettings.resizableWindow, false, v => PlayerSettings.resizableWindow = v);
            changed |= Set(PlayerSettings.allowFullscreenSwitch, false, v => PlayerSettings.allowFullscreenSwitch = v);
            changed |= Set(PlayerSettings.runInBackground, true, v => PlayerSettings.runInBackground = v);
            changed |= Set(PlayerSettings.forceSingleInstance, true, v => PlayerSettings.forceSingleInstance = v);
            // DXGI 翻转模型交换链不支持 DWM 逐像素透明，必须关闭，否则透明区域会显示为黑色。
            changed |= Set(PlayerSettings.useFlipModelSwapchain, false, v => PlayerSettings.useFlipModelSwapchain = v);

            if (changed) AssetDatabase.SaveAssets();
            if (log || changed) Debug.Log(changed ? "已应用桌面岛屿窗口 Player 设置（800×800 无边框透明窗口）。" : "桌面岛屿窗口 Player 设置已是最新。");
        }

        private static bool Set<T>(T current, T target, System.Action<T> assign)
        {
            if (Equals(current, target)) return false;
            assign(target);
            return true;
        }
    }
}
