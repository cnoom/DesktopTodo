using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DesktopTodo.Services;

namespace DesktopTodo.Helpers;

/// <summary>
/// 窗口位置管理辅助类，负责窗口位置的保存、恢复和屏幕检测
/// </summary>
public static class WindowPositionHelper
{
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

    #endregion

    /// <summary>
    /// 检查指定矩形是否与任意显示器的工作区域有交集。
    /// 输入为 WPF DIP 坐标，自动处理 DPI 缩放转换。
    /// </summary>
    public static bool IsPositionOnScreen(double x, double y, double w, double h)
    {
        // 获取主窗口的 DPI 缩放因子（DIP → 物理像素）
        double dpiScaleX = 1.0, dpiScaleY = 1.0;
        var source = PresentationSource.FromVisual(Application.Current?.MainWindow);
        if (source?.CompositionTarget != null)
        {
            dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
            dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
        }

        // 将 DIP 坐标转换为物理像素坐标
        var rect = new RECT
        {
            Left = (int)(x * dpiScaleX),
            Top = (int)(y * dpiScaleY),
            Right = (int)((x + w) * dpiScaleX),
            Bottom = (int)((y + h) * dpiScaleY)
        };
        bool found = false;

        MonitorEnumProc callback = (hMonitor, _, _, _) =>
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                var work = monitorInfo.rcWork;
                if (rect.Left < work.Right && rect.Right > work.Left &&
                    rect.Top < work.Bottom && rect.Bottom > work.Top)
                {
                    found = true;
                    return false;
                }
            }
            return true;
        };

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        return found;
    }

    /// <summary>
    /// 恢复窗口位置和尺寸
    /// </summary>
    public static void RestoreWindowPosition(Window window, ISettingsService settings)
    {
        if (!double.IsNaN(settings.WindowWidth) && !double.IsNaN(settings.WindowHeight))
        {
            window.Width = Math.Max(settings.WindowWidth, window.MinWidth);
            window.Height = Math.Max(settings.WindowHeight, window.MinHeight);
        }

        if (!double.IsNaN(settings.WindowLeft) && !double.IsNaN(settings.WindowTop))
        {
            if (IsPositionOnScreen(settings.WindowLeft, settings.WindowTop, window.Width, window.Height))
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = settings.WindowLeft;
                window.Top = settings.WindowTop;
            }
        }
    }

    /// <summary>
    /// 保存窗口位置和尺寸到设置
    /// </summary>
    public static void SaveWindowPosition(Window window, ISettingsService settings, bool isMiniMode,
        double normalLeft, double normalTop, double normalWidth, double normalHeight)
    {
        if (isMiniMode)
        {
            settings.WindowLeft = normalLeft;
            settings.WindowTop = normalTop;
            settings.WindowWidth = normalWidth;
            settings.WindowHeight = normalHeight;
        }
        else
        {
            settings.WindowLeft = window.Left;
            settings.WindowTop = window.Top;
            settings.WindowWidth = window.Width;
            settings.WindowHeight = window.Height;
        }
    }

    /// <summary>
    /// 重置窗口位置到屏幕中央
    /// </summary>
    public static void ResetToScreenCenter(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.Left = (SystemParameters.PrimaryScreenWidth - window.Width) / 2;
        window.Top = (SystemParameters.PrimaryScreenHeight - window.Height) / 2;
    }
}
