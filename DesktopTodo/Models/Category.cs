using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DesktopTodo.Models;

public class Category : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private bool _isSelected;
    private int _taskCount;

    public int Id { get; set; }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 该分类下的任务数量（"全部" 显示总任务数）
    /// </summary>
    public int TaskCount
    {
        get => _taskCount;
        set { _taskCount = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() => Name;
}