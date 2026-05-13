using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopTodo.ViewModels;

namespace DesktopTodo.Helpers;

/// <summary>
/// 全局键盘快捷键管理器，处理应用程序级别的快捷键绑定
/// </summary>
public class KeyboardShortcutManager
{
    private readonly Window _window;
    private readonly MainViewModel _vm;

    // 外部回调
    private readonly Action _focusTaskInput;
    private readonly Action _startEditSelectedTask;
    private readonly Action _toggleSettingsPopup;

    public KeyboardShortcutManager(
        Window window,
        MainViewModel vm,
        Action focusTaskInput,
        Action startEditSelectedTask,
        Action toggleSettingsPopup)
    {
        _window = window;
        _vm = vm;
        _focusTaskInput = focusTaskInput;
        _startEditSelectedTask = startEditSelectedTask;
        _toggleSettingsPopup = toggleSettingsPopup;

        _window.PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var isInTextBox = Keyboard.FocusedElement is TextBox;

        switch (e.Key)
        {
            case Key.N when Keyboard.Modifiers == ModifierKeys.Control && !isInTextBox:
                // Ctrl+N：聚焦任务输入框
                _focusTaskInput();
                e.Handled = true;
                break;

            case Key.N when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
                // Ctrl+Shift+N：添加子任务（需先选中父任务）
                if (_vm.SelectedTask != null)
                {
                    _vm.AddSubTaskCommand.Execute(_vm.SelectedTask);
                    _focusTaskInput();
                }
                e.Handled = true;
                break;

            case Key.F2:
                // F2：重命名选中任务
                if (_vm.SelectedTask != null && !isInTextBox)
                {
                    _startEditSelectedTask();
                }
                e.Handled = true;
                break;

            case Key.Delete:
                // Delete：删除选中任务
                if (_vm.SelectedTask != null && !isInTextBox)
                {
                    _vm.DeleteTaskCommand.Execute(_vm.SelectedTask);
                }
                e.Handled = true;
                break;

            case Key.Space:
                // Space：切换选中任务的完成状态
                if (_vm.SelectedTask != null && !isInTextBox)
                {
                    _vm.SelectedTask.Task.IsCompleted = !_vm.SelectedTask.Task.IsCompleted;
                }
                e.Handled = true;
                break;

            case Key.OemComma when Keyboard.Modifiers == ModifierKeys.Control:
                // Ctrl+,：切换设置面板
                _toggleSettingsPopup();
                e.Handled = true;
                break;
        }
    }
}
