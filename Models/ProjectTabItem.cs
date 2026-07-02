using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FloatingTodoWidget.Models
{
    // Guid? Id: null=All Tasks, Guid.Empty=Inbox, else project Id.
    // A mutable class (not a record) so the pending-task Count badge can be updated in place
    // without recreating the list — that would break ListBox.SelectedItem reference equality.
    public partial class ProjectTabItem : ObservableObject
    {
        public string Name { get; }
        public Guid? Id { get; }
        public string? Color { get; }

        [ObservableProperty] private int _count;

        public ProjectTabItem(string name, Guid? id, string? color)
        {
            Name  = name;
            Id    = id;
            Color = color;
        }
    }
}
