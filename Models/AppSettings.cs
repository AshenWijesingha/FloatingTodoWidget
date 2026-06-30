using System;

namespace FloatingTodoWidget.Models
{
    public class AppSettings
    {
        public double WindowLeft { get; set; } = 120;
        public double WindowTop { get; set; } = 120;
        public double WindowWidth { get; set; } = 340;
        public double WindowHeight { get; set; } = 540;
        public bool IsDarkTheme { get; set; } = true;
        public bool Topmost { get; set; } = true;
        public bool ClickThrough { get; set; } = false;
        public bool ShowCompleted { get; set; } = true;
        public string WindowMode { get; set; } = "Full";   // Full | Collapse | Tray
        public int CollapseDelayMs { get; set; } = 1500;
        public bool AutoStart { get; set; } = false;
        public bool NotificationsEnabled { get; set; } = true;
        public int DefaultNotifyMinutes { get; set; } = 30;
        public string SortMode { get; set; } = "Priority"; // Priority | DueDate | Created | Alpha
        public Guid? ActiveProjectId { get; set; }
    }
}
