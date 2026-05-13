using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesktopTodo.Models;

namespace DesktopTodo.ViewModels
{
    /// <summary>
    ///     任务行内编辑 ViewModel，管理标题和颜色的编辑状态。
    ///     编辑确认后通过 SaveCommand 将变更写回 TodoTask。
    /// </summary>
    public class InlineEditViewModel : ObservableObject
    {
        private readonly TaskItemViewModel _taskVm;
        private Action? _onClosed;

        private string _editTitle;
        private byte _r;
        private byte _g;
        private byte _b;
        private bool _hasOriginalColor;

        public string EditTitle
        {
            get => _editTitle;
            set => SetProperty(ref _editTitle, value);
        }

        public byte R
        {
            get => _r;
            set => SetProperty(ref _r, value);
        }

        public byte G
        {
            get => _g;
            set => SetProperty(ref _g, value);
        }

        public byte B
        {
            get => _b;
            set => SetProperty(ref _b, value);
        }

        /// <summary>
        ///     当前颜色预览（十六进制格式）
        /// </summary>
        public string ColorHex => $"#{R:X2}{G:X2}{B:X2}";

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ClearColorCommand { get; }

        public InlineEditViewModel(TaskItemViewModel taskVm, Action? onClosed)
        {
            _taskVm = taskVm;
            _onClosed = onClosed;
            _editTitle = taskVm.Task.Title;

            // 解析当前颜色
            _hasOriginalColor = !string.IsNullOrEmpty(taskVm.Task.Color);
            ParseColor(taskVm.Task.Color);

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
            ClearColorCommand = new RelayCommand(ClearColor);

            // RGB 变化时通知 ColorHex 更新
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName is nameof(R) or nameof(G) or nameof(B))
                {
                    OnPropertyChanged(nameof(ColorHex));
                }
            };
        }

        /// <summary>
        ///     延迟设置关闭回调（解决循环引用：EndEdit 引用 panel，panel 需要 VM，VM 需要 EndEdit）
        /// </summary>
        public void SetOnClosed(Action onClosed)
        {
            _onClosed = onClosed;
        }

        private void Save()
        {
            _taskVm.Task.Title = EditTitle;
            _taskVm.Task.Color = _hasOriginalColor ? ColorHex : null;
            _onClosed?.Invoke();
        }

        private void Cancel()
        {
            _onClosed?.Invoke();
        }

        private void ClearColor()
        {
            _hasOriginalColor = false;
            R = 0;
            G = 0;
            B = 0;
        }

        private void ParseColor(string? color)
        {
            if (string.IsNullOrEmpty(color))
            {
                return;
            }

            try
            {
                var brush = (System.Windows.Media.SolidColorBrush?)
                    new System.Windows.Media.BrushConverter().ConvertFrom(color);
                if (brush != null)
                {
                    R = brush.Color.R;
                    G = brush.Color.G;
                    B = brush.Color.B;
                }
            }
            catch
            {
                // 解析失败保持默认值
            }
        }
    }
}
