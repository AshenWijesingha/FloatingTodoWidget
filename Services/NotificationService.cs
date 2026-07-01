using System;
using System.Linq;
using System.Windows.Threading;
using FloatingTodoWidget.Models;
using FloatingTodoWidget.ViewModels;

namespace FloatingTodoWidget.Services
{
    public sealed class NotificationService : IDisposable
    {
        private readonly MainViewModel _vm;
        private readonly DispatcherTimer _timer;
        private System.Windows.Forms.NotifyIcon? _tray; // set by TrayIconService

        public NotificationService(MainViewModel vm)
        {
            _vm = vm;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _timer.Tick += OnTick;
        }

        public void SetTrayIcon(System.Windows.Forms.NotifyIcon icon) => _tray = icon;

        public void Start() => _timer.Start();
        public void Stop()  => _timer.Stop();

        private void OnTick(object? sender, EventArgs e)
        {
            if (!_vm.Settings.NotificationsEnabled) return;
            var now = DateTime.Now;
            var tasks = _vm.GetAllTasks().Where(t => !t.IsCompleted && t.DueDate.HasValue).ToList();

            foreach (var task in tasks)
            {
                var due = task.DueDate!.Value;

                // Overdue (fire once)
                if (due < now && !task.OverdueNotified)
                {
                    task.OverdueNotified = true;
                    Notify("Task Overdue", task.Title, System.Windows.Forms.ToolTipIcon.Warning);
                }
                // Due soon
                else if (!task.DueSoonNotified)
                {
                    int notify = task.NotifyMinutesBefore ?? _vm.Settings.DefaultNotifyMinutes;
                    if (notify > 0 && (due - now).TotalMinutes <= notify && (due - now).TotalMinutes > 0)
                    {
                        task.DueSoonNotified = true;
                        Notify("Due Soon", $"{task.Title} \u2014 due {due:HH:mm}", System.Windows.Forms.ToolTipIcon.Info);
                    }
                }
            }
        }

        private void Notify(string title, string message, System.Windows.Forms.ToolTipIcon icon)
        {
            try
            {
                _tray?.ShowBalloonTip(6000, title, message, icon);
            }
            catch (Exception ex)
            {
                Logger.Error("Notification failed", ex);
            }
        }

        public void Dispose() => _timer.Stop();
    }
}
