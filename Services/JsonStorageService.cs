using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FloatingTodoWidget.Models;

namespace FloatingTodoWidget.Services
{
    public sealed class JsonStorageService : IStorageService
    {
        private static readonly JsonSerializerOptions Opts = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public AppData LoadData()
        {
            try
            {
                if (!File.Exists(AppPaths.DataFile)) return new AppData();
                return JsonSerializer.Deserialize<AppData>(File.ReadAllText(AppPaths.DataFile), Opts)
                       ?? new AppData();
            }
            catch (Exception ex) { Logger.Error("LoadData failed", ex); return new AppData(); }
        }

        public void SaveData(AppData data)
        {
            try
            {
                AppPaths.EnsureFolder();
                var tmp = AppPaths.DataFile + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(data, Opts));
                File.Move(tmp, AppPaths.DataFile, overwrite: true);
            }
            catch (Exception ex) { Logger.Error("SaveData failed", ex); }
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(AppPaths.SettingsFile)) return new AppSettings();
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), Opts)
                       ?? new AppSettings();
            }
            catch (Exception ex) { Logger.Error("LoadSettings failed", ex); return new AppSettings(); }
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                AppPaths.EnsureFolder();
                File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(settings, Opts));
            }
            catch (Exception ex) { Logger.Error("SaveSettings failed", ex); }
        }
    }
}
