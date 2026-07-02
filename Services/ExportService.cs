using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FloatingTodoWidget.Models;

namespace FloatingTodoWidget.Services
{
    public static class ExportService
    {
        public static void ExportText(IEnumerable<TodoItem> tasks, IEnumerable<Project> projects, string path)
        {
            var projectNames = projects.ToDictionary(p => p.Id, p => p.Name);
            var sb = new StringBuilder();

            foreach (var t in tasks.OrderBy(t => t.IsCompleted).ThenByDescending(t => t.Priority))
            {
                var box     = t.IsCompleted ? "[x]" : "[ ]";
                var project = t.ProjectId.HasValue && projectNames.TryGetValue(t.ProjectId.Value, out var pn) ? pn : "Inbox";
                var due     = t.DueDate.HasValue ? $" (due {t.DueDate:yyyy-MM-dd})" : "";
                sb.AppendLine($"{box} [{project}] {t.Title}{due}");

                if (!string.IsNullOrWhiteSpace(t.Notes))
                    sb.AppendLine($"    Notes: {t.Notes}");

                foreach (var sub in t.SubTasks)
                    sb.AppendLine($"    {(sub.IsCompleted ? "[x]" : "[ ]")} {sub.Title}");
            }

            File.WriteAllText(path, sb.ToString());
        }

        public static void ExportCsv(IEnumerable<TodoItem> tasks, IEnumerable<Project> projects, string path)
        {
            var projectNames = projects.ToDictionary(p => p.Id, p => p.Name);
            var sb = new StringBuilder();
            sb.AppendLine("Title,Completed,Priority,DueDate,Project,Notes");

            foreach (var t in tasks)
            {
                var project = t.ProjectId.HasValue && projectNames.TryGetValue(t.ProjectId.Value, out var pn) ? pn : "Inbox";
                var due     = t.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty;
                sb.AppendLine(string.Join(",",
                    Csv(t.Title), t.IsCompleted.ToString(), t.Priority.ToString(), Csv(due), Csv(project), Csv(t.Notes)));
            }

            File.WriteAllText(path, sb.ToString());
        }

        private static string Csv(string? value)
        {
            value ??= string.Empty;
            return value.Contains(',') || value.Contains('"') || value.Contains('\n')
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }
}
