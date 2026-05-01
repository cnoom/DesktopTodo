using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopTodo.Models;
using DesktopTodo.ViewModels;

namespace DesktopTodo.Helpers;

/// <summary>
/// 可视化树查找的公共辅助方法，消除代码重复
/// </summary>
public static class VisualTreeHelpers
{
    /// <summary>
    /// 沿可视化树向上查找指定类型的祖先
    /// </summary>
    public static T? FindAncestorOfType<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T t) return t;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    /// <summary>
    /// 沿可视化树向上查找 DataContext 为 Category 的 Border
    /// </summary>
    public static Border? FindAncestorOfCategoryBorder(DependencyObject? current)
    {
        while (current != null)
        {
            if (current is Border border && border.DataContext is Category)
                return border;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    /// <summary>
    /// 沿可视化树向上查找 DataContext 为 TaskItemViewModel 的元素
    /// </summary>
    public static TaskItemViewModel? FindTaskItemViewModel(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is FrameworkElement fe && fe.DataContext is TaskItemViewModel vm) return vm;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    /// <summary>
    /// 在可视树子元素中按名称查找指定类型的元素
    /// </summary>
    public static T? FindNamedChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t && t.Name == name) return t;
            var result = FindNamedChild<T>(child, name);
            if (result != null) return result;
        }
        return null;
    }

    /// <summary>
    /// 在可视树子元素中查找指定类型的第一个元素
    /// </summary>
    public static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }
}
