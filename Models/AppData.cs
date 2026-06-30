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
