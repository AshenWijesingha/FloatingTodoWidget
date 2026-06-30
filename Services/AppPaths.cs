using System;
using System.IO;

namespace FloatingTodoWidget.Services
{
    /// <summary>Central place for all file paths (under %AppData%\FloatingTodoWidget).</summary>
    public static class AppPaths
    {
        public static string Folder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FloatingTodoWidget");

        public static string TasksFile => Path.Combine(Folder, "tasks.json");
        public static string SettingsFile => Path.Combine(Folder, "settings.json");
        public static string LogFile => Path.Combine(Folder, "app.log");
    }
}
