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

        /// <summary>Recurrence rule for auto-spawning the next occurrence on completion.</summary>
        public Recurrence Recurrence { get; set; } = Recurrence.None;

        /// <summary>Manual drag-and-drop position, used only when SortMode == "Manual".</summary>
        public int SortOrder { get; set; }

        // A due date change means any prior "already notified" state is stale — re-arm both.
        partial void OnDueDateChanged(DateTime? value)
        {
            OverdueNotified = false;
            DueSoonNotified = false;
        }

        // Reopening a completed task should let it notify again if it's overdue.
        partial void OnIsCompletedChanged(bool value)
        {
            if (!value)
            {
                OverdueNotified = false;
                DueSoonNotified = false;
            }
        }
    }
}
