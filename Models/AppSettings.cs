namespace FloatingTodoWidget.Models
{
    /// <summary>Persisted user/window preferences.</summary>
    public class AppSettings
    {
        public double WindowLeft { get; set; } = 120;
        public double WindowTop { get; set; } = 120;
        public double WindowWidth { get; set; } = 320;
        public double WindowHeight { get; set; } = 480;

        public bool IsDarkTheme { get; set; } = true;
        public bool Topmost { get; set; } = true;
        public bool ClickThrough { get; set; } = false;
        public bool ShowCompleted { get; set; } = true;
    }
}
