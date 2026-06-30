using System.Windows;

namespace FloatingTodoWidget.Services
{
    public static class ThemeService
    {
        public static void Apply(bool dark)
        {
            var src = dark
                ? "Resources/Theme.Dark.xaml"
                : "Resources/Theme.Light.xaml";

            var dict = new ResourceDictionary
            {
                Source = new System.Uri(src, System.UriKind.Relative)
            };

            var merged = Application.Current.Resources.MergedDictionaries;
            if (merged.Count > 0)
                merged[0] = dict;
            else
                merged.Add(dict);
        }
    }
}
