#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
#define SLIME_NATIVE_DESKTOP_WINDOW
#endif

using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace SlimeBlindBox.Desktop
{
    /// <summary>
    /// 当前用户（HKCU）范围内的系统设置：开机自启（Run 键）与系统通知总开关。不需要管理员权限。
    /// 只在 Windows Player 中真正读写；Editor 中不写注册表，避免把 Unity 编辑器注册为开机启动。
    /// </summary>
    public static class WindowsShellSettings
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "SlimeBlindBox";
        private const string PushNotificationsKeyPath = @"Software\Microsoft\Windows\CurrentVersion\PushNotifications";
        private const string ToastEnabledValueName = "ToastEnabled";

#if SLIME_NATIVE_DESKTOP_WINDOW
        public static bool AutoStartSupported => true;
#else
        public static bool AutoStartSupported => false;
#endif

        /// <summary>注册表中是否登记了本游戏的开机启动项。登记的路径与当前程序不一致时（游戏被移动）同步为当前路径。</summary>
        public static bool IsAutoStartEnabled()
        {
#if SLIME_NATIVE_DESKTOP_WINDOW
            if (!TryReadString(RunKeyPath, RunValueName, out var value) || string.IsNullOrEmpty(value)) return false;
            var expected = BuildCommand();
            if (expected != null && !string.Equals(value, expected, StringComparison.OrdinalIgnoreCase)) WriteString(RunKeyPath, RunValueName, expected);
            return true;
#else
            return false;
#endif
        }

        /// <summary>登记或移除开机启动项，失败返回 false。</summary>
        public static bool SetAutoStart(bool enabled)
        {
#if SLIME_NATIVE_DESKTOP_WINDOW
            try
            {
                if (!enabled) return DeleteValue(RunKeyPath, RunValueName);
                var command = BuildCommand();
                return command != null && WriteString(RunKeyPath, RunValueName, command);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("设置开机自启失败：" + exception.Message);
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>系统通知总开关（设置 > 系统 > 通知）。读取不到时视为开启。</summary>
        public static bool AreNotificationsEnabled()
        {
#if SLIME_NATIVE_DESKTOP_WINDOW
            return !TryReadDword(PushNotificationsKeyPath, ToastEnabledValueName, out var value) || value != 0;
#else
            return true;
#endif
        }

#if SLIME_NATIVE_DESKTOP_WINDOW
        private static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(unchecked((int)0x80000001));
        private const int KEY_QUERY_VALUE = 0x0001;
        private const int KEY_SET_VALUE = 0x0002;
        private const int REG_SZ = 1;
        private const int REG_DWORD = 4;
        private const int ERROR_SUCCESS = 0;
        private const int ERROR_FILE_NOT_FOUND = 2;

        private static string BuildCommand()
        {
            var path = new StringBuilder(1024);
            var length = GetModuleFileName(IntPtr.Zero, path, path.Capacity);
            return length <= 0 ? null : "\"" + path + "\"";
        }

        private static bool TryReadString(string keyPath, string name, out string value)
        {
            value = null;
            if (RegOpenKeyEx(HKEY_CURRENT_USER, keyPath, 0, KEY_QUERY_VALUE, out var key) != ERROR_SUCCESS) return false;
            try
            {
                var size = 0;
                if (RegQueryValueEx(key, name, IntPtr.Zero, out var type, null, ref size) != ERROR_SUCCESS || type != REG_SZ || size <= 0) return false;
                var data = new byte[size];
                if (RegQueryValueEx(key, name, IntPtr.Zero, out type, data, ref size) != ERROR_SUCCESS) return false;
                value = Encoding.Unicode.GetString(data, 0, size).TrimEnd('\0');
                return true;
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        private static bool TryReadDword(string keyPath, string name, out int value)
        {
            value = 0;
            if (RegOpenKeyEx(HKEY_CURRENT_USER, keyPath, 0, KEY_QUERY_VALUE, out var key) != ERROR_SUCCESS) return false;
            try
            {
                var data = new byte[4];
                var size = data.Length;
                if (RegQueryValueEx(key, name, IntPtr.Zero, out var type, data, ref size) != ERROR_SUCCESS || type != REG_DWORD) return false;
                value = BitConverter.ToInt32(data, 0);
                return true;
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        private static bool WriteString(string keyPath, string name, string value)
        {
            if (RegCreateKeyEx(HKEY_CURRENT_USER, keyPath, 0, null, 0, KEY_SET_VALUE, IntPtr.Zero, out var key, out _) != ERROR_SUCCESS) return false;
            try
            {
                var data = Encoding.Unicode.GetBytes(value + "\0");
                return RegSetValueEx(key, name, 0, REG_SZ, data, data.Length) == ERROR_SUCCESS;
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        private static bool DeleteValue(string keyPath, string name)
        {
            var opened = RegOpenKeyEx(HKEY_CURRENT_USER, keyPath, 0, KEY_SET_VALUE, out var key);
            if (opened == ERROR_FILE_NOT_FOUND) return true;
            if (opened != ERROR_SUCCESS) return false;
            try
            {
                var result = RegDeleteValue(key, name);
                return result == ERROR_SUCCESS || result == ERROR_FILE_NOT_FOUND;
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] private static extern int RegOpenKeyEx(IntPtr key, string subKey, int options, int access, out IntPtr result);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] private static extern int RegCreateKeyEx(IntPtr key, string subKey, int reserved, string className, int options, int access, IntPtr security, out IntPtr result, out int disposition);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] private static extern int RegQueryValueEx(IntPtr key, string name, IntPtr reserved, out int type, byte[] data, ref int size);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] private static extern int RegSetValueEx(IntPtr key, string name, int reserved, int type, byte[] data, int size);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] private static extern int RegDeleteValue(IntPtr key, string name);
        [DllImport("advapi32.dll")] private static extern int RegCloseKey(IntPtr key);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetModuleFileName(IntPtr module, StringBuilder fileName, int size);
#endif
    }
}

