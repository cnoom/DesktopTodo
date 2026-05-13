using System.Collections.ObjectModel;
using DesktopTodo.ViewModels;

namespace DesktopTodo.Interfaces;

/// <summary>
/// 任务拖放操作接口，解耦 TreeViewDragBehavior 与 MainViewModel 的直接依赖
/// </summary>
public interface ITaskDragDropHandler
{
    /// <summary>根级任务集合</summary>
    ObservableCollection<TaskItemViewModel> RootTasks { get; }

    /// <summary>当前选中的任务</summary>
    TaskItemViewModel? SelectedTask { get; set; }

    /// <summary>将任务移动到指定分类</summary>
    void MoveTaskToCategory(TaskItemViewModel item, int? categoryId);

    /// <summary>刷新当前视图</summary>
    void RefreshCurrentView();

    /// <summary>重新排序集合并持久化</summary>
    void ReorderCollection(ObservableCollection<TaskItemViewModel> collection, int? parentId);
}
