namespace StockPin.Views;

using System.Windows.Documents;

/// <summary>Draws the floating row preview at an offset inside the window's adorner layer.</summary>
internal sealed class DragGhostAdorner : Adorner
{
    private readonly VisualCollection _children;
    private readonly UIElement _ghost;
    private Point _offset;

    public DragGhostAdorner(UIElement adornedElement, UIElement ghost) : base(adornedElement)
    {
        _ghost = ghost;
        _children = new VisualCollection(this) { ghost };
        IsHitTestVisible = false;
    }

    protected override int VisualChildrenCount => _children.Count;

    protected override Visual GetVisualChild(int index) => _children[index];

    public void MoveTo(Point offset)
    {
        _offset = offset;
        InvalidateArrange();
    }

    protected override Size MeasureOverride(Size constraint)
    {
        _ghost.Measure(constraint);
        return _ghost.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _ghost.Arrange(new Rect(_offset, _ghost.DesiredSize));
        return finalSize;
    }
}
