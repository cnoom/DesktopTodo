using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DesktopTodo.Models;

public class TodoTask : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _description = string.Empty;
    private bool _isCompleted;
    private string? _color;
    private int? _categoryId;

    public int Id { get; set; }

    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set { _isCompleted = value; OnPropertyChanged(); }
    }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int? ParentTaskId { get; set; }

    public string? Color
    {
        get => _color;
        set { _color = value; OnPropertyChanged(); }
    }

    public int SortOrder { get; set; }

    public int? CategoryId
    {
        get => _categoryId;
        set { _categoryId = value; OnPropertyChanged(); }
    }

    public ObservableCollection<TodoTask> SubTasks { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}