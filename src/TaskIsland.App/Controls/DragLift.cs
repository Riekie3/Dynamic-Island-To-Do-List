using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TaskIsland.App.Services;

namespace TaskIsland.App.Controls;

/// <summary>
/// The visual half of a reorder drag: a snapshot of the dragged chip or row floats above the panel and follows the
/// mouse while the real element fades into a placeholder. It is added to the panel's adorner layer, so it is never
/// hit-testable and never changes layout (PLAN.md section 6.2: render-only feedback).
/// </summary>
public sealed class DragLift : IDisposable
{
    /// <summary>The floating copy sits a few pixels above the pointer so the gap underneath stays visible.</summary>
    private const double LiftOffsetY = 3;
    private readonly FrameworkElement _source;
    private readonly AdornerLayer _layer;
    private readonly LiftAdorner _adorner;
    private readonly FrameworkElement _ghost;
    private readonly ScaleTransform _scale;
    private readonly double _grabX;
    private readonly double _grabY;
    private readonly double _sourceOpacity;
    private bool _disposed;
    private bool _removed;

    private DragLift(FrameworkElement source, AdornerLayer layer, LiftAdorner adorner, FrameworkElement ghost,
        ScaleTransform scale, Point grab, double sourceOpacity)
    {
        _source = source;
        _layer = layer;
        _adorner = adorner;
        _ghost = ghost;
        _scale = scale;
        _grabX = grab.X;
        _grabY = grab.Y;
        _sourceOpacity = sourceOpacity;
    }

    /// <summary>
    /// Takes a snapshot of <paramref name="source"/> and starts the pull-out animation. Returns null when the visual
    /// is already gone or the panel has no adorner layer. <paramref name="container"/> defines the coordinate space of
    /// <paramref name="pointer"/>.
    /// </summary>
    public static DragLift? Start(FrameworkElement source, FrameworkElement container, Point pointer,
        double cornerRadius, double liftScale = 1.05, double dimOpacity = 0.2)
    {
        if (!source.IsVisible || source.ActualWidth <= 0 || source.ActualHeight <= 0) return null;
        var layer = AdornerLayer.GetAdornerLayer(container);
        if (layer is null) return null;

        // Snapshot before dimming the source: a VisualBrush would reflect the source's opacity, a bitmap does not.
        var snapshot = Snapshot(source);
        var width = source.ActualWidth;
        var height = source.ActualHeight;

        var icon = new Image
        {
            Source = snapshot,
            Width = width,
            Height = height,
            Stretch = Stretch.Fill,
            IsHitTestVisible = false,
        };

        var shadow = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(cornerRadius),
            Background = new SolidColorBrush(Color.FromArgb(72, 0, 0, 0)),
            Margin = new Thickness(3, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
        };

        // Task rows and some chips have a transparent background, so the floating copy gets the panel surface behind
        // it; otherwise the rows underneath show through the snapshot text.
        var backing = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(cornerRadius),
            Background = SurfaceBrush(source),
            IsHitTestVisible = false,
        };

        var outline = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(cornerRadius),
            BorderThickness = new Thickness(1),
            BorderBrush = OutlineBrush(source),
            IsHitTestVisible = false,
        };

        var ghost = new Grid { Width = width, Height = height, IsHitTestVisible = false };
        ghost.Children.Add(shadow);
        ghost.Children.Add(backing);
        ghost.Children.Add(icon);
        ghost.Children.Add(outline);

        // Pin the scale to the point the user grabbed, so the chip stays under the pointer while it grows.
        var topLeft = source.TransformToAncestor(container).Transform(new Point(0, 0));
        var grab = new Point(pointer.X - topLeft.X, pointer.Y - topLeft.Y);
        ghost.RenderTransformOrigin = new Point(
            Math.Clamp(width <= 0 ? 0.5 : grab.X / width, 0, 1),
            Math.Clamp(height <= 0 ? 0.5 : grab.Y / height, 0, 1));
        var scale = new ScaleTransform(liftScale, liftScale);
        ghost.RenderTransform = scale;
        ghost.Opacity = 0.65;

        var canvas = new Canvas
        {
            Width = Math.Max(1, container.ActualWidth),
            Height = Math.Max(1, container.ActualHeight),
            IsHitTestVisible = false,
            ClipToBounds = false,
        };
        canvas.Children.Add(ghost);
        var adorner = new LiftAdorner(container, canvas);
        layer.Add(adorner);
        Canvas.SetLeft(ghost, pointer.X - grab.X);
        Canvas.SetTop(ghost, pointer.Y - grab.Y - LiftOffsetY);

        var lift = new DragLift(source, layer, adorner, ghost, scale, grab, source.Opacity);
        lift.Play(pointer, liftScale, dimOpacity);
        return lift;
    }

    /// <summary>Keeps the floating copy under the pointer. Coordinates are relative to the adorner's container.</summary>
    public void MoveTo(Point pointer)
    {
        if (_disposed) return;
        Canvas.SetLeft(_ghost, pointer.X - _grabX);
        Canvas.SetTop(_ghost, pointer.Y - _grabY - LiftOffsetY);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var duration = MotionPolicy.Current?.Fast ?? TimeSpan.Zero;
        Anim.To(_source, UIElement.OpacityProperty, _sourceOpacity, duration, FluentEase.Decelerate);
        if (duration <= TimeSpan.Zero)
        {
            Remove();
            return;
        }
        double ms = duration.TotalMilliseconds;
        Anim.Keys(_ghost, UIElement.OpacityProperty, FluentEase.Accelerate, Remove, (0, _ghost.Opacity), (ms, 0));
        Anim.Keys(_scale, ScaleTransform.ScaleXProperty, FluentEase.Accelerate, null, (0, _scale.ScaleX), (ms, _scale.ScaleX * 0.94));
        Anim.Keys(_scale, ScaleTransform.ScaleYProperty, FluentEase.Accelerate, null, (0, _scale.ScaleY), (ms, _scale.ScaleY * 0.94));
    }

    private void Play(Point pointer, double liftScale, double dimOpacity)
    {
        var duration = MotionPolicy.Current?.Fast ?? TimeSpan.Zero;
        if (duration <= TimeSpan.Zero)
        {
            _ghost.Opacity = 1;
            _scale.ScaleX = liftScale;
            _scale.ScaleY = liftScale;
            _source.Opacity = dimOpacity;
            return;
        }

        MoveTo(pointer);
        double ms = duration.TotalMilliseconds;
        Anim.Keys(_ghost, UIElement.OpacityProperty, FluentEase.Decelerate, null, (0, 0.65), (ms * 0.7, 1));
        // A short overshoot sells the "pulled out" feel without an open-ended spring.
        Anim.Keys(_scale, ScaleTransform.ScaleXProperty, FluentEase.Decelerate, null,
            (0, liftScale * 0.9), (ms, liftScale * 1.015), (ms * 1.4, liftScale));
        Anim.Keys(_scale, ScaleTransform.ScaleYProperty, FluentEase.Decelerate, null,
            (0, liftScale * 0.9), (ms, liftScale * 1.015), (ms * 1.4, liftScale));
        Anim.To(_source, UIElement.OpacityProperty, dimOpacity, duration, FluentEase.Decelerate);
    }

    private void Remove()
    {
        if (_removed) return;
        _removed = true;
        _layer.Remove(_adorner);
    }

    private static BitmapSource Snapshot(FrameworkElement source)
    {
        var dpi = VisualTreeHelper.GetDpi(source);
        int width = Math.Max(1, (int)Math.Ceiling(source.ActualWidth * dpi.DpiScaleX));
        int height = Math.Max(1, (int)Math.Ceiling(source.ActualHeight * dpi.DpiScaleY));
        var bitmap = new RenderTargetBitmap(width, height, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        bitmap.Render(source);
        bitmap.Freeze();
        return bitmap;
    }

    private static Brush OutlineBrush(FrameworkElement source) =>
        source.TryFindResource("StrokeBrush") as Brush ?? new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));

    private static Brush SurfaceBrush(FrameworkElement source) =>
        source.TryFindResource("SurfaceBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x15, 0x17, 0x1C));

    /// <summary>An adorner that lays one canvas over the whole panel, above the normal content.</summary>
    private sealed class LiftAdorner : Adorner
    {
        private readonly VisualCollection _children;
        private readonly UIElement _child;

        public LiftAdorner(UIElement adornedElement, UIElement child) : base(adornedElement)
        {
            _child = child;
            _children = new VisualCollection(this) { child };
            IsHitTestVisible = false;
        }

        protected override int VisualChildrenCount => _children.Count;

        protected override Visual GetVisualChild(int index) => _children[index];

        protected override Size MeasureOverride(Size constraint)
        {
            _child.Measure(constraint);
            return base.MeasureOverride(constraint);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _child.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }
}
