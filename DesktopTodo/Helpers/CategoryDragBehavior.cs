using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopTodo.Models;

namespace DesktopTodo.Helpers;

/// <summary>
/// 分类列表拖拽排序附加行为
/// 固定分类（全部/未分类，Id<=0）不参与拖拽排序
/// </summary>
public static class CategoryDragBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(CategoryDragBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl itemsControl) return;
        if ((bool)e.NewValue)
        {
            itemsControl.AllowDrop = true;
            var handler = new CategoryDragHandler(itemsControl);
            itemsControl.Tag = handler;
        }
        else
        {
            if (itemsControl.Tag is CategoryDragHandler handler)
                handler.Dispose();
            itemsControl.AllowDrop = false;
        }
    }

    private sealed class CategoryDragHandler : IDisposable
    {
        private readonly ItemsControl _itemsControl;
        private Point _dragStartPoint;
        private Category? _draggedCategory;
        private Border? _insertionIndicator;
        private bool _insertBefore;

        public CategoryDragHandler(ItemsControl itemsControl)
        {
            _itemsControl = itemsControl;
            _itemsControl.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            _itemsControl.PreviewMouseMove += OnPreviewMouseMove;
            _itemsControl.DragOver += OnDragOver;
            _itemsControl.DragLeave += OnDragLeave;
            _itemsControl.Drop += OnDrop;
        }

        public void Dispose()
        {
            _itemsControl.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            _itemsControl.PreviewMouseMove -= OnPreviewMouseMove;
            _itemsControl.DragOver -= OnDragOver;
            _itemsControl.DragLeave -= OnDragLeave;
            _itemsControl.Drop -= OnDrop;
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var category = GetCategoryFromPosition(e.OriginalSource);
            // 固定分类不可拖拽
            if (category?.IsFixed != false) return;
            _dragStartPoint = e.GetPosition(null);
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedCategory != null) return;

            var category = GetCategoryFromPosition(e.OriginalSource);
            if (category?.IsFixed != false) return;

            var pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _dragStartPoint.X) <= SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _dragStartPoint.Y) <= SystemParameters.MinimumVerticalDragDistance)
                return;

            _draggedCategory = category;
            var element = GetContainerFromCategory(category);
            if (element != null)
            {
                DragDrop.DoDragDrop(element, category, DragDropEffects.Move);
            }
            _draggedCategory = null;
            ClearInsertionIndicator();
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (_draggedCategory == null || !e.Data.GetDataPresent(typeof(Category)))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            ClearInsertionIndicator();

            var targetCategory = GetCategoryFromPosition(e.OriginalSource);
            if (targetCategory == null || targetCategory.IsFixed)
            {
                // 固定分类上方/下方不可放置，但可以放在固定分类之后
                // 查找最近的非固定分类
                targetCategory = FindNearestSortableCategory(e);
                if (targetCategory == null)
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                    return;
                }
            }

            var container = GetContainerFromCategory(targetCategory);
            if (container == null)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            // 判断插入位置（上方/下方）
            var pos = e.GetPosition(container);
            _insertBefore = pos.Y < container.ActualHeight / 2;

            // 显示插入指示线
            ShowInsertionIndicator(container);

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            ClearInsertionIndicator();
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            ClearInsertionIndicator();

            if (_draggedCategory == null || !e.Data.GetDataPresent(typeof(Category))) return;

            var targetCategory = GetCategoryFromPosition(e.OriginalSource);
            if (targetCategory?.IsFixed == true)
            {
                targetCategory = FindNearestSortableCategory(e);
            }

            if (targetCategory == null || targetCategory == _draggedCategory) return;

            var categories = (ObservableCollection<Category>)_itemsControl.ItemsSource;

            // 获取非固定分类的子列表
            var sortableCategories = categories.Where(c => !c.IsFixed).ToList();
            if (!sortableCategories.Contains(_draggedCategory)) return;

            // 从列表中移除被拖拽项
            categories.Remove(_draggedCategory);

            // 找到目标在完整列表中的位置
            int targetIndex = categories.IndexOf(targetCategory);
            if (!_insertBefore) targetIndex++;

            // 确保不插入到固定分类前面
            int firstSortableIndex = categories.ToList().FindIndex(c => !c.IsFixed);
            if (firstSortableIndex < 0) firstSortableIndex = categories.Count;
            if (targetIndex < firstSortableIndex) targetIndex = firstSortableIndex;

            categories.Insert(targetIndex, _draggedCategory);

            // 更新 SortOrder 并持久化
            var reorderedIds = categories.Where(c => !c.IsFixed).Select(c => c.Id).ToList();
            for (int i = 0; i < categories.Count; i++)
            {
                if (!categories[i].IsFixed)
                {
                    // 计算在非固定列表中的排序索引
                    int sortIdx = 0;
                    for (int j = 0; j <= i; j++)
                    {
                        if (!categories[j].IsFixed)
                        {
                            categories[j].SortOrder = sortIdx;
                            sortIdx++;
                        }
                    }
                }
            }

            // 触发排序持久化
            if (_itemsControl.DataContext is ViewModels.MainViewModel vm)
            {
                vm.ReorderCategories(reorderedIds);
            }

            _draggedCategory = null;
        }

        /// <summary>
        /// 从点击位置获取分类对象
        /// </summary>
        private Category? GetCategoryFromPosition(object originalSource)
        {
            var element = originalSource as UIElement;
            while (element != null)
            {
                if (element is FrameworkElement fe && fe.DataContext is Category cat)
                    return cat;
                element = VisualTreeHelper.GetParent(element) as UIElement;
            }
            return null;
        }

        /// <summary>
        /// 获取分类对应的容器元素
        /// </summary>
        private FrameworkElement? GetContainerFromCategory(Category category)
        {
            for (int i = 0; i < _itemsControl.Items.Count; i++)
            {
                if (_itemsControl.Items[i] is Category c && c == category)
                {
                    return _itemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                }
            }
            return null;
        }

        /// <summary>
        /// 查找鼠标位置最近的非固定分类
        /// </summary>
        private Category? FindNearestSortableCategory(DragEventArgs e)
        {
            var categories = (ObservableCollection<Category>)_itemsControl.ItemsSource;
            var sortableCategories = categories.Where(c => !c.IsFixed).ToList();
            if (sortableCategories.Count == 0) return null;

            // 获取鼠标在 ItemsControl 中的 Y 坐标
            var pos = e.GetPosition(_itemsControl);

            Category? nearest = null;
            double minDist = double.MaxValue;

            foreach (var cat in sortableCategories)
            {
                var container = GetContainerFromCategory(cat);
                if (container == null) continue;

                var containerPos = container.TranslatePoint(new Point(0, 0), _itemsControl);
                double centerY = containerPos.Y + container.ActualHeight / 2;
                double dist = Math.Abs(pos.Y - centerY);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = cat;
                    _insertBefore = pos.Y < centerY;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 显示插入指示线
        /// </summary>
        private void ShowInsertionIndicator(FrameworkElement container)
        {
            ClearInsertionIndicator();

            var border = FindVisualChild<Border>(container);
            if (border != null)
            {
                _insertionIndicator = border;
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00));
                border.BorderThickness = _insertBefore
                    ? new Thickness(0, 2, 0, 0)
                    : new Thickness(0, 0, 0, 2);
            }
        }

        private void ClearInsertionIndicator()
        {
            if (_insertionIndicator != null)
            {
                _insertionIndicator.BorderBrush = Brushes.Transparent;
                _insertionIndicator.BorderThickness = new Thickness(0);
                _insertionIndicator = null;
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result) return result;
                var found = FindVisualChild<T>(child);
                if (found != null) return found;
            }
            return null;
        }
    }
}
