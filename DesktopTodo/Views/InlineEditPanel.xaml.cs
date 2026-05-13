using System;
using System.Windows.Controls;
using DesktopTodo.ViewModels;

namespace DesktopTodo.Views
{
    /// <summary>
    ///     任务行内编辑面板。
    ///     处理焦点管理和键盘快捷键（Enter 保存、Escape 取消）。
    /// </summary>
    public partial class InlineEditPanel : UserControl
    {
        public InlineEditPanel()
        {
            InitializeComponent();
        }

        public InlineEditPanel(InlineEditViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        /// <summary>
        ///     聚焦标题输入框并选中所有文本
        /// </summary>
        public void FocusTitle()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TitleTextBox.Focus();
                TitleTextBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                (DataContext as InlineEditViewModel)?.SaveCommand.Execute(null);
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
                (DataContext as InlineEditViewModel)?.CancelCommand.Execute(null);
            }
            base.OnPreviewKeyDown(e);
        }
    }
}
