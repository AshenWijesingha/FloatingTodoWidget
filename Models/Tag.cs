using System;

namespace FloatingTodoWidget.Models
{
    public class Tag
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#9C27B0";
    }
}
