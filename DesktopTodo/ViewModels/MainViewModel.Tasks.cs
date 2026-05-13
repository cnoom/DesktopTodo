using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using DesktopTodo.Models;

namespace DesktopTodo.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private async Task AddTopLevelTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle)) return;
        var task = new TodoTask
        {
            Title = NewTaskTitle.Trim(),
            CategoryId = SelectedCategory?.Id > 0 ? SelectedCategory.Id : null
        };
        task.Id = await _db.AddTaskAsync(task);
        var vm = new TaskItemViewModel(task, _db.UpdateTaskAsync);
        RootTasks.Add(vm);
        NewTaskTitle = string.Empty;
    }

    [RelayCommand]
    private async Task AddSubTaskAsync(TaskItemViewModel parent)
    {
        if (parent == null || string.IsNullOrWhiteSpace(NewTaskTitle)) return;
        var task = new TodoTask
        {
            Title = NewTaskTitle.Trim(),
            ParentTaskId = parent.Task.Id,
            CategoryId = parent.Task.CategoryId
        };
        task.Id = await _db.AddTaskAsync(task);
        var vm = new TaskItemViewModel(task, _db.UpdateTaskAsync);
        parent.Children.Add(vm);
        NewTaskTitle = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteTaskAsync(TaskItemViewModel? item)
    {
        if (item == null) return;
        var msg = $"确定删除「{item.Task.Title}」及其所有子任务？";
        if (_dialog.Confirm(msg, "确认"))
        {
            await _db.DeleteTaskAsync(item.Task.Id);
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

    public async void MoveTask(int taskId, int? newParentId, int newSortOrder) =>
        await _db.UpdateParentAndSortOrderAsync(taskId, newParentId, newSortOrder);

    public async void ReorderCollection(ObservableCollection<TaskItemViewModel> collection, int? parentId)
    {
        for (int i = 0; i < collection.Count; i++)
        {
            var task = collection[i].Task;
            if (task.ParentTaskId != parentId || task.SortOrder != i)
            {
                await _db.UpdateParentAndSortOrderAsync(task.Id, parentId, i);
                task.ParentTaskId = parentId;
                task.SortOrder = i;
            }
        }
    }

    public async void MoveTaskToCategory(TaskItemViewModel item, int? categoryId)
    {
        if (item == null) return;
        item.Task.CategoryId = categoryId;
        await _db.UpdateTaskAsync(item.Task);
        foreach (var child in item.Children)
            MoveTaskToCategory(child, categoryId);
    }
}
