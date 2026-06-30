using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FloatingTodoWidget.Helpers;
using FloatingTodoWidget.Models;
using FloatingTodoWidget.ViewModels;

namespace FloatingTodoWidget
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow(MainViewModel vm)
        {
            _vm = vm;
            DataContext = _vm;
            InitializeComponent();

            // Restore saved bounds, clamped to the current virtual screen so the
            // window can't end up stranded off-screen after a monitor layout change.
            var screenLeft   = SystemParameters.VirtualScreenLeft;
            var screenTop    = SystemParameters.VirtualScreenTop;
            var screenRight  = screenLeft + SystemParameters.VirtualScreenWidth;
            var screenBottom = screenTop  + SystemParameters.VirtualScreenHeight;

            Width  = Math.Max(MinWidth,  Math.Min(vm.Settings.WindowWidth,  screenRight  - screenLeft));
            Height = Math.Max(MinHeight, Math.Min(vm.Settings.WindowHeight, screenBottom - screenTop));
            Left   = Math.Max(screenLeft, Math.Min(vm.Settings.WindowLeft, screenRight  - Width));
            Top    = Math.Max(screenTop,  Math.Min(vm.Settings.WindowTop,  screenBottom - Height));

            _vm.ExitRequested += (_, _) => Close();
            _vm.FocusInputRequested += (_, _) =>
            {
                InputBox.Focus();
                InputBox.SelectAll();
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Enable acrylic blur using the theme's tint. Falls back silently if unsupported.
            if (TryFindResource("AcrylicTint") is uint tint)
                NativeMethods.EnableAcrylic(this, tint);

            NativeMethods.SetClickThrough(this, _vm.Settings.ClickThrough);
        }

        /// <summary>Drag the window from anywhere on the background, except the text box.</summary>
        private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject d && FindAncestor<TextBox>(d) != null)
                return; // let users select text instead of dragging

            try { DragMove(); }
            catch { /* DragMove throws if the button is already released */ }
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e) =>
            _vm.IsDarkTheme = !_vm.IsDarkTheme;

        /// <summary>
        /// Handle Enter in the input box via PreviewKeyDown so we can mark the event
        /// as handled before it has any chance to propagate to other elements.
        /// This is the reliable fix for duplicate-add that can occur when the key
        /// event bubbles after a KeyBinding executes.
        /// </summary>
        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                _vm.AddTaskCommand.Execute(null);
                e.Handled = true; // stop the event here — prevents any second invocation
            }
        }

        /// <summary>Click the colored bar to cycle the task's priority.</summary>
        private void Priority_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TodoItem item)
            {
                _vm.CyclePriorityCommand.Execute(item);
                e.Handled = true; // don't trigger window drag
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Persist final size/position. Use RestoreBounds if minimized/maximized.
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
                if (current is T match) return match;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
