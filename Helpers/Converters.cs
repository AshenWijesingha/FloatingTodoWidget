using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FloatingTodoWidget.Models;

namespace FloatingTodoWidget.Helpers
{
    public sealed class PriorityToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is not Priority pr) return Brushes.Transparent;
            var color = pr switch
            {
                Priority.High   => Color.FromRgb(0xE0, 0x52, 0x60),
                Priority.Medium => Color.FromRgb(0xF0, 0xA0, 0x30),
                Priority.Low    => Color.FromRgb(0x4C, 0xAF, 0x50),
                _               => Colors.Transparent
            };
            return new SolidColorBrush(color);
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    /// <summary>TodoItem -> priority bar brush, overriding with red/amber for overdue/due-today.</summary>
    public sealed class DueAwarePriorityBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is not TodoItem item) return Brushes.Transparent;
            if (item.IsOverdue)   return new SolidColorBrush(Color.FromRgb(0xE0, 0x52, 0x60));
            if (item.IsDueToday)  return new SolidColorBrush(Color.FromRgb(0xF0, 0xA0, 0x30));
            return item.Priority switch
            {
                Priority.High   => new SolidColorBrush(Color.FromRgb(0xE0, 0x52, 0x60)),
                Priority.Medium => new SolidColorBrush(Color.FromRgb(0xF0, 0xA0, 0x30)),
                Priority.Low    => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                _               => Brushes.Transparent
            };
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is null ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            bool b = value is bool bv && bv;
            if (Invert) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is bool b && !b;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) =>
            v is bool b && !b;
    }

    public sealed class EmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class NonEmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class DueDateConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is not DateTime d) return string.Empty;
            if (d.Date < DateTime.Today) return $"Overdue · {d:MMM d}";
            if (d.Date == DateTime.Today) return $"Due today · {d:MMM d}";
            return $"Due {d:MMM d}";
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class DueDateForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is not TodoItem item || !item.DueDate.HasValue) return Brushes.Gray;
            if (item.IsOverdue)  return new SolidColorBrush(Color.FromRgb(0xE0, 0x52, 0x60));
            if (item.IsDueToday) return new SolidColorBrush(Color.FromRgb(0xF0, 0xA0, 0x30));
            return Brushes.Gray;
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is int n && n > 0 ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public sealed class HexColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is not string hex) return Brushes.Transparent;
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
            catch { return Brushes.Transparent; }
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }
}
