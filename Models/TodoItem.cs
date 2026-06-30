using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FloatingTodoWidget.Models
{
    /// <summary>
    /// A single task. Made observable so checkbox/priority changes update the UI
    /// (strike-through, color) instantly. The source-generated public properties
    /// (Title, IsCompleted, ...) are what get serialized to JSON.
    /// </summary>
    public partial class TodoItem : ObservableObject
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private bool _isCompleted;

        [ObservableProperty]
        private Priority _priority = Priority.None;

        [ObservableProperty]
        private DateTime? _dueDate;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
