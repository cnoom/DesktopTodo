using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using DesktopTodo.Models;

namespace DesktopTodo.ViewModels;

public partial class MainViewModel
{
    private Category UncategorizedCategory => Categories.FirstOrDefault(c => c.Id == -1)
                                              ?? new Category { Id = -1, Name = "未分类" };

    public void LoadCategories()
    {
        Categories.Clear();
        Categories.Add(new Category { Id = 0, Name = "全部" });
        Categories.Add(new Category { Id = -1, Name = "未分类" });
        foreach (var cat in _db.GetAllCategories())
            Categories.Add(cat);
    }

    [RelayCommand]
    private void SelectCategory(Category? cat)
    {
        if (cat == null) return;
        foreach (var c in Categories) c.IsSelected = (c == cat);
        SelectedCategory = cat;
        RefreshCurrentView();
    }

    [RelayCommand]
    private void AddCategory(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        int id = _db.AddCategory(name.Trim());
        var newCat = new Category { Id = id, Name = name.Trim() };
        int index = Categories.IndexOf(UncategorizedCategory) + 1;
        Categories.Insert(index, newCat);
        SelectCategoryCommand.Execute(newCat);
    }

    [RelayCommand]
    private void DeleteCategory(Category cat)
    {
        if (cat == null || cat.Id <= 0) return;
        if (MessageBox.Show($"确定删除分类“{cat.Name}”吗？相关任务将变为未分类。", "删除分类",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _db.DeleteCategory(cat.Id);
            Categories.Remove(cat);
            SelectedCategory = Categories.First(c => c.Id == 0);
            SelectedCategory.IsSelected = true;
            RefreshCurrentView();
        }
    }
}