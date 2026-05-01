namespace DesktopTodo.Interfaces;

/// <summary>
/// 对话框服务抽象，使 ViewModel 不直接依赖 UI 层
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// 显示确认对话框，返回用户是否点击"是"
    /// </summary>
    bool Confirm(string message, string title = "确认");

    /// <summary>
    /// 显示信息提示
    /// </summary>
    void ShowMessage(string message, string title = "提示");
}
