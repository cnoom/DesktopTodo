using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopTodo.Helpers;
using DesktopTodo.Services;
using DesktopTodo.ViewModels;

namespace DesktopTodo.Views;

public partial class MainWindow : Window
{
    internal MainViewModel VM => (MainViewModel)DataContext;
    private ISettingsService _settings = null!;

    // 辅助管理器
    private MiniModeHelper _miniMode = null!;
    private KeyboardShortcutManager _keyboardManager = null!;

    // 拖拽到分类时的高亮状态
    private Border? _dragOverCategoryBorder;

    // 迷你模式下保存正常窗口位置（供 WindowPositionHelper 使用）
    public double NormalLeft { get; private set; }
    public double NormalTop { get; private set; }
    public double NormalWidth { get; private set; }
    public double NormalHeight { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        SetupPlaceholderTexts();
        WindowResizeHelper.Enable(this);
        DesktopEmbedHelper.EmbedWindow(this);
    }

    /// <summary>
    /// 由 App.xaml.cs 在 DataContext 注入后调用
    /// </summary>
    public void RestoreWindowPosition(ISettingsService settings)
    {
        _settings = settings;
        WindowPositionHelper.RestoreWindowPosition(this, settings);

        // 初始化辅助管理器（需要在 DataContext 和 _settings 设置之后）
        _miniMode = new MiniModeHelper(this, MainContentBorder, MiniModeIcon, settings);
        _keyboardManager = new KeyboardShortcutManager(
            this, VM,
            FocusTaskInput,
            StartEditSelectedTask,
            ToggleSettingsPopup);
    }

    /// <summary>
    /// 保存窗口位置和尺寸到设置
    /// </summary>
    public void SaveWindowPosition()
    {
        if (_settings == null) return;
        WindowPositionHelper.SaveWindowPosition(
            this, _settings,
            _miniMode?.IsMiniMode ?? false,
            NormalLeft, NormalTop, NormalWidth, NormalHeight);
    }

    /// <summary>
    /// 重置窗口位置到屏幕中央
    /// </summary>
    public void ResetToScreenCenter()
    {
        if (_miniMode?.IsMiniMode == true)
        {
            _miniMode.ExitMiniMode();
        }

        WindowPositionHelper.ResetToScreenCenter(this);

        if (_settings != null)
        {
            _settings.WindowLeft = double.NaN;
            _settings.WindowTop = double.NaN;
        }
    }

    /// <summary>
    /// 设置 TextBox 占位文字的显示/隐藏逻辑
    /// </summary>
    private void SetupPlaceholderTexts()
    {
        NewTaskTextBox.TextChanged += (s, e) =>
            NewTaskPlaceholder.Visibility = string.IsNullOrEmpty(NewTaskTextBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;

        NewTagTextBox.TextChanged += (s, e) =>
            NewTagPlaceholder.Visibility = string.IsNullOrEmpty(NewTagTextBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;

        NewCategoryTextBox.TextChanged += (s, e) =>
            NewCategoryPlaceholder.Visibility = string.IsNullOrEmpty(NewCategoryTextBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FocusTaskInput()
    {
        NewTaskTextBox.Focus();
        NewTaskTextBox.SelectAll();
    }

    #region 窗口事件

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_miniMode?.IsMiniMode == true) return;
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void MinimizeClick(object sender, RoutedEventArgs e) => _miniMode?.EnterMiniMode();
    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    #endregion

    #region 任务操作事件

    private void NewTaskTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) VM.AddTopLevelTaskCommand.Execute(null);
    }

    private void TaskTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        VM.SelectedTask = e.NewValue as TaskItemViewModel;
    }

    private void AddTopLevelButton_Click(object sender, RoutedEventArgs e) => VM.AddTopLevelTaskCommand.Execute(null);

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TaskItemViewModel vm)
        {
            StartEditForTask(vm);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TaskItemViewModel vm)
            VM.DeleteTaskCommand.Execute(vm);
    }

    #endregion

    #region 行内编辑

    /// <summary>
    /// 对选中的 TreeView 节点启动行内编辑
    /// </summary>
    private void StartEditSelectedTask()
    {
        if (VM.SelectedTask == null) return;
        StartEditForTask(VM.SelectedTask);
    }

    /// <summary>
    /// 通过 TreeViewItem 容器定位并启动行内编辑
    /// </summary>
    private void StartEditForTask(TaskItemViewModel vm)
    {
        var container = TaskTreeView.ItemContainerGenerator.ContainerFromItem(vm) as TreeViewItem;
        if (container == null) return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!container.IsVisible) return;
            container.BringIntoView();

            var tb = VisualTreeHelpers.FindNamedChild<TextBlock>(container, "TaskTitleText");
            if (tb != null)
            {
                StartInlineEdit(vm, tb);
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void StartInlineEdit(TaskItemViewModel vm, TextBlock textBlock)
    {
        if (textBlock.Visibility != Visibility.Visible) return;

        // 从 TextBlock 向上遍历视觉树找到外层行 Grid
        Panel? outerGrid = null;
        DependencyObject current = VisualTreeHelper.GetParent(textBlock);
        while (current != null)
        {
            if (current is Grid g && VisualTreeHelper.GetChildrenCount(g) > 0)
            {
                bool hasInnerGrid = false;
                bool hasStackPanel = false;
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(g); i++)
                {
                    var child = VisualTreeHelper.GetChild(g, i);
                    if (child is Grid) hasInnerGrid = true;
                    if (child is StackPanel) hasStackPanel = true;
                }
                if (hasInnerGrid && hasStackPanel)
                {
                    outerGrid = g;
                    break;
                }
            }
            current = VisualTreeHelper.GetParent(current);
        }

        if (outerGrid == null) return;

        bool editEnded = false;
        bool canClose = false;

        // 启动保护期：300ms 内忽略 LostFocus
        var graceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        graceTimer.Tick += (s, args) =>
        {
            canClose = true;
            graceTimer.Stop();
        };
        graceTimer.Start();

        var editVm = new InlineEditViewModel(vm, null);
        InlineEditPanel inlineEditPanel = new InlineEditPanel(editVm);

        void EndEdit()
        {
            if (editEnded) return;
            editEnded = true;
            graceTimer.Stop();

            outerGrid.Children.Remove(inlineEditPanel);
            TreeViewDragBehavior.SetIsEnabled(TaskTreeView, true);

            for (int i = 0; i < outerGrid.Children.Count; i++)
            {
                var child = outerGrid.Children[i];
                if (child is StackPanel)
                    child.ClearValue(VisibilityProperty);
                else
                    child.Visibility = Visibility.Visible;
            }
        }

        editVm.SetOnClosed(EndEdit);

        inlineEditPanel.LostFocus += (s, args) =>
        {
            if (!canClose) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (editEnded) return;
                if (!inlineEditPanel.IsAncestorOf(Keyboard.FocusedElement as DependencyObject))
                    editVm.SaveCommand.Execute(null);
            }), System.Windows.Threading.DispatcherPriority.Background);
        };

        TreeViewDragBehavior.SetIsEnabled(TaskTreeView, false);

        foreach (UIElement child in outerGrid.Children)
        {
            child.Visibility = Visibility.Collapsed;
        }

        outerGrid.Children.Add(inlineEditPanel);
        inlineEditPanel.FocusTitle();
    }

    #endregion

    #region 标签操作事件

    private void NewTagTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb)
        {
            AddTagFromTextBox(tb);
        }
    }

    private void AddTagButton_Click(object sender, RoutedEventArgs e) => AddTagFromTextBox(NewTagTextBox);

    private void AddTagFromTextBox(TextBox tb)
    {
        if (string.IsNullOrWhiteSpace(tb.Text)) return;
        VM.AddTagToSelectedTask(tb.Text);
        tb.Clear();
    }

    private void RemoveTagFromTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is Models.Tag tag)
        {
            var contextMenu = menuItem.Parent as ContextMenu;
            if (contextMenu?.PlacementTarget is FrameworkElement target)
            {
                var taskVm = VisualTreeHelpers.FindTaskItemViewModel(target);
                if (taskVm != null) VM.RemoveTagFromTask(taskVm, tag);
            }
        }
    }

    private void DeleteTag_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is Models.Tag tag)
        {
            VM.DeleteTag(tag);
        }
    }

    #endregion

    #region 分类操作事件

    private void SidebarBorder_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(TaskItemViewModel)))
        {
            e.Effects = DragDropEffects.Move;

            // 清除上次高亮
            ClearDragOverCategoryHighlight();

            // 高亮鼠标下方的分类项
            var categoryBorder = VisualTreeHelpers.FindAncestorOfCategoryBorder(e.OriginalSource as DependencyObject);
            if (categoryBorder != null)
            {
                _dragOverCategoryBorder = categoryBorder;
                categoryBorder.Background = new SolidColorBrush(Color.FromArgb(0x40, 0x4C, 0xAF, 0x50));
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
            ClearDragOverCategoryHighlight();
        }
        e.Handled = true;
    }

    private void SidebarBorder_Drop(object sender, DragEventArgs e)
    {
        ClearDragOverCategoryHighlight();

        if (e.Data.GetData(typeof(TaskItemViewModel)) is TaskItemViewModel draggedItem)
        {
            var categoryBorder = VisualTreeHelpers.FindAncestorOfCategoryBorder(e.OriginalSource as DependencyObject);
            if (categoryBorder?.DataContext is Models.Category cat)
            {
                int? catId = cat.Id > 0 ? cat.Id : null;
                VM.MoveTaskToCategory(draggedItem, catId);
                VM.RefreshCurrentView();
            }
        }
    }

    private void SidebarBorder_DragLeave(object sender, DragEventArgs e)
    {
        ClearDragOverCategoryHighlight();
    }

    private void ClearDragOverCategoryHighlight()
    {
        if (_dragOverCategoryBorder != null)
        {
            // 清除本地值，让 Style/Trigger 重新接管背景色
            _dragOverCategoryBorder.ClearValue(Border.BackgroundProperty);
            _dragOverCategoryBorder = null;
        }
    }

    private void CategoryItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is Models.Category cat)
            VM.SelectCategoryCommand.Execute(cat);
    }

    private void DeleteCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is Models.Category cat)
            VM.DeleteCategoryCommand.Execute(cat);
    }

    private void NewCategoryTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb)
        {
            VM.AddCategoryCommand.Execute(tb.Text);
            tb.Clear();
        }
    }

    private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        VM.AddCategoryCommand.Execute(NewCategoryTextBox.Text);
        NewCategoryTextBox.Clear();
    }

    #endregion

    #region 设置面板事件

    private void SaveSettingsClick(object sender, RoutedEventArgs e) => VM.SaveSettingsCommand.Execute(null);
    private void SettingsButton_Click(object sender, RoutedEventArgs e) => ToggleSettingsPopup();
    private void CloseSettingsPopup(object sender, RoutedEventArgs e) => SettingsPopup.IsOpen = false;

    private void ToggleSettingsPopup() => SettingsPopup.IsOpen = !SettingsPopup.IsOpen;

    private void ResetSettingsClick(object sender, RoutedEventArgs e)
    {
        VM.BackgroundOpacity = 0.8;
        VM.FontSize = 12.0;
        VM.SetColorCommand.Execute("#F0F0F0");
        VM.SetTaskFontColorCommand.Execute("#FFFFFF");
        if (_settings != null)
        {
            _settings.MiniModeLeft = double.NaN;
            _settings.MiniModeTop = double.NaN;
        }
        VM.SaveSettingsCommand.Execute(null);
    }

    #endregion
}
