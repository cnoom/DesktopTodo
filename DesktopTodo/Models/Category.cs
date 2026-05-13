using CommunityToolkit.Mvvm.ComponentModel;

namespace DesktopTodo.Models;

/// <summary>
/// 任务分类模型，使用 CommunityToolkit 源生成器自动生成属性变更通知
/// </summary>
public partial class Category : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// 该分类下的任务数量（"全部" 显示总任务数）
    /// </summary>
    [ObservableProperty]
    private int _taskCount;

    public int Id { get; set; }

    public override string ToString() => Name;
}
