using System;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;
using DesktopTodo.Helpers;
using DesktopTodo.Interfaces;
using DesktopTodo.Services;
using DesktopTodo.ViewModels;
using DesktopTodo.Views;

namespace DesktopTodo;

public partial class App : Application
{
    private const string MutexName = "DesktopTodo_SingleInstance_Mutex";
    private Mutex? _mutex;
    private bool _ownsMutex;
    private ServiceProvider _serviceProvider = null!;
    private TrayIconHelper? _trayIcon;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 单实例检测
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // 已有实例在运行，激活已有窗口后退出
            _ownsMutex = false;
            ActivateExistingWindow();
            Shutdown();
            return;
        }

        _ownsMutex = true;

        // 关闭最后一个窗口时不自动退出，由托盘图标控制退出
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        base.OnStartup(e);

        var services = new ServiceCollection();

        // 注册服务
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDialogService, DialogService>();

        // 注册 ViewModel
        services.AddSingleton<MainViewModel>();

        // 注册 MainWindow 并绑定 DataContext
        services.AddSingleton<MainWindow>(sp =>
        {
            var window = new MainWindow();
            window.DataContext = sp.GetRequiredService<MainViewModel>();
            return window;
        });

        _serviceProvider = services.BuildServiceProvider();

        _mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        _mainWindow.RestoreWindowPosition(_serviceProvider.GetRequiredService<ISettingsService>());

        // 初始化托盘图标
        _trayIcon = new TrayIconHelper();
        _trayIcon.Initialize(
            onToggleVisibility: ToggleMainWindowVisibility,
            onResetPosition: ResetMainWindowPosition,
            onExit: ExitApp
        );

        // 拦截窗口关闭事件，隐藏到托盘而非退出
        _mainWindow.Closing += (s, args) =>
        {
            // 如果是真正退出，不拦截
            if (_isExiting) return;

            args.Cancel = true;
            HideMainWindow();
        };

        _mainWindow.Show();
    }

    /// <summary>
    /// 是否正在真正退出应用
    /// </summary>
    private bool _isExiting;

    /// <summary>
    /// 切换主窗口显示/隐藏
    /// </summary>
    private void ToggleMainWindowVisibility()
    {
        if (_mainWindow == null) return;

        if (_trayIcon!.IsHidden)
        {
            ShowMainWindow();
        }
        else
        {
            HideMainWindow();
        }
    }

    /// <summary>
    /// 隐藏主窗口
    /// </summary>
    private void HideMainWindow()
    {
        if (_mainWindow == null) return;

        _mainWindow.SaveWindowPosition();
        _mainWindow.VM.SaveSettingsCommand.Execute(null);
        _mainWindow.Hide();
        _trayIcon!.SetHiddenState(true);
    }

    /// <summary>
    /// 显示主窗口
    /// </summary>
    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;

        _mainWindow.Show();
        _trayIcon!.SetHiddenState(false);
    }

    /// <summary>
    /// 重置主窗口显示位置到屏幕中央
    /// </summary>
    private void ResetMainWindowPosition()
    {
        if (_mainWindow == null) return;

        // 如果窗口处于隐藏状态，先显示
        if (_trayIcon!.IsHidden)
        {
            _mainWindow.Show();
            _trayIcon.SetHiddenState(false);
        }

        _mainWindow.ResetToScreenCenter();
    }

    /// <summary>
    /// 退出应用
    /// </summary>
    private void ExitApp()
    {
        _isExiting = true;

        if (_mainWindow != null)
        {
            _mainWindow.SaveWindowPosition();
            _mainWindow.VM.SaveSettingsCommand.Execute(null);
        }

        _trayIcon?.Dispose();
        _trayIcon = null;

        Shutdown();
    }

    /// <summary>
    /// 激活已存在的窗口
    /// </summary>
    private void ActivateExistingWindow()
    {
        try
        {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            foreach (var process in System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName))
            {
                if (process.Id != currentProcess.Id && process.MainWindowHandle != IntPtr.Zero)
                {
                    // 恢复窗口（如果最小化）
                    ShowWindow(process.MainWindowHandle, SW_RESTORE);
                    // 置顶窗口
                    SetForegroundWindow(process.MainWindowHandle);
                    break;
                }
            }
        }
        catch
        {
            // 激活失败忽略
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();

        if (_ownsMutex)
            _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private const int SW_RESTORE = 9;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
