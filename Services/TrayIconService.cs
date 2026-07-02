using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using FloatingTodoWidget.ViewModels;

namespace FloatingTodoWidget.Services
{
    public sealed class TrayIconService : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private readonly MainViewModel _vm;
        private readonly Window _window;
        private readonly System.Windows.Forms.NotifyIcon _icon;

        // The Icon assigned to NotifyIcon.Icon and the raw HICON it wraps (from Bitmap.GetHicon()).
        // Icon.Dispose() does NOT release a handle obtained via GetHicon()/FromHandle() — the docs
        // are explicit that the caller must call DestroyIcon separately, or the handle leaks for
        // the life of the process. We recreate this icon every time PendingCount changes, so both
        // must be torn down on every update (and on Dispose) to avoid exhausting GDI handles.
        private System.Drawing.Icon? _currentIcon;
        private IntPtr _currentHIcon = IntPtr.Zero;

        public System.Windows.Forms.NotifyIcon NotifyIcon => _icon;

        public TrayIconService(MainViewModel vm, Window window)
        {
            _vm     = vm;
            _window = window;

            _icon = new System.Windows.Forms.NotifyIcon
            {
                Text    = "Floating To-Do",
                Visible = true
            };
            SetIcon(vm.PendingCount);

            // Context menu
            _icon.ContextMenuStrip = BuildContextMenu();

            _icon.MouseDoubleClick += (_, _) =>
            {
                _window.Show();
                _window.Activate();
                _window.WindowState = System.Windows.WindowState.Normal;
            };

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.PendingCount))
                    SetIcon(vm.PendingCount);
            };
        }

        private void SetIcon(int count)
        {
            var (icon, hIcon) = CreateIconWithHandle(count);
            _icon.Icon = icon;

            // Safe to tear down the previous icon now: NotifyIcon has already switched to the
            // new one, so nothing will reference the old handle again.
            _currentIcon?.Dispose();
            if (_currentHIcon != IntPtr.Zero) DestroyIcon(_currentHIcon);

            _currentIcon  = icon;
            _currentHIcon = hIcon;
        }

        private System.Windows.Forms.ContextMenuStrip BuildContextMenu()
        {
            var menu = new System.Windows.Forms.ContextMenuStrip();

            var show = new System.Windows.Forms.ToolStripMenuItem("Show / Hide");
            show.Click += (_, _) =>
            {
                if (_window.IsVisible) _window.Hide();
                else { _window.Show(); _window.Activate(); }
            };

            var newTask = new System.Windows.Forms.ToolStripMenuItem("New Task");
            newTask.Click += (_, _) =>
            {
                _window.Show();
                _window.Activate();
                _vm.FocusInputCommand.Execute(null);
            };

            var exit = new System.Windows.Forms.ToolStripMenuItem("Exit");
            exit.Click += (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(
                () => _vm.ExitCommand.Execute(null));

            menu.Items.Add(show);
            menu.Items.Add(newTask);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(exit);
            return menu;
        }

        private static (System.Drawing.Icon Icon, IntPtr HIcon) CreateIconWithHandle(int count)
        {
            // Draw a 16x16 icon with optional count badge
            using var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(91, 127, 255));
            g.FillEllipse(brush, 0, 0, 15, 15);
            if (count > 0)
            {
                var text = count > 99 ? "99" : count.ToString();
                using var font = new System.Drawing.Font("Arial", 7f, System.Drawing.FontStyle.Bold);
                var sf = new System.Drawing.StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(text, font, Brushes.White, new RectangleF(0, 0, 16, 16), sf);
            }
            var hIcon = bmp.GetHicon();
            return (System.Drawing.Icon.FromHandle(hIcon), hIcon);
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.ContextMenuStrip?.Dispose();
            _icon.Dispose();
            _currentIcon?.Dispose();
            if (_currentHIcon != IntPtr.Zero) DestroyIcon(_currentHIcon);
            _currentHIcon = IntPtr.Zero;
        }
    }
}
