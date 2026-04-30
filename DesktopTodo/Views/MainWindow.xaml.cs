using System;
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
    private MainViewModel VM => (MainViewModel)DataContext;
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
    private void SaveWindowPosition()
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
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

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

            // 限制在显示器区域内
            ClampToScreen(ref newPixelX, ref newPixelY);

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

        int iconSizePixel = (int)(MiniIconSize * GetDpiScale().scaleX);

        var monitor = MonitorFromPoint(cursorPixel, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (GetMonitorInfo(monitor, ref info))
            {
                // 使用 rcMonitor（完整显示器区域）而非 rcWork（排除任务栏）
                var area = info.rcMonitor;
                if (pixelX < area.Left) pixelX = area.Left;
                if (pixelY < area.Top) pixelY = area.Top;
                if (pixelX + iconSizePixel > area.Right) pixelX = area.Right - iconSizePixel;
                if (pixelY + iconSizePixel > area.Bottom) pixelY = area.Bottom - iconSizePixel;
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

        // 从 TextBlock 向上遍历视觉树找到外层行 Grid（Margin="2,3" 的那个）
        // 外层 Grid 包含：内层内容 Grid + 操作按钮浮动层 StackPanel
        Panel outerGrid = null;
        DependencyObject current = VisualTreeHelper.GetParent(textBlock);
        while (current != null)
        {
            if (current is Grid g && VisualTreeHelper.GetChildrenCount(g) > 0)
            {
                // 检查这个 Grid 的子元素中是否同时包含 Grid 和 StackPanel（即内层+按钮层）
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

        // 编辑面板：TextBox + RGB 颜色编辑
        var editPanel = new StackPanel
        {
            MinWidth = 280
        };

        var textBox = new TextBox
        {
            Text = vm.Task.Title,
            MinWidth = 200,
            Height = Math.Max(textBlock.ActualHeight, 24)
        };

        // 颜色预览条
        var colorPreview = new Border
        {
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // 解析当前颜色初始化 RGB
        byte initR = 0, initG = 0, initB = 0;
        if (!string.IsNullOrEmpty(vm.Task.Color))
        {
            try
            {
                var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(vm.Task.Color);
                if (brush != null)
                {
                    initR = brush.Color.R;
                    initG = brush.Color.G;
                    initB = brush.Color.B;
                }
            }
            catch { }
        }

        // 更新颜色预览条
        void UpdateColorPreview()
        {
            colorPreview.Background = new SolidColorBrush(Color.FromRgb(initR, initG, initB));
            vm.Task.Color = $"#{initR:X2}{initG:X2}{initB:X2}";
        }
        UpdateColorPreview();

        // RGB 滑动条面板
        var rgbPanel = new Grid
        {
            Margin = new Thickness(0, 2, 0, 0)
        };
        rgbPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        rgbPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rgbPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        rgbPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rgbPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rgbPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // R 滑动条
        var rLabel = new TextBlock { Text = "R", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Red };
        Grid.SetRow(rLabel, 0); Grid.SetColumn(rLabel, 0);
        var rSlider = new Slider { Minimum = 0, Maximum = 255, Value = initR, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(4, 2, 0, 2) };
        Grid.SetRow(rSlider, 0); Grid.SetColumn(rSlider, 1);
        var rValue = new TextBlock { Text = initR.ToString(), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        Grid.SetRow(rValue, 0); Grid.SetColumn(rValue, 2);
        rSlider.ValueChanged += (s, args) => { initR = (byte)rSlider.Value; rValue.Text = initR.ToString(); UpdateColorPreview(); };

        // G 滑动条
        var gLabel = new TextBlock { Text = "G", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Green };
        Grid.SetRow(gLabel, 1); Grid.SetColumn(gLabel, 0);
        var gSlider = new Slider { Minimum = 0, Maximum = 255, Value = initG, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(4, 2, 0, 2) };
        Grid.SetRow(gSlider, 1); Grid.SetColumn(gSlider, 1);
        var gValue = new TextBlock { Text = initG.ToString(), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        Grid.SetRow(gValue, 1); Grid.SetColumn(gValue, 2);
        gSlider.ValueChanged += (s, args) => { initG = (byte)gSlider.Value; gValue.Text = initG.ToString(); UpdateColorPreview(); };

        // B 滑动条
        var bLabel = new TextBlock { Text = "B", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DodgerBlue };
        Grid.SetRow(bLabel, 2); Grid.SetColumn(bLabel, 0);
        var bSlider = new Slider { Minimum = 0, Maximum = 255, Value = initB, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(4, 2, 0, 2) };
        Grid.SetRow(bSlider, 2); Grid.SetColumn(bSlider, 1);
        var bValue = new TextBlock { Text = initB.ToString(), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        Grid.SetRow(bValue, 2); Grid.SetColumn(bValue, 2);
        bSlider.ValueChanged += (s, args) => { initB = (byte)bSlider.Value; bValue.Text = initB.ToString(); UpdateColorPreview(); };

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

        void EndEdit(bool save)
        {
            if (editEnded) return;
            editEnded = true;
            graceTimer.Stop();

            if (save) vm.Task.Title = textBox.Text;
            outerGrid.Children.Remove(editPanel);

            // 恢复拖拽功能
            TreeViewDragBehavior.SetIsEnabled(TaskTreeView, true);

            // 恢复所有原始子元素的可见性
            // 使用 ClearValue 恢复默认值，避免覆盖 DataTemplate 触发器
            for (int i = 0; i < outerGrid.Children.Count; i++)
            {
                var child = outerGrid.Children[i];
                if (child is StackPanel)
                    child.ClearValue(VisibilityProperty); // ActionButtonsPanel 恢复由触发器控制
                else
                    child.Visibility = Visibility.Visible;
            }
        }

        // 清除颜色按钮
        var clearBtn = new Button
        {
            Content = "清除颜色",
            FontSize = 10,
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Focusable = false,
            Cursor = Cursors.Hand
        };
        clearBtn.Click += (s, args) =>
        {
            vm.Task.Color = null;
            colorPreview.Background = Brushes.Transparent;
            rSlider.Value = 0; gSlider.Value = 0; bSlider.Value = 0;
        };

        // 保存退出按钮
        var saveBtn = new Button
        {
            Content = "保存",
            FontSize = 11,
            Padding = new Thickness(12, 3, 12, 3),
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand
        };
        saveBtn.Click += (s, args) => EndEdit(true);

        rgbPanel.Children.Add(rLabel);
        rgbPanel.Children.Add(rSlider);
        rgbPanel.Children.Add(rValue);
        rgbPanel.Children.Add(gLabel);
        rgbPanel.Children.Add(gSlider);
        rgbPanel.Children.Add(gValue);
        rgbPanel.Children.Add(bLabel);
        rgbPanel.Children.Add(bSlider);
        rgbPanel.Children.Add(bValue);

        editPanel.Children.Add(textBox);
        editPanel.Children.Add(colorPreview);
        editPanel.Children.Add(rgbPanel);

        // 按钮行：清除颜色 + 保存
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 0)
        };
        buttonRow.Children.Add(clearBtn);
        clearBtn.Margin = new Thickness(0);
        buttonRow.Children.Add(saveBtn);
        saveBtn.Margin = new Thickness(8, 0, 0, 0);

        editPanel.Children.Add(buttonRow);

        textBox.LostFocus += (s, args) =>
        {
            // 保护期内不处理 LostFocus
            if (!canClose) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (editEnded) return;
                if (!editPanel.IsAncestorOf(Keyboard.FocusedElement as DependencyObject))
                    EndEdit(true);
            }), System.Windows.Threading.DispatcherPriority.Background);
        };

        // 在编辑面板上用隧道事件统一处理快捷键，无论焦点在 TextBox 还是 Slider 上都能响应
        editPanel.PreviewKeyDown += (s, args) =>
        {
            if (args.Key == Key.Enter) { args.Handled = true; EndEdit(true); }
            else if (args.Key == Key.Escape) { args.Handled = true; EndEdit(false); }
        };

        // 禁用拖拽，避免编辑时误触拖拽操作
        TreeViewDragBehavior.SetIsEnabled(TaskTreeView, false);

        // 隐藏所有原始子元素，进入纯编辑状态
        foreach (UIElement child in outerGrid.Children)
        {
            child.Visibility = Visibility.Collapsed;
        }

        // 添加编辑面板
        outerGrid.Children.Add(editPanel);

        // 延迟聚焦：确保编辑面板完成布局后再获取焦点，避免被 TreeViewItem 抢回
        Dispatcher.BeginInvoke(new Action(() =>
        {
            textBox.Focus();
            textBox.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
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
        while (element != null)
        {
            if (element is FrameworkElement fe && fe.DataContext is TaskItemViewModel vm) return vm;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
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

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        SaveWindowPosition();
        VM.SaveSettingsCommand.Execute(null);
        base.OnClosing(e);
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
            var categoryBorder = FindAncestorOfCategoryBorder(e.OriginalSource as DependencyObject);
            if (categoryBorder?.DataContext is Models.Category cat)
            {
                int? catId = cat.Id > 0 ? cat.Id : null;
                VM.MoveTaskToCategory(draggedItem, catId);
                VM.RefreshCurrentView();
            }
        }
    }

    private static Border? FindAncestorOfCategoryBorder(DependencyObject? current)
    {
        while (current != null)
        {
            if (current is Border border && border.DataContext is Models.Category)
                return border;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

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

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
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