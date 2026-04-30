using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopTodo.Models;
using DesktopTodo.ViewModels;

namespace DesktopTodo.Helpers;

public static class TreeViewDragBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(TreeViewDragBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TreeView treeView) return;
        if ((bool)e.NewValue)
        {
            treeView.AllowDrop = true;
            var handler = new DragHandler(treeView);
            treeView.Tag = handler;
        }
        else
        {
            if (treeView.Tag is DragHandler handler)
                handler.Dispose();
            treeView.AllowDrop = false;
        }
    }

    private sealed class DragHandler : IDisposable
    {
        private readonly TreeView _treeView;
        private Point _dragStartPoint;
        private TaskItemViewModel? _draggedItem;
        private Border? _lastHighlightedItem;
        private DropTargetType _dropTargetType = DropTargetType.None;
        private TaskItemViewModel? _targetVm;
        private Category? _targetCategory;

        public DragHandler(TreeView treeView)
        {
            _treeView = treeView;
            _treeView.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            _treeView.MouseMove += OnMouseMove;
            _treeView.DragEnter += OnDragEnter;
            _treeView.DragOver += OnDragOver;
            _treeView.DragLeave += OnDragLeave;
            _treeView.Drop += OnDrop;
        }

        private MainViewModel VM => (MainViewModel)_treeView.DataContext;

        public void Dispose()
        {
            _treeView.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            _treeView.MouseMove -= OnMouseMove;
            _treeView.DragEnter -= OnDragEnter;
            _treeView.DragOver -= OnDragOver;
            _treeView.DragLeave -= OnDragLeave;
            _treeView.Drop -= OnDrop;
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
            _dragStartPoint = e.GetPosition(null);

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(pos.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var element = e.OriginalSource as UIElement;
                var item = FindAncestorOfType<TreeViewItem>(element);
                if (item?.DataContext is TaskItemViewModel vm)
                {
                    _draggedItem = vm;
                    DragDrop.DoDragDrop(item, vm, DragDropEffects.Move);
                    _draggedItem = null;
                }
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(TaskItemViewModel)))
            {
                e.Effects = DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.Move;
            }
            e.Handled = true;
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(TaskItemViewModel)))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            ClearLastHighlight();

            var targetElement = e.OriginalSource as UIElement;
            var targetItem = FindAncestorOfType<TreeViewItem>(targetElement);
            var categoryBorder = FindAncestorOfCategoryBorder(targetElement);

            if (categoryBorder != null)
            {
                _dropTargetType = DropTargetType.Category;
                _targetVm = null;
                _targetCategory = categoryBorder.DataContext as Category;
                _lastHighlightedItem = categoryBorder;
                categoryBorder.Background = new SolidColorBrush(Color.FromArgb(0x60, 0x4C, 0xAF, 0x50));
                e.Effects = DragDropEffects.Move;
            }
            else if (targetItem == null)
            {
                _dropTargetType = DropTargetType.Root;
                _targetVm = null;
                _targetCategory = null;
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                var headerBorder = targetItem.Template.FindName("Bd", targetItem) as Border;
                if (headerBorder != null)
                {
                    var pos = e.GetPosition(headerBorder);
                    double headerHeight = headerBorder.ActualHeight;
                    if (pos.Y < headerHeight * 0.3)
                    {
                        _dropTargetType = DropTargetType.Before;
                        headerBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00));
                        headerBorder.BorderThickness = new Thickness(0, 3, 0, 0);
                    }
                    else if (pos.Y > headerHeight * 0.7)
                    {
                        _dropTargetType = DropTargetType.After;
                        headerBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00));
                        headerBorder.BorderThickness = new Thickness(0, 0, 0, 3);
                    }
                    else
                    {
                        _dropTargetType = DropTargetType.Child;
                        headerBorder.Background = new SolidColorBrush(Color.FromArgb(0x60, 0x1E, 0x90, 0xFF));
                    }
                    _targetVm = targetItem.DataContext as TaskItemViewModel;
                    _targetCategory = null;
                    _lastHighlightedItem = headerBorder;
                }
                else
                {
                    var pos = e.GetPosition(targetItem);
                    if (pos.Y < targetItem.ActualHeight * 0.3)
                        _dropTargetType = DropTargetType.Before;
                    else if (pos.Y > targetItem.ActualHeight * 0.7)
                        _dropTargetType = DropTargetType.After;
                    else
                        _dropTargetType = DropTargetType.Child;
                    _targetVm = targetItem.DataContext as TaskItemViewModel;
                    _targetCategory = null;
                }
                e.Effects = DragDropEffects.Move;
            }
            e.Handled = true;
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            ClearLastHighlight();
            _dropTargetType = DropTargetType.None;
            _targetVm = null;
            _targetCategory = null;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            ClearLastHighlight();

            if (_draggedItem == null || !e.Data.GetDataPresent(typeof(TaskItemViewModel))) return;

            if (_dropTargetType == DropTargetType.Category && _targetCategory != null)
            {
                int? catId = _targetCategory.Id > 0 ? _targetCategory.Id : null;
                VM.MoveTaskToCategory(_draggedItem, catId);
                VM.RefreshCurrentView();
                _draggedItem = null;
                _dropTargetType = DropTargetType.None;
                _targetCategory = null;
                return;
            }

            if (_targetVm != null && (_draggedItem == _targetVm || IsDescendant(_draggedItem, _targetVm)))
            {
                _dropTargetType = DropTargetType.None;
                _targetVm = null;
                return;
            }

            var itemToSelect = _draggedItem;
            ObservableCollection<TaskItemViewModel>? sourceCollection = GetParentCollection(_draggedItem);
            sourceCollection?.Remove(_draggedItem);

            switch (_dropTargetType)
            {
                case DropTargetType.Root:
                    VM.RootTasks.Add(_draggedItem);
                    VM.ReorderCollection(VM.RootTasks, null);
                    break;
                case DropTargetType.Before:
                    if (_targetVm != null)
                    {
                        var targetColl = GetParentCollection(_targetVm);
                        if (targetColl != null)
                        {
                            int index = targetColl.IndexOf(_targetVm);
                            targetColl.Insert(index, _draggedItem);
                            VM.ReorderCollection(targetColl, _targetVm.Task.ParentTaskId);
                        }
                    }
                    break;
                case DropTargetType.After:
                    if (_targetVm != null)
                    {
                        var targetColl = GetParentCollection(_targetVm);
                        if (targetColl != null)
                        {
                            int index = targetColl.IndexOf(_targetVm) + 1;
                            targetColl.Insert(index, _draggedItem);
                            VM.ReorderCollection(targetColl, _targetVm.Task.ParentTaskId);
                        }
                    }
                    break;
                case DropTargetType.Child:
                    if (_targetVm != null)
                    {
                        _targetVm.Children.Add(_draggedItem);
                        _targetVm.IsExpanded = true;
                        VM.ReorderCollection(_targetVm.Children, _targetVm.Task.Id);
                    }
                    break;
            }

            if (sourceCollection != null && sourceCollection.Count > 0 && sourceCollection != GetParentCollection(_draggedItem))
            {
                var parentId = sourceCollection.FirstOrDefault()?.Task.ParentTaskId;
                VM.ReorderCollection(sourceCollection, parentId);
            }

            _dropTargetType = DropTargetType.None;
            _targetVm = null;
            _draggedItem = null;

            VM.SelectedTask = itemToSelect;
            _treeView.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (itemToSelect != null)
                {
                    var container = _treeView.ItemContainerGenerator.ContainerFromItem(itemToSelect) as TreeViewItem;
                    container?.BringIntoView();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ClearLastHighlight()
        {
            if (_lastHighlightedItem != null)
            {
                _lastHighlightedItem.Background = Brushes.Transparent;
                _lastHighlightedItem.BorderBrush = Brushes.Transparent;
                _lastHighlightedItem.BorderThickness = new Thickness(0);
                _lastHighlightedItem = null;
            }
        }

        private static Border? FindAncestorOfCategoryBorder(DependencyObject? current)
        {
            while (current != null)
            {
                if (current is Border border && border.DataContext is Category)
                    return border;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static T? FindAncestorOfType<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private bool IsDescendant(TaskItemViewModel parent, TaskItemViewModel potentialDescendant)
        {
            foreach (var child in parent.Children)
                if (child == potentialDescendant || IsDescendant(child, potentialDescendant))
                    return true;
            return false;
        }

        private ObservableCollection<TaskItemViewModel>? GetParentCollection(TaskItemViewModel item)
        {
            if (VM.RootTasks.Contains(item)) return VM.RootTasks;
            foreach (var root in VM.RootTasks)
            {
                var found = FindParentCollection(root.Children, item);
                if (found != null) return found;
            }
            return null;
        }

        private static ObservableCollection<TaskItemViewModel>? FindParentCollection(ObservableCollection<TaskItemViewModel> collection, TaskItemViewModel item)
        {
            if (collection.Contains(item)) return collection;
            foreach (var child in collection)
            {
                var found = FindParentCollection(child.Children, item);
                if (found != null) return found;
            }
            return null;
        }

        private enum DropTargetType
        {
            None, Root, Before, After, Child, Category
        }
    }
}