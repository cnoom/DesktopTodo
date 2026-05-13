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

    /// <summary>
    /// 排序顺序，用于拖拽排序后持久化
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 是否为固定分类（全部/未分类），不参与拖拽排序
    /// </summary>
    public bool IsFixed => Id <= 0;

    public override string ToString() => Name;
}
