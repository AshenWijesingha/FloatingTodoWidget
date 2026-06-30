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

        protected override void OnStartup(StartupEventArgs e)
        {
            // ---- Single instance ----
            _singleInstance = new Mutex(true, "FloatingTodoWidget_SingleInstance", out var isNew);
            if (!isNew)
            {
                MessageBox.Show("Floating To-Do Widget is already running.", "Already running",
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
                Logger.Error("Unhandled domain exception", args.ExceptionObject as Exception);

            base.OnStartup(e);

            IStorageService storage = new JsonStorageService();
            var settings = storage.LoadSettings();
            ThemeService.Apply(settings.IsDarkTheme);

            var vm = new MainViewModel(storage, settings);
            var window = new MainWindow(vm);
            window.Show();

            Logger.Info("Application started.");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _singleInstance?.ReleaseMutex();
            _singleInstance?.Dispose();
            base.OnExit(e);
        }
    }
}
