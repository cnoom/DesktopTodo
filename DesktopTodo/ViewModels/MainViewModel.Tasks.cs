using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using DesktopTodo.Models;

namespace DesktopTodo.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private void AddTopLevelTask()
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle)) return;
        var task = new TodoTask
        {
            Title = NewTaskTitle.Trim(),
            CategoryId = SelectedCategory?.Id > 0 ? SelectedCategory.Id : null
        };
        task.Id = _db.AddTask(task);
        var vm = new TaskItemViewModel(task, _db.UpdateTask);
        RootTasks.Add(vm);
        NewTaskTitle = string.Empty;
    }

    [RelayCommand]
    private void AddSubTask(TaskItemViewModel parent)
    {
        if (parent == null || string.IsNullOrWhiteSpace(NewTaskTitle)) return;
        var task = new TodoTask
        {
            Title = NewTaskTitle.Trim(),
            ParentTaskId = parent.Task.Id,
            CategoryId = parent.Task.CategoryId
        };
        task.Id = _db.AddTask(task);
        var vm = new TaskItemViewModel(task, _db.UpdateTask);
        parent.Children.Add(vm);
        NewTaskTitle = string.Empty;
    }

    [RelayCommand]
    private void DeleteTask(TaskItemViewModel? item)
    {
        if (item == null) return;
        var msg = $"确定删除「{item.Task.Title}」及其所有子任务？";
        if (MessageBox.Show(msg, "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            _db.DeleteTask(item.Task.Id);
            RemoveFromTree(item);
        }
    }

    private void RemoveFromTree(TaskItemViewModel item)
    {
        if (RootTasks.Contains(item)) { RootTasks.Remove(item); return; }
        bool TryRemove(ObservableCollection<TaskItemViewModel> collection)
        {
            foreach (var child in collection)
            {
                if (child == item) { collection.Remove(child); return true; }
                if (TryRemove(child.Children)) return true;
            }
            return false;
        }
        foreach (var root in RootTasks) { if (TryRemove(root.Children)) break; }
    }

    public void MoveTask(int taskId, int? newParentId, int newSortOrder) =>
        _db.UpdateParentAndSortOrder(taskId, newParentId, newSortOrder);

    public void ReorderCollection(ObservableCollection<TaskItemViewModel> collection, int? parentId)
    {
        for (int i = 0; i < collection.Count; i++)
        {
            var task = collection[i].Task;
            if (task.ParentTaskId != parentId || task.SortOrder != i)
            {
                _db.UpdateParentAndSortOrder(task.Id, parentId, i);
                task.ParentTaskId = parentId;
                task.SortOrder = i;
            }
        }
    }

    public void MoveTaskToCategory(TaskItemViewModel item, int? categoryId)
    {
        if (item == null) return;
        item.Task.CategoryId = categoryId;
        _db.UpdateTask(item.Task);
        foreach (var child in item.Children)
            MoveTaskToCategory(child, categoryId);
    }
}