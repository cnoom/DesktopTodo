using System.Windows.Media;

namespace DesktopTodo.Services;

public interface ISettingsService
{
    double BackgroundOpacity { get; set; }
    Color BackgroundColor { get; set; }
    double FontSize { get; set; }
    Color TaskFontColor { get; set; }
    void Save();
    void Load();
}