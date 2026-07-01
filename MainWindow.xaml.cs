using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FloatingTodoWidget.Helpers;
using FloatingTodoWidget.Models;
using FloatingTodoWidget.ViewModels;

namespace FloatingTodoWidget
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private DispatcherTimer? _collapseTimer;
        private bool _isCollapsed;
        private double _storedHeight;

        public MainWindow(MainViewModel vm)
        {
            _vm = vm;
            DataContext = _vm;
            InitializeComponent();

            var s  = vm.Settings;
            var vw = SystemParameters.VirtualScreenWidth;
            var vh = SystemParameters.VirtualScreenHeight;
            var vl = SystemParameters.VirtualScreenLeft;
            var vt = SystemParameters.VirtualScreenTop;

            Width  = Math.Max(MinWidth,  Math.Min(s.WindowWidth,  vw));
            Height = Math.Max(MinHeight, Math.Min(s.WindowHeight, vh));
            Left   = Math.Max(vl, Math.Min(s.WindowLeft, vl + vw - Width));
            Top    = Math.Max(vt, Math.Min(s.WindowTop,  vt + vh - Height));

            _vm.ExitRequested       += (_, _) => Close();
            _vm.ShowWindowRequested += (_, _) => { Show(); Activate(); WindowState = WindowState.Normal; };
            _vm.FocusInputRequested += (_, _) => { InputBox.Focus(); InputBox.SelectAll(); };
            _vm.WindowModeChangeRequested += (_, _) => ApplyWindowMode(_vm.WindowMode);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (TryFindResource("AcrylicTint") is uint tint)
                NativeMethods.EnableAcrylic(this, tint);
            NativeMethods.SetClickThrough(this, _vm.Settings.ClickThrough);
        }

        // ── Drag ──
        private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject d && FindAncestor<TextBox>(d) != null) return;
            try { DragMove(); } catch { }
        }

        // ── Input Enter key ──
        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Return) return;
            _vm.AddTaskCommand.Execute(null);
            e.Handled = true;
        }

        // ── New project Enter / Escape ──
        private void NewProjectBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)  { _vm.ConfirmAddProjectCommand.Execute(null); e.Handled = true; }
            if (e.Key == Key.Escape)  { _vm.CancelAddProjectCommand.Execute(null);  e.Handled = true; }
        }

        // ── Theme toggle ──
        private void ThemeToggle_Click(object sender, RoutedEventArgs e) =>
            _vm.IsDarkTheme = !_vm.IsDarkTheme;

        // ── Priority bar click ──
        private void Priority_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TodoItem item)
            {
                _vm.CyclePriorityCommand.Execute(item);
                e.Handled = true;
            }
        }

        // ── Expand task row click ──
        private void TaskRow_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TodoItem item)
            {
                _vm.ToggleExpandCommand.Execute(item);
                e.Handled = true;
            }
        }

        // ── Link click ──
        private void Link_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBlock tb)
            {
                _vm.OpenLinkCommand.Execute(tb.Text);
                e.Handled = true;
            }
        }

        // ── Collapsed bar click ──
        private void CollapsedBar_Click(object sender, MouseButtonEventArgs e) => Expand();

        // ── Collapse / Expand ──
        private void Root_MouseEnter(object sender, MouseEventArgs e)
        {
            _collapseTimer?.Stop();
            if (_isCollapsed) Expand();
        }

        private void Root_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_vm.WindowMode != "Collapse") return;
            _collapseTimer?.Stop();
            _collapseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_vm.Settings.CollapseDelayMs)
            };
            _collapseTimer.Tick += (_, _) => { _collapseTimer!.Stop(); Collapse(); };
            _collapseTimer.Start();
        }

        private void Collapse()
        {
            if (_isCollapsed) return;
            _isCollapsed             = true;
            MainContent.Visibility   = Visibility.Collapsed;
            CollapsedBar.Visibility  = Visibility.Visible;
            _storedHeight            = Height;
            MinHeight                = 32;
            Height                   = 32;
        }

        private void Expand()
        {
            if (!_isCollapsed) return;
            _isCollapsed             = false;
            CollapsedBar.Visibility  = Visibility.Collapsed;
            MainContent.Visibility   = Visibility.Visible;
            MinHeight                = 240;
            Height                   = _storedHeight > 240 ? _storedHeight : _vm.Settings.WindowHeight;
        }

        public void ApplyWindowMode(string mode)
        {
            _collapseTimer?.Stop();
            _isCollapsed             = false;
            CollapsedBar.Visibility  = Visibility.Collapsed;
            MainContent.Visibility   = Visibility.Visible;
            MinHeight                = 240;
            Height                   = _vm.Settings.WindowHeight;

            if (mode == "Tray")
            {
                Hide();
            }
            // "Full" and "Collapse" both show window; Collapse enables hover behavior via Root_MouseLeave
        }

        // ── Persist bounds ──
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            var b = WindowState == WindowState.Normal
                ? new Rect(Left, Top, Width, Height)
                : RestoreBounds;
            _vm.PersistWindowBounds(b.Left, b.Top, b.Width, b.Height);
            base.OnClosing(e);
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T m) return m;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
