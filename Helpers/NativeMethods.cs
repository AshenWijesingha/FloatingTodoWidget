using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FloatingTodoWidget.Helpers
{
    public static class NativeMethods
    {
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int nIndex);
        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS m);
        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED     = 0x00080000;

        // ── Global hotkey (WM_HOTKEY) ──
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT     = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT   = 0x0004;
        public const uint VK_T = 0x54;

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS { public int Left, Right, Top, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public int AccentState, AccentFlags;
            public uint GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        public static void EnableAcrylic(Window window, uint tintColor)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                var accent = new AccentPolicy { AccentState = 4, GradientColor = tintColor };
                int size = Marshal.SizeOf(accent);
                var ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WindowCompositionAttributeData { Attribute = 19, Data = ptr, SizeOfData = size };
                SetWindowCompositionAttribute(hwnd, ref data);
                Marshal.FreeHGlobal(ptr);
            }
            catch { /* acrylic optional */ }
        }

        public static void SetClickThrough(Window window, bool enable)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            style = enable
                ? style | WS_EX_TRANSPARENT | WS_EX_LAYERED
                : style & ~WS_EX_TRANSPARENT;
            SetWindowLong(hwnd, GWL_EXSTYLE, style);
        }

        public static bool RegisterGlobalHotKey(Window window, int id, uint modifiers, uint key)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                return RegisterHotKey(hwnd, id, modifiers, key);
            }
            catch { return false; }
        }

        public static void UnregisterGlobalHotKey(Window window, int id)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                UnregisterHotKey(hwnd, id);
            }
            catch { /* best effort */ }
        }
    }
}
