using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using TaskIsland.Core.Services;

namespace TaskIsland.App.Controls;

/// <summary>The four island states (PLAN.md section 6.8).</summary>
public enum IslandState { Mini, Reaction, Info, Panel }

/// <summary>
/// The island morph (PLAN.md section 6.2). One <see cref="RectangleGeometry"/> is shared by the background paths and
/// the content clip; a morph only changes that geometry's rectangle/radius and a few opacities, so no element is
/// measured or arranged per frame. Content is laid out once at its final size and revealed by the growing clip.
/// The island may sit on any screen edge (docs/DRAG-PLACE-PLAN.md): the attached side is drawn flat by extending the
/// geometry past the window, and a bottom-attached layout is anchored to the host's bottom so the open panel grows up.
/// </summary>
public sealed class IslandHost : Grid
{
    /// <summary>The usual host (and window) width. A wide reminder pill widens it for a few seconds (<see cref="SetHostWidth"/>).</summary>
    public const double HostWidth = 452;

    /// <summary>
    /// Mini, Reaction and Info are drawn at this scale: callers pass the design sizes (120 x 32, 232 x 56, NotchWidth x 36)
    /// and the face/info content is laid out at the design size under the same scale. The panel is not scaled.
    /// </summary>
    public const double CompactScale = 0.8;

    private static readonly ScaleTransform CompactTransform = Frozen(new ScaleTransform(CompactScale, CompactScale));

    /// <summary>Bottom corner radius of the compact states when flush, as a share of the height (a notch, not a pill).</summary>
    private const double FlushRadius = 0.3;

    public static readonly DependencyProperty MorphProperty = DependencyProperty.Register(
        nameof(Morph), typeof(double), typeof(IslandHost),
        new PropertyMetadata(0.0, (d, e) => ((IslandHost)d).OnMorph((double)e.NewValue)));

    private readonly RectangleGeometry _geo = new(), _glossGeo = new();
    private readonly Path _glow, _body, _panelBody, _gloss, _outline;
    private Path? _pulse;
    private Grid? _alertBorder;
    private double _hostW = HostWidth, _hostH = 64;
    private readonly Border _shadow;
    private readonly Grid _shadowHost = new() { IsHitTestVisible = false };
    private readonly DropShadowEffect _shadowEffect;
    private readonly Grid _clipLayer;
    private readonly TranslateTransform _panelSlide = new();
    private readonly TranslateTransform _contentShift = new();
    private FrameworkElement? _face, _info, _panel;
    private Look _from, _to, _now;
    private Size _requested = new(120, 32);
    private bool _fromPanel, _toPanel, _resetting, _mascotLook = true, _bottomAnchored;
    private IslandEdge _edge = IslandEdge.Top;
    private double _compactCentre = HostWidth / 2, _contentAnchor = HostWidth / 2;
    private int _generation;
    private double _panelOpacity = 0.94;

    public IslandHost()
    {
        Width = HostWidth;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Stretch;
        Background = null;

        _glow = new Path { Data = _geo, StrokeThickness = 7 * CompactScale, IsHitTestVisible = false };
        _glow.SetResourceReference(Shape.StrokeProperty, "MascotGlowRingBrush");

        _shadowEffect = new DropShadowEffect { BlurRadius = 28, ShadowDepth = 8, Direction = 270, Color = Colors.Black, Opacity = 0.35 };
        _shadow = new Border
        {
            CornerRadius = new CornerRadius(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Effect = _shadowEffect,
            CacheMode = new BitmapCache(),
            Visibility = Visibility.Collapsed,
            Opacity = 0,
            IsHitTestVisible = false,
        };
        _shadow.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");

        _body = new Path { Data = _geo };
        _body.SetResourceReference(Shape.FillProperty, "MascotBodyBrush");
        _panelBody = new Path { Data = _geo, Opacity = 0 };
        _panelBody.SetResourceReference(Shape.FillProperty, "SurfaceBrush");
        _gloss = new Path { Data = _glossGeo, IsHitTestVisible = false };
        _gloss.SetResourceReference(Shape.FillProperty, "MascotGlossBrush");
        _clipLayer = new Grid { Clip = _geo, Background = null };
        _outline = new Path { Data = _geo, StrokeThickness = 2 * CompactScale, IsHitTestVisible = false };
        _outline.SetResourceReference(Shape.StrokeProperty, "MascotOutlineBrush");

        _shadowHost.Children.Add(_shadow);
        foreach (var e in new UIElement[] { _glow, _shadowHost, _body, _panelBody, _gloss, _clipLayer, _outline }) Children.Add(e);
        _now = Target(IslandState.Mini, 120, 32);
        Render(_now);
    }

    public double Morph { get => (double)GetValue(MorphProperty); set => SetValue(MorphProperty, value); }

    /// <summary>
    /// Reminder pulse (PLAN.md section 6.2): an accent ring the shape of the island scales 1 → 1.12 and fades out,
    /// <paramref name="cycles"/> times, then the element is removed again, so nothing stays alive afterwards.
    /// </summary>
    public void Pulse(TimeSpan cycle, int cycles)
    {
        if (_pulse is not null) return;
        var geo = _geo.Clone();
        geo.Freeze();
        var r = IslandRect;
        var scale = new ScaleTransform(1, 1, r.Left + r.Width / 2, r.Top + r.Height / 2);
        var ring = new Path { Data = geo, StrokeThickness = 2, IsHitTestVisible = false, RenderTransform = scale };
        ring.SetResourceReference(Shape.StrokeProperty, "AccentBrush");
        _pulse = ring;
        Children.Add(ring);
        double ms = cycle.TotalMilliseconds;
        var grow = new List<(double, double)>();
        var fade = new List<(double, double)>();
        for (int i = 0; i < cycles; i++)
        {
            grow.Add((i * ms, 1)); grow.Add(((i + 1) * ms - 1, 1.12));
            fade.Add((i * ms, 1)); fade.Add(((i + 1) * ms - 1, 0));
        }
        grow.Add((cycles * ms, 1.12));
        fade.Add((cycles * ms, 0));
        void Done()
        {
            Children.Remove(ring);
            if (ReferenceEquals(_pulse, ring)) _pulse = null;
        }
        Anim.Keys(scale, ScaleTransform.ScaleXProperty, FluentEase.Decelerate, null, grow.ToArray());
        Anim.Keys(scale, ScaleTransform.ScaleYProperty, FluentEase.Decelerate, null, grow.ToArray());
        Anim.Keys(ring, OpacityProperty, null, Done, fade.ToArray());
    }

    public IslandState State { get; private set; } = IslandState.Mini;
    public bool IsAnimating { get; private set; }
    /// <summary>Current visible island rectangle in host coordinates.</summary>
    public Rect IslandRect
    {
        get
        {
            double cx = State == IslandState.Panel ? _hostW / 2 : CompactCentreFor(_now.W);
            double y = _bottomAnchored ? _hostH - _now.H : 0;
            return new Rect(cx - _now.W / 2, y, _now.W, _now.H);
        }
    }

    /// <summary>Current host width in DIPs (<see cref="HostWidth"/> except while a wide reminder pill shows).</summary>
    public double CurrentHostWidth => _hostW;

    /// <summary>Current host height in DIPs (64 collapsed, 600 expanded).</summary>
    public double CurrentHostHeight => _hostH;

    /// <summary>
    /// Widens or narrows the host. The island stays centred, and the window is resized by the same amount around the
    /// same centre, so nothing moves on screen.
    /// </summary>
    public void SetHostWidth(double width)
    {
        if (Math.Abs(width - _hostW) < 0.5) return;
        _hostW = width;
        Width = width;
        Render(_now);
    }

    /// <summary>Collapsed 64 or expanded 600. Bottom-anchored layouts are drawn against the host's bottom edge.</summary>
    public void SetHostHeight(double height)
    {
        if (Math.Abs(height - _hostH) < 0.5) return;
        _hostH = height;
        Render(_now);
    }

    private static readonly SolidColorBrush AlertRed = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x30)));

    /// <summary>
    /// Reminder alert border: a red line with a soft red glow on the island's own shape (it follows the morph). With
    /// <paramref name="blink"/> it fades between full and dim about once a second (well under the 3 flashes a second
    /// limit for flashing content); otherwise it stays steady. Removed again by <see cref="StopAlertBorder"/>, so no
    /// animation runs after the alert.
    /// </summary>
    public void StartAlertBorder(bool blink)
    {
        StopAlertBorder();
        var glow = new Path { Data = _geo, Stroke = AlertRed, StrokeThickness = 8, Opacity = 0.4, IsHitTestVisible = false };
        var line = new Path { Data = _geo, Stroke = AlertRed, StrokeThickness = 2.5, IsHitTestVisible = false };
        var ring = new Grid { IsHitTestVisible = false, Background = null };
        ring.Children.Add(glow);
        ring.Children.Add(line);
        _alertBorder = ring;
        Children.Add(ring);
        if (!blink) return;
        var a = new DoubleAnimation(1, 0.12, TimeSpan.FromMilliseconds(450))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        ring.BeginAnimation(OpacityProperty, a);
    }

    public bool HasAlertBorder => _alertBorder is not null;
    public double AlertBorderOpacity => _alertBorder?.Opacity ?? 0;

    public void StopAlertBorder()
    {
        if (_alertBorder is null) return;
        _alertBorder.BeginAnimation(OpacityProperty, null);
        Children.Remove(_alertBorder);
        _alertBorder = null;
    }

    /// <summary>
    /// The screen edge the island touches (docs/DRAG-PLACE-PLAN.md): that side is drawn flat by extending the shared
    /// geometry by its radius, while the other corners stay rounded. <see cref="IslandEdge.None"/> is the round,
    /// floating pill.
    /// </summary>
    public IslandEdge Edge
    {
        get => _edge;
        set
        {
            if (_edge == value) return;
            _edge = value;
            _shadow.CornerRadius = EffectiveEdge switch
            {
                IslandEdge.Top => new CornerRadius(0, 0, 24, 24),
                IslandEdge.Bottom => new CornerRadius(24, 24, 0, 0),
                _ => new CornerRadius(24),
            };
            if (!IsAnimating) Snap(); else Render(_now);
        }
    }

    /// <summary>True when the island is drawn against the bottom of the window (a bottom-edge pill or an upward panel).</summary>
    public bool BottomAnchored
    {
        get => _bottomAnchored;
        set
        {
            if (_bottomAnchored == value) return;
            _bottomAnchored = value;
            if (_panel is not null) _panel.VerticalAlignment = value ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            if (!IsAnimating) Snap(); else Render(_now);
        }
    }

    /// <summary>
    /// Centre of the compact island in host DIPs. Normally the host centre; while the expanded window slides inwards so
    /// the panel fits, this keeps the pill where it was dropped. Side-attached pills align by <see cref="Edge"/>.
    /// </summary>
    public double CompactCentre
    {
        get => _compactCentre;
        set
        {
            if (Math.Abs(_compactCentre - value) < 0.01) return;
            _compactCentre = value;
            if (!IsAnimating) Snap(); else Render(_now);
        }
    }

    /// <summary>Face rig for Mini/Reaction. Not clipped, so accessories may sit over the outline.</summary>
    public FrameworkElement? Face
    {
        get => _face;
        set
        {
            if (_face is not null) Children.Remove(_face);
            _face = value;
            if (value is null) return;
            value.HorizontalAlignment = HorizontalAlignment.Center;
            value.VerticalAlignment = VerticalAlignment.Top;
            value.LayoutTransform = CompactTransform;
            value.RenderTransform = _contentShift;
            value.Margin = new Thickness(0, -4 * CompactScale, 0, 0); // the rig's pill starts at y = 4
            Children.Add(value);
            Render(_now);
        }
    }

    /// <summary>Info pill content (count, ring, peek), clipped by the island shape.</summary>
    public FrameworkElement? Info
    {
        get => _info;
        set
        {
            SetClipped(ref _info, value);
            if (value is not null)
            {
                value.LayoutTransform = CompactTransform;
                value.RenderTransform = _contentShift;
            }
        }
    }

    /// <summary>The to-do panel, created on first expand.</summary>
    public FrameworkElement? Panel
    {
        get => _panel;
        set
        {
            SetClipped(ref _panel, value);
            if (value is not null)
            {
                value.VerticalAlignment = _bottomAnchored ? VerticalAlignment.Bottom : VerticalAlignment.Top;
                value.RenderTransform = _panelSlide;
            }
        }
    }

    public double PanelOpacity
    {
        get => _panelOpacity;
        set { _panelOpacity = value; if (State == IslandState.Panel && !IsAnimating) Snap(); }
    }

    /// <summary>Mascot look (gradient body, neon outline, glow) or the classic black pill.</summary>
    public void SetMascotLook(bool on)
    {
        _mascotLook = on;
        _body.SetResourceReference(Shape.FillProperty, on ? "MascotBodyBrush" : "PillBrush");
        _outline.SetResourceReference(Shape.StrokeProperty, on ? "MascotOutlineBrush" : "StrokeBrush");
        _outline.StrokeThickness = on ? 2 * CompactScale : 1;
        if (!IsAnimating) Snap();
    }

    /// <summary>Classic pill hover colour (the mascot gradient keeps its look).</summary>
    public void SetHover(bool hover)
    {
        if (!_mascotLook) _body.SetResourceReference(Shape.FillProperty, hover ? "PillHoverBrush" : "PillBrush");
    }

    /// <summary>
    /// Lays the panel out at its final width (once, before the expand) and returns its height. The panel never grows
    /// past <paramref name="maxHeight"/>: a pill near the middle gets a shorter, scrolling panel instead of one that
    /// runs off the monitor.
    /// </summary>
    public double MeasurePanel(double maxHeight)
    {
        if (_panel is null) return 0;
        _panel.MaxHeight = maxHeight;
        if (_panel.Visibility != Visibility.Visible)
        {
            _panel.Opacity = 0;
            _panel.Visibility = Visibility.Visible;
        }
        _panel.UpdateLayout();
        return Math.Min(_panel.ActualHeight, maxHeight);
    }

    /// <summary>
    /// Morphs to a state. Only the shared geometry and opacities change per frame. A new call smoothly takes over
    /// from wherever a running morph is. Sizes are design sizes; Mini/Reaction/Info are scaled by <see cref="CompactScale"/>.
    /// </summary>
    public void AnimateTo(IslandState state, double width, double height, TimeSpan duration, IEasingFunction ease,
        Action? completed = null)
    {
        var snapshot = _now;
        _fromPanel = State == IslandState.Panel;
        _toPanel = state == IslandState.Panel;
        if (_toPanel) _contentAnchor = CompactCentreFor(snapshot.W);
        State = state;
        _requested = new Size(width, height);
        _from = snapshot;
        _to = Target(state, width, height);
        int generation = ++_generation;

        Show(_face, _from.Face, _to.Face);
        Show(_info, _from.Info, _to.Info);
        Show(_panel, _from.Panel, _to.Panel);

        _resetting = true;
        BeginAnimation(MorphProperty, null);
        SetValue(MorphProperty, 0.0);
        _resetting = false;
        Render(_from);

        if (duration <= TimeSpan.Zero)
        {
            Finish(generation, completed);
            return;
        }
        IsAnimating = true;
        var anim = new DoubleAnimation(0, 1, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop };
        anim.Completed += (_, _) => Finish(generation, completed);
        BeginAnimation(MorphProperty, anim);
        _resetting = true;
        SetValue(MorphProperty, 1.0); // final value as the local value; the clock ends at the same value
        _resetting = false;
    }

    /// <summary>The cached panel shadow: shown only while still (PLAN.md section 6.2).</summary>
    public void ShowShadow(bool show, TimeSpan fade)
    {
        if (!show || State != IslandState.Panel)
        {
            Anim.Stop(_shadow, OpacityProperty, 0);
            _shadow.Visibility = Visibility.Collapsed;
            return;
        }
        if (TryFindResource("ShadowOpacity") is double o && Math.Abs(_shadowEffect.Opacity - o) > 0.001) _shadowEffect.Opacity = o;
        _shadow.Width = _to.W;
        _shadow.Height = _to.H;
        _shadow.VerticalAlignment = _bottomAnchored ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        _shadow.CornerRadius = EffectiveEdge switch
        {
            IslandEdge.Top => new CornerRadius(0, 0, 24, 24),
            IslandEdge.Bottom => new CornerRadius(24, 24, 0, 0),
            _ => new CornerRadius(24),
        };
        // The shadow layer is solid, so it is cut out under the panel; otherwise it would hide the panel opacity setting.
        double sx = (_hostW - _to.W) / 2;
        double sy = _bottomAnchored ? _hostH - _to.H : 0;
        var flush = EffectiveEdge;
        double left = flush == IslandEdge.Left ? 24 : 0, right = flush == IslandEdge.Right ? 24 : 0;
        double up = flush == IslandEdge.Top ? 24 : 0, down = flush == IslandEdge.Bottom ? 24 : 0;
        _shadowHost.Clip = new CombinedGeometry(GeometryCombineMode.Exclude,
            new RectangleGeometry(new Rect(-100, -100, _hostW + 200, _hostH + 200)),
            new RectangleGeometry(new Rect(sx - left, sy - up, _to.W + left + right, _to.H + up + down), 24, 24));
        _shadow.Visibility = Visibility.Visible;
        Anim.To(_shadow, OpacityProperty, 1, fade);
    }

    private void Finish(int generation, Action? completed)
    {
        if (generation != _generation) return;
        IsAnimating = false;
        Render(_to);
        Hide(_face, _to.Face);
        Hide(_info, _to.Info);
        Hide(_panel, _to.Panel);
        completed?.Invoke();
    }

    private void Snap() => AnimateTo(State, _requested.Width, _requested.Height, TimeSpan.Zero, FluentEase.Decelerate);

    private void OnMorph(double p)
    {
        if (_resetting) return;
        // Background colour lerps over the first half of the way to the panel (and the last half back).
        double c = _toPanel ? Clamp01(p / 0.5) : _fromPanel ? Clamp01((p - 0.5) / 0.5) : p;
        var f = _from;
        var t = _to;
        Render(new Look(
            Lerp(f.W, t.W, p), Lerp(f.H, t.H, p), Lerp(f.R, t.R, p), Lerp(f.Cx, t.Cx, p),
            Lerp(f.Body, t.Body, c), Lerp(f.PanelBody, t.PanelBody, c), Lerp(f.Gloss, t.Gloss, c),
            Lerp(f.Outline, t.Outline, p), Lerp(f.Glow, t.Glow, c),
            Fade(f.Face, t.Face, p), Fade(f.Info, t.Info, p), Fade(f.Panel, t.Panel, p)));
    }

    private void Render(Look l)
    {
        _now = l;
        double x = l.Cx - l.W / 2;
        double y = _bottomAnchored ? _hostH - l.H : 0;
        var flush = EffectiveEdge;
        double left = flush == IslandEdge.Left ? l.R : 0;
        double right = flush == IslandEdge.Right ? l.R : 0;
        double up = flush == IslandEdge.Top ? l.R : 0;
        double down = flush == IslandEdge.Bottom ? l.R : 0;
        _geo.Rect = new Rect(x - left, y - up, l.W + left + right, l.H + up + down);
        _geo.RadiusX = _geo.RadiusY = l.R;
        // The gloss is only visible in the compact states, so it uses their scale.
        const double k = CompactScale;
        double gh = Math.Min(20 * k, l.H * 0.36);
        _glossGeo.Rect = new Rect(x + 10 * k, y + 4 * k, Math.Max(0, l.W - 20 * k), gh);
        _glossGeo.RadiusX = _glossGeo.RadiusY = gh / 2;
        _body.Opacity = l.Body;
        _panelBody.Opacity = l.PanelBody;
        _gloss.Opacity = l.Gloss;
        _outline.Opacity = l.Outline;
        _glow.Opacity = l.Glow * 0.22;
        double anchor = State == IslandState.Panel ? _contentAnchor : CompactCentreFor(l.W);
        _contentShift.X = anchor - _hostW / 2;
        _contentShift.Y = _bottomAnchored ? _hostH - l.H : 0;
        if (_face is not null) _face.Opacity = l.Face;
        if (_info is not null) _info.Opacity = l.Info;
        if (_panel is not null)
        {
            _panel.Opacity = l.Panel;
            _panelSlide.Y = 6 * (1 - l.Panel);
        }
    }

    private Look Target(IslandState state, double w, double h)
    {
        double m = _mascotLook ? 1 : 0;
        if (state != IslandState.Panel)
        {
            w *= CompactScale;
            h *= CompactScale;
        }
        double r = state == IslandState.Panel ? 24
            : EffectiveEdge is IslandEdge.Top or IslandEdge.Bottom ? h * FlushRadius : h / 2;
        double cx = state == IslandState.Panel ? _hostW / 2 : CompactCentreFor(w);
        return state switch
        {
            IslandState.Mini => new Look(w, h, r, cx, 1, 0, m, m, m, 1, 0, 0),
            IslandState.Reaction => new Look(w, h, r, cx, 1, 0, m, m, m, 1, 0, 0),
            IslandState.Info => new Look(w, h, r, cx, 1, 0, m, m, m, 0, 1, 0),
            _ => new Look(w, h, 24, cx, 0, _panelOpacity, 0, _mascotLook ? 0.4 : 1, 0, 0, 0, 1),
        };
    }

    /// <summary>
    /// The side that is flat against the screen for the current top/bottom anchoring. A side-attached panel is drawn
    /// rounded (it is inset by the host's side margin); only the compact pill touches a left or right edge.
    /// </summary>
    private IslandEdge EffectiveEdge => _edge switch
    {
        IslandEdge.Top => _bottomAnchored ? IslandEdge.None : IslandEdge.Top,
        IslandEdge.Bottom => _bottomAnchored ? IslandEdge.Bottom : IslandEdge.None,
        IslandEdge.Left or IslandEdge.Right when State == IslandState.Panel => IslandEdge.None,
        _ => _edge,
    };

    /// <summary>
    /// Where the compact shape is drawn horizontally. A side-attached pill (and a reminder pill that widens from it)
    /// is anchored to that screen side; otherwise it uses the free centre given by the window placement.
    /// </summary>
    private double CompactCentreFor(double width) => _edge switch
    {
        IslandEdge.Left => width / 2,
        IslandEdge.Right => _hostW - width / 2,
        _ => _compactCentre,
    };

    private void SetClipped(ref FrameworkElement? field, FrameworkElement? value)
    {
        if (field is not null) _clipLayer.Children.Remove(field);
        field = value;
        if (value is null) return;
        value.HorizontalAlignment = HorizontalAlignment.Center;
        value.VerticalAlignment = VerticalAlignment.Top;
        value.Visibility = Visibility.Collapsed;
        _clipLayer.Children.Add(value);
        Render(_now);
    }

    private static void Show(FrameworkElement? e, double from, double to)
    {
        if (e is not null && (from > 0 || to > 0)) e.Visibility = Visibility.Visible;
    }

    /// <summary>Hidden content is Collapsed, not just transparent, so layout, rendering and hit testing skip it.</summary>
    private static void Hide(FrameworkElement? e, double to)
    {
        if (e is not null && to <= 0) e.Visibility = Visibility.Collapsed;
    }

    private static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }
    private static double Lerp(double a, double b, double p) => a + (b - a) * p;
    private static double Clamp01(double v) => Math.Clamp(v, 0, 1);

    /// <summary>Crossfade rule: outgoing gone by 30 %, incoming from 35 % on; shared content just blends.</summary>
    private static double Fade(double from, double to, double p) =>
        from > 0 && to > 0 ? Lerp(from, to, p)
        : to > 0 ? to * Clamp01((p - 0.35) / 0.65)
        : from * (1 - Clamp01(p / 0.3));

    private readonly record struct Look(double W, double H, double R, double Cx, double Body, double PanelBody,
        double Gloss, double Outline, double Glow, double Face, double Info, double Panel);
}
