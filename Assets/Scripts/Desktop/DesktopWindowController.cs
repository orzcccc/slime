#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
#define SLIME_NATIVE_DESKTOP_WINDOW
#endif

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SlimeBlindBox.App;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.Desktop
{
    /// <summary>
    /// 桌面岛屿窗口：480×480 实际尺寸（可按 100/125/150/200% 整体缩放）、无边框、透明背景、
    /// 按住岛屿拖动（可锁定）、透明区域点击穿透、始终置顶、显示/隐藏岛屿。
    /// 原生窗口能力只在 Windows Player 中生效；Editor 中按普通 Game 视图运行。
    /// </summary>
    public sealed class DesktopWindowController : MonoBehaviour
    {
        /// <summary>100% 档位的实际窗口尺寸；800×800 设计坐标由 CanvasScaler 统一缩放到 0.6 倍。</summary>
        public const int WindowWidth = 480;
        public const int WindowHeight = 480;

        // 设计尺寸独立于窗口像素；修改窗口大小时不能同步改小参考分辨率。
        private const float UiReferenceSize = 800f;

        private const string PositionXKey = "desktop.window.x";
        private const string PositionYKey = "desktop.window.y";
        private const string PositionSavedKey = "desktop.window.saved";
        private const int MinimumVisiblePixels = 120;
        private const float VisibleAlphaThreshold = 0.02f;
        private const float IslandAlphaHitThreshold = 0.1f;

        private static readonly Color EditorBackground = new Color32(58, 65, 80, 255);

        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
        private PointerEventData pointerData;
        private EventSystem pointerEventSystem;
        private Camera configuredCamera;
        private float nextHitAreaRefresh;
        private bool canvasScaleDirty = true;

        public static DesktopWindowController Instance { get; private set; }

        /// <summary>当前窗口缩放百分比（100/125/150/200）。</summary>
        public static int ScalePercent { get; private set; } = 100;

        /// <summary>当前窗口的实际像素边长。</summary>
        public static int WindowSize => Mathf.RoundToInt(WindowWidth * ScalePercent / 100f);

        /// <summary>锁定后按住岛屿不再拖动窗口，按钮等控件照常可用。</summary>
        public bool PositionLocked { get; set; }

        /// <summary>岛屿窗口是否显示（托盘左键可隐藏）。</summary>
        public bool IslandVisible { get; private set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PrepareWindow()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
            ScalePercent = ResolveStartupScale();
#if SLIME_NATIVE_DESKTOP_WINDOW
            Screen.SetResolution(WindowSize, WindowSize, FullScreenMode.Windowed);
            // 场景加载前就切换成无边框透明窗口并移到目标位置，避免启动时先闪出带标题栏的默认窗口。
            ApplyNativeWindowStyle(FindUnityWindow());
#endif
        }

        /// <summary>玩家选过缩放就用玩家的选择，否则取与系统缩放最接近的一档。</summary>
        private static int ResolveStartupScale()
        {
            var saved = LocalSettings.SavedScalePercent;
            if (saved > 0) return saved;
#if SLIME_NATIVE_DESKTOP_WINDOW
            return LocalSettings.NearestScalePercent(GetSystemDpi() / 96f);
#else
            return 100;
#endif
        }

        private void Awake()
        {
            Instance = this;
            PositionLocked = LocalSettings.LockPosition;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            ConfigureCamera();
#if SLIME_NATIVE_DESKTOP_WINDOW
            InitializeNativeWindow();
#endif
        }

        private void LateUpdate()
        {
            if (configuredCamera == null || configuredCamera != Camera.main) ConfigureCamera();
            if (canvasScaleDirty) ApplyCanvasScale();
            if (Time.unscaledTime >= nextHitAreaRefresh)
            {
                nextHitAreaRefresh = Time.unscaledTime + 1f;
                EnableIslandAlphaHitTest();
                ApplyCanvasScale();
            }
#if SLIME_NATIVE_DESKTOP_WINDOW
            UpdateNativeWindow();
#endif
        }

        private void OnApplicationQuit()
        {
#if SLIME_NATIVE_DESKTOP_WINDOW
            SaveWindowPosition();
#endif
        }

        private void ConfigureCamera()
        {
            var targetCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
            if (targetCamera == null) return;
            configuredCamera = targetCamera;
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
#if SLIME_NATIVE_DESKTOP_WINDOW
            // 透明窗口依赖帧缓冲 Alpha：清屏为全透明，并关闭会丢弃 Alpha 的 HDR 与后处理。
            targetCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
#else
            targetCamera.backgroundColor = EditorBackground;
#endif
            targetCamera.allowHDR = false;
            var cameraData = targetCamera.GetUniversalAdditionalCameraData();
            if (cameraData != null) cameraData.renderPostProcessing = false;
        }

        /// <summary>切换始终置顶。失败时返回 false，由调用方回滚开关。</summary>
        public bool SetAlwaysOnTop(bool enabled)
        {
#if SLIME_NATIVE_DESKTOP_WINDOW
            if (windowHandle == IntPtr.Zero) return false;
            return SetWindowPos(windowHandle, enabled ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
#else
            return true;
#endif
        }

        /// <summary>按百分比整体缩放窗口与界面，保持中心不变；超出屏幕时挪回所在显示器工作区内。</summary>
        public bool ApplyScale(int percent)
        {
            if (Array.IndexOf(LocalSettings.ScalePercents, percent) < 0) return false;
#if SLIME_NATIVE_DESKTOP_WINDOW
            if (windowHandle == IntPtr.Zero || !GetWindowRect(windowHandle, out var rect)) return false;
            var centerX = (rect.left + rect.right) / 2;
            var centerY = (rect.top + rect.bottom) / 2;
            ScalePercent = percent;
            var size = WindowSize;
            var target = new RECT { left = centerX - size / 2, top = centerY - size / 2 };
            target.right = target.left + size;
            target.bottom = target.top + size;
            var position = ClampFullyInside(target.left, target.top, MonitorFromRect(ref target, MONITOR_DEFAULTTONEAREST));
            Screen.SetResolution(size, size, FullScreenMode.Windowed);
            SetWindowPos(windowHandle, IntPtr.Zero, position.x, position.y, size, size, SWP_NOZORDER | SWP_NOACTIVATE);
            SaveWindowPosition();
#else
            ScalePercent = percent;
#endif
            canvasScaleDirty = true;
            return true;
        }

        /// <summary>把窗口移到主屏幕工作区右下角并保存位置。</summary>
        public bool ResetPosition()
        {
#if SLIME_NATIVE_DESKTOP_WINDOW
            if (windowHandle == IntPtr.Zero) return false;
            var work = GetWorkArea(MonitorFromPoint(new POINT(), MONITOR_DEFAULTTOPRIMARY));
            var size = WindowSize;
            var moved = SetWindowPos(windowHandle, IntPtr.Zero, work.right - size, work.bottom - size, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
            if (moved) SaveWindowPosition();
            return moved;
#else
            return true;
#endif
        }

        /// <summary>显示或隐藏岛屿窗口。隐藏期间游戏继续运行，托盘图标保留。</summary>
        public void SetIslandVisible(bool visible)
        {
            IslandVisible = visible;
#if SLIME_NATIVE_DESKTOP_WINDOW
            if (windowHandle != IntPtr.Zero) ShowWindow(windowHandle, visible ? SW_SHOWNOACTIVATE : SW_HIDE);
#endif
        }

        /// <summary>显示岛屿并尝试置于前台（托盘打开设置或退出确认时使用）。</summary>
        public void BringToFront()
        {
            SetIslandVisible(true);
#if SLIME_NATIVE_DESKTOP_WINDOW
            if (windowHandle != IntPtr.Zero) SetForegroundWindow(windowHandle);
#endif
        }

        /// <summary>窗口缩放后，界面按同一比例放大，布局比例不变。</summary>
        private void ApplyCanvasScale()
        {
#if SLIME_NATIVE_DESKTOP_WINDOW
            var factor = WindowSize / UiReferenceSize;
            var found = false;
            foreach (var canvas in FindObjectsOfType<Canvas>())
            {
                if (!canvas.isRootCanvas) continue;
                found = true;
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(UiReferenceSize, UiReferenceSize);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = .5f;
                }
                else if (!Mathf.Approximately(canvas.scaleFactor, factor))
                {
                    canvas.scaleFactor = factor;
                }
            }
            if (found) canvasScaleDirty = false;
#else
            canvasScaleDirty = false;
#endif
        }

        /// <summary>岛屿等可读纹理的图片只在不透明像素上响应点击，透明像素交给桌面。</summary>
        private static void EnableIslandAlphaHitTest()
        {
            foreach (var image in FindObjectsOfType<Image>())
            {
                if (image.sprite == null || !image.raycastTarget) continue;
                var texture = image.sprite.texture;
                if (texture != null && texture.isReadable) image.alphaHitTestMinimumThreshold = IslandAlphaHitThreshold;
            }
        }

        /// <summary>
        /// 判断屏幕坐标处是否有玩家能看见的 UI（全透明容器不算）。
        /// topmost 为最上层的射线目标（含透明点击区），用于判断按下的是不是控件。
        /// </summary>
        private bool TryGetVisibleUi(Vector2 screenPosition, out GameObject topmost)
        {
            topmost = null;
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            if (pointerData == null || pointerEventSystem != eventSystem)
            {
                pointerData = new PointerEventData(eventSystem);
                pointerEventSystem = eventSystem;
            }
            pointerData.position = screenPosition;
            raycastResults.Clear();
            eventSystem.RaycastAll(pointerData, raycastResults);
            if (raycastResults.Count > 0) topmost = raycastResults[0].gameObject;
            foreach (var result in raycastResults)
            {
                var graphic = result.gameObject.GetComponent<Graphic>();
                if (graphic == null || !graphic.enabled) continue;
                // 文字的射线区域是整个矩形，不能代表实际可见内容，按透明处理。
                if (graphic is TMP_Text) continue;
                if (graphic.color.a <= VisibleAlphaThreshold) continue;
                if (graphic.canvasRenderer.GetInheritedAlpha() <= VisibleAlphaThreshold) continue;
                return true;
            }
            return false;
        }

        /// <summary>按钮、下拉框、滚动列表等控件保留自身交互，其余可见区域可拖动窗口。</summary>
        private static bool IsDragHandle(GameObject target)
        {
            for (var current = target.transform; current != null; current = current.parent)
            {
                if (current.GetComponent<Selectable>() != null) return false;
                if (current.GetComponent<ScrollRect>() != null) return false;
            }
            return true;
        }

#if SLIME_NATIVE_DESKTOP_WINDOW
        private IntPtr windowHandle;
        private bool isClickThrough;
        private bool isDragging;
        private POINT dragStartCursor;
        private RECT dragStartWindow;

        private void InitializeNativeWindow()
        {
            windowHandle = FindUnityWindow();
            if (windowHandle == IntPtr.Zero)
            {
                Debug.LogWarning("未找到游戏窗口句柄，桌面窗口功能未启用。");
                return;
            }

            ApplyNativeWindowStyle(windowHandle);
            if (LocalSettings.AlwaysOnTop && !SetAlwaysOnTop(true)) Debug.LogWarning("恢复始终置顶失败。");
        }

        private static bool nativeStyleApplied;

        /// <summary>设置无边框样式、DWM 透明和启动位置。只执行一次，重复调用直接返回。</summary>
        private static void ApplyNativeWindowStyle(IntPtr handle)
        {
            if (handle == IntPtr.Zero || nativeStyleApplied) return;
            nativeStyleApplied = true;

            SetWindowLongPtrCompat(handle, GWL_STYLE, new IntPtr(WS_POPUP | WS_VISIBLE));
            var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(handle, ref margins);

            var position = ResolveStartupPosition();
            SetWindowPos(handle, IntPtr.Zero, position.x, position.y, WindowSize, WindowSize,
                SWP_FRAMECHANGED | SWP_NOZORDER | SWP_SHOWWINDOW);
        }

        private void UpdateNativeWindow()
        {
            if (windowHandle == IntPtr.Zero) return;
            if (!GetCursorPos(out var cursor)) return;

            if (isDragging)
            {
                if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) == 0)
                {
                    isDragging = false;
                    ClampWindowToScreen();
                    SaveWindowPosition();
                }
                else
                {
                    var x = dragStartWindow.left + cursor.x - dragStartCursor.x;
                    var y = dragStartWindow.top + cursor.y - dragStartCursor.y;
                    SetWindowPos(windowHandle, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                }
                SetClickThrough(false);
                return;
            }

            // 点击穿透时 Unity 收不到鼠标消息，因此直接读取系统光标位置做命中检测。
            var client = cursor;
            ScreenToClient(windowHandle, ref client);
            var screenPosition = new Vector2(client.x, Screen.height - client.y);
            var inside = client.x >= 0 && client.y >= 0 && client.x < Screen.width && client.y < Screen.height;
            GameObject hit = null;
            var overUi = inside && TryGetVisibleUi(screenPosition, out hit);

            if (overUi && !PositionLocked && Input.GetMouseButtonDown(0) && IsDragHandle(hit) && GetWindowRect(windowHandle, out dragStartWindow))
            {
                isDragging = true;
                dragStartCursor = cursor;
            }

            SetClickThrough(!overUi);
        }

        private void SetClickThrough(bool enabled)
        {
            if (enabled == isClickThrough) return;
            isClickThrough = enabled;
            var exStyle = GetWindowLongPtrCompat(windowHandle, GWL_EXSTYLE).ToInt64();
            exStyle = enabled
                ? exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT
                : exStyle & ~(WS_EX_LAYERED | WS_EX_TRANSPARENT);
            SetWindowLongPtrCompat(windowHandle, GWL_EXSTYLE, new IntPtr(exStyle));
        }

        private static Vector2Int ResolveStartupPosition()
        {
            if (PlayerPrefs.GetInt(PositionSavedKey, 0) == 1)
            {
                var saved = new RECT
                {
                    left = PlayerPrefs.GetInt(PositionXKey),
                    top = PlayerPrefs.GetInt(PositionYKey),
                };
                saved.right = saved.left + WindowSize;
                saved.bottom = saved.top + WindowSize;
                // 保存的位置不在任何显示器上（例如拔掉了副屏）时，回到主屏幕中央。
                if (MonitorFromRect(ref saved, MONITOR_DEFAULTTONULL) != IntPtr.Zero)
                    return ClampToWorkArea(saved.left, saved.top, MonitorFromRect(ref saved, MONITOR_DEFAULTTONEAREST));
            }

            var primary = MonitorFromPoint(new POINT(), MONITOR_DEFAULTTOPRIMARY);
            var work = GetWorkArea(primary);
            return new Vector2Int(
                work.left + (work.right - work.left - WindowSize) / 2,
                work.top + (work.bottom - work.top - WindowSize) / 2);
        }

        private void ClampWindowToScreen()
        {
            if (!GetWindowRect(windowHandle, out var rect)) return;
            var clamped = ClampToWorkArea(rect.left, rect.top, MonitorFromRect(ref rect, MONITOR_DEFAULTTONEAREST));
            if (clamped.x != rect.left || clamped.y != rect.top)
                SetWindowPos(windowHandle, IntPtr.Zero, clamped.x, clamped.y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }

        private static Vector2Int ClampToWorkArea(int x, int y, IntPtr monitor)
        {
            var work = GetWorkArea(monitor);
            x = Mathf.Clamp(x, work.left - WindowSize + MinimumVisiblePixels, work.right - MinimumVisiblePixels);
            y = Mathf.Clamp(y, work.top - WindowSize + MinimumVisiblePixels, work.bottom - MinimumVisiblePixels);
            return new Vector2Int(x, y);
        }

        /// <summary>整个窗口放进工作区；窗口比工作区还大时贴左上角。</summary>
        private static Vector2Int ClampFullyInside(int x, int y, IntPtr monitor)
        {
            var work = GetWorkArea(monitor);
            x = Mathf.Max(work.left, Mathf.Min(x, work.right - WindowSize));
            y = Mathf.Max(work.top, Mathf.Min(y, work.bottom - WindowSize));
            return new Vector2Int(x, y);
        }

        /// <summary>系统 DPI（96 = 100%）。旧系统没有该接口时按 96 处理。</summary>
        private static int GetSystemDpi()
        {
            try
            {
                var dpi = (int)GetDpiForSystem();
                return dpi > 0 ? dpi : 96;
            }
            catch (EntryPointNotFoundException)
            {
                return 96;
            }
        }

        private static RECT GetWorkArea(IntPtr monitor)
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info)) return info.rcWork;
            return new RECT { left = 0, top = 0, right = Display.main.systemWidth, bottom = Display.main.systemHeight };
        }

        private void SaveWindowPosition()
        {
            if (windowHandle == IntPtr.Zero || !GetWindowRect(windowHandle, out var rect)) return;
            PlayerPrefs.SetInt(PositionXKey, rect.left);
            PlayerPrefs.SetInt(PositionYKey, rect.top);
            PlayerPrefs.SetInt(PositionSavedKey, 1);
            PlayerPrefs.Save();
        }

        private static IntPtr foundWindow;

        private static IntPtr FindUnityWindow()
        {
            foundWindow = IntPtr.Zero;
            EnumWindows(FindWindowCallback, IntPtr.Zero);
            return foundWindow;
        }

        [AOT.MonoPInvokeCallback(typeof(EnumWindowsProc))]
        private static bool FindWindowCallback(IntPtr handle, IntPtr parameter)
        {
            GetWindowThreadProcessId(handle, out var processId);
            if (processId != (uint)System.Diagnostics.Process.GetCurrentProcess().Id) return true;
            var className = new System.Text.StringBuilder(64);
            GetClassName(handle, className, className.Capacity);
            if (className.ToString() != "UnityWndClass") return true;
            foundWindow = handle;
            return false;
        }

        private static IntPtr GetWindowLongPtrCompat(IntPtr handle, int index) =>
            IntPtr.Size == 8 ? GetWindowLongPtr(handle, index) : new IntPtr(GetWindowLong(handle, index));

        private static void SetWindowLongPtrCompat(IntPtr handle, int index, IntPtr value)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr(handle, index, value);
            else SetWindowLong(handle, index, value.ToInt32());
        }

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const long WS_POPUP = 0x80000000L;
        private const long WS_VISIBLE = 0x10000000L;
        private const long WS_EX_LAYERED = 0x00080000L;
        private const long WS_EX_TRANSPARENT = 0x00000020L;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint MONITOR_DEFAULTTONULL = 0;
        private const uint MONITOR_DEFAULTTOPRIMARY = 1;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int VK_LBUTTON = 0x01;
        private const int SW_HIDE = 0;
        private const int SW_SHOWNOACTIVATE = 4;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x; public int y; }
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int left; public int top; public int right; public int bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct MARGINS { public int cxLeftWidth; public int cxRightWidth; public int cyTopHeight; public int cyBottomHeight; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr handle, System.Text.StringBuilder className, int maxCount);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] private static extern int GetWindowLong(IntPtr handle, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] private static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] private static extern int SetWindowLong(IntPtr handle, int index, int value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] private static extern IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr value);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out RECT rect);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr handle, ref POINT point);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref RECT rect, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT point, uint flags);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
        [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr handle, ref MARGINS margins);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr handle, int command);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
        [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
#endif
    }
}
