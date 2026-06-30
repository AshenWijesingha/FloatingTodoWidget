using System;

namespace FloatingTodoWidget.Models
{
    // Guid? Id: null=All Tasks, Guid.Empty=Inbox, else project Id
    public record ProjectTabItem(string Name, Guid? Id, string? Color);
}
