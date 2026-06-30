using FloatingTodoWidget.Models;

namespace FloatingTodoWidget.Services
{
    public interface IStorageService
    {
        AppData LoadData();
        void SaveData(AppData data);
        AppSettings LoadSettings();
        void SaveSettings(AppSettings settings);
    }
}
