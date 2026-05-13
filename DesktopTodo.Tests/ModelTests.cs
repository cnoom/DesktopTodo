using DesktopTodo.Models;

namespace DesktopTodo.Tests;

/// <summary>
/// Model 属性变更通知测试
/// </summary>
public class ModelTests
{
    [Fact]
    public void TodoTask_Title变更_触发PropertyChanged()
    {
        var task = new TodoTask();
        bool fired = false;
        task.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TodoTask.Title))
                fired = true;
        };

        task.Title = "新标题";
        Assert.True(fired);
    }

    [Fact]
    public void TodoTask_IsCompleted变更_触发PropertyChanged()
    {
        var task = new TodoTask();
        bool fired = false;
        task.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TodoTask.IsCompleted))
                fired = true;
        };

        task.IsCompleted = true;
        Assert.True(fired);
    }

    [Fact]
    public void TodoTask_Color变更_触发PropertyChanged()
    {
        var task = new TodoTask();
        bool fired = false;
        task.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TodoTask.Color))
                fired = true;
        };

        task.Color = "#FF5733";
        Assert.True(fired);
    }

    [Fact]
    public void Category_Name变更_触发PropertyChanged()
    {
        var category = new Category();
        bool fired = false;
        category.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Category.Name))
                fired = true;
        };

        category.Name = "新分类";
        Assert.True(fired);
    }

    [Fact]
    public void Tag_Name变更_触发PropertyChanged()
    {
        var tag = new Tag();
        bool fired = false;
        tag.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Tag.Name))
                fired = true;
        };

        tag.Name = "新标签";
        Assert.True(fired);
    }

    [Fact]
    public void Tag_IsSelected变更_触发PropertyChanged()
    {
        var tag = new Tag();
        bool fired = false;
        tag.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Tag.IsSelected))
                fired = true;
        };

        tag.IsSelected = true;
        Assert.True(fired);
    }

    [Fact]
    public void TodoTask_默认值_正确()
    {
        var task = new TodoTask();
        Assert.Equal(string.Empty, task.Title);
        Assert.False(task.IsCompleted);
        Assert.Null(task.Color);
        Assert.Null(task.ParentTaskId);
        Assert.Null(task.CategoryId);
        Assert.Equal(string.Empty, task.Description);
    }

    [Fact]
    public void Category_默认值_正确()
    {
        var category = new Category();
        Assert.Equal(string.Empty, category.Name);
        Assert.Equal(0, category.Id);
    }

    [Fact]
    public void Tag_默认值_正确()
    {
        var tag = new Tag();
        Assert.Equal(string.Empty, tag.Name);
        Assert.Equal(0, tag.Id);
        Assert.False(tag.IsSelected);
    }
}
