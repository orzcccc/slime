#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
#define SLIME_NATIVE_DESKTOP_WINDOW
#endif

using System;
using System.Collections.Generic;
using System.IO;
using SlimeBlindBox.App;
using UnityEngine;

namespace SlimeBlindBox.Desktop
{
    /// <summary>托盘菜单中需要由设置界面统一处理的命令（保证与设置开关同步）。</summary>
    public enum TrayCommand
    {
        AlwaysOnTop = 2,
        LockPosition = 3,
        ResetPosition = 4,
        OpenSettings = 5,
        Exit = 6,
    }

    /// <summary>
    /// 常驻的托盘控制器：左键显示/隐藏岛屿，右键原生菜单，待领取存满时切换图标并按设置发送一次系统通知。
    /// 原生能力只在 Windows Player 中启用；Editor 中只保留存满状态判断，便于调试。
    /// </summary>
    public sealed class DesktopTrayController : MonoBehaviour
    {
        private const int ToggleIslandCommand = 1;
        private const string IconFolder = "Tray";
        private const string NormalIconFile = "TrayIcon.ico";
        private const string FullIconFile = "TrayIconFull.ico";

        /// <summary>需要设置界面处理的托盘命令。</summary>
        public event Action<TrayCommand> CommandRequested;

        private SlimeGameController controller;
        private bool hasState;
        private bool wasFull;
#if SLIME_NATIVE_DESKTOP_WINDOW
        private TrayIconService tray;
#endif

        private void Start()
        {
            controller = GetComponent<SlimeGameController>();
            if (controller != null) controller.StateChanged += OnStateChanged;
#if SLIME_NATIVE_DESKTOP_WINDOW
            var folder = Path.Combine(Application.streamingAssetsPath, IconFolder);
            tray = new TrayIconService();
            if (!tray.Create(DesktopText.TrayTooltip, Path.Combine(folder, NormalIconFile), Path.Combine(folder, FullIconFile),
                    BuildMenu, OnMenuCommand, ToggleIsland))
            {
                Debug.LogWarning("托盘图标创建失败，托盘菜单不可用。");
                tray.Dispose();
                tray = null;
            }
#endif
        }

        private void Update()
        {
#if SLIME_NATIVE_DESKTOP_WINDOW
            tray?.Pump();
#endif
        }

        private void OnDestroy()
        {
            if (controller != null) controller.StateChanged -= OnStateChanged;
#if SLIME_NATIVE_DESKTOP_WINDOW
            tray?.Dispose();
            tray = null;
#endif
        }

        /// <summary>待领取达到上限时切换存满图标；从未满变为存满时（每次存满一次）发送系统通知。</summary>
        private void OnStateChanged()
        {
            var state = controller.GameState;
            if (state == null) return;
            var full = state.box_capacity > 0 && state.box_count >= state.box_capacity;
            // 启动时已存满不通知：玩家此刻就在看游戏。
            if (hasState && full && !wasFull && LocalSettings.BoxFullReminder) Notify(state.box_capacity);
            hasState = true;
            wasFull = full;
#if SLIME_NATIVE_DESKTOP_WINDOW
            tray?.SetFull(full);
#endif
        }

        private void Notify(int capacity)
        {
#if SLIME_NATIVE_DESKTOP_WINDOW
            if (tray == null || !WindowsShellSettings.AreNotificationsEnabled()) return;
            if (!tray.ShowNotification(DesktopText.BoxFullTitle, string.Format(DesktopText.BoxFullMessageFormat, capacity)))
                Debug.LogWarning("存满提醒发送失败。");
#endif
        }

#if SLIME_NATIVE_DESKTOP_WINDOW
        private static List<TrayMenuItem> BuildMenu()
        {
            var window = DesktopWindowController.Instance;
            var visible = window == null || window.IslandVisible;
            return new List<TrayMenuItem>
            {
                new TrayMenuItem { Command = ToggleIslandCommand, Label = visible ? DesktopText.HideIsland : DesktopText.ShowIsland },
                new TrayMenuItem { Command = (int)TrayCommand.AlwaysOnTop, Label = DesktopText.AlwaysOnTop, Checked = LocalSettings.AlwaysOnTop },
                new TrayMenuItem { Command = (int)TrayCommand.LockPosition, Label = DesktopText.LockPosition, Checked = LocalSettings.LockPosition },
                new TrayMenuItem(),
                new TrayMenuItem { Command = (int)TrayCommand.ResetPosition, Label = DesktopText.ResetPosition },
                new TrayMenuItem { Command = (int)TrayCommand.OpenSettings, Label = DesktopText.OpenSettings },
                new TrayMenuItem(),
                new TrayMenuItem { Command = (int)TrayCommand.Exit, Label = DesktopText.Exit },
            };
        }
#endif

        private void OnMenuCommand(int command)
        {
            if (command == ToggleIslandCommand)
            {
                ToggleIsland();
                return;
            }
            if (Enum.IsDefined(typeof(TrayCommand), command)) CommandRequested?.Invoke((TrayCommand)command);
        }

        private static void ToggleIsland()
        {
            var window = DesktopWindowController.Instance;
            if (window != null) window.SetIslandVisible(!window.IslandVisible);
        }
    }
}

