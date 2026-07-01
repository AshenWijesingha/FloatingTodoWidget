using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
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
        private bool _suspendSave;

        public AppSettings Settings { get; }

        /// <summary>Backing collection of all tasks.</summary>
        public ObservableCollection<TodoItem> Tasks { get; } = new();

        /// <summary>Filtered/sorted view bound to the UI.</summary>
        public ICollectionView TasksView { get; }

        [ObservableProperty]
        private string _newTaskText = string.Empty;

        public int PendingCount => Tasks.Count(t => !t.IsCompleted);

        public bool HasCompleted => Tasks.Any(t => t.IsCompleted);

        // ---- Settings-backed observable properties ----
        [ObservableProperty] private bool _isDarkTheme;
        [ObservableProperty] private bool _isTopmost;
        [ObservableProperty] private bool _showCompleted;
        [ObservableProperty] private bool _autoStart;

        public event EventHandler? ExitRequested;
        public event EventHandler? FocusInputRequested;

        public MainViewModel(IStorageService storage, AppSettings settings)
        {
            _storage = storage;
            Settings = settings;

            _isDarkTheme = settings.IsDarkTheme;
            _isTopmost = settings.Topmost;
            _showCompleted = settings.ShowCompleted;
            _autoStart = StartupService.IsEnabled();

            // Load persisted tasks.
            _suspendSave = true;
            var appData = _storage.LoadData();
            foreach (var t in appData.Tasks)
                AddTaskInternal(t);
            _suspendSave = false;

            Tasks.CollectionChanged += OnTasksChanged;

            // Filtered view: hide completed when toggled off; completed sink to bottom.
            TasksView = CollectionViewSource.GetDefaultView(Tasks);
            TasksView.Filter = o => ShowCompleted || (o is TodoItem t && !t.IsCompleted);
            if (TasksView is ListCollectionView lcv)
                lcv.CustomSort = Comparer<TodoItem>.Create(CompareTasks);

            RefreshCounts();
        }

        private static int CompareTasks(TodoItem a, TodoItem b)
        {
            // Active first, then by priority desc, then newest first.
            int c = a.IsCompleted.CompareTo(b.IsCompleted);
            if (c != 0) return c;
            c = b.Priority.CompareTo(a.Priority);
            if (c != 0) return c;
            return b.CreatedAt.CompareTo(a.CreatedAt);
        }

        // ======================= Commands =======================

        [RelayCommand]
        private void AddTask()
        {
            var raw = NewTaskText?.Trim();
            if (string.IsNullOrWhiteSpace(raw)) return;

            // Clear first so any reentrant call (WPF can dispatch a pending key event during
            // Tasks.Add → CollectionChanged → TasksView.Refresh) sees empty text and aborts.
            NewTaskText = string.Empty;

            var parsed = QuickAddParser.Parse(raw);
            if (string.IsNullOrWhiteSpace(parsed.Title)) return;

            AddTaskInternal(new TodoItem { Title = parsed.Title, Priority = parsed.Priority, DueDate = parsed.DueDate });
            SaveTasks();
        }

        [RelayCommand]
        private void DeleteTask(TodoItem? item)
        {
            if (item is null) return;
            Tasks.Remove(item);
            SaveTasks();
        }

        [RelayCommand]
        private void CyclePriority(TodoItem? item)
        {
            if (item is null) return;
            item.Priority = item.Priority switch
            {
                Priority.None => Priority.Low,
                Priority.Low => Priority.Medium,
                Priority.Medium => Priority.High,
                _ => Priority.None
            };
            // Refresh the sort order (priority affects ordering).
            // SaveTasks() is handled by OnItemChanged which fires on the Priority change above.
            TasksView.Refresh();
        }

        [RelayCommand]
        private void ClearCompleted()
        {
            foreach (var done in Tasks.Where(t => t.IsCompleted).ToList())
                Tasks.Remove(done);
            SaveTasks();
        }

        [RelayCommand]
        private void FocusInput() => FocusInputRequested?.Invoke(this, EventArgs.Empty);

        [RelayCommand]
        private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);

        // ======================= Setting toggles =======================

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
            TasksView.Refresh();
            SaveSettings();
        }

        partial void OnAutoStartChanged(bool value) => StartupService.SetEnabled(value);

        // ======================= Persistence plumbing =======================

        private void AddTaskInternal(TodoItem item)
        {
            item.PropertyChanged += OnItemChanged;
            Tasks.Add(item);
        }

        private void OnTasksChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (TodoItem i in e.OldItems) i.PropertyChanged -= OnItemChanged;

            TasksView?.Refresh();
            RefreshCounts();
        }

        private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(TodoItem.IsCompleted))
            {
                TasksView.Refresh();
                RefreshCounts();
            }
            SaveTasks();
        }

        private void RefreshCounts()
        {
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(HasCompleted));
        }

        private void SaveTasks()
        {
            if (_suspendSave) return;
            var appData = new AppData { Tasks = Tasks.ToList() };
            _storage.SaveData(appData);
        }

        public void SaveSettings() => _storage.SaveSettings(Settings);

        /// <summary>Called by the window on close to persist final size/position.</summary>
        public void PersistWindowBounds(double left, double top, double width, double height)
        {
            Settings.WindowLeft = left;
            Settings.WindowTop = top;
            Settings.WindowWidth = width;
            Settings.WindowHeight = height;
            SaveSettings();
        }
    }
}
