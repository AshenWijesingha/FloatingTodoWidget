using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FloatingTodoWidget.Models;

namespace FloatingTodoWidget.Helpers
{
    /// <summary>Priority -> colored brush for the left indicator bar.</summary>
    public sealed class PriorityToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            var color = value is Priority pr ? pr switch
            {
                Priority.High => Color.FromRgb(0xE0, 0x52, 0x60),   // red
                Priority.Medium => Color.FromRgb(0xF0, 0xA0, 0x30), // amber
                Priority.Low => Color.FromRgb(0x4C, 0xAF, 0x50),    // green
                _ => Colors.Transparent
            } : Colors.Transparent;

            return new SolidColorBrush(color);
        }

        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    /// <summary>null/empty due date -> Collapsed.</summary>
    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is null ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    /// <summary>DateTime? -> short display string ("Due Jul 5").</summary>
    public sealed class DueDateConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is DateTime d ? $"Due {d:MMM d}" : string.Empty;

        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    /// <summary>Empty/null string -> Visible (shows placeholder); non-empty -> Collapsed.</summary>
    public sealed class EmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }
}
