using System;
using System.IO;

namespace FloatingTodoWidget.Services
{
    public static class Logger
    {
        public static void Info(string msg) => Write("INFO", msg, null);
        public static void Error(string msg, Exception? ex = null) => Write("ERROR", msg, ex);

        private static void Write(string level, string msg, Exception? ex)
        {
            try
            {
                AppPaths.EnsureFolder();
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {msg}";
                if (ex != null) line += $"\n  {ex}";
                File.AppendAllText(AppPaths.LogFile, line + Environment.NewLine);
            }
            catch { /* never throw from logger */ }
        }
    }
}
