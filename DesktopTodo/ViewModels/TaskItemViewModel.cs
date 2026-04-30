using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DesktopTodo.Models;

namespace DesktopTodo.ViewModels;

public partial class TaskItemViewModel : ObservableObject
{
    [ObservableProperty]
    private TodoTask _task;

    [ObservableProperty]
    private bool _isExpanded = true;

    public ObservableCollection<TaskItemViewModel> Children { get; } = new();
    public ObservableCollection<Tag> Tags { get; } = new();

    private readonly Action<TodoTask>? _saveAction;

    public TaskItemViewModel(TodoTask task, Action<TodoTask>? saveAction = null)
    {
        _task = task;
        _saveAction = saveAction;

        if (_saveAction != null)
        {
            task.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName is nameof(TodoTask.Title) or nameof(TodoTask.IsCompleted)
                    or nameof(TodoTask.Description) or nameof(TodoTask.Color) or nameof(TodoTask.CategoryId))
                    _saveAction(task);
            };
        }
    }
}