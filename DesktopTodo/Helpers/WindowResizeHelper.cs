using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace DesktopTodo.Helpers;

/// <summary>
/// 窗口边缘拖拽调整大小辅助类
/// 仅处理边缘区域的命中测试，其余交给 WPF 正常处理
/// 支持 DPI 缩放下的精确坐标计算
/// </summary>
public static class WindowResizeHelper
{
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_GETMINMAXINFO = 0x0024;

    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

    /// <summary>
    /// 边缘感应区域大小（屏幕像素）
    /// </summary>
    private const int ResizeBorderPixels = 6;

    #region Win32 API

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    #endregion

    private static Window? _window;

    /// <summary>
    /// 为窗口启用边缘拖拽调整大小
    /// </summary>
    public static void Enable(Window window)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));
        _window = window;

        window.SourceInitialized += (s, e) =>
        {
            var helper = new WindowInteropHelper(window);
            HwndSource.FromHwnd(helper.Handle)?.AddHook(WndProc);
        };
    }

    /// <summary>
    /// 获取 DPI 缩放因子（从屏幕像素到 WPF 逻辑单位）
    /// </summary>
    private static double GetDpiScale(Window window)
    {
        var source = PresentationSource.FromVisual(window);
        if (source?.CompositionTarget != null)
        {
            return source.CompositionTarget.TransformFromDevice.M11;
        }
        return 1.0;
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_GETMINMAXINFO:
            {
                if (_window != null)
                {
                    var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                    mmi.ptMinTrackSize.X = (int)_window.MinWidth;
                    mmi.ptMinTrackSize.Y = (int)_window.MinHeight;
                    Marshal.StructureToPtr(mmi, lParam, true);
                }
                handled = true;
                return IntPtr.Zero;
            }

            case WM_NCHITTEST:
            {
                if (_window == null) return IntPtr.Zero;

                // 获取鼠标在窗口客户区中的屏幕像素坐标
                GetCursorPos(out var screenPoint);
                var clientPoint = screenPoint;
                ScreenToClient(hwnd, ref clientPoint);

                int pixelX = clientPoint.X;
                int pixelY = clientPoint.Y;

                // 将窗口 WPF 尺寸转换为屏幕像素尺寸
                double dpiScale = GetDpiScale(_window);
                int pixelWidth = (int)(_window.ActualWidth / dpiScale);
                int pixelHeight = (int)(_window.ActualHeight / dpiScale);

                int border = ResizeBorderPixels;

                bool onLeft = pixelX >= 0 && pixelX <= border;
                bool onRight = pixelX >= pixelWidth - border && pixelX <= pixelWidth;
                bool onTop = pixelY >= 0 && pixelY <= border;
                bool onBottom = pixelY >= pixelHeight - border && pixelY <= pixelHeight;

                // 只处理边缘区域，其余交给 WPF

                // 四角
                if (onTop && onLeft)
                {
                    handled = true;
                    return (IntPtr)HTTOPLEFT;
                }
                if (onTop && onRight)
                {
                    handled = true;
                    return (IntPtr)HTTOPRIGHT;
                }
                if (onBottom && onLeft)
                {
                    handled = true;
                    return (IntPtr)HTBOTTOMLEFT;
                }
                if (onBottom && onRight)
                {
                    handled = true;
                    return (IntPtr)HTBOTTOMRIGHT;
                }

                // 四边
                if (onLeft)
                {
                    handled = true;
                    return (IntPtr)HTLEFT;
                }
                if (onRight)
                {
                    handled = true;
                    return (IntPtr)HTRIGHT;
                }
                if (onTop)
                {
                    handled = true;
                    return (IntPtr)HTTOP;
                }
                if (onBottom)
                {
                    handled = true;
                    return (IntPtr)HTBOTTOM;
                }

                // 非边缘区域：不拦截，交给 WPF
                return IntPtr.Zero;
            }
        }

        return IntPtr.Zero;
    }
}
