using System.Collections.Generic;
using DesktopTodo.Models;

namespace DesktopTodo.Interfaces;

public interface IDatabaseService
{
    // Tasks
    List<TodoTask> GetAllTasks();
    int AddTask(TodoTask task);
    void UpdateTask(TodoTask task);
    void DeleteTask(int id);
    void UpdateParentAndSortOrder(int taskId, int? newParentId, int newSortOrder);

    // Tags
    List<Tag> GetAllTags();
    int AddTag(string tagName);
    List<Tag> GetTagsForTask(int taskId);
    void AddTagToTask(int taskId, int tagId);
    void RemoveTagFromTask(int taskId, int tagId);
    List<int> GetTaskIdsWithTag(int tagId);
    void DeleteTagCascade(int tagId);

    // Categories
    List<Category> GetAllCategories();
    int AddCategory(string name);
    void DeleteCategory(int categoryId);
}