using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace DesktopTodo.Helpers;

/// <summary>
/// 系统托盘图标管理器
/// </summary>
public class TrayIconHelper : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private bool _isDisposed;

    /// <summary>
    /// 窗口是否处于隐藏状态
    /// </summary>
    public bool IsHidden { get; private set; }

    /// <summary>
    /// 初始化托盘图标
    /// </summary>
    /// <param name="onToggleVisibility">隐藏/显示窗口的回调</param>
    /// <param name="onResetPosition">重置窗口位置的回调</param>
    /// <param name="onExit">退出应用的回调</param>
    public void Initialize(Action onToggleVisibility, Action onResetPosition, Action onExit)
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "DesktopTodo",
            Visible = true
        };

        var contextMenu = new ContextMenuStrip();

        var toggleItem = new ToolStripMenuItem("隐藏")
        {
            Name = "ToggleVisibility"
        };
        toggleItem.Click += (s, e) =>
        {
            onToggleVisibility();
            UpdateToggleText();
        };

        var resetItem = new ToolStripMenuItem("重置显示位置");
        resetItem.Click += (s, e) => onResetPosition();

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (s, e) => onExit();

        contextMenu.Items.AddRange(new ToolStripItem[] { toggleItem, resetItem, new ToolStripSeparator(), exitItem });
        _notifyIcon.ContextMenuStrip = contextMenu;

        // 双击托盘图标切换显示/隐藏
        _notifyIcon.DoubleClick += (s, e) =>
        {
            onToggleVisibility();
            UpdateToggleText();
        };
    }

    /// <summary>
    /// 更新隐藏/显示菜单项文本
    /// </summary>
    private void UpdateToggleText()
    {
        if (_notifyIcon?.ContextMenuStrip == null) return;

        var toggleItem = _notifyIcon.ContextMenuStrip.Items["ToggleVisibility"] as ToolStripMenuItem;
        if (toggleItem != null)
        {
            toggleItem.Text = IsHidden ? "显示" : "隐藏";
        }
    }

    /// <summary>
    /// 设置隐藏状态（由外部调用更新状态）
    /// </summary>
    /// <param name="hidden">是否隐藏</param>
    public void SetHiddenState(bool hidden)
    {
        IsHidden = hidden;
        UpdateToggleText();
    }

    /// <summary>
    /// 从可执行文件中提取应用程序图标
    /// </summary>
    private static Icon LoadAppIcon()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
                return Icon.ExtractAssociatedIcon(exePath)!;
        }
        catch
        {
            // 提取失败时使用默认图标
        }
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
