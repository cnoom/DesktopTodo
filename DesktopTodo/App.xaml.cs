using System;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;
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

        MainWindow mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.RestoreWindowPosition(_serviceProvider.GetRequiredService<ISettingsService>());
        mainWindow.Show();
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