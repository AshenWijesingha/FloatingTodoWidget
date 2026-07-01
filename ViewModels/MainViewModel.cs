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
        public ObservableCollection<TodoItem>       Tasks            { get; } = new();
        public ObservableCollection<Project>        Projects         { get; } = new();
        public ObservableCollection<Tag>            AllTags          { get; } = new();
        public ObservableCollection<Tag>            ActiveTagFilters { get; } = new();
        public ObservableCollection<ProjectTabItem> ProjectTabs      { get; } = new();
        public ICollectionView TasksView { get; }

        // ── Input ──
        [ObservableProperty] private string _newTaskText  = string.Empty;
        [ObservableProperty] private string _parsePreview = string.Empty;

        // ── Filtering / State ──
        [ObservableProperty] private ProjectTabItem? _activeProjectTab;
        [ObservableProperty] private bool   _isDarkTheme;
        [ObservableProperty] private bool   _isTopmost;
        [ObservableProperty] private bool   _showCompleted;
        [ObservableProperty] private bool   _autoStart;
        [ObservableProperty] private bool   _notificationsEnabled;
        [ObservableProperty] private string _sortMode    = "Priority";
        [ObservableProperty] private string _windowMode  = "Full";
        [ObservableProperty] private bool   _isAddingProject;
        [ObservableProperty] private string _newProjectName = string.Empty;

        // ── Computed ──
        public int  PendingCount  => Tasks.Count(t => !t.IsCompleted);
        public bool HasCompleted  => Tasks.Any(t => t.IsCompleted);
        public bool HasTagFilters => ActiveTagFilters.Count > 0;

        // ── Events ──
        public event EventHandler? ExitRequested;
        public event EventHandler? FocusInputRequested;
        public event EventHandler? ShowWindowRequested;
        public event EventHandler<string>? WindowModeChangeRequested;
        public event EventHandler? DataChanged; // for NotificationService to re-check

        // ── Computed helpers ──
        private Guid? ActiveProjectId => ActiveProjectTab?.Id;

        public MainViewModel(IStorageService storage, AppSettings settings)
        {
            _storage = storage;
            Settings = settings;

            _isDarkTheme          = settings.IsDarkTheme;
            _isTopmost            = settings.Topmost;
            _showCompleted        = settings.ShowCompleted;
            _autoStart            = StartupService.IsEnabled();
            _notificationsEnabled = settings.NotificationsEnabled;
            _sortMode             = settings.SortMode;
            _windowMode           = settings.WindowMode;

            // Build view before loading so filter works immediately
            TasksView = CollectionViewSource.GetDefaultView(Tasks);
            TasksView.Filter = FilterTask;
            if (TasksView is ListCollectionView lcv)
                lcv.CustomSort = Comparer<TodoItem>.Create(CompareTasks);

            // Load data
            _suspendSave = true;
            _data = storage.LoadData();
            foreach (var p in _data.Projects.OrderBy(p => p.SortOrder)) Projects.Add(p);
            foreach (var t in _data.Tags)  AllTags.Add(t);
            foreach (var t in _data.Tasks) { SubscribeItem(t); Tasks.Add(t); }
            _suspendSave = false;

            Tasks.CollectionChanged           += OnTasksChanged;
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
                if (pid.Value == Guid.Empty && t.ProjectId.HasValue) return false; // Inbox: no project
                if (pid.Value != Guid.Empty && t.ProjectId != pid)   return false; // specific project
            }
            if (ActiveTagFilters.Count > 0 && !ActiveTagFilters.Any(tag => t.TagIds.Contains(tag.Id)))
                return false;
            return true;
        }

        private int CompareTasks(TodoItem a, TodoItem b)
        {
            // Completed items always sink to bottom
            int c = a.IsCompleted.CompareTo(b.IsCompleted);
            if (c != 0) return c;

            switch (SortMode)
            {
                case "DueDate":
                    if (a.DueDate.HasValue && b.DueDate.HasValue)
                        return a.DueDate.Value.CompareTo(b.DueDate.Value);
                    if (a.DueDate.HasValue) return -1;
                    if (b.DueDate.HasValue) return  1;
                    // Fall through to priority as tiebreak
                    c = b.Priority.CompareTo(a.Priority);
                    return c != 0 ? c : b.CreatedAt.CompareTo(a.CreatedAt);

                case "Created":
                    return b.CreatedAt.CompareTo(a.CreatedAt);

                case "Alpha":
                    return string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);

                default: // "Priority"
                    c = b.Priority.CompareTo(a.Priority);
                    if (c != 0) return c;
                    if (a.DueDate.HasValue && b.DueDate.HasValue)
                        return a.DueDate.Value.CompareTo(b.DueDate.Value);
                    if (a.DueDate.HasValue) return -1;
                    if (b.DueDate.HasValue) return  1;
                    return b.CreatedAt.CompareTo(a.CreatedAt);
            }
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

        [RelayCommand]
        private void SetWindowMode(string? mode)
        {
            if (string.IsNullOrEmpty(mode)) return;
            WindowMode = mode;
            // OnWindowModeChanged fires WindowModeChangeRequested
        }

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
            NewProjectName  = string.Empty;
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
                lcv.CustomSort = Comparer<TodoItem>.Create(CompareTasks);
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
            WindowModeChangeRequested?.Invoke(this, value);
        }

        // ─────────────────── Parse Preview ───────────────────

        partial void OnNewTaskTextChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) { ParsePreview = string.Empty; return; }
            var r = QuickAddParser.Parse(value);
            var parts = new List<string>();
            if (r.Priority != Priority.None)   parts.Add($"!{r.Priority}");
            if (r.DueDate.HasValue)             parts.Add($"Due {r.DueDate:MMM d}");
            if (r.ProjectName != null)          parts.Add($"#{r.ProjectName}");
            if (r.TagNames.Length > 0)          parts.Add("~" + string.Join(",", r.TagNames));
            if (!string.IsNullOrEmpty(r.Note))  parts.Add($"\"{(r.Note.Length > 20 ? r.Note[..20] + "\u2026" : r.Note)}\"");
            if (r.Links.Length > 0)             parts.Add($"\U0001F517 {r.Links.Length}");
            ParsePreview = parts.Count > 0 ? "\u25b8 " + string.Join(" \u00b7 ", parts) : string.Empty;
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
