using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopTodo.Helpers;

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

    private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_ACTIVATE = 1;
    private const int WM_SETFOCUS = 0x0007;

    public static void EmbedWindow(Window window)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));

        window.Loaded += (s, e) =>
        {
            var helper = new WindowInteropHelper(window);
            IntPtr hWnd = helper.Handle;

            IntPtr progMan = FindWindow(ProgmanClass, "Program Manager");
            if (progMan == IntPtr.Zero)
            {
                Debug.WriteLine("桌面嵌入失败：找不到 Progman 窗口。");
                return;
            }

            SendMessageTimeout(progMan, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);

            IntPtr workerW = IntPtr.Zero;
            IntPtr shellDefView = IntPtr.Zero;
            do
            {
                workerW = FindWindowEx(IntPtr.Zero, workerW, WorkerWClass, null!);
                if (workerW != IntPtr.Zero)
                {
                    shellDefView = FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null!);
                    if (shellDefView != IntPtr.Zero)
                        break;
                }
            } while (workerW != IntPtr.Zero);

            if (workerW == IntPtr.Zero)
            {
                Debug.WriteLine("桌面嵌入失败：找不到 WorkerW 窗口。");
                return;
            }

            SetParent(hWnd, workerW);
            SetWindowPos(hWnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);

            HwndSource source = HwndSource.FromHwnd(hWnd);
            if (source != null)
            {
                source.AddHook(WndProcHook);
            }
        };
    }

    private static IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_MOUSEACTIVATE:
                handled = true;
                return (IntPtr)MA_ACTIVATE;
            case WM_SETFOCUS:
                break;
        }
        return IntPtr.Zero;
    }
}