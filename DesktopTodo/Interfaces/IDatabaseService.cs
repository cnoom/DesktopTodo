using System.Collections.Generic;
using System.Threading.Tasks;
using DesktopTodo.Models;

namespace DesktopTodo.Interfaces;

/// <summary>
/// 数据库服务接口，所有数据库操作均为异步
/// </summary>
public interface IDatabaseService
{
    // Tasks
    Task<List<TodoTask>> GetAllTasksAsync();
    Task<List<TodoTask>> GetTasksByCategoryAsync(int? categoryId);
    Task<List<TodoTask>> GetTasksByTagIdsAsync(HashSet<int> taskIds);
    Task<int> AddTaskAsync(TodoTask task);
    Task UpdateTaskAsync(TodoTask task);
    Task DeleteTaskAsync(int id);
    Task UpdateParentAndSortOrderAsync(int taskId, int? newParentId, int newSortOrder);

    // Tags
    Task<List<Tag>> GetAllTagsAsync();
    Task<int> AddTagAsync(string tagName);
    Task<List<Tag>> GetTagsForTaskAsync(int taskId);
    Task AddTagToTaskAsync(int taskId, int tagId);
    Task RemoveTagFromTaskAsync(int taskId, int tagId);
    Task<List<int>> GetTaskIdsWithTagAsync(int tagId);
    Task DeleteTagCascadeAsync(int tagId);

    // Categories
    Task<List<Category>> GetAllCategoriesAsync();
    Task<int> AddCategoryAsync(string name);
    Task DeleteCategoryAsync(int categoryId);
    Task ReorderCategoriesAsync(List<int> categoryIds);
}
