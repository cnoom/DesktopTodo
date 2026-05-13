using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DesktopTodo.Helpers;
using DesktopTodo.Services;
using DesktopTodo.ViewModels;

namespace DesktopTodo.Views;

public partial class MainWindow : Window
{
    internal MainViewModel VM => (MainViewModel)DataContext;
    private ISettingsService _settings = null!;

    // 迷你模式状态
    private bool _isMiniMode;
    private double _normalLeft, _normalTop, _normalWidth, _normalHeight;
    private const double MiniIconSize = 48;

    // 迷你模式拖拽
    private bool _isDraggingMini;
    private int _dragStartMouseX;         // 鼠标按下时的屏幕物理像素 X
    private int _dragStartMouseY;         // 鼠标按下时的屏幕物理像素 Y
    private int _dragStartWindowX;        // 拖拽起始时窗口物理像素 X
    private int _dragStartWindowY;        // 拖拽起始时窗口物理像素 Y
    private int _currentMiniPixelX;       // 拖拽过程中窗口当前物理像素 X
    private int _currentMiniPixelY;       // 拖拽过程中窗口当前物理像素 Y
    
    private const int SnapThreshold = 20; // 像素，可调

    public MainWindow()
    {
        InitializeComponent();
        SetupPlaceholderTexts();
        WindowResizeHelper.Enable(this);
        DesktopEmbedHelper.EmbedWindow(this);
    }

    /// <summary>
    /// 由 App.xaml.cs 在 DataContext 注入后调用，用于恢复窗口位置和尺寸
    /// </summary>
    public void RestoreWindowPosition(ISettingsService settings)
    {
        _settings = settings;

        if (!double.IsNaN(settings.WindowWidth) && !double.IsNaN(settings.WindowHeight))
        {
            Width = Math.Max(settings.WindowWidth, MinWidth);
            Height = Math.Max(settings.WindowHeight, MinHeight);
        }

        if (!double.IsNaN(settings.WindowLeft) && !double.IsNaN(settings.WindowTop))
        {
            // 检查保存的位置是否在任意显示器可见区域内
            if (IsPositionOnScreen(settings.WindowLeft, settings.WindowTop, Width, Height))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = settings.WindowLeft;
                Top = settings.WindowTop;
            }
            // 不可见时保持 WindowStartupLocation="CenterScreen"
        }
    }

    /// <summary>
    /// 检查指定矩形是否与任意显示器的工作区域有交集
    /// </summary>
    private static bool IsPositionOnScreen(double x, double y, double w, double h)
    {
        var rect = new RECT { Left = (int)x, Top = (int)y, Right = (int)(x + w), Bottom = (int)(y + h) };
        bool found = false;

        MonitorEnumProc callback = (hMonitor, _, _, _) =>
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                var work = monitorInfo.rcWork;
                // 检查交集：矩形是否与该显示器工作区域重叠
                if (rect.Left < work.Right && rect.Right > work.Left &&
                    rect.Top < work.Bottom && rect.Bottom > work.Top)
                {
                    found = true;
                    return false; // 找到交集，停止枚举
                }
            }
            return true; // 继续枚举
        };

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        return found;
    }

    /// <summary>
    /// 保存窗口位置和尺寸到设置
    /// </summary>
    public void SaveWindowPosition()
    {
        if (_settings == null) return;

        // 迷你模式下保存正常模式的尺寸
        if (_isMiniMode)
        {
            _settings.WindowLeft = _normalLeft;
            _settings.WindowTop = _normalTop;
            _settings.WindowWidth = _normalWidth;
            _settings.WindowHeight = _normalHeight;
            return;
        }

        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        _settings.WindowWidth = Width;
        _settings.WindowHeight = Height;
    }

    /// <summary>
    /// 重置窗口位置到屏幕中央
    /// </summary>
    public void ResetToScreenCenter()
    {
        // 如果处于迷你模式，先退出迷你模式
        if (_isMiniMode)
        {
            ExitMiniMode();
        }

        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;

        // 清除保存的位置信息，下次启动将居中
        if (_settings != null)
        {
            _settings.WindowLeft = double.NaN;
            _settings.WindowTop = double.NaN;
        }
    }

    #region Win32 显示器枚举

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(POINT pt, uint dwFlags);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    #endregion

    /// <summary>
    /// 设置 TextBox 占位文字的显示/隐藏逻辑
    /// </summary>
    private void SetupPlaceholderTexts()
    {
        // 任务输入占位文字
        NewTaskTextBox.TextChanged += (s, e) =>
            NewTaskPlaceholder.Visibility = string.IsNullOrEmpty(NewTaskTextBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;

        // 标签输入占位文字
        NewTagTextBox.TextChanged += (s, e) =>
            NewTagPlaceholder.Visibility = string.IsNullOrEmpty(NewTagTextBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;

        // 分类输入占位文字
        NewCategoryTextBox.TextChanged += (s, e) =>
            NewCategoryPlaceholder.Visibility = string.IsNullOrEmpty(NewCategoryTextBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 迷你模式下不响应窗口拖拽
        if (_isMiniMode) return;
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void MinimizeClick(object sender, RoutedEventArgs e)
    {
        EnterMiniMode();
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    #region 迷你模式

    /// <summary>
    /// 进入迷你模式：缩小为小图标，保持桌面嵌入
    /// </summary>
    private void EnterMiniMode()
    {
        if (_isMiniMode) return;

        // 保存正常模式下的窗口位置和尺寸
        _normalLeft = Left;
        _normalTop = Top;
        _normalWidth = Width;
        _normalHeight = Height;

        _isMiniMode = true;

        // 隐藏主内容，显示迷你图标
        MainContentBorder.Visibility = Visibility.Collapsed;
        MiniModeIcon.Visibility = Visibility.Visible;

        // 切换为 NoResize 消除隐形调整边框，使窗口尺寸精确匹配内容
        ResizeMode = ResizeMode.NoResize;

        // 禁用边缘调整大小辅助，避免 48x48 窗口中 WM_NCHITTEST 干扰拖拽
        WindowResizeHelper.SetEnabled(false);

        // 固定小窗口尺寸
        Width = MiniIconSize;
        Height = MiniIconSize;

        // 读取保存的迷你模式位置，校验是否在屏幕范围内
        if (_settings != null && !double.IsNaN(_settings.MiniModeLeft) && !double.IsNaN(_settings.MiniModeTop))
        {
            if (IsPositionOnScreen(_settings.MiniModeLeft, _settings.MiniModeTop, MiniIconSize, MiniIconSize))
            {
                Left = _settings.MiniModeLeft;
                Top = _settings.MiniModeTop;
            }
            else
            {
                SetDefaultMiniPosition();
            }
        }
        else
        {
            SetDefaultMiniPosition();
        }
    }

    /// <summary>
    /// 设置迷你模式默认位置（原窗口中心）
    /// </summary>
    private void SetDefaultMiniPosition()
    {
        Left = _normalLeft + (_normalWidth - MiniIconSize) / 2;
        Top = _normalTop + (_normalHeight - MiniIconSize) / 2;
    }

    /// <summary>
    /// 退出迷你模式：恢复窗口大小和位置
    /// </summary>
    private void ExitMiniMode()
    {
        if (!_isMiniMode) return;
        _isMiniMode = false;

        // 隐藏迷你图标
        MiniModeIcon.Visibility = Visibility.Collapsed;

        // 恢复尺寸和位置
        Width = _normalWidth;
        Height = _normalHeight;
        Left = _normalLeft;
        Top = _normalTop;

        // 恢复主内容
        MainContentBorder.Visibility = Visibility.Visible;

        // 恢复可调整大小模式
        ResizeMode = ResizeMode.CanResize;

        // 重新启用边缘调整大小辅助
        WindowResizeHelper.SetEnabled(true);
    }

    /// <summary>
    /// 迷你图标鼠标按下：准备拖拽
    /// </summary>
    private void MiniModeIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isMiniMode) return;

        _isDraggingMini = false;

        // 记录鼠标按下时的屏幕物理像素坐标
        GetCursorPos(out var mousePixel);
        _dragStartMouseX = mousePixel.X;
        _dragStartMouseY = mousePixel.Y;

        // 用 Win32 GetWindowRect 获取窗口真实屏幕物理像素位置
        // （不使用 WPF Left/Top 换算，因为嵌入 WorkerW 后可能存在偏移）
        var hWnd = new WindowInteropHelper(this).Handle;
        GetWindowRect(hWnd, out var windowRect);
        _dragStartWindowX = windowRect.Left;
        _dragStartWindowY = windowRect.Top;
        _currentMiniPixelX = _dragStartWindowX;
        _currentMiniPixelY = _dragStartWindowY;

        MiniModeIcon.CaptureMouse();
        e.Handled = true;
    }

    /// <summary>
    /// 迷你图标鼠标移动：拖拽中（全部在物理像素空间计算，避免 DPI 单位混乱）
    /// </summary>
    private void MiniModeIcon_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isMiniMode || e.LeftButton != MouseButtonState.Pressed || !MiniModeIcon.IsMouseCaptured)
            return;

        GetCursorPos(out var mousePixel);
        int dx = mousePixel.X - _dragStartMouseX;
        int dy = mousePixel.Y - _dragStartMouseY;

        // 超过 3 像素判定为拖拽（避免误触）
        if (!_isDraggingMini && (Math.Abs(dx) > 3 || Math.Abs(dy) > 3))
        {
            _isDraggingMini = true;
        }

        if (_isDraggingMini)
        {
            int newPixelX = _dragStartWindowX + dx;
            int newPixelY = _dragStartWindowY + dy;

            _currentMiniPixelX = newPixelX;
            _currentMiniPixelY = newPixelY;

            // 直接用物理像素调用 SetWindowPos
            var hWnd = new WindowInteropHelper(this).Handle;
            SetWindowPos(hWnd, IntPtr.Zero, newPixelX, newPixelY, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }

    /// <summary>
    /// 迷你图标鼠标释放：结束拖拽或点击恢复
    /// </summary>
    private void MiniModeIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isMiniMode || !MiniModeIcon.IsMouseCaptured) return;

        // 限制在显示器区域内
        ClampToScreen(ref _currentMiniPixelX, ref _currentMiniPixelY);
        
        MiniModeIcon.ReleaseMouseCapture();

        if (_isDraggingMini)
        {
            // 将物理像素转回 DIP，同步到 WPF 属性并保存
            var dpi = GetDpiScale();
            Left = _currentMiniPixelX / dpi.scaleX;
            Top = _currentMiniPixelY / dpi.scaleY;

            if (_settings != null)
            {
                _settings.MiniModeLeft = Left;
                _settings.MiniModeTop = Top;
                _settings.Save();
            }
            _isDraggingMini = false;
        }
        else
        {
            // 非拖拽 = 单击 → 恢复窗口
            ExitMiniMode();
        }

        e.Handled = true;
    }

    /// <summary>
    /// 将位置限制在鼠标所在显示器的完整区域内（物理像素坐标）
    /// </summary>
    private void ClampToScreen(ref int pixelX, ref int pixelY)
    {
        GetCursorPos(out var cursorPixel);

        var dpi = GetDpiScale();
        int windowWidth = (int)(ActualWidth * dpi.scaleX);
        int windowHeight = (int)(ActualHeight * dpi.scaleY);

        var monitor = MonitorFromWindow(cursorPixel, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (GetMonitorInfo(monitor, ref info))
            {
                var area = info.rcMonitor;

                if (pixelX < area.Left) pixelX = area.Left;
                if (pixelY < area.Top) pixelY = area.Top;

                if (pixelX + windowWidth > area.Right)
                    pixelX = area.Right - windowWidth;

                if (pixelY + windowHeight > area.Bottom)
                    pixelY = area.Bottom - windowHeight;
            }
        }
    }

    /// <summary>
    /// 获取当前窗口的 DPI 缩放因子
    /// </summary>
    private (double scaleX, double scaleY) GetDpiScale()
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            return (source.CompositionTarget.TransformToDevice.M11,
                    source.CompositionTarget.TransformToDevice.M22);
        }
        return (1.0, 1.0);
    }

    #endregion

    /// <summary>
    /// 全局键盘快捷键处理（PreviewKeyDown 隧道事件，优于 TextBox 处理）
    /// </summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 如果在 TextBox 中编辑，仅保留全局快捷键
        var isInTextBox = Keyboard.FocusedElement is TextBox;

        switch (e.Key)
        {
            case Key.N when Keyboard.Modifiers == ModifierKeys.Control && !isInTextBox:
                // Ctrl+N：聚焦任务输入框
                NewTaskTextBox.Focus();
                NewTaskTextBox.SelectAll();
                e.Handled = true;
                break;

            case Key.N when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
                // Ctrl+Shift+N：添加子任务（需先选中父任务）
                if (VM.SelectedTask != null)
                {
                    VM.AddSubTaskCommand.Execute(VM.SelectedTask);
                    NewTaskTextBox.Focus();
                    NewTaskTextBox.SelectAll();
                }
                e.Handled = true;
                break;

            case Key.F2:
                // F2：重命名选中任务
                if (VM.SelectedTask != null && !isInTextBox)
                {
                    StartEditSelectedTask();
                }
                e.Handled = true;
                break;

            case Key.Delete:
                // Delete：删除选中任务
                if (VM.SelectedTask != null && !isInTextBox)
                {
                    VM.DeleteTaskCommand.Execute(VM.SelectedTask);
                }
                e.Handled = true;
                break;

            case Key.Space:
                // Space：切换选中任务的完成状态
                if (VM.SelectedTask != null && !isInTextBox)
                {
                    VM.SelectedTask.Task.IsCompleted = !VM.SelectedTask.Task.IsCompleted;
                }
                e.Handled = true;
                break;

            case Key.OemComma when Keyboard.Modifiers == ModifierKeys.Control:
                // Ctrl+,：切换设置面板
                ToggleSettingsPopup();
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// 对选中的 TreeView 节点启动行内编辑
    /// </summary>
    private void StartEditSelectedTask()
    {
        if (VM.SelectedTask == null) return;
        StartEditForTask(VM.SelectedTask);
    }

    private void NewTaskTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) VM.AddTopLevelTaskCommand.Execute(null);
    }

    private void TaskTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        VM.SelectedTask = e.NewValue as TaskItemViewModel;
    }

    private void NewTagTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb)
        {
            AddTagFromTextBox(tb);
        }
    }

    /// <summary>
    /// 标签"添加"按钮点击
    /// </summary>
    private void AddTagButton_Click(object sender, RoutedEventArgs e)
    {
        AddTagFromTextBox(NewTagTextBox);
    }

    /// <summary>
    /// 从 TextBox 读取标签名并添加到选中任务
    /// </summary>
    private void AddTagFromTextBox(TextBox tb)
    {
        if (string.IsNullOrWhiteSpace(tb.Text)) return;
        VM.AddTagToSelectedTask(tb.Text);
        tb.Clear();
    }

    private void AddTopLevelButton_Click(object sender, RoutedEventArgs e) => VM.AddTopLevelTaskCommand.Execute(null);

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TaskItemViewModel vm)
        {
            StartEditForTask(vm);
        }
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

            // 在 TreeViewItem 的视觉树中查找标题 TextBlock（名为 TaskTitleText）
            var tb = FindNamedChild<TextBlock>(container, "TaskTitleText");
            if (tb != null)
            {
                StartInlineEdit(vm, tb);
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void StartInlineEdit(TaskItemViewModel vm, TextBlock textBlock)
    {
        // 防止重复编辑
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

        // 创建编辑面板（先声明，EndEdit 回调需要引用）
        var editVm = new InlineEditViewModel(vm, null);
        InlineEditPanel inlineEditPanel = new InlineEditPanel(editVm);

        void EndEdit()
        {
            if (editEnded) return;
            editEnded = true;
            graceTimer.Stop();

            outerGrid.Children.Remove(inlineEditPanel);

            // 恢复拖拽功能
            TreeViewDragBehavior.SetIsEnabled(TaskTreeView, true);

            // 恢复所有原始子元素的可见性
            for (int i = 0; i < outerGrid.Children.Count; i++)
            {
                var child = outerGrid.Children[i];
                if (child is StackPanel)
                    child.ClearValue(VisibilityProperty);
                else
                    child.Visibility = Visibility.Visible;
            }
        }

        // 设置 EndEdit 回调（延迟赋值，因为 EndEdit 引用了 inlineEditPanel）
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

        // 禁用拖拽，避免编辑时误触
        TreeViewDragBehavior.SetIsEnabled(TaskTreeView, false);

        // 隐藏所有原始子元素
        foreach (UIElement child in outerGrid.Children)
        {
            child.Visibility = Visibility.Collapsed;
        }

        // 添加编辑面板
        outerGrid.Children.Add(inlineEditPanel);

        // 延迟聚焦
        inlineEditPanel.FocusTitle();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TaskItemViewModel vm)
            VM.DeleteTaskCommand.Execute(vm);
    }

    private void RemoveTagFromTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is Models.Tag tag)
        {
            var contextMenu = menuItem.Parent as ContextMenu;
            if (contextMenu?.PlacementTarget is FrameworkElement target)
            {
                var taskVm = FindTaskItemViewModelFromElement(target);
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

    private TaskItemViewModel? FindTaskItemViewModelFromElement(DependencyObject element)
    {
        return VisualTreeHelpers.FindTaskItemViewModel(element);
    }

    private void SaveSettingsClick(object sender, RoutedEventArgs e) => VM.SaveSettingsCommand.Execute(null);

    /// <summary>
    /// 设置按钮点击
    /// </summary>
    private void SettingsButton_Click(object sender, RoutedEventArgs e) => ToggleSettingsPopup();

    /// <summary>
    /// 关闭设置弹窗
    /// </summary>
    private void CloseSettingsPopup(object sender, RoutedEventArgs e) => SettingsPopup.IsOpen = false;

    /// <summary>
    /// 切换设置弹窗显示
    /// </summary>
    private void ToggleSettingsPopup() => SettingsPopup.IsOpen = !SettingsPopup.IsOpen;

    /// <summary>
    /// 恢复默认设置
    /// </summary>
    private void ResetSettingsClick(object sender, RoutedEventArgs e)
    {
        VM.BackgroundOpacity = 0.8;
        VM.FontSize = 12.0;
        VM.SetColorCommand.Execute("#F0F0F0");
        VM.SetTaskFontColorCommand.Execute("#FFFFFF");
        // 重置迷你模式位置记录
        if (_settings != null)
        {
            _settings.MiniModeLeft = double.NaN;
            _settings.MiniModeTop = double.NaN;
        }
        VM.SaveSettingsCommand.Execute(null);
    }

    // 分类拖放事件
    private void SidebarBorder_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(TaskItemViewModel)))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void SidebarBorder_Drop(object sender, DragEventArgs e)
    {
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

    private static Border? FindAncestorOfCategoryBorder(DependencyObject? current)
        => VisualTreeHelpers.FindAncestorOfCategoryBorder(current);


    // 分类事件
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

    // 辅助方法
    private static T? FindNamedChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        => VisualTreeHelpers.FindNamedChild<T>(parent, name);

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        => VisualTreeHelpers.FindVisualChild<T>(parent);
}