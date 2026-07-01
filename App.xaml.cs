using System;
using System.Threading;
using System.Windows;
using FloatingTodoWidget.Services;
using FloatingTodoWidget.ViewModels;

namespace FloatingTodoWidget
{
    public partial class App : Application
    {
        private Mutex? _singleInstance;
        private NotificationService? _notificationService;
        private TrayIconService? _trayService;

        protected override void OnStartup(StartupEventArgs e)
        {
            // ---- Single instance ----
            _singleInstance = new Mutex(true, "FloatingTodoWidget_v2_SingleInstance", out var isNew);
            if (!isNew)
            {
                MessageBox.Show("Floating To-Do is already running.", "Already Running",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // ---- Global error handling / logging ----
            DispatcherUnhandledException += (_, args) =>
            {
                Logger.Error("Unhandled UI exception", args.Exception);
                args.Handled = true; // keep the widget alive
            };
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Logger.Error("Unhandled exception", args.ExceptionObject as Exception);

            base.OnStartup(e);

            // Keep app alive when main window is hidden (tray mode)
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            IStorageService storage = new JsonStorageService();
            var settings = storage.LoadSettings();
            ThemeService.Apply(settings.IsDarkTheme);

            var vm     = new MainViewModel(storage, settings);
            var window = new MainWindow(vm);

            // Tray service must exist before notification service
            _trayService = new TrayIconService(vm, window);

            // Notification service
            _notificationService = new NotificationService(vm);
            _notificationService.SetTrayIcon(_trayService.NotifyIcon);
            _notificationService.Start();

            // Wire window mode changes from ViewModel to MainWindow
            vm.WindowModeChangeRequested += (_, mode) => window.ApplyWindowMode(mode);

            // Apply initial window mode
            if (settings.WindowMode == "Tray")
                window.ApplyWindowMode("Tray");
            else
                window.Show();

            Logger.Info("Application started v2.0");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notificationService?.Dispose();
            _trayService?.Dispose();
            _singleInstance?.ReleaseMutex();
            _singleInstance?.Dispose();
            base.OnExit(e);
        }
    }
}
