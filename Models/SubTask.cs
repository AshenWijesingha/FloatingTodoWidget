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
