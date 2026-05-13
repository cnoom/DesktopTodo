using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DesktopTodo.Helpers;

/// <summary>
/// 拖拽插入位置指示线装饰器，显示一条橙色水平线指示插入位置
/// </summary>
public class InsertionIndicator : Adorner
{
    private readonly bool _isBefore;
    private readonly Pen _indicatorPen;
    private readonly double _indicatorWidth;

    public InsertionIndicator(UIElement adornedElement, bool isBefore)
        : base(adornedElement)
    {
        _isBefore = isBefore;
        _indicatorWidth = adornedElement.RenderSize.Width;

        // 橙色指示线
        _indicatorPen = new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00)), 2.5)
        {
            DashStyle = new DashStyle(new DoubleCollection { 4, 2 }, 0)
        };

        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var size = AdornedElement.RenderSize;
        double y = _isBefore ? 0 : size.Height;

        // 绘制水平指示线
        drawingContext.DrawLine(_indicatorPen, new Point(2, y), new Point(size.Width - 2, y));

        // 绘制两端的三角箭头标记
        var triangleSize = 5;
        var brush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00));

        if (_isBefore)
        {
            // 左上角三角
            DrawTriangle(drawingContext, brush, new Point(0, y), triangleSize, true);
            // 右上角三角
            DrawTriangle(drawingContext, brush, new Point(size.Width, y), triangleSize, false);
        }
        else
        {
            // 左下角三角
            DrawTriangle(drawingContext, brush, new Point(0, y), triangleSize, false);
            // 右下角三角
            DrawTriangle(drawingContext, brush, new Point(size.Width, y), triangleSize, true);
        }
    }

    private static void DrawTriangle(DrawingContext dc, Brush brush, Point origin, double size, bool pointDown)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(origin, false, true);
            if (pointDown)
            {
                context.LineTo(new Point(origin.X - size, origin.Y - size * 2), true, false);
                context.LineTo(new Point(origin.X + size, origin.Y - size * 2), true, false);
            }
            else
            {
                context.LineTo(new Point(origin.X - size, origin.Y + size * 2), true, false);
                context.LineTo(new Point(origin.X + size, origin.Y + size * 2), true, false);
            }
        }
        dc.DrawGeometry(brush, null, geometry);
    }
}
