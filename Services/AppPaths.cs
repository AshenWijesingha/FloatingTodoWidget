using System;
using System.IO;

namespace FloatingTodoWidget.Services
{
    public static class AppPaths
    {
        private static readonly string Folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "FloatingTodoWidget");

        public static string DataFile     => Path.Combine(Folder, "data.json");
        public static string SettingsFile => Path.Combine(Folder, "settings.json");
        public static string LogFile      => Path.Combine(Folder, "app.log");

        public static void EnsureFolder() => Directory.CreateDirectory(Folder);
    }
}
