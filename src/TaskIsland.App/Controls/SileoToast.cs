using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using TaskIsland.App.Services;
using TaskIsland.Core.Toasts;

namespace TaskIsland.App.Controls;

/// <summary>A spring curve (<see cref="Spring"/>) for the WPF animation system.</summary>
public sealed class SpringEase(double bounce = Spring.DefaultBounce) : IEasingFunction
{
    public static readonly SpringEase Default = new();
    public static readonly SpringEase NoBounce = new(0);

    public double Ease(double normalizedTime) => Spring.Ease(normalizedTime, bounce);
}

/// <summary>
/// A Sileo-style toast (SILEO-PLAN.md, phase S1): a tab with a state badge and title, and a body that melts down out of
/// it with a spring. Everything is laid out once at its final size; opening and closing only rebuild one outline
/// geometry (shared by the fill and the body's clip) and fade the body, so nothing is measured per frame
/// (PLAN.md section 6.2). Under reduced motion every change is instant.
/// </summary>
public sealed class SileoToast : Grid
{
    public const double ToastWidth = 350;
    public const double TabHeight = 40;
    public const double Roundness = 16;
    /// <summary>Sileo's spring length (DURATION_MS).</summary>
    public static readonly TimeSpan SpringDuration = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan SwapDuration = TimeSpan.FromMilliseconds(420);
    private const double TabPadding = 8;       // left/right padding inside the tab
    private const double TabExtra = 10;        // Sileo's PILL_PADDING
    private const double MinBody = 50;         // Sileo: expanded height is at least 2.25 x the tab

    public static readonly DependencyProperty OpennessProperty = DependencyProperty.Register(
        nameof(Openness), typeof(double), typeof(SileoToast),
        new PropertyMetadata(0.0, (d, _) => ((SileoToast)d).UpdateShape()));

    private readonly StreamGeometry _empty = new();
    private readonly Path _fill;
    private readonly Grid _headerHost;
    private readonly TranslateTransform _headerShift = new();
    private readonly ScaleTransform _headerScale = new(1, 1);
    private readonly Border _body;
    private readonly TextBlock _description;
    private readonly Button _button;
    private FrameworkElement? _currentHeader;
    private RotateTransform? _spinner;
    private TabTween? _tabTween;
    private double _tabWidth = TabHeight;
    private double _bodyHeight;
    private ToastAlign _align = ToastAlign.Left;
    private bool _hasBody;

    public SileoToast()
    {
        Width = ToastWidth;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        ClipToBounds = false;

        _fill = new Path { IsHitTestVisible = true };
        _fill.SetResourceReference(Shape.FillProperty, "ToastFillBrush");
        Children.Add(_fill);

        _headerHost = new Grid
        {
            Height = TabHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TransformGroup { Children = { _headerScale, _headerShift } },
        };
        Children.Add(_headerHost);

        _description = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 13, LineHeight = 19 };
        _description.SetResourceReference(TextBlock.ForegroundProperty, "IslandTextSecondaryBrush");
        _description.SetResourceReference(TextBlock.FontFamilyProperty, "TextFont");
        _button = new Button
        {
            Margin = new Thickness(0, 10, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Visibility = Visibility.Collapsed,
            Template = ButtonTemplate(),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        _button.Click += (_, _) => ButtonClick?.Invoke(this, EventArgs.Empty);
        _body = new Border
        {
            Margin = new Thickness(0, TabHeight, 0, 0),
            Padding = new Thickness(14, 8, 14, 14),
            VerticalAlignment = VerticalAlignment.Top,
            Opacity = 0,
            Child = new StackPanel { Children = { _description, _button } },
        };
        Children.Add(_body);

        SetContent(ToastState.Success, "Done", null, null, animate: false);
    }

    /// <summary>0 = only the tab, 1 = fully open (the spring briefly goes a little above 1).</summary>
    public double Openness
    {
        get => (double)GetValue(OpennessProperty);
        set => SetValue(OpennessProperty, value);
    }

    public ToastAlign Align
    {
        get => _align;
        set { _align = value; UpdateShape(); }
    }

    public ToastState State { get; private set; }
    public string Title { get; private set; } = "";
    public bool HasBody => _hasBody;
    public bool IsOpen { get; private set; }

    /// <summary>Total height when fully open (tab + body). The control always reserves this, so it never re-lays out.</summary>
    public double OpenHeight => TabHeight + _bodyHeight;

    public event EventHandler? ButtonClick;

    /// <summary>
    /// Shows new content. When the toast is open it closes first, then the header crossfades in place (the old title
    /// blurs out) and the body text is swapped, like Sileo's refresh.
    /// </summary>
    public void SetContent(ToastState state, string title, string? description, string? buttonText, bool animate = true)
    {
        animate &= !Reduced;
        bool headerChanged = state != State || title != Title || _currentHeader is null;
        State = state;
        Title = title;

        if (headerChanged) SwapHeader(state, title, animate);

        _description.Text = description ?? "";
        _description.Visibility = string.IsNullOrEmpty(description) ? Visibility.Collapsed : Visibility.Visible;
        if (string.IsNullOrEmpty(buttonText))
        {
            _button.Visibility = Visibility.Collapsed;
        }
        else
        {
            _button.Content = buttonText;
            _button.Foreground = StateBrush(state);
            _button.Background = SoftBrush(state);
            _button.Visibility = Visibility.Visible;
        }
        _hasBody = state != ToastState.Loading && (!string.IsNullOrEmpty(description) || !string.IsNullOrEmpty(buttonText));
        MeasureBody();
        if (!_hasBody && IsOpen) Close(animate);
        UpdateShape();
    }

    public void Open(bool animate = true)
    {
        if (!_hasBody) return;
        IsOpen = true;
        AnimateOpenness(1, animate ? SpringEase.Default : null);
        Anim.To(_body, OpacityProperty, 1, Duration(animate, 0.5), FluentEase.Decelerate);
        // Sileo nudges the title down and shrinks it a touch while open.
        Anim.To(_headerShift, TranslateTransform.YProperty, 3, Duration(animate, 1), SpringEase.Default);
        Anim.To(_headerScale, ScaleTransform.ScaleXProperty, 0.9, Duration(animate, 1), SpringEase.Default);
        Anim.To(_headerScale, ScaleTransform.ScaleYProperty, 0.9, Duration(animate, 1), SpringEase.Default);
    }

    public void Close(bool animate = true)
    {
        IsOpen = false;
        AnimateOpenness(0, animate ? SpringEase.NoBounce : null);
        Anim.To(_body, OpacityProperty, 0, Duration(animate, 0.35), FluentEase.Accelerate);
        Anim.To(_headerShift, TranslateTransform.YProperty, 0, Duration(animate, 1), SpringEase.NoBounce);
        Anim.To(_headerScale, ScaleTransform.ScaleXProperty, 1, Duration(animate, 1), SpringEase.NoBounce);
        Anim.To(_headerScale, ScaleTransform.ScaleYProperty, 1, Duration(animate, 1), SpringEase.NoBounce);
    }

    // ---- shape ----

    private void AnimateOpenness(double to, IEasingFunction? ease)
    {
        if (ease is null) Anim.Stop(this, OpennessProperty, to);
        else Anim.To(this, OpennessProperty, to, SpringDuration, ease);
    }

    private void UpdateShape()
    {
        double open = Math.Max(0, Openness);
        double body = _hasBody ? _bodyHeight * open : 0;
        double tabX = ToastOutline.TabX(_align, ToastWidth, _tabWidth);
        var outline = ToastOutline.Build(ToastWidth, tabX, _tabWidth, TabHeight, body, Roundness);
        var geo = ToGeometry(outline);
        _fill.Data = geo;
        _body.Clip = geo.Bounds.IsEmpty ? _empty : ShiftedClip(geo);
        _headerHost.Margin = new Thickness(tabX, 0, 0, 0);
        _headerHost.Width = _tabWidth;
    }

    /// <summary>The body sits below the tab, so its clip is the outline moved up by the tab height.</summary>
    private static Geometry ShiftedClip(Geometry outline)
    {
        var clip = outline.Clone();
        clip.Transform = new TranslateTransform(0, -TabHeight);
        clip.Freeze();
        return clip;
    }

    private static StreamGeometry ToGeometry(Outline o)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(o.StartX, o.StartY), isFilled: true, isClosed: true);
            foreach (var s in o.Steps)
            {
                var p = new Point(s.X, s.Y);
                if (s.IsArc && s.Radius > 0)
                    ctx.ArcTo(p, new Size(s.Radius, s.Radius), 0, false,
                        s.Clockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, true, true);
                else
                    ctx.LineTo(p, true, true);
            }
        }
        g.Freeze();
        return g;
    }

    private void MeasureBody()
    {
        // Outside a live layout pass WPF keeps the old DesiredSize unless every level is marked dirty.
        _description.InvalidateMeasure();
        _button.InvalidateMeasure();
        ((UIElement)_body.Child).InvalidateMeasure();
        _body.InvalidateMeasure();
        _body.Measure(new Size(ToastWidth, double.PositiveInfinity));
        double content = _body.DesiredSize.Height - _body.Margin.Top;   // the margin is the tab's room, not body
        _bodyHeight = _hasBody ? Math.Max(MinBody, content) : 0;
        Height = TabHeight + _bodyHeight;
    }

    // ---- header ----

    private void SwapHeader(ToastState state, string title, bool animate)
    {
        var header = BuildHeader(state, title);
        header.Measure(new Size(double.PositiveInfinity, TabHeight));
        double newWidth = Math.Min(ToastWidth, header.DesiredSize.Width + TabPadding * 2 + TabExtra);

        var old = _currentHeader;
        _currentHeader = header;
        _headerHost.Children.Add(header);

        if (old is not null && animate)
        {
            // Old title blurs and fades out; the new one blurs in. The blur exists only for the swap.
            var blurOut = new BlurEffect { Radius = 0 };
            old.Effect = blurOut;
            Anim.To(old, OpacityProperty, 0, SwapDuration, FluentEase.Accelerate,
                completed: () => _headerHost.Children.Remove(old));
            Anim.To(blurOut, BlurEffect.RadiusProperty, 6, SwapDuration, FluentEase.Accelerate);

            var blurIn = new BlurEffect { Radius = 6 };
            header.Effect = blurIn;
            header.Opacity = 0;
            Anim.To(header, OpacityProperty, 1, SpringDuration, FluentEase.Decelerate);
            Anim.To(blurIn, BlurEffect.RadiusProperty, 0, SpringDuration, FluentEase.Decelerate,
                completed: () => { if (header.Effect == blurIn) header.Effect = null; });

            double from = _tabWidth;
            AnimateTabWidth(from, newWidth);
        }
        else
        {
            if (old is not null) _headerHost.Children.Remove(old);
            _tabTween?.Cancel();
            _tabWidth = newWidth;
        }
        UpdateSpinner(state, header);
    }

    /// <summary>The tab's width springs to the new title's width (Sileo: pill width follows its text).</summary>
    private void AnimateTabWidth(double from, double to)
    {
        _tabTween?.Cancel();   // a swap during a swap continues from where the tab is now
        _tabTween = new TabTween(this, from, to);
        Anim.To(_tabTween, TabTween.ProgressProperty, 1, SpringDuration, SpringEase.Default);
    }

    private sealed class TabTween : Animatable
    {
        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
            nameof(Progress), typeof(double), typeof(TabTween),
            new PropertyMetadata(0.0, (d, e) => ((TabTween)d).Apply((double)e.NewValue)));

        private SileoToast? _owner;
        private readonly double _from, _to;

        public TabTween() { }

        public TabTween(SileoToast owner, double from, double to)
        {
            _owner = owner;
            _from = from;
            _to = to;
        }

        public void Cancel()
        {
            _owner = null;
            BeginAnimation(ProgressProperty, null);
        }

        public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

        private void Apply(double p)
        {
            if (_owner is null) return;
            _owner._tabWidth = Math.Max(TabHeight, _from + (_to - _from) * p);
            _owner.UpdateShape();
        }

        protected override Freezable CreateInstanceCore() => new TabTween();
    }

    private FrameworkElement BuildHeader(ToastState state, string title)
    {
        var glyph = new TextBlock
        {
            Text = Glyph(state),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = StateBrush(state),
            RenderTransformOrigin = new Point(0.5, 0.5),
        };
        glyph.SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");
        var badge = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(12),
            Background = SoftBrush(state),
            Child = glyph,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var text = new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = StateBrush(state),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = ToastWidth - 24 - TabPadding * 2 - TabExtra - 8,
        };
        text.SetResourceReference(TextBlock.FontFamilyProperty, "TextFont");
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(TabPadding, 0, TabPadding, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { badge, text },
            Tag = glyph,
        };
    }

    /// <summary>The loading badge spins; the rotation runs only while a Loading header is shown.</summary>
    private void UpdateSpinner(ToastState state, FrameworkElement header)
    {
        _spinner?.BeginAnimation(RotateTransform.AngleProperty, null);
        _spinner = null;
        if (state != ToastState.Loading || header.Tag is not TextBlock glyph || Reduced) return;
        _spinner = new RotateTransform();
        glyph.RenderTransform = _spinner;
        _spinner.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1)) { RepeatBehavior = RepeatBehavior.Forever });
    }

    // ---- look ----

    private static bool Reduced => MotionPolicy.Current?.IsReduced ?? false;

    private static TimeSpan Duration(bool animate, double share) =>
        animate ? TimeSpan.FromMilliseconds(SpringDuration.TotalMilliseconds * share) : TimeSpan.Zero;

    private static string Glyph(ToastState state) => state switch
    {
        ToastState.Success => "",   // CheckMark
        ToastState.Error => "",     // Cancel
        ToastState.Warning => "",   // Warning
        ToastState.Info => "",      // Info
        ToastState.Action => "",    // Forward
        _ => "",                    // Sync (spins)
    };

    private Brush StateBrush(ToastState state) => (Brush)FindResource($"Toast{state}Brush");

    private Brush SoftBrush(ToastState state) => (Brush)FindResource($"Toast{state}SoftBrush");

    private static ControlTemplate ButtonTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        border.SetValue(Border.PaddingProperty, new Thickness(12, 5, 12, 6));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
        {
            RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent,
        });
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(TextBlock.FontSizeProperty, 12.5);
        content.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        border.AppendChild(content);
        return new ControlTemplate(typeof(Button)) { VisualTree = border };
    }
}
