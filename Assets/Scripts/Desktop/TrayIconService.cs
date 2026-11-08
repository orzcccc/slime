#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
#define SLIME_NATIVE_DESKTOP_WINDOW
#endif

#if SLIME_NATIVE_DESKTOP_WINDOW
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SlimeBlindBox.Desktop
{
    /// <summary>托盘菜单项。Command 为 0 表示分隔线。</summary>
    internal struct TrayMenuItem
    {
        public int Command;
        public string Label;
        public bool Checked;
    }

    /// <summary>
    /// Windows 通知区域图标：常态 / 存满两种图标、左键回调、右键原生菜单、气泡通知。
    /// 消息窗口在主线程创建，回调由 Unity 主线程的消息循环派发；回调内只记录事件，
    /// 由 Pump() 在 Update 中处理，避免在窗口过程中执行游戏逻辑。
    /// </summary>
    internal sealed class TrayIconService : IDisposable
    {
        private const int IconId = 1;
        private const int WM_APP = 0x8000;
        private const int WM_TRAY = WM_APP + 1;
        private const int WM_NULL = 0x0000;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;
        private const int NIM_ADD = 0;
        private const int NIM_MODIFY = 1;
        private const int NIM_DELETE = 2;
        private const int NIF_MESSAGE = 0x01;
        private const int NIF_ICON = 0x02;
        private const int NIF_TIP = 0x04;
        private const int NIF_INFO = 0x10;
        private const int NIIF_INFO = 0x01;
        private const int IMAGE_ICON = 1;
        private const int LR_LOADFROMFILE = 0x10;
        private const int SM_CXSMICON = 49;
        private const int SM_CYSMICON = 50;
        private const uint MF_STRING = 0x0000;
        private const uint MF_CHECKED = 0x0008;
        private const uint MF_SEPARATOR = 0x0800;
        private const uint TPM_RIGHTBUTTON = 0x0002;
        private const uint TPM_BOTTOMALIGN = 0x0020;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint WS_POPUP = 0x80000000;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const string WindowClassName = "SlimeBlindBoxTrayWindow";

        private static TrayIconService active;
        private static readonly WndProc WindowProcedure = HandleMessage;
        private static uint taskbarCreatedMessage;

        private readonly Queue<Action> pending = new Queue<Action>();
        private IntPtr window;
        private IntPtr normalIcon;
        private IntPtr fullIcon;
        private bool isFull;
        private bool iconAdded;
        private string tooltip;
        private Func<List<TrayMenuItem>> menuProvider;
        private Action<int> onCommand;
        private Action onLeftClick;

        public bool Create(string tip, string normalIconPath, string fullIconPath,
            Func<List<TrayMenuItem>> menu, Action<int> command, Action leftClick)
        {
            if (active != null) return false;
            tooltip = tip;
            menuProvider = menu;
            onCommand = command;
            onLeftClick = leftClick;

            var instance = GetModuleHandle(null);
            var windowClass = new WNDCLASSEX
            {
                cbSize = Marshal.SizeOf(typeof(WNDCLASSEX)),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(WindowProcedure),
                hInstance = instance,
                lpszClassName = WindowClassName,
            };
            RegisterClassEx(ref windowClass);
            // 不可见的顶层工具窗口：菜单需要它成为前台窗口，点击菜单外部时才会自动关闭。
            window = CreateWindowEx(WS_EX_TOOLWINDOW, WindowClassName, string.Empty, WS_POPUP, 0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
            if (window == IntPtr.Zero) return false;

            active = this;
            taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
            var width = GetSystemMetrics(SM_CXSMICON);
            var height = GetSystemMetrics(SM_CYSMICON);
            normalIcon = LoadImage(IntPtr.Zero, normalIconPath, IMAGE_ICON, width, height, LR_LOADFROMFILE);
            fullIcon = LoadImage(IntPtr.Zero, fullIconPath, IMAGE_ICON, width, height, LR_LOADFROMFILE);
            if (normalIcon == IntPtr.Zero) Debug.LogWarning("托盘图标加载失败：" + normalIconPath);
            if (fullIcon == IntPtr.Zero) fullIcon = normalIcon;
            iconAdded = AddIcon();
            return iconAdded;
        }

        /// <summary>处理窗口过程记录下的托盘事件。</summary>
        public void Pump()
        {
            while (pending.Count > 0) pending.Dequeue()?.Invoke();
        }

        public void SetFull(bool full)
        {
            if (full == isFull) return;
            isFull = full;
            if (!iconAdded) return;
            var data = NewData(NIF_ICON);
            data.hIcon = CurrentIcon;
            Shell_NotifyIcon(NIM_MODIFY, ref data);
        }

        public bool ShowNotification(string title, string message)
        {
            if (!iconAdded) return false;
            var data = NewData(NIF_INFO);
            data.szInfoTitle = title;
            data.szInfo = message;
            data.dwInfoFlags = NIIF_INFO;
            return Shell_NotifyIcon(NIM_MODIFY, ref data);
        }

        public void Dispose()
        {
            if (iconAdded)
            {
                var data = NewData(0);
                Shell_NotifyIcon(NIM_DELETE, ref data);
                iconAdded = false;
            }
            if (fullIcon != IntPtr.Zero && fullIcon != normalIcon) DestroyIcon(fullIcon);
            if (normalIcon != IntPtr.Zero) DestroyIcon(normalIcon);
            normalIcon = fullIcon = IntPtr.Zero;
            if (window != IntPtr.Zero) DestroyWindow(window);
            window = IntPtr.Zero;
            if (active == this) active = null;
        }

        private IntPtr CurrentIcon => isFull ? fullIcon : normalIcon;

        private bool AddIcon()
        {
            var data = NewData(NIF_MESSAGE | NIF_ICON | NIF_TIP);
            data.uCallbackMessage = WM_TRAY;
            data.hIcon = CurrentIcon;
            data.szTip = tooltip;
            return Shell_NotifyIcon(NIM_ADD, ref data);
        }

        private NOTIFYICONDATA NewData(int flags) => new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = window,
            uID = IconId,
            uFlags = flags,
            szTip = string.Empty,
            szInfo = string.Empty,
            szInfoTitle = string.Empty,
        };

        private void ShowMenu()
        {
            var items = menuProvider?.Invoke();
            if (items == null || items.Count == 0) return;
            var menu = CreatePopupMenu();
            if (menu == IntPtr.Zero) return;
            try
            {
                foreach (var item in items)
                {
                    if (item.Command == 0) AppendMenu(menu, MF_SEPARATOR, UIntPtr.Zero, null);
                    else AppendMenu(menu, MF_STRING | (item.Checked ? MF_CHECKED : 0), new UIntPtr((uint)item.Command), item.Label);
                }
                GetCursorPos(out var cursor);
                SetForegroundWindow(window);
                var command = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_BOTTOMALIGN, cursor.x, cursor.y, window, IntPtr.Zero);
                PostMessage(window, WM_NULL, IntPtr.Zero, IntPtr.Zero);
                if (command > 0) pending.Enqueue(() => onCommand?.Invoke(command));
            }
            finally
            {
                DestroyMenu(menu);
            }
        }

        [AOT.MonoPInvokeCallback(typeof(WndProc))]
        private static IntPtr HandleMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam)
        {
            var service = active;
            if (service != null && handle == service.window)
            {
                if (message == WM_TRAY)
                {
                    var mouse = lParam.ToInt64() & 0xFFFF;
                    if (mouse == WM_LBUTTONUP) service.pending.Enqueue(service.onLeftClick);
                    else if (mouse == WM_RBUTTONUP) service.ShowMenu();
                    return IntPtr.Zero;
                }
                // 资源管理器重启后通知区域会清空，需要重新登记图标。
                if (taskbarCreatedMessage != 0 && message == taskbarCreatedMessage)
                {
                    service.iconAdded = service.AddIcon();
                    return IntPtr.Zero;
                }
            }
            return DefWindowProc(handle, message, wParam, lParam);
        }

        private delegate IntPtr WndProc(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public int cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public int uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string moduleName);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassEx(ref WNDCLASSEX windowClass);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
        [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr handle);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DefWindowProc(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadImage(IntPtr instance, string name, int type, int width, int height, int flags);
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr menu);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string text);
        [DllImport("user32.dll")] private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr handle, IntPtr parameters);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(int message, ref NOTIFYICONDATA data);
    }
}
#endif

