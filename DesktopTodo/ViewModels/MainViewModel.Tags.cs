using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using DesktopTodo.Models;

namespace DesktopTodo.ViewModels;

public partial class MainViewModel
{
    public async void LoadAllTags() => AllTags = new ObservableCollection<Tag>(await _db.GetAllTagsAsync());

    public async void LoadTaskTags(TaskItemViewModel vm)
    {
        vm.Tags.Clear();
        foreach (var tag in await _db.GetTagsForTaskAsync(vm.Task.Id))
            vm.Tags.Add(tag);
    }

    public async void AddTagToSelectedTask(string tagName)
    {
        if (SelectedTask == null || string.IsNullOrWhiteSpace(tagName)) return;
        var trimmed = tagName.Trim();
        int tagId = await _db.AddTagAsync(trimmed);
        await _db.AddTagToTaskAsync(SelectedTask.Task.Id, tagId);
        LoadTaskTags(SelectedTask);
        if (AllTags.All(t => t.Name != trimmed))
            AllTags.Add(new Tag { Id = tagId, Name = trimmed });
        OnPropertyChanged(nameof(RootTasks));
    }

    public async void RemoveTagFromTask(TaskItemViewModel vm, Tag tag)
    {
        await _db.RemoveTagFromTaskAsync(vm.Task.Id, tag.Id);
        LoadTaskTags(vm);
        var remainingTasks = await _db.GetTaskIdsWithTagAsync(tag.Id);
        if (remainingTasks.Count == 0)
        {
            await _db.DeleteTagCascadeAsync(tag.Id);
            LoadAllTags();
            if (SelectedFilterTag != null && SelectedFilterTag.Name == tag.Name)
            {
                SelectedFilterTag = null;
                RefreshCurrentView();
            }
        }
        OnPropertyChanged(nameof(RootTasks));
    }

    public async void DeleteTag(Tag tag)
    {
        await _db.DeleteTagCascadeAsync(tag.Id);
        LoadAllTags();
        if (SelectedFilterTag != null && SelectedFilterTag.Name == tag.Name)
        {
            SelectedFilterTag = null;
            RefreshCurrentView();
        }
        else RefreshCurrentView();
    }

    [RelayCommand]
    private async Task ToggleFilterByTagAsync(Tag? tag)
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
            await FilterTasksByTagAsync(tag.Id);
        }
    }

    private async Task FilterTasksByTagAsync(int tagId)
    {
        var ids = (await _db.GetTaskIdsWithTagAsync(tagId)).ToHashSet();
        BuildTreeFromList(await _db.GetTasksByTagIdsAsync(ids));
    }
}
