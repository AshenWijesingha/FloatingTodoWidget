using System;
using System.Globalization;
using System.Text.RegularExpressions;
using FloatingTodoWidget.Models;

namespace FloatingTodoWidget.Helpers
{
    /// <summary>
    /// Lightweight quick-add syntax:
    ///   "Buy milk !high @2026-07-05"
    ///   tokens:  !low/!med/!high (or !l/!m/!h)  and  @yyyy-MM-dd
    /// </summary>
    public static class QuickAddParser
    {
        private static readonly Regex PriorityRx =
            new(@"(?:^|\s)!(high|h|med|m|medium|low|l)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DateRx =
            new(@"(?:^|\s)@(\d{4}-\d{2}-\d{2})\b", RegexOptions.Compiled);

        public static (string title, Priority priority, DateTime? due) Parse(string raw)
        {
            var priority = Priority.None;
            DateTime? due = null;
            var text = raw;

            var p = PriorityRx.Match(text);
            if (p.Success)
            {
                priority = p.Groups[1].Value.ToLowerInvariant() switch
                {
                    "high" or "h" => Priority.High,
                    "med" or "m" or "medium" => Priority.Medium,
                    "low" or "l" => Priority.Low,
                    _ => Priority.None
                };
                text = text.Remove(p.Index, p.Length);
            }

            var d = DateRx.Match(text);
            if (d.Success &&
                DateTime.TryParseExact(d.Groups[1].Value, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                due = parsed;
                text = text.Remove(d.Index, d.Length);
            }

            return (text.Trim(), priority, due);
        }
    }
}
