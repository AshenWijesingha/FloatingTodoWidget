using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using FloatingTodoWidget.Services;

namespace FloatingTodoWidget.Helpers
{
    /// <summary>
    /// Win32 interop for acrylic blur-behind and click-through.
    /// All calls are wrapped so any failure degrades gracefully (plain semi-transparent window).
    /// </summary>
    internal static class NativeMethods
    {
        // ---- Acrylic / blur-behind ----
        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public uint GradientColor; // AABBGGRR
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private const int WCA_ACCENT_POLICY = 19;
        private const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4; // Win10 1803+/Win11
        private const int ACCENT_ENABLE_BLURBEHIND = 3;        // older fallback

        /// <param name="tintAabbggrr">Tint color in 0xAABBGGRR. Alpha controls how opaque the tint is.</param>
        public static void EnableAcrylic(Window window, uint tintAabbggrr)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                var accent = new AccentPolicy
                {
                    AccentState = ACCENT_ENABLE_ACRYLICBLURBEHIND,
                    GradientColor = tintAabbggrr
                };

                var size = Marshal.SizeOf(accent);
                var ptr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(accent, ptr, false);
                    var data = new WindowCompositionAttributeData
                    {
                        Attribute = WCA_ACCENT_POLICY,
                        SizeOfData = size,
                        Data = ptr
                    };
                    SetWindowCompositionAttribute(hwnd, ref data);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
            catch (Exception ex)
            {
                // Fallback: the window simply stays semi-transparent via its Border background.
                Logger.Error("EnableAcrylic failed; using fallback", ex);
            }
        }

        // ---- Click-through ----
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;

        public static void SetClickThrough(Window window, bool enable)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                var ex = GetWindowLong(hwnd, GWL_EXSTYLE);
                ex = enable
                    ? ex | WS_EX_LAYERED | WS_EX_TRANSPARENT
                    : ex & ~WS_EX_TRANSPARENT;
                SetWindowLong(hwnd, GWL_EXSTYLE, ex);
            }
            catch (Exception ex)
            {
                Logger.Error("SetClickThrough failed", ex);
            }
        }
    }
}
