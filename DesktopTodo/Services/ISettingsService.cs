using System.Windows.Media;

namespace DesktopTodo.Services;

public interface ISettingsService
{
    double BackgroundOpacity { get; set; }
    Color BackgroundColor { get; set; }
    double FontSize { get; set; }
    Color TaskFontColor { get; set; }

    /// <summary>窗口左边缘位置（屏幕坐标）</summary>
    double WindowLeft { get; set; }
    /// <summary>窗口上边缘位置（屏幕坐标）</summary>
    double WindowTop { get; set; }
    /// <summary>窗口宽度</summary>
    double WindowWidth { get; set; }
    /// <summary>窗口高度</summary>
    double WindowHeight { get; set; }

    void Save();
    void Load();
}