using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
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

    private readonly Func<TodoTask, Task>? _saveAction;

    public TaskItemViewModel(TodoTask task, Func<TodoTask, Task>? saveAction = null)
    {
        _task = task;
        _saveAction = saveAction;

        if (_saveAction != null)
        {
            task.PropertyChanged += async (s, e) =>
            {
                if (e.PropertyName is nameof(TodoTask.Title) or nameof(TodoTask.IsCompleted)
                    or nameof(TodoTask.Description) or nameof(TodoTask.Color) or nameof(TodoTask.CategoryId))
                {
                    await _saveAction(task);
                }
            };
        }
    }
}