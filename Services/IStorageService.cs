using System.Collections.Generic;
using FloatingTodoWidget.Models;

namespace FloatingTodoWidget.Services
{
    public interface IStorageService
    {
        List<TodoItem> LoadTasks();
        void SaveTasks(IEnumerable<TodoItem> tasks);

        AppSettings LoadSettings();
        void SaveSettings(AppSettings settings);
    }
}
