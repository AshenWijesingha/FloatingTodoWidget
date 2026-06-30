using System;
using System.Windows;

namespace FloatingTodoWidget.Services
{
    /// <summary>Swaps the theme color dictionary at runtime (index 0 of merged dictionaries).</summary>
    public static class ThemeService
    {
        public static void Apply(bool dark)
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri(
                    $"pack://application:,,,/Resources/Theme.{(dark ? "Dark" : "Light")}.xaml",
                    UriKind.Absolute)
            };

            var merged = Application.Current.Resources.MergedDictionaries;
            if (merged.Count == 0) merged.Add(dict);
            else merged[0] = dict; // keep Styles.xaml (index 1) untouched
        }
    }
}
