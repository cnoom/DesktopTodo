using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using DesktopTodo.Services;

namespace DesktopTodo.Helpers;

/// <summary>
/// 迷你模式辅助类，管理窗口缩小为桌面图标的逻辑
/// 包含 DPI 感知的拖拽、屏幕吸附、位置持久化
/// </summary>
public class MiniModeHelper
{
    private readonly Window _window;
    private readonly FrameworkElement _mainContent;
    private readonly FrameworkElement _miniIcon;
    private readonly ISettingsService _settings;

    private bool _isMiniMode;
    private double _normalLeft, _normalTop, _normalWidth, _normalHeight;
    private const double MiniIconSize = 48;

    /// <summary>保存的正常模式窗口位置（供外部读取）</summary>
    public double NormalLeft => _normalLeft;
    /// <summary>保存的正常模式窗口位置（供外部读取）</summary>
    public double NormalTop => _normalTop;
    /// <summary>保存的正常模式窗口尺寸（供外部读取）</summary>
    public double NormalWidth => _normalWidth;
    /// <summary>保存的正常模式窗口尺寸（供外部读取）</summary>
    public double NormalHeight => _normalHeight;

    // 迷你模式拖拽状态
    private bool _isDraggingMini;
    private int _dragStartMouseX;
    private int _dragStartMouseY;
    private int _dragStartWindowX;
    private int _dragStartWindowY;
    private int _currentMiniPixelX;
    private int _currentMiniPixelY;

    /// <summary>迷你模式状态变更通知</summary>
    public bool IsMiniMode => _isMiniMode;

    public MiniModeHelper(Window window, FrameworkElement mainContent, FrameworkElement miniIcon, ISettingsService settings)
    {
        _window = window;
        _mainContent = mainContent;
        _miniIcon = miniIcon;
        _settings = settings;

        // 绑定迷你图标事件
        _miniIcon.MouseLeftButtonDown += MiniModeIcon_MouseLeftButtonDown;
        _miniIcon.MouseMove += MiniModeIcon_MouseMove;
        _miniIcon.MouseLeftButtonUp += MiniModeIcon_MouseLeftButtonUp;
    }

    /// <summary>
    /// 进入迷你模式：缩小为小图标，保持桌面嵌入
    /// </summary>
    public void EnterMiniMode()
    {
        if (_isMiniMode) return;

        // 保存正常模式下的窗口位置和尺寸
        _normalLeft = _window.Left;
        _normalTop = _window.Top;
        _normalWidth = _window.Width;
        _normalHeight = _window.Height;

        _isMiniMode = true;

        // 隐藏主内容，显示迷你图标
        _mainContent.Visibility = Visibility.Collapsed;
        _miniIcon.Visibility = Visibility.Visible;

        // 切换为 NoResize 消除隐形调整边框
        _window.ResizeMode = ResizeMode.NoResize;

        // 禁用边缘调整大小辅助
        WindowResizeHelper.SetEnabled(false);

        // 固定小窗口尺寸
        _window.Width = MiniIconSize;
        _window.Height = MiniIconSize;

        // 读取保存的迷你模式位置
        if (!double.IsNaN(_settings.MiniModeLeft) && !double.IsNaN(_settings.MiniModeTop))
        {
            if (WindowPositionHelper.IsPositionOnScreen(_settings.MiniModeLeft, _settings.MiniModeTop, MiniIconSize, MiniIconSize))
            {
                _window.Left = _settings.MiniModeLeft;
                _window.Top = _settings.MiniModeTop;
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
    /// 退出迷你模式：恢复窗口大小和位置
    /// </summary>
    public void ExitMiniMode()
    {
        if (!_isMiniMode) return;
        _isMiniMode = false;

        // 隐藏迷你图标
        _miniIcon.Visibility = Visibility.Collapsed;

        // 恢复尺寸和位置
        _window.Width = _normalWidth;
        _window.Height = _normalHeight;
        _window.Left = _normalLeft;
        _window.Top = _normalTop;

        // 恢复主内容
        _mainContent.Visibility = Visibility.Visible;

        // 恢复可调整大小模式
        _window.ResizeMode = ResizeMode.CanResize;

        // 重新启用边缘调整大小辅助
        WindowResizeHelper.SetEnabled(true);
    }

    /// <summary>
    /// 保存迷你模式位置到设置
    /// </summary>
    public void SavePosition()
    {
        if (!_isMiniMode) return;
        _settings.MiniModeLeft = _window.Left;
        _settings.MiniModeTop = _window.Top;
        _settings.Save();
    }

    private void SetDefaultMiniPosition()
    {
        _window.Left = _normalLeft + (_normalWidth - MiniIconSize) / 2;
        _window.Top = _normalTop + (_normalHeight - MiniIconSize) / 2;
    }

    #region 迷你图标拖拽事件

    private void MiniModeIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isMiniMode) return;

        _isDraggingMini = false;

        // 记录鼠标按下时的屏幕物理像素坐标
        GetCursorPos(out var mousePixel);
        _dragStartMouseX = mousePixel.X;
        _dragStartMouseY = mousePixel.Y;

        // 用 Win32 GetWindowRect 获取窗口真实屏幕物理像素位置
        var hWnd = new WindowInteropHelper(_window).Handle;
        GetWindowRect(hWnd, out var windowRect);
        _dragStartWindowX = windowRect.Left;
        _dragStartWindowY = windowRect.Top;
        _currentMiniPixelX = _dragStartWindowX;
        _currentMiniPixelY = _dragStartWindowY;

        _miniIcon.CaptureMouse();
        e.Handled = true;
    }

    private void MiniModeIcon_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isMiniMode || e.LeftButton != MouseButtonState.Pressed || !_miniIcon.IsMouseCaptured)
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
            var hWnd = new WindowInteropHelper(_window).Handle;
            SetWindowPos(hWnd, IntPtr.Zero, newPixelX, newPixelY, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }

    private void MiniModeIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isMiniMode || !_miniIcon.IsMouseCaptured) return;

        // 限制在显示器区域内
        ClampToScreen(ref _currentMiniPixelX, ref _currentMiniPixelY);

        _miniIcon.ReleaseMouseCapture();

        if (_isDraggingMini)
        {
            // 将物理像素转回 DIP，同步到 WPF 属性并保存
            var (scaleX, scaleY) = GetDpiScale();
            _window.Left = _currentMiniPixelX / scaleX;
            _window.Top = _currentMiniPixelY / scaleY;

            _settings.MiniModeLeft = _window.Left;
            _settings.MiniModeTop = _window.Top;
            _settings.Save();

            _isDraggingMini = false;
        }
        else
        {
            // 非拖拽 = 单击 → 恢复窗口
            ExitMiniMode();
        }

        e.Handled = true;
    }

    #endregion

    #region Win32 互操作

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public NativeRect rcMonitor;
        public NativeRect rcWork;
        public uint dwFlags;
    }

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    /// <summary>
    /// 将位置限制在鼠标所在显示器的完整区域内（物理像素坐标）
    /// </summary>
    private void ClampToScreen(ref int pixelX, ref int pixelY)
    {
        GetCursorPos(out var cursorPixel);

        var (scaleX, scaleY) = GetDpiScale();
        int windowWidth = (int)(_window.ActualWidth * scaleX);
        int windowHeight = (int)(_window.ActualHeight * scaleY);

        var monitor = MonitorFromWindow(cursorPixel, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var info = new MonitorInfo { cbSize = Marshal.SizeOf(typeof(MonitorInfo)) };
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
        var source = PresentationSource.FromVisual(_window);
        if (source?.CompositionTarget != null)
        {
            return (source.CompositionTarget.TransformToDevice.M11,
                    source.CompositionTarget.TransformToDevice.M22);
        }
        return (1.0, 1.0);
    }

    #endregion
}
