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
