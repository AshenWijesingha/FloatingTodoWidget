using System;
using Microsoft.Win32;

namespace FloatingTodoWidget.Services
{
    /// <summary>Toggles "start with Windows" via the per-user Run registry key (no admin needed).</summary>
    public static class StartupService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "FloatingTodoWidget";

        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
                return key?.GetValue(AppName) is not null;
            }
            catch (Exception ex)
            {
                Logger.Error("Read autostart failed", ex);
                return false;
            }
        }

        public static void SetEnabled(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                                ?? Registry.CurrentUser.CreateSubKey(RunKey);
                if (key is null) return;

                if (enable)
                {
                    // Environment.ProcessPath = the running .exe (.NET 6+).
                    var exe = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exe))
                        key.SetValue(AppName, $"\"{exe}\"");
                }
                else
                {
                    key.DeleteValue(AppName, throwOnMissingValue: false);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Write autostart failed", ex);
            }
        }
    }
}
