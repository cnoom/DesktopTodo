using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopTodo.Helpers;

/// <summary>
/// 桌面嵌入辅助类，将窗口嵌入到 WorkerW 层，显示在壁纸和桌面图标之间。
/// 支持嵌入失败时的重试机制和回退方案。
/// </summary>
public static class DesktopEmbedHelper
{
    private const string ProgmanClass = "Progman";
    private const string WorkerWClass = "WorkerW";

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    private static readonly IntPtr HWND_BOTTOM = new(1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_ACTIVATE = 1;

    // 用于从 Alt+Tab 列表中隐藏窗口
    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_APPWINDOW = 0x00040000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    // 嵌入状态
    private static bool _isEmbedded;
    private static Window? _embeddedWindow;
    private static HwndSource? _hwndSource;

    /// <summary>窗口是否已成功嵌入桌面</summary>
    public static bool IsEmbedded => _isEmbedded;

    /// <summary>
    /// 将窗口嵌入到 WorkerW 层。
    /// 嵌入失败时自动重试，最终失败则回退为普通窗口模式。
    /// </summary>
    public static void EmbedWindow(Window window)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));

        _embeddedWindow = window;

        window.SourceInitialized += (s, e) =>
        {
            var helper = new WindowInteropHelper(window);
            SetToolWindowStyle(helper.Handle);
        };

        window.Loaded += (s, e) =>
        {
            // 尝试嵌入，最多重试 3 次，间隔递增
            TryEmbedWithRetry(window, maxRetries: 3);
        };
    }

    /// <summary>
    /// 带重试的嵌入逻辑。开机自启时桌面可能尚未就绪。
    /// </summary>
    private static void TryEmbedWithRetry(Window window, int maxRetries)
    {
        var helper = new WindowInteropHelper(window);
        IntPtr hWnd = helper.Handle;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (TryEmbedOnce(hWnd))
            {
                _isEmbedded = true;

                // 嵌入成功后挂载 WndProc 钩子
                _hwndSource = HwndSource.FromHwnd(hWnd);
                if (_hwndSource != null)
                {
                    _hwndSource.AddHook(WndProcHook);
                }

                Debug.WriteLine($"[DesktopEmbed] 桌面嵌入成功（第 {attempt + 1} 次尝试）");
                return;
            }

            if (attempt < maxRetries)
            {
                int delay = (attempt + 1) * 500; // 500ms, 1000ms, 1500ms
                Debug.WriteLine($"[DesktopEmbed] 嵌入失败，{delay}ms 后重试（{attempt + 1}/{maxRetries}）");
                System.Threading.Thread.Sleep(delay);
            }
        }

        // 所有重试均失败，回退为普通窗口模式
        Debug.WriteLine("[DesktopEmbed] 桌面嵌入失败，回退为普通窗口模式");
        FallbackToNormalWindow(window);
    }

    /// <summary>
    /// 单次嵌入尝试。
    /// 找到不包含 SHELLDLL_DefView 的 WorkerW（背景层），嵌入其中。
    /// </summary>
    private static bool TryEmbedOnce(IntPtr hWnd)
    {
        IntPtr progMan = FindWindow(ProgmanClass, "Program Manager");
        if (progMan == IntPtr.Zero)
        {
            return false;
        }

        // 发送消息触发 Progman 分裂出新的 WorkerW
        SendMessageTimeout(progMan, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 2000, out _);

        // 查找包含 SHELLDLL_DefView 的 WorkerW（桌面图标层）
        IntPtr iconWorkerW = IntPtr.Zero;
        IntPtr workerW = IntPtr.Zero;
        do
        {
            workerW = FindWindowEx(IntPtr.Zero, workerW, WorkerWClass, null!);
            if (workerW != IntPtr.Zero)
            {
                IntPtr shellDefView = FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null!);
                if (shellDefView != IntPtr.Zero)
                {
                    iconWorkerW = workerW;
                    break;
                }
            }
        } while (workerW != IntPtr.Zero);

        if (iconWorkerW == IntPtr.Zero)
        {
            return false;
        }

        // 找到不包含 SHELLDLL_DefView 的 WorkerW（背景层）
        // Progman 0x052C 消息会在 iconWorkerW 之后创建一个新的空 WorkerW
        IntPtr backgroundWorkerW = FindWindowEx(IntPtr.Zero, iconWorkerW, WorkerWClass, null!);
        if (backgroundWorkerW == IntPtr.Zero)
        {
            // 未找到背景 WorkerW，尝试直接嵌入到 iconWorkerW（兼容旧方案）
            backgroundWorkerW = iconWorkerW;
        }

        SetParent(hWnd, backgroundWorkerW);
        SetWindowPos(hWnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);

        // 嵌入后再次确保样式正确
        SetToolWindowStyle(hWnd);

        return true;
    }

    /// <summary>
    /// 嵌入失败时的回退方案：让窗口作为普通窗口可见
    /// </summary>
    private static void FallbackToNormalWindow(Window window)
    {
        // 回退时不再设置 WS_EX_TOOLWINDOW，保留 Alt+Tab 切换能力
        // ShowInTaskbar 在 XAML 中为 false，这里需要临时启用以确保用户能找到窗口
        window.ShowInTaskbar = true;
        window.Topmost = true;

        // 1 秒后取消置顶（给用户足够时间发现窗口）
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            window.Topmost = false;
        };
        timer.Start();
    }

    /// <summary>
    /// 设置 WS_EX_TOOLWINDOW 并移除 WS_EX_APPWINDOW，使窗口不出现在 Alt+Tab 中
    /// </summary>
    private static void SetToolWindowStyle(IntPtr hWnd)
    {
        int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        SetWindowLong(hWnd, GWL_EXSTYLE, (exStyle | (int)WS_EX_TOOLWINDOW) & ~(int)WS_EX_APPWINDOW);
    }

    private static IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_MOUSEACTIVATE:
                handled = true;
                return (IntPtr)MA_ACTIVATE;
        }
        return IntPtr.Zero;
    }
}
