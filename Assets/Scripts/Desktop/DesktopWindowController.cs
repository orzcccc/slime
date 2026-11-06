#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
#define SLIME_NATIVE_DESKTOP_WINDOW
#endif

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;

namespace SlimeBlindBox.Desktop
{
    /// <summary>
    /// 桌面岛屿窗口：固定 800×800、无边框、透明背景、按住岛屿拖动、透明区域点击穿透。
    /// 原生窗口能力只在 Windows Player 中生效；Editor 中按普通 Game 视图运行。
    /// </summary>
    public sealed class DesktopWindowController : MonoBehaviour
    {
        public const int WindowWidth = 800;
        public const int WindowHeight = 800;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PrepareWindow()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
#if SLIME_NATIVE_DESKTOP_WINDOW
            Screen.SetResolution(WindowWidth, WindowHeight, FullScreenMode.Windowed);
            // 场景加载前就切换成无边框透明窗口并移到目标位置，避免启动时先闪出带标题栏的默认窗口。
            ApplyNativeWindowStyle(FindUnityWindow());
#endif
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
            if (Time.unscaledTime >= nextHitAreaRefresh)
            {
                nextHitAreaRefresh = Time.unscaledTime + 1f;
                EnableIslandAlphaHitTest();
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

        /// <summary>判断屏幕坐标处是否有玩家能看见的 UI（全透明容器不算）。</summary>
        private bool TryGetVisibleUi(Vector2 screenPosition, out GameObject hit)
        {
            hit = null;
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
            foreach (var result in raycastResults)
            {
                var graphic = result.gameObject.GetComponent<Graphic>();
                if (graphic == null || !graphic.enabled) continue;
                // 文字的射线区域是整个矩形，不能代表实际可见内容，按透明处理。
                if (graphic is TMP_Text) continue;
                if (graphic.color.a <= VisibleAlphaThreshold) continue;
                if (graphic.canvasRenderer.GetInheritedAlpha() <= VisibleAlphaThreshold) continue;
                hit = result.gameObject;
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
            SetWindowPos(handle, IntPtr.Zero, position.x, position.y, WindowWidth, WindowHeight,
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

            if (overUi && Input.GetMouseButtonDown(0) && IsDragHandle(hit) && GetWindowRect(windowHandle, out dragStartWindow))
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
                saved.right = saved.left + WindowWidth;
                saved.bottom = saved.top + WindowHeight;
                // 保存的位置不在任何显示器上（例如拔掉了副屏）时，回到主屏幕中央。
                if (MonitorFromRect(ref saved, MONITOR_DEFAULTTONULL) != IntPtr.Zero)
                    return ClampToWorkArea(saved.left, saved.top, MonitorFromRect(ref saved, MONITOR_DEFAULTTONEAREST));
            }

            var primary = MonitorFromPoint(new POINT(), MONITOR_DEFAULTTOPRIMARY);
            var work = GetWorkArea(primary);
            return new Vector2Int(
                work.left + (work.right - work.left - WindowWidth) / 2,
                work.top + (work.bottom - work.top - WindowHeight) / 2);
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
            x = Mathf.Clamp(x, work.left - WindowWidth + MinimumVisiblePixels, work.right - MinimumVisiblePixels);
            y = Mathf.Clamp(y, work.top - WindowHeight + MinimumVisiblePixels, work.bottom - MinimumVisiblePixels);
            return new Vector2Int(x, y);
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
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint MONITOR_DEFAULTTONULL = 0;
        private const uint MONITOR_DEFAULTTOPRIMARY = 1;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int VK_LBUTTON = 0x01;

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
#endif
    }
}
