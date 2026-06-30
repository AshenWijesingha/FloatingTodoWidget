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
