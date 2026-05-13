using CommunityToolkit.Mvvm.ComponentModel;

namespace DesktopTodo.Models;

/// <summary>
/// 标签模型，使用 CommunityToolkit 源生成器自动生成属性变更通知
/// </summary>
public partial class Tag : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    public int Id { get; set; }

    public override string ToString() => Name;
}
