using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FloatingTodoWidget.Models;

namespace FloatingTodoWidget.Services
{
    /// <summary>JSON persistence via System.Text.Json (no third-party deps).</summary>
    public sealed class JsonStorageService : IStorageService
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public List<TodoItem> LoadTasks()
        {
            try
            {
                if (!File.Exists(AppPaths.DataFile))
                    return new List<TodoItem>();

                var json = File.ReadAllText(AppPaths.DataFile);
                return JsonSerializer.Deserialize<List<TodoItem>>(json, Options)
                       ?? new List<TodoItem>();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load tasks", ex);
                return new List<TodoItem>();
            }
        }

        public void SaveTasks(IEnumerable<TodoItem> tasks)
        {
            try
            {
                AppPaths.EnsureFolder();
                var json = JsonSerializer.Serialize(tasks.ToList(), Options);
                // Write to a temp file then atomically replace the target.
                // File.Move with overwrite is a single OS operation — no separate delete needed.
                var tmp = AppPaths.DataFile + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, AppPaths.DataFile, overwrite: true);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save tasks", ex);
            }
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(AppPaths.SettingsFile))
                    return new AppSettings();

                var json = File.ReadAllText(AppPaths.SettingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load settings", ex);
                return new AppSettings();
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                AppPaths.EnsureFolder();
                var json = JsonSerializer.Serialize(settings, Options);
                File.WriteAllText(AppPaths.SettingsFile, json);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save settings", ex);
            }
        }
    }
}
