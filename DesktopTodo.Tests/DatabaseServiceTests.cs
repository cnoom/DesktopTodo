using DesktopTodo.Interfaces;
using DesktopTodo.Models;
using DesktopTodo.Services;

namespace DesktopTodo.Tests;

/// <summary>
/// 数据库服务单元测试
/// 使用临时文件路径，每个测试独立隔离
/// </summary>
public class DatabaseServiceTests : IDisposable
{
    private readonly IDatabaseService _db;
    private readonly string _dbPath;

    public DatabaseServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
        _db = new DatabaseService(_dbPath);
    }

    public void Dispose()
    {
        (_db as IDisposable)?.Dispose();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    #region 任务 CRUD 测试

    [Fact]
    public async Task GetAllTasksAsync_空数据库_返回空列表()
    {
        var tasks = await _db.GetAllTasksAsync();
        Assert.Empty(tasks);
    }

    [Fact]
    public async Task AddTaskAsync_添加任务_成功返回ID()
    {
        var id = await _db.AddTaskAsync(new TodoTask { Title = "测试任务" });
        Assert.True(id > 0);

        var all = await _db.GetAllTasksAsync();
        Assert.Single(all);
        Assert.Equal("测试任务", all[0].Title);
    }

    [Fact]
    public async Task UpdateTaskAsync_更新标题_成功保存()
    {
        var id = await _db.AddTaskAsync(new TodoTask { Title = "原始标题" });
        var tasks = await _db.GetAllTasksAsync();
        var task = tasks.First(t => t.Id == id);

        task.Title = "更新后的标题";
        await _db.UpdateTaskAsync(task);

        var all = await _db.GetAllTasksAsync();
        Assert.Equal("更新后的标题", all[0].Title);
    }

    [Fact]
    public async Task DeleteTaskAsync_删除任务_成功移除()
    {
        var id = await _db.AddTaskAsync(new TodoTask { Title = "待删除" });
        await _db.DeleteTaskAsync(id);

        var all = await _db.GetAllTasksAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task AddTaskAsync_带分类和颜色_正确保存()
    {
        var catId = await _db.AddCategoryAsync("测试分类");
        var id = await _db.AddTaskAsync(new TodoTask
        {
            Title = "带颜色任务",
            Color = "#FF5733",
            CategoryId = catId
        });

        var all = await _db.GetAllTasksAsync();
        Assert.Equal("#FF5733", all[0].Color);
        Assert.Equal(catId, all[0].CategoryId);
    }

    [Fact]
    public async Task UpdateParentAndSortOrderAsync_更新排序_成功()
    {
        var id1 = await _db.AddTaskAsync(new TodoTask { Title = "任务1" });
        var id2 = await _db.AddTaskAsync(new TodoTask { Title = "任务2" });

        await _db.UpdateParentAndSortOrderAsync(id2, null, 0);
        await _db.UpdateParentAndSortOrderAsync(id1, null, 1);

        var all = await _db.GetAllTasksAsync();
        Assert.Equal(id2, all[0].Id);
        Assert.Equal(id1, all[1].Id);
    }

    [Fact]
    public async Task GetTasksByCategoryAsync_按分类过滤_返回正确结果()
    {
        var catId = await _db.AddCategoryAsync("工作");

        await _db.AddTaskAsync(new TodoTask { Title = "工作1", CategoryId = catId });
        await _db.AddTaskAsync(new TodoTask { Title = "个人" });

        var workTasks = await _db.GetTasksByCategoryAsync(catId);
        Assert.Single(workTasks);
        Assert.Equal("工作1", workTasks[0].Title);
    }

    [Fact]
    public async Task GetTasksByCategoryAsync_null分类_返回未分类任务()
    {
        await _db.AddTaskAsync(new TodoTask { Title = "未分类" });
        var catId = await _db.AddCategoryAsync("分类");
        await _db.AddTaskAsync(new TodoTask { Title = "已分类", CategoryId = catId });

        var uncat = await _db.GetTasksByCategoryAsync(null);
        Assert.Single(uncat);
        Assert.Equal("未分类", uncat[0].Title);
    }

    #endregion

    #region 分类 CRUD 测试

    [Fact]
    public async Task GetAllCategoriesAsync_空数据库_返回空列表()
    {
        var categories = await _db.GetAllCategoriesAsync();
        Assert.Empty(categories);
    }

    [Fact]
    public async Task AddCategoryAsync_添加分类_成功返回ID()
    {
        var id = await _db.AddCategoryAsync("工作");
        Assert.True(id > 0);

        var all = await _db.GetAllCategoriesAsync();
        Assert.Single(all);
        Assert.Equal("工作", all[0].Name);
    }

    [Fact]
    public async Task DeleteCategoryAsync_删除分类_任务分类置空()
    {
        var catId = await _db.AddCategoryAsync("待删除");
        await _db.AddTaskAsync(new TodoTask { Title = "分类任务", CategoryId = catId });

        await _db.DeleteCategoryAsync(catId);

        var categories = await _db.GetAllCategoriesAsync();
        Assert.Empty(categories);

        var tasks = await _db.GetAllTasksAsync();
        Assert.Null(tasks[0].CategoryId);
    }

    #endregion

    #region 标签 CRUD 测试

    [Fact]
    public async Task GetAllTagsAsync_空数据库_返回空列表()
    {
        var tags = await _db.GetAllTagsAsync();
        Assert.Empty(tags);
    }

    [Fact]
    public async Task AddTagAsync_添加标签_成功返回ID()
    {
        var id = await _db.AddTagAsync("重要");
        Assert.True(id > 0);

        var all = await _db.GetAllTagsAsync();
        Assert.Single(all);
        Assert.Equal("重要", all[0].Name);
    }

    [Fact]
    public async Task DeleteTagCascadeAsync_级联删除_解除任务关联()
    {
        var tagId = await _db.AddTagAsync("待删除");
        var taskId = await _db.AddTaskAsync(new TodoTask { Title = "任务" });
        await _db.AddTagToTaskAsync(taskId, tagId);

        await _db.DeleteTagCascadeAsync(tagId);

        var tags = await _db.GetAllTagsAsync();
        Assert.Empty(tags);

        var taskTags = await _db.GetTagsForTaskAsync(taskId);
        Assert.Empty(taskTags);
    }

    #endregion

    #region 任务-标签关联测试

    [Fact]
    public async Task AddTagToTaskAsync_添加关联_成功查询()
    {
        var taskId = await _db.AddTaskAsync(new TodoTask { Title = "任务" });
        var tagId = await _db.AddTagAsync("标签1");

        await _db.AddTagToTaskAsync(taskId, tagId);

        var tags = await _db.GetTagsForTaskAsync(taskId);
        Assert.Single(tags);
        Assert.Equal("标签1", tags[0].Name);
    }

    [Fact]
    public async Task RemoveTagFromTaskAsync_移除关联_成功解除()
    {
        var taskId = await _db.AddTaskAsync(new TodoTask { Title = "任务" });
        var tagId = await _db.AddTagAsync("标签1");
        await _db.AddTagToTaskAsync(taskId, tagId);

        await _db.RemoveTagFromTaskAsync(taskId, tagId);

        var tags = await _db.GetTagsForTaskAsync(taskId);
        Assert.Empty(tags);
    }

    [Fact]
    public async Task GetTagsForTaskAsync_多个标签_全部返回()
    {
        var taskId = await _db.AddTaskAsync(new TodoTask { Title = "多标签任务" });

        for (int i = 1; i <= 3; i++)
        {
            var tagId = await _db.AddTagAsync($"标签{i}");
            await _db.AddTagToTaskAsync(taskId, tagId);
        }

        var tags = await _db.GetTagsForTaskAsync(taskId);
        Assert.Equal(3, tags.Count);
    }

    [Fact]
    public async Task GetTaskIdsWithTagAsync_按标签过滤任务ID()
    {
        var tagId = await _db.AddTagAsync("重要");
        var taskId1 = await _db.AddTaskAsync(new TodoTask { Title = "任务1" });
        var taskId2 = await _db.AddTaskAsync(new TodoTask { Title = "任务2" });

        await _db.AddTagToTaskAsync(taskId1, tagId);

        var ids = await _db.GetTaskIdsWithTagAsync(tagId);
        Assert.Single(ids);
        Assert.Equal(taskId1, ids[0]);
    }

    #endregion
}
