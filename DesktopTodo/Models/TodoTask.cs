using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DesktopTodo.Models;

/// <summary>
/// 待办任务模型，使用 CommunityToolkit 源生成器自动生成属性变更通知
/// </summary>
public partial class TodoTask : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private string? _color;

    [ObservableProperty]
    private int? _categoryId;

    public int Id { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public int? ParentTaskId { get; set; }

    public int SortOrder { get; set; }

    public ObservableCollection<TodoTask> SubTasks { get; set; } = new();
}
