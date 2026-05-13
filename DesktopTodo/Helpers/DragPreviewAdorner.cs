using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DesktopTodo.Helpers;

/// <summary>
/// 拖拽预览装饰器，在拖拽时显示半透明的任务标题预览
/// </summary>
public class DragPreviewAdorner : Adorner
{
    private readonly VisualCollection _visuals;
    private readonly ContentPresenter _contentPresenter;
    private readonly UIElement _adornedElement;
    private Point _offset;

    public DragPreviewAdorner(UIElement adornedElement, object data, double opacity = 0.75)
        : base(adornedElement)
    {
        _adornedElement = adornedElement;

        _contentPresenter = new ContentPresenter
        {
            Content = data,
            Opacity = opacity
        };

        _visuals = new VisualCollection(this) { _contentPresenter };
    }

    /// <summary>
    /// 设置拖拽偏移量（鼠标相对于拖拽项左上角的偏移）
    /// </summary>
    public Point Offset
    {
        get => _offset;
        set
        {
            _offset = value;
            _contentPresenter.RenderTransform = new TranslateTransform(-_offset.X, -_offset.Y);
        }
    }

    protected override Visual GetVisualChild(int index) => _visuals[index];
    protected override int VisualChildrenCount => _visuals.Count;

    protected override Size MeasureOverride(Size constraint)
    {
        _contentPresenter.Measure(constraint);
        return _contentPresenter.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _contentPresenter.Arrange(new Rect(finalSize));
        return finalSize;
    }
}
