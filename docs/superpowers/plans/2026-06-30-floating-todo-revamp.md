# Floating Todo Widget — Full Revamp Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete rewrite of FloatingTodoWidget with projects, tags, sub-tasks, notes, links, overdue notifications, window collapse/tray modes, and duplicate-proof task creation.

**Architecture:** MVVM with CommunityToolkit.Mvvm, single `data.json` (projects+tags+tasks), `settings.json`. Window modes: Full/Collapse/Tray. Balloon-tip notifications via `System.Windows.Forms.NotifyIcon`. System tray via same. All existing files replaced.

**Tech Stack:** WPF · .NET 8 · CommunityToolkit.Mvvm 8.3.2 · System.Windows.Forms (built-in)

## Global Constraints

- Target: `net8.0-windows`, `UseWPF=true`, `UseWindowsForms=true`
- Nullable: enable, LangVersion: latest, ImplicitUsings: disable
- All files under `C:\Users\Ashen\Downloads\FloatingTodoWidget\FloatingTodoWidget\`
- Atomic saves: write `.tmp` then `File.Move(..., overwrite:true)`
- All XAML brushes use `DynamicResource` (never `StaticResource` for theme keys)
- Inbox = `ProjectId == null`. `ActiveProjectId = null` means All Tasks, `Guid.Empty` means Inbox.
- `AddTask` uses `Interlocked` guard + clears `NewTaskText` before any collection work

---

## Task 1: Update csproj + Data Models

**Files:**
- Modify: `FloatingTodoWidget.csproj`
- Rewrite: `Models/Priority.cs`, `Models/TodoItem.cs`, `Models/AppSettings.cs`
- Create: `Models/Project.cs`, `Models/Tag.cs`, `Models/SubTask.cs`, `Models/AppData.cs`, `Models/ProjectTabItem.cs`

- [ ] **Step 1: Rewrite FloatingTodoWidget.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>disable</ImplicitUsings>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AssemblyName>FloatingTodoWidget</AssemblyName>
    <Product>Floating To-Do Widget</Product>
    <Version>2.0.0</Version>
    <SatelliteResourceLanguages>en</SatelliteResourceLanguages>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.3.2" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Write Models/Priority.cs**

```csharp
namespace FloatingTodoWidget.Models
{
    public enum Priority { None = 0, Low = 1, Medium = 2, High = 3 }
}
```

- [ ] **Step 3: Write Models/Project.cs**

```csharp
using System;

namespace FloatingTodoWidget.Models
{
    public class Project
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#5B7FFF";
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
```

- [ ] **Step 4: Write Models/Tag.cs**

```csharp
using System;

namespace FloatingTodoWidget.Models
{
    public class Tag
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#9C27B0";
    }
}
```

- [ ] **Step 5: Write Models/SubTask.cs**

```csharp
using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FloatingTodoWidget.Models
{
    public partial class SubTask : ObservableObject
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        [ObservableProperty] private string _title = string.Empty;
        [ObservableProperty] private bool _isCompleted;
        public int SortOrder { get; set; }
    }
}
```

- [ ] **Step 6: Rewrite Models/TodoItem.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FloatingTodoWidget.Models
{
    public partial class TodoItem : ObservableObject
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [ObservableProperty] private string _title = string.Empty;
        [ObservableProperty] private bool _isCompleted;
        [ObservableProperty] private Priority _priority = Priority.None;
        [ObservableProperty] private DateTime? _dueDate;

        [JsonIgnore]
        [ObservableProperty] private bool _isExpanded;

        public int? NotifyMinutesBefore { get; set; }
        public Guid? ProjectId { get; set; }
        public List<Guid> TagIds { get; set; } = new();
        public List<SubTask> SubTasks { get; set; } = new();
        public string Notes { get; set; } = string.Empty;
        public List<string> Links { get; set; } = new();
        public bool OverdueNotified { get; set; }
        public bool DueSoonNotified { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonIgnore]
        public bool IsOverdue => DueDate.HasValue && DueDate.Value.Date < DateTime.Today && !IsCompleted;

        [JsonIgnore]
        public bool IsDueToday => DueDate.HasValue && DueDate.Value.Date == DateTime.Today && !IsCompleted;
    }
}
```

- [ ] **Step 7: Rewrite Models/AppSettings.cs**

```csharp
using System;

namespace FloatingTodoWidget.Models
{
    public class AppSettings
    {
        public double WindowLeft { get; set; } = 120;
        public double WindowTop { get; set; } = 120;
        public double WindowWidth { get; set; } = 340;
        public double WindowHeight { get; set; } = 540;
        public bool IsDarkTheme { get; set; } = true;
        public bool Topmost { get; set; } = true;
        public bool ClickThrough { get; set; } = false;
        public bool ShowCompleted { get; set; } = true;
        public string WindowMode { get; set; } = "Full";   // Full | Collapse | Tray
        public int CollapseDelayMs { get; set; } = 1500;
        public bool AutoStart { get; set; } = false;
        public bool NotificationsEnabled { get; set; } = true;
        public int DefaultNotifyMinutes { get; set; } = 30;
        public string SortMode { get; set; } = "Priority"; // Priority | DueDate | Created | Alpha
        public Guid? ActiveProjectId { get; set; }
    }
}
```

- [ ] **Step 8: Write Models/AppData.cs**

```csharp
using System.Collections.Generic;

namespace FloatingTodoWidget.Models
{
    public class AppData
    {
        public List<Project> Projects { get; set; } = new();
        public List<Tag> Tags { get; set; } = new();
        public List<TodoItem> Tasks { get; set; } = new();
    }
}
```

- [ ] **Step 9: Write Models/ProjectTabItem.cs**

```csharp
using System;

namespace FloatingTodoWidget.Models
{
    // Guid? Id: null=All Tasks, Guid.Empty=Inbox, else project Id
    public record ProjectTabItem(string Name, Guid? Id, string? Color);
}
```

- [ ] **Step 10: Build to verify**

```
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "feat: add data models for projects, tags, subtasks, notes, links"
```

---

## Task 2: Core Services

**Files:**
- Rewrite: `Services/AppPaths.cs`, `Services/Logger.cs`, `Services/ThemeService.cs`, `Services/StartupService.cs`

- [ ] **Step 1: Rewrite Services/AppPaths.cs**

```csharp
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
```

- [ ] **Step 2: Rewrite Services/Logger.cs**

```csharp
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
```

- [ ] **Step 3: Rewrite Services/ThemeService.cs**

```csharp
using System.Windows;

namespace FloatingTodoWidget.Services
{
    public static class ThemeService
    {
        public static void Apply(bool dark)
        {
            var src = dark
                ? "Resources/Theme.Dark.xaml"
                : "Resources/Theme.Light.xaml";

            var dict = new ResourceDictionary
            {
                Source = new System.Uri(src, System.UriKind.Relative)
            };

            var merged = Application.Current.Resources.MergedDictionaries;
            if (merged.Count > 0)
                merged[0] = dict;
            else
                merged.Add(dict);
        }
    }
}
```

- [ ] **Step 4: Rewrite Services/StartupService.cs**

```csharp
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
```

- [ ] **Step 5: Build**

```
dotnet build
```
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: rewrite core services (AppPaths, Logger, ThemeService, StartupService)"
```

---

## Task 3: Storage Service

**Files:**
- Rewrite: `Services/IStorageService.cs`, `Services/JsonStorageService.cs`

- [ ] **Step 1: Rewrite Services/IStorageService.cs**

```csharp
using FloatingTodoWidget.Models;

namespace FloatingTodoWidget.Services
{
    public interface IStorageService
    {
        AppData LoadData();
        void SaveData(AppData data);
        AppSettings LoadSettings();
        void SaveSettings(AppSettings settings);
    }
}
```

- [ ] **Step 2: Rewrite Services/JsonStorageService.cs**

```csharp
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
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build
git add -A
git commit -m "feat: rewrite storage service with unified data.json"
```

---

## Task 4: Extended QuickAddParser

**Files:**
- Rewrite: `Helpers/QuickAddParser.cs`

Syntax: `Buy milk !high @2026-07-05 @notify:30m #work ~bug,frontend "check docs" https://example.com`

- [ ] **Step 1: Rewrite Helpers/QuickAddParser.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using FloatingTodoWidget.Models;

namespace FloatingTodoWidget.Helpers
{
    public record ParseResult(
        string Title,
        Priority Priority,
        DateTime? DueDate,
        int? NotifyMinutesBefore,
        string? ProjectName,
        string[] TagNames,
        string Note,
        string[] Links
    );

    public static class QuickAddParser
    {
        // !high !med !low (and abbreviations)
        private static readonly Regex PriorityRx =
            new(@"(?<!\S)!(high|h|med|m|medium|low|l)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // @2026-07-05 or @today or @tomorrow
        private static readonly Regex DateRx =
            new(@"(?<!\S)@(today|tomorrow|\d{4}-\d{2}-\d{2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // @notify:30m or @notify:2h
        private static readonly Regex NotifyRx =
            new(@"(?<!\S)@notify:(\d+)(m|h)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // #projectname
        private static readonly Regex ProjectRx =
            new(@"(?<!\S)#([\w\-]+)", RegexOptions.Compiled);

        // ~tag or ~tag1,tag2,tag3
        private static readonly Regex TagRx =
            new(@"(?<!\S)~([\w\-,]+)", RegexOptions.Compiled);

        // "quoted note"
        private static readonly Regex NoteRx =
            new(@"""([^""]*)""", RegexOptions.Compiled);

        // bare URL
        private static readonly Regex UrlRx =
            new(@"https?://\S+", RegexOptions.Compiled);

        public static ParseResult Parse(string raw)
        {
            var text = raw ?? string.Empty;
            var priority = Priority.None;
            DateTime? dueDate = null;
            int? notifyMinutes = null;
            string? projectName = null;
            var tagNames = new List<string>();
            var note = string.Empty;
            var links = new List<string>();

            // Extract URLs first (greedy, before other tokens eat text)
            foreach (Match m in UrlRx.Matches(text))
                links.Add(m.Value.TrimEnd('.', ',', ')'));
            text = UrlRx.Replace(text, " ");

            // Extract quoted note
            var nm = NoteRx.Match(text);
            if (nm.Success) { note = nm.Groups[1].Value.Trim(); text = text.Remove(nm.Index, nm.Length); }

            // Extract @notify before @date (both start with @)
            var notm = NotifyRx.Match(text);
            if (notm.Success)
            {
                int val = int.Parse(notm.Groups[1].Value);
                notifyMinutes = notm.Groups[2].Value.ToLower() == "h" ? val * 60 : val;
                text = text.Remove(notm.Index, notm.Length);
            }

            // Extract date
            var dm = DateRx.Match(text);
            if (dm.Success)
            {
                var ds = dm.Groups[1].Value.ToLower();
                dueDate = ds switch
                {
                    "today"    => DateTime.Today,
                    "tomorrow" => DateTime.Today.AddDays(1),
                    _ => DateTime.TryParseExact(ds, "yyyy-MM-dd",
                             CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateTime?)null
                };
                text = text.Remove(dm.Index, dm.Length);
            }

            // Extract priority
            var pm = PriorityRx.Match(text);
            if (pm.Success)
            {
                priority = pm.Groups[1].Value.ToLower() switch
                {
                    "high" or "h"            => Priority.High,
                    "med"  or "m" or "medium" => Priority.Medium,
                    "low"  or "l"            => Priority.Low,
                    _                        => Priority.None
                };
                text = text.Remove(pm.Index, pm.Length);
            }

            // Extract project
            var prm = ProjectRx.Match(text);
            if (prm.Success) { projectName = prm.Groups[1].Value; text = text.Remove(prm.Index, prm.Length); }

            // Extract tags
            var trm = TagRx.Match(text);
            if (trm.Success)
            {
                tagNames.AddRange(trm.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries));
                text = text.Remove(trm.Index, trm.Length);
            }

            return new ParseResult(
                Title: Regex.Replace(text, @"\s{2,}", " ").Trim(),
                Priority: priority,
                DueDate: dueDate,
                NotifyMinutesBefore: notifyMinutes,
                ProjectName: projectName,
                TagNames: tagNames.ToArray(),
                Note: note,
                Links: links.ToArray()
            );
        }
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: extend QuickAddParser with projects, tags, notes, links, notify"
```

---

## Task 5: Converters + NativeMethods

**Files:**
- Rewrite: `Helpers/Converters.cs`, `Helpers/NativeMethods.cs`

- [ ] **Step 1: Rewrite Helpers/Converters.cs**

```csharp
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FloatingTodoWidget.Models;

namespace FloatingTodoWidget.Helpers
{
    public sealed class PriorityToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is not Priority pr) return Brushes.Transparent;
            var color = pr switch
            {
                Priority.High   => Color.FromRgb(0xE0, 0x52, 0x60),
                Priority.Medium => Color.FromRgb(0xF0, 0xA0, 0x30),
                Priority.Low    => Color.FromRgb(0x4C, 0xAF, 0x50),
                _               => Colors.Transparent
            };
            return new SolidColorBrush(color);
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    /// <summary>TodoItem -> priority bar brush, overriding with red/amber for overdue/due-today.</summary>
    public sealed class DueAwarePriorityBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is not TodoItem item) return Brushes.Transparent;
            if (item.IsOverdue)   return new SolidColorBrush(Color.FromRgb(0xE0, 0x52, 0x60));
            if (item.IsDueToday)  return new SolidColorBrush(Color.FromRgb(0xF0, 0xA0, 0x30));
            return item.Priority switch
            {
                Priority.High   => new SolidColorBrush(Color.FromRgb(0xE0, 0x52, 0x60)),
                Priority.Medium => new SolidColorBrush(Color.FromRgb(0xF0, 0xA0, 0x30)),
                Priority.Low    => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                _               => Brushes.Transparent
            };
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is null ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            bool b = value is bool bv && bv;
            if (Invert) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is bool b && !b;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) =>
            value is bool b && !b;
    }

    public sealed class EmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class NonEmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class DueDateConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is not DateTime d) return string.Empty;
            if (d.Date < DateTime.Today) return $"Overdue · {d:MMM d}";
            if (d.Date == DateTime.Today) return $"Due today · {d:MMM d}";
            return $"Due {d:MMM d}";
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class DueDateForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is not TodoItem item || !item.DueDate.HasValue) return Brushes.Gray;
            if (item.IsOverdue)  return new SolidColorBrush(Color.FromRgb(0xE0, 0x52, 0x60));
            if (item.IsDueToday) return new SolidColorBrush(Color.FromRgb(0xF0, 0xA0, 0x30));
            return Brushes.Gray;
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is int n && n > 0 ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class HexColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is not string hex) return Brushes.Transparent;
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
            catch { return Brushes.Transparent; }
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }
}
```

- [ ] **Step 2: Rewrite Helpers/NativeMethods.cs**

```csharp
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

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED     = 0x00080000;

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
    }
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build
git add -A
git commit -m "feat: rewrite converters and native methods"
```

---

## Task 6: Resources (Themes + Styles)

**Files:**
- Rewrite: `Resources/Theme.Dark.xaml`, `Resources/Theme.Light.xaml`, `Resources/Styles.xaml`

- [ ] **Step 1: Rewrite Resources/Theme.Dark.xaml**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:sys="clr-namespace:System;assembly=mscorlib">
    <SolidColorBrush x:Key="WindowBackgroundBrush" Color="#CC1A1A1A"/>
    <SolidColorBrush x:Key="SurfaceBrush"          Color="#2A2A2A"/>
    <SolidColorBrush x:Key="SurfaceAltBrush"       Color="#242424"/>
    <SolidColorBrush x:Key="BorderBrush"           Color="#3A3A3A"/>
    <SolidColorBrush x:Key="TextBrush"             Color="#E8E8E8"/>
    <SolidColorBrush x:Key="SubtleTextBrush"       Color="#888"/>
    <SolidColorBrush x:Key="AccentBrush"           Color="#5B7FFF"/>
    <SolidColorBrush x:Key="HoverBrush"            Color="#333"/>
    <SolidColorBrush x:Key="ExpandedBgBrush"       Color="#222"/>
    <SolidColorBrush x:Key="InputBgBrush"          Color="#2A2A2A"/>
    <SolidColorBrush x:Key="TabActiveBrush"        Color="#2A2A2A"/>
    <SolidColorBrush x:Key="TabInactiveBrush"      Color="Transparent"/>
    <SolidColorBrush x:Key="SeparatorBrush"        Color="#333"/>
    <SolidColorBrush x:Key="OverdueBrush"          Color="#E05260"/>
    <SolidColorBrush x:Key="DueTodayBrush"         Color="#F0A030"/>
    <sys:UInt32 x:Key="AcrylicTint">0x99111111</sys:UInt32>
</ResourceDictionary>
```

- [ ] **Step 2: Rewrite Resources/Theme.Light.xaml**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:sys="clr-namespace:System;assembly=mscorlib">
    <SolidColorBrush x:Key="WindowBackgroundBrush" Color="#E8EFEFEF"/>
    <SolidColorBrush x:Key="SurfaceBrush"          Color="#F5F5F5"/>
    <SolidColorBrush x:Key="SurfaceAltBrush"       Color="#ECECEC"/>
    <SolidColorBrush x:Key="BorderBrush"           Color="#D0D0D0"/>
    <SolidColorBrush x:Key="TextBrush"             Color="#1A1A1A"/>
    <SolidColorBrush x:Key="SubtleTextBrush"       Color="#777"/>
    <SolidColorBrush x:Key="AccentBrush"           Color="#5B7FFF"/>
    <SolidColorBrush x:Key="HoverBrush"            Color="#E5E5E5"/>
    <SolidColorBrush x:Key="ExpandedBgBrush"       Color="#EDEDED"/>
    <SolidColorBrush x:Key="InputBgBrush"          Color="#F0F0F0"/>
    <SolidColorBrush x:Key="TabActiveBrush"        Color="#FFFFFF"/>
    <SolidColorBrush x:Key="TabInactiveBrush"      Color="Transparent"/>
    <SolidColorBrush x:Key="SeparatorBrush"        Color="#DDD"/>
    <SolidColorBrush x:Key="OverdueBrush"          Color="#D0323F"/>
    <SolidColorBrush x:Key="DueTodayBrush"         Color="#D08020"/>
    <sys:UInt32 x:Key="AcrylicTint">0x88F0F0F0</sys:UInt32>
</ResourceDictionary>
```

- [ ] **Step 3: Rewrite Resources/Styles.xaml**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- ── Icon Button ── -->
    <Style x:Key="IconButton" TargetType="Button">
        <Setter Property="Background"       Value="Transparent"/>
        <Setter Property="BorderThickness"  Value="0"/>
        <Setter Property="Cursor"           Value="Hand"/>
        <Setter Property="Width"            Value="28"/>
        <Setter Property="Height"          Value="28"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="bg" CornerRadius="6" Background="{TemplateBinding Background}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="bg" Property="Background" Value="{DynamicResource HoverBrush}"/>
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter TargetName="bg" Property="Opacity" Value="0.7"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ── Accent (Add) Button ── -->
    <Style x:Key="AccentButton" TargetType="Button" BasedOn="{StaticResource IconButton}">
        <Setter Property="Background" Value="{DynamicResource AccentBrush}"/>
        <Setter Property="Width"      Value="34"/>
        <Setter Property="Height"     Value="34"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="bg" CornerRadius="8" Background="{TemplateBinding Background}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="bg" Property="Opacity" Value="0.85"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ── Text Button (footer links) ── -->
    <Style x:Key="TextButton" TargetType="Button">
        <Setter Property="Background"      Value="Transparent"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="Cursor"          Value="Hand"/>
        <Setter Property="Padding"         Value="4,2"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}">
                        <ContentPresenter/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter Property="Opacity" Value="0.75"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ── Project Tab ── -->
    <Style x:Key="ProjectTab" TargetType="ListBoxItem">
        <Setter Property="Background"      Value="Transparent"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="Cursor"          Value="Hand"/>
        <Setter Property="Padding"         Value="10,4"/>
        <Setter Property="Margin"          Value="0,0,2,0"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ListBoxItem">
                    <Border x:Name="bg" CornerRadius="6" Background="{TemplateBinding Background}"
                            Padding="{TemplateBinding Padding}">
                        <ContentPresenter/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsSelected" Value="True">
                            <Setter TargetName="bg" Property="Background" Value="{DynamicResource TabActiveBrush}"/>
                        </Trigger>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="bg" Property="Background" Value="{DynamicResource HoverBrush}"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ── Project ListBox (tabs row) ── -->
    <Style x:Key="TabListBox" TargetType="ListBox">
        <Setter Property="Background"         Value="Transparent"/>
        <Setter Property="BorderThickness"    Value="0"/>
        <Setter Property="ScrollViewer.HorizontalScrollBarVisibility" Value="Auto"/>
        <Setter Property="ScrollViewer.VerticalScrollBarVisibility"   Value="Disabled"/>
        <Setter Property="ItemContainerStyle" Value="{StaticResource ProjectTab}"/>
        <Setter Property="ItemsPanel">
            <Setter.Value>
                <ItemsPanelTemplate>
                    <StackPanel Orientation="Horizontal"/>
                </ItemsPanelTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ── Input TextBox ── -->
    <Style x:Key="InputBox" TargetType="TextBox">
        <Setter Property="Foreground"              Value="{DynamicResource TextBrush}"/>
        <Setter Property="CaretBrush"              Value="{DynamicResource TextBrush}"/>
        <Setter Property="Background"              Value="{DynamicResource InputBgBrush}"/>
        <Setter Property="BorderThickness"         Value="0"/>
        <Setter Property="Padding"                 Value="10,8"/>
        <Setter Property="FontSize"                Value="13"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="TextBox">
                    <Border CornerRadius="8" Background="{TemplateBinding Background}">
                        <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}"/>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ── Notes TextBox (multiline, transparent) ── -->
    <Style x:Key="NotesBox" TargetType="TextBox">
        <Setter Property="Foreground"      Value="{DynamicResource TextBrush}"/>
        <Setter Property="CaretBrush"      Value="{DynamicResource TextBrush}"/>
        <Setter Property="Background"      Value="Transparent"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="Padding"         Value="0"/>
        <Setter Property="FontSize"        Value="12"/>
        <Setter Property="AcceptsReturn"   Value="True"/>
        <Setter Property="TextWrapping"    Value="Wrap"/>
        <Setter Property="MinHeight"       Value="40"/>
    </Style>

    <!-- ── Flat CheckBox (task) ── -->
    <Style x:Key="FlatCheckBox" TargetType="CheckBox">
        <Setter Property="Cursor"           Value="Hand"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="CheckBox">
                    <Border x:Name="box" Width="18" Height="18" CornerRadius="5"
                            BorderThickness="1.5"
                            BorderBrush="{DynamicResource SubtleTextBrush}"
                            Background="Transparent">
                        <Path x:Name="check" Stretch="Uniform" Margin="3"
                              Data="M0,5 L4,9 L11,0"
                              Stroke="White" StrokeThickness="2"
                              Visibility="Collapsed"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsChecked" Value="True">
                            <Setter TargetName="box"   Property="Background"   Value="{DynamicResource AccentBrush}"/>
                            <Setter TargetName="box"   Property="BorderBrush"  Value="{DynamicResource AccentBrush}"/>
                            <Setter TargetName="check" Property="Visibility"   Value="Visible"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ── Tag Chip ── -->
    <Style x:Key="TagChip" TargetType="Border">
        <Setter Property="CornerRadius"    Value="10"/>
        <Setter Property="Padding"         Value="8,3"/>
        <Setter Property="Margin"          Value="0,0,4,0"/>
        <Setter Property="Background"      Value="{DynamicResource SurfaceBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="BorderBrush"     Value="{DynamicResource BorderBrush}"/>
    </Style>

    <!-- ── Slim ScrollBar ── -->
    <Style TargetType="ScrollBar">
        <Setter Property="Width"      Value="6"/>
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ScrollBar">
                    <Track x:Name="PART_Track" IsDirectionReversed="True">
                        <Track.Thumb>
                            <Thumb>
                                <Thumb.Template>
                                    <ControlTemplate TargetType="Thumb">
                                        <Border CornerRadius="3" Background="{DynamicResource SubtleTextBrush}" Opacity="0.45"/>
                                    </ControlTemplate>
                                </Thumb.Template>
                            </Thumb>
                        </Track.Thumb>
                    </Track>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ── Context Menu / MenuItem ── -->
    <Style TargetType="MenuItem">
        <Setter Property="Foreground" Value="{DynamicResource TextBrush}"/>
        <Setter Property="Padding"    Value="6,3"/>
    </Style>

</ResourceDictionary>
```

- [ ] **Step 4: Build + commit**

```bash
dotnet build
git add -A
git commit -m "feat: rewrite themes and styles for new UI"
```

---

## Task 7: MainViewModel

**Files:**
- Rewrite: `ViewModels/MainViewModel.cs`

- [ ] **Step 1: Write ViewModels/MainViewModel.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingTodoWidget.Helpers;
using FloatingTodoWidget.Models;
using FloatingTodoWidget.Services;

namespace FloatingTodoWidget.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IStorageService _storage;
        private AppData _data;
        private bool _suspendSave;
        private int _addingDepth = 0;

        public AppSettings Settings { get; }

        // ── Collections ──
        public ObservableCollection<TodoItem>      Tasks     { get; } = new();
        public ObservableCollection<Project>       Projects  { get; } = new();
        public ObservableCollection<Tag>           AllTags   { get; } = new();
        public ObservableCollection<Tag>           ActiveTagFilters { get; } = new();
        public ObservableCollection<ProjectTabItem> ProjectTabs { get; } = new();
        public ICollectionView TasksView { get; }

        // ── Input ──
        [ObservableProperty] private string _newTaskText  = string.Empty;
        [ObservableProperty] private string _parsePreview = string.Empty;

        // ── Filtering / State ──
        [ObservableProperty] private ProjectTabItem? _activeProjectTab;
        [ObservableProperty] private bool  _isDarkTheme;
        [ObservableProperty] private bool  _isTopmost;
        [ObservableProperty] private bool  _showCompleted;
        [ObservableProperty] private bool  _autoStart;
        [ObservableProperty] private bool  _notificationsEnabled;
        [ObservableProperty] private string _sortMode = "Priority";
        [ObservableProperty] private string _windowMode = "Full";
        [ObservableProperty] private bool  _isAddingProject;
        [ObservableProperty] private string _newProjectName = string.Empty;

        // ── Computed ──
        public int  PendingCount    => Tasks.Count(t => !t.IsCompleted);
        public bool HasCompleted    => Tasks.Any(t => t.IsCompleted);
        public bool HasTagFilters   => ActiveTagFilters.Count > 0;

        // ── Events ──
        public event EventHandler? ExitRequested;
        public event EventHandler? FocusInputRequested;
        public event EventHandler? ShowWindowRequested;
        public event EventHandler? DataChanged; // for NotificationService to re-check

        // ── Computed helpers ──
        private Guid? ActiveProjectId => ActiveProjectTab?.Id;

        public MainViewModel(IStorageService storage, AppSettings settings)
        {
            _storage = storage;
            Settings = settings;

            _isDarkTheme        = settings.IsDarkTheme;
            _isTopmost          = settings.Topmost;
            _showCompleted      = settings.ShowCompleted;
            _autoStart          = StartupService.IsEnabled();
            _notificationsEnabled = settings.NotificationsEnabled;
            _sortMode           = settings.SortMode;
            _windowMode         = settings.WindowMode;

            // Build view before loading so filter works immediately
            TasksView = CollectionViewSource.GetDefaultView(Tasks);
            TasksView.Filter = FilterTask;
            if (TasksView is ListCollectionView lcv)
                lcv.CustomSort = System.Collections.Generic.Comparer<TodoItem>.Create(CompareTasks);

            // Load data
            _suspendSave = true;
            _data = storage.LoadData();
            foreach (var p in _data.Projects.OrderBy(p => p.SortOrder)) Projects.Add(p);
            foreach (var t in _data.Tags)    AllTags.Add(t);
            foreach (var t in _data.Tasks)   { SubscribeItem(t); Tasks.Add(t); }
            _suspendSave = false;

            Tasks.CollectionChanged    += OnTasksChanged;
            ActiveTagFilters.CollectionChanged += (_, _) => RefreshView();

            RebuildProjectTabs(settings.ActiveProjectId);
        }

        // ─────────────────── Filtering / Sorting ───────────────────

        private bool FilterTask(object o)
        {
            if (o is not TodoItem t) return false;
            if (!ShowCompleted && t.IsCompleted) return false;
            var pid = ActiveProjectId;
            if (pid.HasValue)
            {
                if (pid.Value == Guid.Empty && t.ProjectId.HasValue)  return false;  // Inbox
                if (pid.Value != Guid.Empty && t.ProjectId != pid)     return false;  // specific project
            }
            if (ActiveTagFilters.Count > 0 && !ActiveTagFilters.Any(tag => t.TagIds.Contains(tag.Id)))
                return false;
            return true;
        }

        private static int CompareTasks(TodoItem a, TodoItem b)
        {
            int c = a.IsCompleted.CompareTo(b.IsCompleted);
            if (c != 0) return c;
            c = b.Priority.CompareTo(a.Priority);
            if (c != 0) return c;
            if (a.DueDate.HasValue && b.DueDate.HasValue)
                return a.DueDate.Value.CompareTo(b.DueDate.Value);
            if (a.DueDate.HasValue) return -1;
            if (b.DueDate.HasValue) return  1;
            return b.CreatedAt.CompareTo(a.CreatedAt);
        }

        private void RefreshView()
        {
            TasksView.Refresh();
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(HasCompleted));
            OnPropertyChanged(nameof(HasTagFilters));
        }

        // ─────────────────── Project Tabs ───────────────────

        private void RebuildProjectTabs(Guid? selectId = null)
        {
            ProjectTabs.Clear();
            ProjectTabs.Add(new ProjectTabItem("All",   null,       null));
            ProjectTabs.Add(new ProjectTabItem("Inbox", Guid.Empty, null));
            foreach (var p in Projects.OrderBy(x => x.SortOrder))
                ProjectTabs.Add(new ProjectTabItem(p.Name, p.Id, p.Color));

            ActiveProjectTab = selectId.HasValue
                ? ProjectTabs.FirstOrDefault(t => t.Id == selectId) ?? ProjectTabs[0]
                : ProjectTabs[0];
        }

        partial void OnActiveProjectTabChanged(ProjectTabItem? value)
        {
            Settings.ActiveProjectId = value?.Id;
            RefreshView();
        }

        // ─────────────────── AddTask (duplicate-proof) ───────────────────

        [RelayCommand]
        private void AddTask()
        {
            if (Interlocked.CompareExchange(ref _addingDepth, 1, 0) != 0) return;

            var raw = NewTaskText?.Trim() ?? string.Empty;
            NewTaskText  = string.Empty;   // clear BEFORE any collection work
            ParsePreview = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(raw)) return;
                var r = QuickAddParser.Parse(raw);
                if (string.IsNullOrWhiteSpace(r.Title)) return;

                // Resolve / create project
                Guid? projectId = null;
                if (r.ProjectName != null)
                {
                    var proj = Projects.FirstOrDefault(p => p.Name.Equals(r.ProjectName, StringComparison.OrdinalIgnoreCase));
                    if (proj == null)
                    {
                        proj = new Project { Name = r.ProjectName, SortOrder = Projects.Count };
                        Projects.Add(proj);
                        _data.Projects.Add(proj);
                        RebuildProjectTabs(ActiveProjectId);
                    }
                    projectId = proj.Id;
                }
                else if (ActiveProjectId.HasValue && ActiveProjectId.Value != Guid.Empty)
                    projectId = ActiveProjectId;

                // Resolve / create tags
                var tagIds = new List<Guid>();
                foreach (var tn in r.TagNames)
                {
                    var tag = AllTags.FirstOrDefault(t => t.Name.Equals(tn, StringComparison.OrdinalIgnoreCase));
                    if (tag == null)
                    {
                        tag = new Tag { Name = tn };
                        AllTags.Add(tag);
                        _data.Tags.Add(tag);
                    }
                    tagIds.Add(tag.Id);
                }

                var item = new TodoItem
                {
                    Title               = r.Title,
                    Priority            = r.Priority,
                    DueDate             = r.DueDate,
                    NotifyMinutesBefore = r.NotifyMinutesBefore,
                    ProjectId           = projectId,
                    TagIds              = tagIds,
                    Notes               = r.Note,
                    Links               = r.Links.ToList()
                };

                _data.Tasks.Add(item);
                SubscribeItem(item);
                Tasks.Add(item);
                SaveData();
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                Interlocked.Exchange(ref _addingDepth, 0);
            }
        }

        // ─────────────────── Task Commands ───────────────────

        [RelayCommand]
        private void DeleteTask(TodoItem? item)
        {
            if (item is null) return;
            _data.Tasks.Remove(item);
            Tasks.Remove(item);
            SaveData();
        }

        [RelayCommand]
        private void ToggleExpand(TodoItem? item)
        {
            if (item is null) return;
            item.IsExpanded = !item.IsExpanded;
        }

        [RelayCommand]
        private void CyclePriority(TodoItem? item)
        {
            if (item is null) return;
            item.Priority = item.Priority switch
            {
                Priority.None   => Priority.Low,
                Priority.Low    => Priority.Medium,
                Priority.Medium => Priority.High,
                _               => Priority.None
            };
            TasksView.Refresh();
        }

        [RelayCommand]
        private void ClearCompleted()
        {
            foreach (var done in Tasks.Where(t => t.IsCompleted).ToList())
            {
                _data.Tasks.Remove(done);
                Tasks.Remove(done);
            }
            SaveData();
        }

        [RelayCommand]
        private void FocusInput() => FocusInputRequested?.Invoke(this, EventArgs.Empty);

        [RelayCommand]
        private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);

        [RelayCommand]
        private void ShowWindow() => ShowWindowRequested?.Invoke(this, EventArgs.Empty);

        // ─────────────────── Sub-task Commands ───────────────────

        [RelayCommand]
        private void AddSubTask(TodoItem? item)
        {
            if (item is null) return;
            item.SubTasks.Add(new SubTask { SortOrder = item.SubTasks.Count });
            SaveData();
        }

        [RelayCommand]
        private void RemoveSubTask(SubTask? sub)
        {
            if (sub is null) return;
            var parent = Tasks.FirstOrDefault(t => t.SubTasks.Contains(sub));
            if (parent is null) return;
            parent.SubTasks.Remove(sub);
            SaveData();
        }

        // ─────────────────── Link Commands ───────────────────

        [RelayCommand]
        private void OpenLink(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { /* ignore */ }
        }

        [RelayCommand]
        private void RemoveLink(string? url)
        {
            if (url is null) return;
            var parent = Tasks.FirstOrDefault(t => t.Links.Contains(url));
            parent?.Links.Remove(url);
            SaveData();
        }

        // ─────────────────── Project Commands ───────────────────

        [RelayCommand]
        private void StartAddProject()
        {
            NewProjectName = string.Empty;
            IsAddingProject = true;
        }

        [RelayCommand]
        private void ConfirmAddProject()
        {
            var name = NewProjectName.Trim();
            IsAddingProject = false;
            NewProjectName  = string.Empty;
            if (string.IsNullOrWhiteSpace(name)) return;
            if (Projects.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return;
            var proj = new Project { Name = name, SortOrder = Projects.Count };
            Projects.Add(proj);
            _data.Projects.Add(proj);
            RebuildProjectTabs(proj.Id);
            SaveData();
        }

        [RelayCommand]
        private void CancelAddProject()
        {
            IsAddingProject = false;
            NewProjectName  = string.Empty;
        }

        // ─────────────────── Tag Filter Commands ───────────────────

        [RelayCommand]
        private void ToggleTagFilter(Tag? tag)
        {
            if (tag is null) return;
            if (ActiveTagFilters.Contains(tag))
                ActiveTagFilters.Remove(tag);
            else
                ActiveTagFilters.Add(tag);
        }

        [RelayCommand]
        private void ClearTagFilters() => ActiveTagFilters.Clear();

        // ─────────────────── Sort ───────────────────

        [RelayCommand]
        private void SetSort(string? mode)
        {
            if (string.IsNullOrEmpty(mode)) return;
            SortMode = mode;
            Settings.SortMode = mode;
            if (TasksView is ListCollectionView lcv)
                lcv.CustomSort = System.Collections.Generic.Comparer<TodoItem>.Create(CompareTasks);
            TasksView.Refresh();
            SaveSettings();
        }

        // ─────────────────── Settings ───────────────────

        partial void OnIsDarkThemeChanged(bool value)
        {
            ThemeService.Apply(value);
            Settings.IsDarkTheme = value;
            SaveSettings();
        }

        partial void OnIsTopmostChanged(bool value)
        {
            Settings.Topmost = value;
            SaveSettings();
        }

        partial void OnShowCompletedChanged(bool value)
        {
            Settings.ShowCompleted = value;
            RefreshView();
            SaveSettings();
        }

        partial void OnAutoStartChanged(bool value) => StartupService.SetEnabled(value);

        partial void OnNotificationsEnabledChanged(bool value)
        {
            Settings.NotificationsEnabled = value;
            SaveSettings();
        }

        partial void OnWindowModeChanged(string value)
        {
            Settings.WindowMode = value;
            SaveSettings();
        }

        // ─────────────────── Parse Preview ───────────────────

        partial void OnNewTaskTextChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) { ParsePreview = string.Empty; return; }
            var r = QuickAddParser.Parse(value);
            var parts = new List<string>();
            if (r.Priority != Priority.None) parts.Add($"!{r.Priority}");
            if (r.DueDate.HasValue)          parts.Add($"Due {r.DueDate:MMM d}");
            if (r.ProjectName != null)       parts.Add($"#{r.ProjectName}");
            if (r.TagNames.Length > 0)       parts.Add("~" + string.Join(",", r.TagNames));
            if (!string.IsNullOrEmpty(r.Note)) parts.Add($"\"{(r.Note.Length > 20 ? r.Note[..20] + "…" : r.Note)}\"");
            if (r.Links.Length > 0)          parts.Add($"🔗 {r.Links.Length}");
            ParsePreview = parts.Count > 0 ? "▸ " + string.Join(" · ", parts) : string.Empty;
        }

        // ─────────────────── Persistence ───────────────────

        private void SubscribeItem(TodoItem item)
        {
            item.PropertyChanged += OnItemChanged;
        }

        private void OnTasksChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (TodoItem i in e.OldItems) i.PropertyChanged -= OnItemChanged;
            RefreshView();
        }

        private void OnItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(TodoItem.IsCompleted) or nameof(TodoItem.Priority))
                RefreshView();
            if (!_suspendSave && e.PropertyName != nameof(TodoItem.IsExpanded))
                SaveData();
        }

        private void SaveData()
        {
            if (_suspendSave) return;
            // Sync back observable collections to _data
            _data.Projects = Projects.ToList();
            _data.Tags     = AllTags.ToList();
            _data.Tasks    = Tasks.ToList();
            _storage.SaveData(_data);
        }

        public void SaveSettings() => _storage.SaveSettings(Settings);

        public void PersistWindowBounds(double left, double top, double width, double height)
        {
            Settings.WindowLeft   = left;
            Settings.WindowTop    = top;
            Settings.WindowWidth  = width;
            Settings.WindowHeight = height;
            SaveSettings();
        }

        // ── Expose for NotificationService ──
        public IEnumerable<TodoItem> GetAllTasks() => _data.Tasks;
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: rewrite MainViewModel with projects, tags, duplicate-proof AddTask"
```

---

## Task 8: MainWindow XAML + Code-Behind

**Files:**
- Rewrite: `MainWindow.xaml`, `MainWindow.xaml.cs`

- [ ] **Step 1: Rewrite MainWindow.xaml**

```xml
<Window x:Class="FloatingTodoWidget.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:h="clr-namespace:FloatingTodoWidget.Helpers"
        xmlns:m="clr-namespace:FloatingTodoWidget.Models"
        xmlns:vm="clr-namespace:FloatingTodoWidget.ViewModels"
        Title="Tasks"
        Width="340" Height="540"
        MinWidth="260" MinHeight="240"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        ResizeMode="CanResizeWithGrip"
        ShowInTaskbar="True"
        Topmost="{Binding IsTopmost}">

    <Window.Resources>
        <h:PriorityToBrushConverter         x:Key="PriorityBrush"/>
        <h:DueAwarePriorityBrushConverter   x:Key="DueAwareBrush"/>
        <h:NullToVisibilityConverter        x:Key="NullToVis"/>
        <h:BoolToVisibilityConverter        x:Key="BoolToVis"/>
        <h:BoolToVisibilityConverter        x:Key="InvBoolToVis" Invert="True"/>
        <h:InverseBoolConverter             x:Key="InvBool"/>
        <h:EmptyStringToVisibilityConverter x:Key="EmptyToVis"/>
        <h:NonEmptyStringToVisibilityConverter x:Key="NonEmptyToVis"/>
        <h:DueDateConverter                 x:Key="DueDate"/>
        <h:DueDateForegroundConverter       x:Key="DueFg"/>
        <h:CountToVisibilityConverter       x:Key="CountToVis"/>
        <h:HexColorToBrushConverter         x:Key="HexBrush"/>
        <BooleanToVisibilityConverter       x:Key="SysBoolToVis"/>
    </Window.Resources>

    <Window.InputBindings>
        <KeyBinding Modifiers="Control" Key="N" Command="{Binding FocusInputCommand}"/>
    </Window.InputBindings>

    <!-- Root: rounded, semi-transparent, draggable -->
    <Border x:Name="RootBorder"
            CornerRadius="14"
            Background="{DynamicResource WindowBackgroundBrush}"
            BorderBrush="{DynamicResource BorderBrush}"
            BorderThickness="1"
            MouseLeftButtonDown="RootBorder_MouseLeftButtonDown"
            MouseEnter="Root_MouseEnter"
            MouseLeave="Root_MouseLeave">

        <Border.ContextMenu>
            <ContextMenu>
                <MenuItem Header="New task (Ctrl+N)"   Command="{Binding FocusInputCommand}"/>
                <Separator/>
                <MenuItem Header="Show completed"      IsCheckable="True" IsChecked="{Binding ShowCompleted}"/>
                <MenuItem Header="Dark theme"          IsCheckable="True" IsChecked="{Binding IsDarkTheme}"/>
                <MenuItem Header="Always on top"       IsCheckable="True" IsChecked="{Binding IsTopmost}"/>
                <MenuItem Header="Start with Windows"  IsCheckable="True" IsChecked="{Binding AutoStart}"/>
                <MenuItem Header="Notifications"       IsCheckable="True" IsChecked="{Binding NotificationsEnabled}"/>
                <Separator/>
                <MenuItem Header="Window mode">
                    <MenuItem Header="Full"     Command="{Binding SetWindowModeCommand}" CommandParameter="Full"/>
                    <MenuItem Header="Collapse" Command="{Binding SetWindowModeCommand}" CommandParameter="Collapse"/>
                    <MenuItem Header="Tray"     Command="{Binding SetWindowModeCommand}" CommandParameter="Tray"/>
                </MenuItem>
                <MenuItem Header="Sort by">
                    <MenuItem Header="Priority"  Command="{Binding SetSortCommand}" CommandParameter="Priority"/>
                    <MenuItem Header="Due Date"  Command="{Binding SetSortCommand}" CommandParameter="DueDate"/>
                    <MenuItem Header="Created"   Command="{Binding SetSortCommand}" CommandParameter="Created"/>
                    <MenuItem Header="A–Z"       Command="{Binding SetSortCommand}" CommandParameter="Alpha"/>
                </MenuItem>
                <Separator/>
                <MenuItem Header="Clear completed" Command="{Binding ClearCompletedCommand}"/>
                <Separator/>
                <MenuItem Header="Exit" Command="{Binding ExitCommand}"/>
            </ContextMenu>
        </Border.ContextMenu>

        <Grid>
            <!-- Collapsed bar (shown only in Collapse mode when mouse away) -->
            <Border x:Name="CollapsedBar"
                    Visibility="Collapsed"
                    CornerRadius="14"
                    Background="Transparent"
                    Padding="16,0"
                    Cursor="Hand"
                    MouseLeftButtonDown="CollapsedBar_Click">
                <TextBlock VerticalAlignment="Center" FontSize="12"
                           Foreground="{DynamicResource SubtleTextBrush}">
                    <Run Text="● "/>
                    <Run Text="{Binding PendingCount, Mode=OneWay}"/>
                    <Run Text=" pending — To-Do"/>
                </TextBlock>
            </Border>

            <!-- Main content -->
            <Grid x:Name="MainContent" Margin="14,12,14,8">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>  <!-- Header -->
                    <RowDefinition Height="Auto"/>  <!-- Project Tabs -->
                    <RowDefinition Height="Auto"/>  <!-- Input -->
                    <RowDefinition Height="Auto"/>  <!-- Parse Preview -->
                    <RowDefinition Height="Auto"/>  <!-- Tag Filters -->
                    <RowDefinition Height="*"/>     <!-- Task List -->
                    <RowDefinition Height="Auto"/>  <!-- Footer -->
                </Grid.RowDefinitions>

                <!-- ═══ Header ═══ -->
                <DockPanel Grid.Row="0" LastChildFill="False" Margin="0,0,0,8">
                    <TextBlock DockPanel.Dock="Left" Text="Tasks"
                               FontSize="16" FontWeight="SemiBold"
                               Foreground="{DynamicResource TextBrush}"
                               VerticalAlignment="Center"/>
                    <Border DockPanel.Dock="Left" Margin="8,0,0,0" Padding="7,2"
                            CornerRadius="9" Background="{DynamicResource SurfaceBrush}"
                            VerticalAlignment="Center">
                        <TextBlock Text="{Binding PendingCount}" FontSize="11"
                                   Foreground="{DynamicResource SubtleTextBrush}"/>
                    </Border>
                    <Button DockPanel.Dock="Right" Style="{StaticResource IconButton}"
                            Command="{Binding ExitCommand}" ToolTip="Exit">
                        <Path Data="M0,0 L10,10 M10,0 L0,10"
                              Stroke="{DynamicResource TextBrush}" StrokeThickness="1.6"/>
                    </Button>
                    <Button DockPanel.Dock="Right" Style="{StaticResource IconButton}"
                            Click="ThemeToggle_Click" ToolTip="Toggle theme">
                        <Path Data="M5,0 A5,5 0 1 0 5,10 A3.2,3.2 0 1 1 5,0 Z"
                              Fill="{DynamicResource TextBrush}"/>
                    </Button>
                </DockPanel>

                <!-- ═══ Project Tabs ═══ -->
                <Grid Grid.Row="1" Margin="0,0,0,8">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>

                    <ListBox Grid.Column="0"
                             Style="{StaticResource TabListBox}"
                             ItemsSource="{Binding ProjectTabs}"
                             SelectedItem="{Binding ActiveProjectTab}">
                        <ListBox.ItemTemplate>
                            <DataTemplate DataType="{x:Type m:ProjectTabItem}">
                                <TextBlock Text="{Binding Name}" FontSize="12"
                                           Foreground="{DynamicResource TextBrush}"/>
                            </DataTemplate>
                        </ListBox.ItemTemplate>
                    </ListBox>

                    <!-- Add project button / inline input -->
                    <StackPanel Grid.Column="1" Orientation="Horizontal">
                        <Button Style="{StaticResource IconButton}"
                                Command="{Binding StartAddProjectCommand}"
                                ToolTip="New project"
                                Visibility="{Binding IsAddingProject, Converter={StaticResource InvBoolToVis}}">
                            <Path Data="M6,0 L6,12 M0,6 L12,6"
                                  Stroke="{DynamicResource SubtleTextBrush}" StrokeThickness="1.5"/>
                        </Button>
                        <Border Visibility="{Binding IsAddingProject, Converter={StaticResource BoolToVis}}">
                            <StackPanel Orientation="Horizontal">
                                <TextBox x:Name="NewProjectBox"
                                         Width="90" FontSize="12"
                                         Style="{StaticResource InputBox}"
                                         Text="{Binding NewProjectName, UpdateSourceTrigger=PropertyChanged}"
                                         PreviewKeyDown="NewProjectBox_PreviewKeyDown"/>
                                <Button Style="{StaticResource IconButton}"
                                        Command="{Binding CancelAddProjectCommand}" Width="22" Height="22">
                                    <Path Data="M0,0 L8,8 M8,0 L0,8"
                                          Stroke="{DynamicResource SubtleTextBrush}" StrokeThickness="1.4"/>
                                </Button>
                            </StackPanel>
                        </Border>
                    </StackPanel>
                </Grid>

                <!-- ═══ Quick-Add Input ═══ -->
                <Grid Grid.Row="2" Margin="0,0,0,2">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <Grid Grid.Column="0">
                        <TextBox x:Name="InputBox"
                                 Style="{StaticResource InputBox}"
                                 Text="{Binding NewTaskText, UpdateSourceTrigger=PropertyChanged}"
                                 PreviewKeyDown="InputBox_PreviewKeyDown"/>
                        <TextBlock IsHitTestVisible="False"
                                   Text="Add task… !high #work ~tag @today"
                                   FontSize="12" Margin="10,0,0,0"
                                   VerticalAlignment="Center"
                                   Foreground="{DynamicResource SubtleTextBrush}"
                                   Visibility="{Binding NewTaskText, Converter={StaticResource EmptyToVis}}"/>
                    </Grid>
                    <Button Grid.Column="1" Margin="8,0,0,0"
                            Style="{StaticResource AccentButton}"
                            Command="{Binding AddTaskCommand}" ToolTip="Add (Enter)">
                        <Path Data="M6,0 L6,12 M0,6 L12,6" Stroke="White" StrokeThickness="2"/>
                    </Button>
                </Grid>

                <!-- ═══ Parse Preview ═══ -->
                <TextBlock Grid.Row="3"
                           Text="{Binding ParsePreview}"
                           FontSize="11" Margin="2,2,0,6"
                           Foreground="{DynamicResource AccentBrush}"
                           Visibility="{Binding ParsePreview, Converter={StaticResource NonEmptyToVis}}"/>

                <!-- ═══ Tag Filter Chips ═══ -->
                <ItemsControl Grid.Row="4"
                              ItemsSource="{Binding ActiveTagFilters}"
                              Margin="0,0,0,6"
                              Visibility="{Binding HasTagFilters, Converter={StaticResource BoolToVis}}">
                    <ItemsControl.ItemsPanel>
                        <ItemsPanelTemplate><WrapPanel/></ItemsPanelTemplate>
                    </ItemsControl.ItemsPanel>
                    <ItemsControl.ItemTemplate>
                        <DataTemplate DataType="{x:Type m:Tag}">
                            <Border Style="{StaticResource TagChip}">
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Text="{Binding Name}" FontSize="11"
                                               Foreground="{DynamicResource TextBrush}"
                                               VerticalAlignment="Center"/>
                                    <Button Style="{StaticResource TextButton}" Padding="4,0,0,0"
                                            Command="{Binding DataContext.ToggleTagFilterCommand,
                                                      RelativeSource={RelativeSource AncestorType=Window}}"
                                            CommandParameter="{Binding}">
                                        <Path Data="M0,0 L6,6 M6,0 L0,6"
                                              Stroke="{DynamicResource SubtleTextBrush}" StrokeThickness="1.2"/>
                                    </Button>
                                </StackPanel>
                            </Border>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>

                <!-- ═══ Task List ═══ -->
                <ScrollViewer Grid.Row="5"
                              VerticalScrollBarVisibility="Auto"
                              HorizontalScrollBarVisibility="Disabled">
                    <ItemsControl ItemsSource="{Binding TasksView}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate DataType="{x:Type m:TodoItem}">
                                <Border x:Name="row" Margin="0,1" CornerRadius="8"
                                        Background="Transparent">
                                    <StackPanel>
                                        <!-- Task Row -->
                                        <Grid Margin="4,5">
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="4"/>
                                                <ColumnDefinition Width="Auto"/>
                                                <ColumnDefinition Width="*"/>
                                                <ColumnDefinition Width="Auto"/>
                                                <ColumnDefinition Width="Auto"/>
                                            </Grid.ColumnDefinitions>

                                            <!-- Priority bar -->
                                            <Border Grid.Column="0" Width="4" CornerRadius="2" Margin="0,2,8,2"
                                                    Cursor="Hand"
                                                    Background="{Binding Converter={StaticResource DueAwareBrush}}"
                                                    MouseLeftButtonDown="Priority_Click"
                                                    ToolTip="Click to cycle priority"/>

                                            <!-- Checkbox -->
                                            <CheckBox Grid.Column="1" Margin="0,0,8,0"
                                                      Style="{StaticResource FlatCheckBox}"
                                                      IsChecked="{Binding IsCompleted}"/>

                                            <!-- Title + Due -->
                                            <StackPanel Grid.Column="2" VerticalAlignment="Center"
                                                        Cursor="Hand"
                                                        MouseLeftButtonDown="TaskRow_Click">
                                                <TextBlock x:Name="titleBlock"
                                                           Text="{Binding Title}"
                                                           TextWrapping="Wrap" FontSize="13"
                                                           Foreground="{DynamicResource TextBrush}"/>
                                                <TextBlock Text="{Binding DueDate, Converter={StaticResource DueDate}}"
                                                           Visibility="{Binding DueDate, Converter={StaticResource NullToVis}}"
                                                           Foreground="{Binding Converter={StaticResource DueFg}}"
                                                           FontSize="10" Margin="0,2,0,0"/>
                                            </StackPanel>

                                            <!-- Expand chevron -->
                                            <TextBlock Grid.Column="3"
                                                       Text="{Binding IsExpanded, Converter={StaticResource InvBool}}"
                                                       FontSize="10" Margin="4,0"
                                                       Foreground="{DynamicResource SubtleTextBrush}"
                                                       VerticalAlignment="Center"
                                                       Cursor="Hand"
                                                       MouseLeftButtonDown="TaskRow_Click">
                                                <TextBlock.Style>
                                                    <Style TargetType="TextBlock">
                                                        <Setter Property="Text" Value="▸"/>
                                                        <Style.Triggers>
                                                            <DataTrigger Binding="{Binding IsExpanded}" Value="True">
                                                                <Setter Property="Text" Value="▾"/>
                                                            </DataTrigger>
                                                        </Style.Triggers>
                                                    </Style>
                                                </TextBlock.Style>
                                            </TextBlock>

                                            <!-- Delete -->
                                            <Button Grid.Column="4" Style="{StaticResource IconButton}"
                                                    Width="24" Height="24"
                                                    Command="{Binding DataContext.DeleteTaskCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                                    CommandParameter="{Binding}" ToolTip="Delete">
                                                <Path Data="M1,2 L9,2 M2,2 L2,9 L8,9 L8,2 M4,0 L6,0"
                                                      Stroke="{DynamicResource SubtleTextBrush}"
                                                      StrokeThickness="1.3"/>
                                            </Button>
                                        </Grid>

                                        <!-- Detail Panel (expanded) -->
                                        <Border x:Name="detail"
                                                Visibility="{Binding IsExpanded, Converter={StaticResource BoolToVis}}"
                                                Background="{DynamicResource ExpandedBgBrush}"
                                                CornerRadius="0,0,8,8"
                                                Padding="16,8,8,10">
                                            <StackPanel>
                                                <!-- Notes -->
                                                <TextBlock Text="Notes" FontSize="10"
                                                           Foreground="{DynamicResource SubtleTextBrush}"
                                                           Margin="0,0,0,3"/>
                                                <TextBox Style="{StaticResource NotesBox}"
                                                         Text="{Binding Notes, UpdateSourceTrigger=LostFocus}"
                                                         AcceptsReturn="True"
                                                         Foreground="{DynamicResource TextBrush}"/>

                                                <!-- Sub-tasks -->
                                                <TextBlock Text="Sub-tasks" FontSize="10"
                                                           Foreground="{DynamicResource SubtleTextBrush}"
                                                           Margin="0,8,0,3"
                                                           Visibility="{Binding SubTasks.Count,
                                                               Converter={StaticResource CountToVis}}"/>
                                                <ItemsControl ItemsSource="{Binding SubTasks}">
                                                    <ItemsControl.ItemTemplate>
                                                        <DataTemplate DataType="{x:Type m:SubTask}">
                                                            <Grid Margin="0,2">
                                                                <Grid.ColumnDefinitions>
                                                                    <ColumnDefinition Width="Auto"/>
                                                                    <ColumnDefinition Width="*"/>
                                                                    <ColumnDefinition Width="Auto"/>
                                                                </Grid.ColumnDefinitions>
                                                                <CheckBox Grid.Column="0"
                                                                          Style="{StaticResource FlatCheckBox}"
                                                                          IsChecked="{Binding IsCompleted}"
                                                                          Margin="0,0,6,0"/>
                                                                <TextBox Grid.Column="1"
                                                                         Text="{Binding Title, UpdateSourceTrigger=LostFocus}"
                                                                         Background="Transparent"
                                                                         BorderThickness="0"
                                                                         FontSize="12"
                                                                         Foreground="{DynamicResource TextBrush}"/>
                                                                <Button Grid.Column="2"
                                                                        Style="{StaticResource IconButton}"
                                                                        Width="20" Height="20"
                                                                        Command="{Binding DataContext.RemoveSubTaskCommand,
                                                                            RelativeSource={RelativeSource AncestorType=Window}}"
                                                                        CommandParameter="{Binding}">
                                                                    <Path Data="M0,0 L6,6 M6,0 L0,6"
                                                                          Stroke="{DynamicResource SubtleTextBrush}"
                                                                          StrokeThickness="1.2"/>
                                                                </Button>
                                                            </Grid>
                                                        </DataTemplate>
                                                    </ItemsControl.ItemTemplate>
                                                </ItemsControl>
                                                <Button HorizontalAlignment="Left"
                                                        Style="{StaticResource TextButton}"
                                                        Margin="0,4,0,0"
                                                        Command="{Binding DataContext.AddSubTaskCommand,
                                                            RelativeSource={RelativeSource AncestorType=Window}}"
                                                        CommandParameter="{Binding}">
                                                    <TextBlock Text="+ sub-task" FontSize="11"
                                                               Foreground="{DynamicResource SubtleTextBrush}"/>
                                                </Button>

                                                <!-- Links -->
                                                <TextBlock Text="Links" FontSize="10"
                                                           Foreground="{DynamicResource SubtleTextBrush}"
                                                           Margin="0,8,0,3"
                                                           Visibility="{Binding Links.Count,
                                                               Converter={StaticResource CountToVis}}"/>
                                                <ItemsControl ItemsSource="{Binding Links}">
                                                    <ItemsControl.ItemTemplate>
                                                        <DataTemplate>
                                                            <Grid Margin="0,1">
                                                                <Grid.ColumnDefinitions>
                                                                    <ColumnDefinition Width="*"/>
                                                                    <ColumnDefinition Width="Auto"/>
                                                                </Grid.ColumnDefinitions>
                                                                <TextBlock Grid.Column="0"
                                                                           Text="{Binding}"
                                                                           FontSize="11"
                                                                           Foreground="{DynamicResource AccentBrush}"
                                                                           TextDecorations="Underline"
                                                                           Cursor="Hand"
                                                                           TextTrimming="CharacterEllipsis"
                                                                           MouseLeftButtonDown="Link_Click"/>
                                                                <Button Grid.Column="1"
                                                                        Style="{StaticResource IconButton}"
                                                                        Width="20" Height="20"
                                                                        Command="{Binding DataContext.RemoveLinkCommand,
                                                                            RelativeSource={RelativeSource AncestorType=Window}}"
                                                                        CommandParameter="{Binding}">
                                                                    <Path Data="M0,0 L6,6 M6,0 L0,6"
                                                                          Stroke="{DynamicResource SubtleTextBrush}"
                                                                          StrokeThickness="1.2"/>
                                                                </Button>
                                                            </Grid>
                                                        </DataTemplate>
                                                    </ItemsControl.ItemTemplate>
                                                </ItemsControl>
                                            </StackPanel>
                                        </Border>
                                    </StackPanel>

                                    <DataTemplate.Triggers>
                                        <DataTrigger Binding="{Binding IsCompleted}" Value="True">
                                            <Setter TargetName="titleBlock"
                                                    Property="TextDecorations" Value="Strikethrough"/>
                                            <Setter TargetName="titleBlock"
                                                    Property="Foreground"
                                                    Value="{DynamicResource SubtleTextBrush}"/>
                                            <Setter TargetName="row" Property="Opacity" Value="0.55"/>
                                        </DataTrigger>
                                        <Trigger SourceName="row" Property="IsMouseOver" Value="True">
                                            <Setter TargetName="row"
                                                    Property="Background"
                                                    Value="{DynamicResource HoverBrush}"/>
                                        </Trigger>
                                    </DataTemplate.Triggers>
                                </Border>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </ScrollViewer>

                <!-- ═══ Footer ═══ -->
                <DockPanel Grid.Row="6" Margin="0,8,0,0" LastChildFill="False">
                    <TextBlock DockPanel.Dock="Left" FontSize="11"
                               Foreground="{DynamicResource SubtleTextBrush}">
                        <Run Text="{Binding PendingCount, Mode=OneWay}"/>
                        <Run Text=" pending"/>
                    </TextBlock>
                    <Button DockPanel.Dock="Right" Style="{StaticResource TextButton}"
                            Command="{Binding ClearCompletedCommand}"
                            Visibility="{Binding HasCompleted, Converter={StaticResource BoolToVis}}">
                        <TextBlock Text="Clear done" FontSize="11"
                                   Foreground="{DynamicResource SubtleTextBrush}"/>
                    </Button>
                    <Button DockPanel.Dock="Right" Style="{StaticResource TextButton}" Margin="0,0,8,0"
                            Command="{Binding ClearTagFiltersCommand}"
                            Visibility="{Binding HasTagFilters, Converter={StaticResource BoolToVis}}">
                        <TextBlock Text="Clear filters" FontSize="11"
                                   Foreground="{DynamicResource SubtleTextBrush}"/>
                    </Button>
                </DockPanel>
            </Grid>
        </Grid>
    </Border>
</Window>
```

- [ ] **Step 2: Rewrite MainWindow.xaml.cs**

```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FloatingTodoWidget.Helpers;
using FloatingTodoWidget.Models;
using FloatingTodoWidget.ViewModels;

namespace FloatingTodoWidget
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private DispatcherTimer? _collapseTimer;
        private bool _isCollapsed;

        public MainWindow(MainViewModel vm)
        {
            _vm = vm;
            DataContext = _vm;
            InitializeComponent();

            var s  = vm.Settings;
            var vw = SystemParameters.VirtualScreenWidth;
            var vh = SystemParameters.VirtualScreenHeight;
            var vl = SystemParameters.VirtualScreenLeft;
            var vt = SystemParameters.VirtualScreenTop;

            Width  = Math.Max(MinWidth,  Math.Min(s.WindowWidth,  vw));
            Height = Math.Max(MinHeight, Math.Min(s.WindowHeight, vh));
            Left   = Math.Max(vl, Math.Min(s.WindowLeft, vl + vw - Width));
            Top    = Math.Max(vt, Math.Min(s.WindowTop,  vt + vh - Height));

            _vm.ExitRequested      += (_, _) => Close();
            _vm.ShowWindowRequested += (_, _) => { Show(); Activate(); WindowState = WindowState.Normal; };
            _vm.FocusInputRequested += (_, _) => { InputBox.Focus(); InputBox.SelectAll(); };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (TryFindResource("AcrylicTint") is uint tint)
                NativeMethods.EnableAcrylic(this, tint);
            NativeMethods.SetClickThrough(this, _vm.Settings.ClickThrough);
        }

        // ── Drag ──
        private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject d && FindAncestor<TextBox>(d) != null) return;
            try { DragMove(); } catch { }
        }

        // ── Input Enter key ──
        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Return) return;
            _vm.AddTaskCommand.Execute(null);
            e.Handled = true;
        }

        // ── New project Enter / Escape ──
        private void NewProjectBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)  { _vm.ConfirmAddProjectCommand.Execute(null); e.Handled = true; }
            if (e.Key == Key.Escape)  { _vm.CancelAddProjectCommand.Execute(null);  e.Handled = true; }
        }

        // ── Theme toggle ──
        private void ThemeToggle_Click(object sender, RoutedEventArgs e) =>
            _vm.IsDarkTheme = !_vm.IsDarkTheme;

        // ── Priority bar click ──
        private void Priority_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TodoItem item)
            {
                _vm.CyclePriorityCommand.Execute(item);
                e.Handled = true;
            }
        }

        // ── Expand task row click ──
        private void TaskRow_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TodoItem item)
            {
                _vm.ToggleExpandCommand.Execute(item);
                e.Handled = true;
            }
        }

        // ── Link click ──
        private void Link_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBlock tb)
            {
                _vm.OpenLinkCommand.Execute(tb.Text);
                e.Handled = true;
            }
        }

        // ── Collapsed bar click ──
        private void CollapsedBar_Click(object sender, MouseButtonEventArgs e) => Expand();

        // ── Collapse / Expand ──
        private void Root_MouseEnter(object sender, MouseEventArgs e)
        {
            _collapseTimer?.Stop();
            if (_isCollapsed) Expand();
        }

        private void Root_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_vm.WindowMode != "Collapse") return;
            _collapseTimer?.Stop();
            _collapseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_vm.Settings.CollapseDelayMs)
            };
            _collapseTimer.Tick += (_, _) => { _collapseTimer.Stop(); Collapse(); };
            _collapseTimer.Start();
        }

        private void Collapse()
        {
            if (_isCollapsed) return;
            _isCollapsed     = true;
            MainContent.Visibility  = Visibility.Collapsed;
            CollapsedBar.Visibility = Visibility.Visible;
            var h = Height;
            MinHeight = 32;
            Height    = 32;
            _storedHeight = h;
        }

        private double _storedHeight;

        private void Expand()
        {
            if (!_isCollapsed) return;
            _isCollapsed     = false;
            CollapsedBar.Visibility = Visibility.Collapsed;
            MainContent.Visibility  = Visibility.Visible;
            MinHeight = 240;
            Height    = _storedHeight > 240 ? _storedHeight : _vm.Settings.WindowHeight;
        }

        public void ApplyWindowMode(string mode)
        {
            _collapseTimer?.Stop();
            _isCollapsed = false;
            CollapsedBar.Visibility = Visibility.Collapsed;
            MainContent.Visibility  = Visibility.Visible;
            MinHeight = 240;
            Height    = _vm.Settings.WindowHeight;

            if (mode == "Tray")
            {
                Hide();
            }
            // "Full" and "Collapse" both show window; collapse just enables the hover behavior
        }

        // ── Persist bounds ──
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            var b = WindowState == WindowState.Normal
                ? new Rect(Left, Top, Width, Height)
                : RestoreBounds;
            _vm.PersistWindowBounds(b.Left, b.Top, b.Width, b.Height);
            base.OnClosing(e);
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T m) return m;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
```

- [ ] **Step 3: Build**

```
dotnet build
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: rewrite MainWindow with project tabs, tag chips, detail panel, collapse mode"
```

---

## Task 9: Notification + Tray Services

**Files:**
- Create: `Services/NotificationService.cs`, `Services/TrayIconService.cs`

- [ ] **Step 1: Write Services/NotificationService.cs**

```csharp
using System;
using System.Linq;
using System.Windows.Threading;
using FloatingTodoWidget.Models;
using FloatingTodoWidget.ViewModels;

namespace FloatingTodoWidget.Services
{
    public sealed class NotificationService : IDisposable
    {
        private readonly MainViewModel _vm;
        private readonly DispatcherTimer _timer;
        private System.Windows.Forms.NotifyIcon? _tray; // set by TrayIconService

        public NotificationService(MainViewModel vm)
        {
            _vm = vm;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _timer.Tick += OnTick;
        }

        public void SetTrayIcon(System.Windows.Forms.NotifyIcon icon) => _tray = icon;

        public void Start() => _timer.Start();
        public void Stop()  => _timer.Stop();

        private void OnTick(object? sender, EventArgs e)
        {
            if (!_vm.Settings.NotificationsEnabled) return;
            var now = DateTime.Now;
            var tasks = _vm.GetAllTasks().Where(t => !t.IsCompleted && t.DueDate.HasValue).ToList();

            foreach (var task in tasks)
            {
                var due = task.DueDate!.Value;

                // Overdue (fire once)
                if (due < now && !task.OverdueNotified)
                {
                    task.OverdueNotified = true;
                    Notify("Task Overdue", task.Title, System.Windows.Forms.ToolTipIcon.Warning);
                }
                // Due soon
                else if (!task.DueSoonNotified)
                {
                    int notify = task.NotifyMinutesBefore ?? _vm.Settings.DefaultNotifyMinutes;
                    if ((due - now).TotalMinutes <= notify && (due - now).TotalMinutes > 0)
                    {
                        task.DueSoonNotified = true;
                        Notify("Due Soon", $"{task.Title} — due {due:HH:mm}", System.Windows.Forms.ToolTipIcon.Info);
                    }
                }
            }
        }

        private void Notify(string title, string message, System.Windows.Forms.ToolTipIcon icon)
        {
            try
            {
                _tray?.ShowBalloonTip(6000, title, message, icon);
            }
            catch (Exception ex)
            {
                Logger.Error("Notification failed", ex);
            }
        }

        public void Dispose() => _timer.Stop();
    }
}
```

- [ ] **Step 2: Write Services/TrayIconService.cs**

```csharp
using System;
using System.Drawing;
using System.Windows;
using FloatingTodoWidget.ViewModels;

namespace FloatingTodoWidget.Services
{
    public sealed class TrayIconService : IDisposable
    {
        private readonly MainViewModel _vm;
        private readonly Window _window;
        private readonly System.Windows.Forms.NotifyIcon _icon;

        public System.Windows.Forms.NotifyIcon NotifyIcon => _icon;

        public TrayIconService(MainViewModel vm, Window window)
        {
            _vm     = vm;
            _window = window;

            _icon = new System.Windows.Forms.NotifyIcon
            {
                Text    = "Floating To-Do",
                Visible = true,
                Icon    = CreateIcon(vm.PendingCount)
            };

            // Context menu
            _icon.ContextMenuStrip = BuildContextMenu();

            _icon.MouseDoubleClick += (_, _) =>
            {
                _window.Show();
                _window.Activate();
                _window.WindowState = System.Windows.WindowState.Normal;
            };

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.PendingCount))
                    _icon.Icon = CreateIcon(vm.PendingCount);
            };
        }

        private System.Windows.Forms.ContextMenuStrip BuildContextMenu()
        {
            var menu = new System.Windows.Forms.ContextMenuStrip();
            var show = new System.Windows.Forms.ToolStripMenuItem("Show / Hide");
            show.Click += (_, _) =>
            {
                if (_window.IsVisible) _window.Hide();
                else { _window.Show(); _window.Activate(); }
            };
            var newTask = new System.Windows.Forms.ToolStripMenuItem("New Task");
            newTask.Click += (_, _) =>
            {
                _window.Show();
                _window.Activate();
                _vm.FocusInputCommand.Execute(null);
            };
            var exit = new System.Windows.Forms.ToolStripMenuItem("Exit");
            exit.Click += (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(
                () => _vm.ExitCommand.Execute(null));

            menu.Items.Add(show);
            menu.Items.Add(newTask);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(exit);
            return menu;
        }

        private static System.Drawing.Icon CreateIcon(int count)
        {
            // Draw a simple icon with count badge
            using var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            g.FillEllipse(new SolidBrush(Color.FromArgb(91, 127, 255)), 0, 0, 15, 15);
            if (count > 0)
            {
                var text = count > 99 ? "99" : count.ToString();
                var font = new Font("Arial", 7f, FontStyle.Bold);
                var sf = new System.Drawing.StringFormat
                    { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(text, font, Brushes.White, new RectangleF(0, 0, 16, 16), sf);
            }
            return System.Drawing.Icon.FromHandle(bmp.GetHicon());
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.Dispose();
        }
    }
}
```

- [ ] **Step 3: Build**

```
dotnet build
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add NotificationService and TrayIconService"
```

---

## Task 10: App Wiring + SetWindowMode command

**Files:**
- Rewrite: `App.xaml`, `App.xaml.cs`
- Modify: `ViewModels/MainViewModel.cs` — add `SetWindowModeCommand`

- [ ] **Step 1: Add SetWindowModeCommand to MainViewModel**

In `ViewModels/MainViewModel.cs`, replace the existing `OnWindowModeChanged` partial method and add a proper relay command:

```csharp
// Add this field near the top (after _addingDepth):
public event EventHandler<string>? WindowModeChangeRequested;

// Replace partial void OnWindowModeChanged with:
partial void OnWindowModeChanged(string value)
{
    Settings.WindowMode = value;
    SaveSettings();
    WindowModeChangeRequested?.Invoke(this, value);
}

// Add this command (after SetSort):
[RelayCommand]
private void SetWindowMode(string? mode)
{
    if (string.IsNullOrEmpty(mode)) return;
    WindowMode = mode;
}
```

- [ ] **Step 2: Rewrite App.xaml**

```xml
<Application x:Class="FloatingTodoWidget.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Resources/Theme.Dark.xaml"/>
                <ResourceDictionary Source="Resources/Styles.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 3: Rewrite App.xaml.cs**

```csharp
using System;
using System.Threading;
using System.Windows;
using FloatingTodoWidget.Services;
using FloatingTodoWidget.ViewModels;

namespace FloatingTodoWidget
{
    public partial class App : Application
    {
        private Mutex? _singleInstance;
        private NotificationService? _notificationService;
        private TrayIconService? _trayService;

        protected override void OnStartup(StartupEventArgs e)
        {
            _singleInstance = new Mutex(true, "FloatingTodoWidget_v2_SingleInstance", out var isNew);
            if (!isNew)
            {
                MessageBox.Show("Floating To-Do is already running.", "Already Running",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            DispatcherUnhandledException += (_, args) =>
            {
                Logger.Error("Unhandled UI exception", args.Exception);
                args.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Logger.Error("Unhandled exception", args.ExceptionObject as Exception);

            base.OnStartup(e);

            IStorageService storage = new JsonStorageService();
            var settings = storage.LoadSettings();
            ThemeService.Apply(settings.IsDarkTheme);

            var vm     = new MainViewModel(storage, settings);
            var window = new MainWindow(vm);

            // Tray service (must exist before notification service)
            _trayService = new TrayIconService(vm, window);

            // Notification service
            _notificationService = new NotificationService(vm);
            _notificationService.SetTrayIcon(_trayService.NotifyIcon);
            _notificationService.Start();

            // Window mode changes
            vm.WindowModeChangeRequested += (_, mode) => window.ApplyWindowMode(mode);

            if (settings.WindowMode == "Tray")
                window.ApplyWindowMode("Tray");
            else
                window.Show();

            Logger.Info("Application started v2.0");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notificationService?.Dispose();
            _trayService?.Dispose();
            _singleInstance?.ReleaseMutex();
            _singleInstance?.Dispose();
            base.OnExit(e);
        }
    }
}
```

- [ ] **Step 4: Fix DataTemplate triggers placement**

The `DataTemplate.Triggers` block in `MainWindow.xaml` must be OUTSIDE the root `Border` element but inside `DataTemplate`. Verify the XAML structure is:

```
<DataTemplate>
    <Border x:Name="row" ...>
        <StackPanel>
            ...
        </StackPanel>
    </Border>
    <DataTemplate.Triggers>     ← must be here, sibling of Border
        ...
    </DataTemplate.Triggers>
</DataTemplate>
```

Open `MainWindow.xaml` and confirm the `<DataTemplate.Triggers>` block is properly placed after the closing `</Border>` tag but before `</DataTemplate>`.

- [ ] **Step 5: Build**

```
dotnet build
```
Expected: 0 errors. Fix any compile errors before continuing.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: wire all services in App.xaml.cs, add window mode switching"
```

---

## Task 11: Build, Run & Smoke Test

- [ ] **Step 1: Full clean build**

```bash
dotnet clean
dotnet build -c Release
```
Expected: Build succeeded, 0 errors, 0 warnings (or only benign warnings).

- [ ] **Step 2: Run debug build**

```bash
dotnet run
```
Or launch `bin\Debug\net8.0-windows\FloatingTodoWidget.exe`.

- [ ] **Step 3: Verify — basic task creation (no duplicates)**

1. Type `Buy milk` in the input box
2. Press Enter once
3. Verify exactly ONE task appears
4. Repeat 5 times rapidly — still no duplicates

- [ ] **Step 4: Verify — quick-add syntax**

Type: `Fix bug !high @tomorrow #work ~backend "check the logs" https://github.com`
Press Enter. Verify:
- Title: "Fix bug"
- Priority bar: red (High)
- Due date: tomorrow's date shown
- Expand task → Notes shows "check the logs", Links shows the URL

- [ ] **Step 5: Verify — projects**

1. Click `+` next to tabs → type `Personal` → Enter
2. "Personal" tab appears
3. Click it → add a task → task appears only under Personal tab
4. Click "All" → task appears in all-tasks view

- [ ] **Step 6: Verify — collapse mode**

1. Right-click → Window mode → Collapse
2. Move mouse off window → after 1.5s collapses to 32px bar
3. Mouse over bar → expands back

- [ ] **Step 7: Verify — tray mode**

1. Right-click → Window mode → Tray
2. Window hides, tray icon appears
3. Double-click tray icon → window shows

- [ ] **Step 8: Verify — notifications**

1. Add task with `@notify:1m @today` (due today, notify in 1 min)
2. Wait ~60 seconds → balloon tip appears

- [ ] **Step 9: Commit final**

```bash
git add -A
git commit -m "feat: complete FloatingTodoWidget v2.0 revamp"
```

---

## Self-Review

**Spec coverage:**
- ✅ Duplicate-proof AddTask (Interlocked + early text clear)
- ✅ Projects (tabs, inline add, Inbox, All)
- ✅ Tags (QuickAddParser `~tag`, ActiveTagFilters, chip UI)
- ✅ Sub-tasks (detail panel, add/remove/toggle)
- ✅ Notes (detail panel TextBox)
- ✅ Links (detail panel, clickable, remove)
- ✅ Overdue/due-today visual (DueAwarePriorityBrushConverter, DueDateForegroundConverter)
- ✅ Balloon tip notifications (NotificationService + NotifyIcon)
- ✅ Window collapse mode (hover delay, 32px bar)
- ✅ System tray mode (hide window, NotifyIcon, show on double-click)
- ✅ Extended QuickAddParser
- ✅ Parse preview below input
- ✅ Sort (Priority/DueDate/Created/Alpha — sort command wired)
- ✅ Dark/light theme
- ✅ Acrylic blur
- ✅ Always-on-top
- ✅ Auto-start
- ✅ Clear completed

**Type consistency check:**
- `ParseResult` record defined in Task 4, consumed in Task 7 MainViewModel ✅
- `ProjectTabItem(string Name, Guid? Id, string? Color)` defined Task 1, used Task 7 ✅
- `AppData` fields (Projects/Tags/Tasks) consistent across storage and ViewModel ✅
- `IStorageService` methods (`LoadData`/`SaveData`/`LoadSettings`/`SaveSettings`) consistent ✅
- `NotificationService.SetTrayIcon(NotifyIcon)` matches Task 10 App.xaml.cs call ✅

**Placeholder scan:** None found. All steps have concrete code or concrete commands.
