using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using DesktopTodo.Models;

namespace DesktopTodo.ViewModels;

public partial class MainViewModel
{
    public void LoadAllTags() => AllTags = new ObservableCollection<Tag>(_db.GetAllTags());

    public void LoadTaskTags(TaskItemViewModel vm)
    {
        vm.Tags.Clear();
        foreach (var tag in _db.GetTagsForTask(vm.Task.Id))
            vm.Tags.Add(tag);
    }

    public void AddTagToSelectedTask(string tagName)
    {
        if (SelectedTask == null || string.IsNullOrWhiteSpace(tagName)) return;
        var trimmed = tagName.Trim();
        int tagId = _db.AddTag(trimmed);
        _db.AddTagToTask(SelectedTask.Task.Id, tagId);
        LoadTaskTags(SelectedTask);
        if (AllTags.All(t => t.Name != trimmed))
            AllTags.Add(new Tag { Id = tagId, Name = trimmed });
        OnPropertyChanged(nameof(RootTasks));
    }

    public void RemoveTagFromTask(TaskItemViewModel vm, Tag tag)
    {
        _db.RemoveTagFromTask(vm.Task.Id, tag.Id);
        LoadTaskTags(vm);
        var remainingTasks = _db.GetTaskIdsWithTag(tag.Id);
        if (remainingTasks.Count == 0)
        {
            _db.DeleteTagCascade(tag.Id);
            LoadAllTags();
            if (SelectedFilterTag != null && SelectedFilterTag.Name == tag.Name)
            {
                SelectedFilterTag = null;
                RefreshCurrentView();
            }
        }
        OnPropertyChanged(nameof(RootTasks));
    }

    public void DeleteTag(Tag tag)
    {
        _db.DeleteTagCascade(tag.Id);
        LoadAllTags();
        if (SelectedFilterTag != null && SelectedFilterTag.Name == tag.Name)
        {
            SelectedFilterTag = null;
            RefreshCurrentView();
        }
        else RefreshCurrentView();
    }

    [RelayCommand]
    private void ToggleFilterByTag(Tag? tag)
    {
        if (tag == null) return;
        foreach (var t in AllTags) t.IsSelected = false;
        if (SelectedFilterTag == tag)
        {
            SelectedFilterTag = null;
            RefreshCurrentView();
        }
        else
        {
            SelectedFilterTag = tag;
            tag.IsSelected = true;
            FilterTasksByTag(tag.Id);
        }
    }

    private void FilterTasksByTag(int tagId) => FilterTasksByIds(_db.GetTaskIdsWithTag(tagId).ToHashSet());

    private void FilterTasksByIds(HashSet<int> allowedIds) =>
        BuildTreeFromList(_db.GetTasksByTagIds(allowedIds));
}