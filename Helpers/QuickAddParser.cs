using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using FloatingTodoWidget.Models;

namespace FloatingTodoWidget.Helpers
{
    public record ParseResult(
        string Title,
        Priority Priority,
        DateTime? DueDate,
        int? NotifyMinutesBefore,
        string? ProjectName,
        string[] TagNames,
        string Note,
        string[] Links
    );

    public static class QuickAddParser
    {
        // !high !med !low (and abbreviations)
        private static readonly Regex PriorityRx =
            new(@"(?<!\S)!(high|h|med|m|medium|low|l)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // @2026-07-05 or @today or @tomorrow
        private static readonly Regex DateRx =
            new(@"(?<!\S)@(today|tomorrow|\d{4}-\d{2}-\d{2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // @notify:30m or @notify:2h
        private static readonly Regex NotifyRx =
            new(@"(?<!\S)@notify:(\d+)(m|h)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // #projectname
        private static readonly Regex ProjectRx =
            new(@"(?<!\S)#([\w\-]+)", RegexOptions.Compiled);

        // ~tag or ~tag1,tag2,tag3
        private static readonly Regex TagRx =
            new(@"(?<!\S)~([\w\-,]+)", RegexOptions.Compiled);

        // "quoted note"
        private static readonly Regex NoteRx =
            new(@"""([^""]*)""", RegexOptions.Compiled);

        // bare URL
        private static readonly Regex UrlRx =
            new(@"https?://\S+", RegexOptions.Compiled);

        public static ParseResult Parse(string raw)
        {
            var text = raw ?? string.Empty;
            var priority = Priority.None;
            DateTime? dueDate = null;
            int? notifyMinutes = null;
            string? projectName = null;
            var tagNames = new List<string>();
            var note = string.Empty;
            var links = new List<string>();

            // Extract URLs first (greedy, before other tokens eat text)
            foreach (Match m in UrlRx.Matches(text))
                links.Add(m.Value.TrimEnd('.', ',', ')'));
            text = UrlRx.Replace(text, " ");

            // Extract quoted note
            var nm = NoteRx.Match(text);
            if (nm.Success) { note = nm.Groups[1].Value.Trim(); text = text.Remove(nm.Index, nm.Length); }

            // Extract @notify before @date (both start with @)
            var notm = NotifyRx.Match(text);
            if (notm.Success)
            {
                int val = int.Parse(notm.Groups[1].Value);
                notifyMinutes = notm.Groups[2].Value.ToLower() == "h" ? val * 60 : val;
                text = text.Remove(notm.Index, notm.Length);
            }

            // Extract date
            var dm = DateRx.Match(text);
            if (dm.Success)
            {
                var ds = dm.Groups[1].Value.ToLower();
                dueDate = ds switch
                {
                    "today"    => DateTime.Today,
                    "tomorrow" => DateTime.Today.AddDays(1),
                    _ => DateTime.TryParseExact(ds, "yyyy-MM-dd",
                             CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateTime?)null
                };
                text = text.Remove(dm.Index, dm.Length);
            }

            // Extract priority
            var pm = PriorityRx.Match(text);
            if (pm.Success)
            {
                priority = pm.Groups[1].Value.ToLower() switch
                {
                    "high" or "h"            => Priority.High,
                    "med"  or "m" or "medium" => Priority.Medium,
                    "low"  or "l"            => Priority.Low,
                    _                        => Priority.None
                };
                text = text.Remove(pm.Index, pm.Length);
            }

            // Extract project
            var prm = ProjectRx.Match(text);
            if (prm.Success) { projectName = prm.Groups[1].Value; text = text.Remove(prm.Index, prm.Length); }

            // Extract tags
            var trm = TagRx.Match(text);
            if (trm.Success)
            {
                tagNames.AddRange(trm.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries));
                text = text.Remove(trm.Index, trm.Length);
            }

            return new ParseResult(
                Title: Regex.Replace(text, @"\s{2,}", " ").Trim(),
                Priority: priority,
                DueDate: dueDate,
                NotifyMinutesBefore: notifyMinutes,
                ProjectName: projectName,
                TagNames: tagNames.ToArray(),
                Note: note,
                Links: links.ToArray()
            );
        }
    }
}
