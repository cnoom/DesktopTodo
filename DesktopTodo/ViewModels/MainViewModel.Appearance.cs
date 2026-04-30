using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;

namespace DesktopTodo.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private void SetColor(string colorHex)
    {
        try
        {
            BackgroundColor = (Color)ColorConverter.ConvertFromString(colorHex);
        }
        catch { }
    }

    [RelayCommand]
    private void SetTaskFontColor(string colorHex)
    {
        try
        {
            TaskFontColor = (Color)ColorConverter.ConvertFromString(colorHex);
        }
        catch { }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settings.Save();
    }
}