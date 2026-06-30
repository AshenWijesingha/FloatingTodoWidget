using System;
using Microsoft.Win32;

namespace FloatingTodoWidget.Services
{
    public static class StartupService
    {
        private const string Key  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string Name = "FloatingTodoWidget";

        public static bool IsEnabled()
        {
            using var k = Registry.CurrentUser.OpenSubKey(Key, false);
            return k?.GetValue(Name) != null;
        }

        public static void SetEnabled(bool enable)
        {
            using var k = Registry.CurrentUser.OpenSubKey(Key, true);
            if (k == null) return;
            if (enable)
                k.SetValue(Name, $"\"{Environment.ProcessPath}\"");
            else
                k.DeleteValue(Name, throwOnMissingValue: false);
        }
    }
}
