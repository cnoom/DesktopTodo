using System;
using System.Diagnostics;
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
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] 解析背景颜色失败 '{colorHex}': {ex.Message}");
        }
    }

    [RelayCommand]
    private void SetTaskFontColor(string colorHex)
    {
        try
        {
            TaskFontColor = (Color)ColorConverter.ConvertFromString(colorHex);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] 解析任务字体颜色失败 '{colorHex}': {ex.Message}");
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settings.Save();
    }
}