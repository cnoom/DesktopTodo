using System.Windows;
using DesktopTodo.Interfaces;

namespace DesktopTodo.Services;

/// <summary>
/// 基于 WPF MessageBox 的对话框服务实现
/// </summary>
public class DialogService : IDialogService
{
    public bool Confirm(string message, string title = "确认")
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public void ShowMessage(string message, string title = "提示")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
