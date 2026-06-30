using System;
using System.IO;

namespace FloatingTodoWidget.Services
{
    /// <summary>Minimal thread-safe file logger. Logging must never throw.</summary>
    public static class Logger
    {
        private static readonly object Gate = new();

        public static void Info(string message) => Write("INFO", message);

        public static void Error(string message, Exception? ex = null) =>
            Write("ERROR", ex is null ? message : $"{message} :: {ex}");

        private static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(AppPaths.Folder);
                    File.AppendAllText(AppPaths.LogFile,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Swallow - a broken logger should never crash the widget.
            }
        }
    }
}
