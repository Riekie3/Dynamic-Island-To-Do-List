using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TaskIsland.App.Mascot;
using TaskIsland.App.Services;
using TaskIsland.Core.Mascot;

namespace TaskIsland.App.Controls;

/// <summary>
/// The mascot face (PLAN.md section 6.8): a 240 x 64 canvas with four independent parts (left eye, right eye, mouth,
/// accessory). Each part has its own transforms and two slots, so an expression change fades/scales the old shape out
/// and the new one in, staggered per part. Glows are the same geometry drawn underneath with a wider translucent
/// stroke (no blur effects). All animations are finite and touch render properties only.
/// Built in code rather than XAML because every visual is generated from <see cref="Expressions"/> data.
/// </summary>
public sealed class FaceRig : Canvas
{
    public const double FrameWidth = 240, FrameHeight = 64;

    /// <summary>Mini notch: where the dots sit relative to the eye centres (a 120 x 32 pill sharing the top edge).</summary>
    public static readonly Vector MiniLeftOffset = new(24, -12), MiniRightOffset = new(-24, -12);

    /// <summary>Accessories sit 3 px lower than in the HTML rig so they stay inside the window (the pill touches its top).</summary>
    private const double AccessoryDrop = 3;

    private static readonly Brush Feature = Frozen(new SolidColorBrush(Expressions.FeatureColor));
    private static readonly Brush Glow = Frozen(new SolidColorBrush(Expressions.GlowColor));
    private static readonly Brush AccOutline = Frozen(new SolidColorBrush(Expressions.AccessoryOutlineColor));
    private static readonly Brush Notch = Frozen(new SolidColorBrush(Expressions.NotchColor));
    private static readonly Brush Heart = Frozen(new SolidColorBrush(Expressions.HeartColor));

    private readonly Part _left, _right, _mouth, _acc;
    private readonly TranslateTransform _shake = new();
    private Reaction? _shown;
    private bool _mini;

    public FaceRig() : this(withShell: false) { }

    /// <param name="withShell">Draw its own pill (the small face in the panel header); the island draws its own.</param>
    public FaceRig(bool withShell)
    {
        Width = FrameWidth;
        Height = FrameHeight;
        IsHitTestVisible = false;
        RenderTransform = _shake;
        SnapsToDevicePixels = false;
        if (withShell) AddShell();
        _left = AddPart(Expressions.LeftEyeCentre, accessory: false);
        _right = AddPart(Expressions.RightEyeCentre, accessory: false);
        _mouth = AddPart(Expressions.MouthCentre, accessory: false);
        _acc = AddPart(Expressions.AccessoryCentre, accessory: true);
        _acc.Root.Margin = new Thickness(0, AccessoryDrop, 0, 0);
    }

    public Reaction? Shown => _shown;
    public bool IsMini => _mini;

    private static MotionPolicy? Motion => MotionPolicy.Current;
    private static bool Reduced => Motion?.IsReduced ?? false;

    /// <summary>Shows an expression. <paramref name="arrive"/> = how long the eyes take to move out of the Mini pose.</summary>
    public void Show(Reaction reaction, bool animate, TimeSpan arrive = default)
    {
        var e = Expressions.Get(reaction);
        bool fromMini = _mini;
        bool anim = animate && !Reduced;
        _mini = false;
        _shown = reaction;
        StopMicros();

        double extra = fromMini && anim ? 110 : 0;
        Brush eyeBrush = e.EyeColor is not null ? Heart : Feature;
        _left.Set(e.LeftEye, eyeBrush, anim, 0);
        _right.Set(e.RightEye, eyeBrush, anim, 0);
        _mouth.Set(e.Mouth, Feature, anim, 30 + extra);
        _acc.Set(e.Accessory, Feature, anim, 60 + extra);

        var dur = anim ? (arrive > TimeSpan.Zero ? arrive : TimeSpan.FromMilliseconds(200)) : TimeSpan.Zero;
        foreach (var eye in new[] { _left, _right })
        {
            Anim.To(eye.PoseMove, TranslateTransform.XProperty, 0, dur, FluentEase.Decelerate);
            Anim.To(eye.PoseMove, TranslateTransform.YProperty, 0, dur, FluentEase.Decelerate);
            Anim.To(eye.PoseScale, ScaleTransform.ScaleXProperty, 1, dur, FluentEase.Decelerate);
            Anim.To(eye.PoseScale, ScaleTransform.ScaleYProperty, 1, dur, FluentEase.Decelerate);
        }
        if (anim) PlayMicros(reaction, Math.Max(dur.TotalMilliseconds, 200) + extra);
    }

    /// <summary>Mini notch: two dots (or closed eyes when sleepy) inside the small pill, no mouth or accessory.</summary>
    public void ShowMini(bool sleepy, bool animate, TimeSpan duration = default)
    {
        bool anim = animate && !Reduced;
        StopMicros();
        _mini = true;
        _shown = null;
        var sleepyFace = Expressions.Get(Reaction.Sleepy);
        _left.Set(sleepy ? sleepyFace.LeftEye : Expressions.DotLeft, Feature, anim, 0);
        _right.Set(sleepy ? sleepyFace.RightEye : Expressions.DotRight, Feature, anim, 0);
        _mouth.Set(PartShape.Empty, Feature, anim, 0);
        _acc.Set(PartShape.Empty, Feature, anim, 0);

        var dur = anim ? (duration > TimeSpan.Zero ? duration : TimeSpan.FromMilliseconds(180)) : TimeSpan.Zero;
        double scale = sleepy ? 0.45 : 1;
        foreach (var (eye, offset) in new[] { (_left, MiniLeftOffset), (_right, MiniRightOffset) })
        {
            Anim.To(eye.PoseMove, TranslateTransform.XProperty, offset.X, dur, FluentEase.Accelerate);
            Anim.To(eye.PoseMove, TranslateTransform.YProperty, offset.Y, dur, FluentEase.Accelerate);
            Anim.To(eye.PoseScale, ScaleTransform.ScaleXProperty, scale, dur, FluentEase.Accelerate);
            Anim.To(eye.PoseScale, ScaleTransform.ScaleYProperty, scale, dur, FluentEase.Accelerate);
        }
    }

    // ---- micro-animations (section 6.8 table); each plays once and ends ----

    private void PlayMicros(Reaction r, double start)
    {
        var ease = FluentEase.Decelerate;
        bool blink = r is Reaction.Neutral or Reaction.Happy or Reaction.Curious;
        bool smile = r is Reaction.Happy or Reaction.Wink or Reaction.Love or Reaction.Excited;
        bool bounce = r is Reaction.Laughing or Reaction.Excited;
        bool twinkle = r is Reaction.Excited or Reaction.Focused;
        bool hasAcc = Expressions.Get(r).Accessory != PartShape.Empty;

        if (blink)
        {
            double b = start + 400;
            foreach (var eye in new[] { _left, _right })
                Anim.Keys(eye.MicroScale, ScaleTransform.ScaleYProperty, null, null,
                    (0, 1), (b, 1), (b + 70, 0.1), (b + 140, 1), (b + 1600, 1), (b + 1670, 0.1), (b + 1740, 1));
        }
        if (r == Reaction.Love)
        {
            foreach (var eye in new[] { _left, _right })
                foreach (var dp in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
                    Anim.Keys(eye.MicroScale, dp, ease, null,
                        (0, 1), (start, 1), (start + 150, 1.15), (start + 300, 1), (start + 450, 1.15), (start + 600, 1));
        }
        if (smile)
            Anim.Keys(_mouth.MicroScale, ScaleTransform.ScaleYProperty, ease, null,
                (0, 1), (start - 60, 0.6), (start + 70, 1.1), (start + 140, 1));
        if (bounce)
            foreach (var eye in new[] { _left, _right })
                Anim.Keys(eye.MicroMove, TranslateTransform.YProperty, ease, null,
                    (0, 0), (start, 0), (start + 120, -2), (start + 240, 0), (start + 360, -2), (start + 480, 0));
        if (r == Reaction.Curious)
            foreach (var eye in new[] { _left, _right })
                Anim.Keys(eye.MicroMove, TranslateTransform.XProperty, ease, null,
                    (0, 0), (start, 0), (start + 200, -3), (start + 400, 3), (start + 600, 0));
        if (r == Reaction.Sad)
            Anim.Keys(_shake, TranslateTransform.XProperty, null, null,
                (0, 0), (start, 0), (start + 40, 3), (start + 80, -3), (start + 120, 3), (start + 160, -3),
                (start + 200, 3), (start + 240, -3), (start + 280, 0));
        if (hasAcc)
        {
            // Accessory pop: 0 -> 1.15 -> 1 with a -10 degree turn; Excited/Focused sparkles then twinkle twice.
            double a = start;
            var keys = new List<(double, double)> { (0, 0), (a, 0), (a + 130, 1.15), (a + 200, 1) };
            if (twinkle) keys.AddRange([(a + 350, 0.7), (a + 500, 1), (a + 650, 0.7), (a + 800, 1)]);
            Anim.Keys(_acc.MicroScale, ScaleTransform.ScaleXProperty, ease, null, keys.ToArray());
            Anim.Keys(_acc.MicroScale, ScaleTransform.ScaleYProperty, ease, null, keys.ToArray());
            Anim.Keys(_acc.MicroRotate, RotateTransform.AngleProperty, ease, null, (0, -10), (a, -10), (a + 200, 0));
        }
        if (r == Reaction.Sleepy)
        {
            // zZ drifts up and fades a little, once, and stays there (no loop).
            Anim.Keys(_acc.MicroMove, TranslateTransform.YProperty, ease, null, (0, 0), (start + 200, 0), (start + 1400, -4));
            Anim.Keys(_acc.Root, OpacityProperty, ease, null, (0, 1), (start + 200, 1), (start + 1400, 0.55));
        }
    }

    private void StopMicros()
    {
        foreach (var p in new[] { _left, _right, _mouth, _acc })
        {
            Anim.Stop(p.MicroScale, ScaleTransform.ScaleXProperty, 1);
            Anim.Stop(p.MicroScale, ScaleTransform.ScaleYProperty, 1);
            Anim.Stop(p.MicroRotate, RotateTransform.AngleProperty, 0);
            Anim.Stop(p.MicroMove, TranslateTransform.XProperty, 0);
            Anim.Stop(p.MicroMove, TranslateTransform.YProperty, 0);
            Anim.Stop(p.Root, OpacityProperty, 1);
        }
        Anim.Stop(_shake, TranslateTransform.XProperty, 0);
    }

    // ---- visual tree ----

    private void AddShell()
    {
        var app = Application.Current;
        Rectangle R(double x, double y, double w, double h, double rx) =>
            new() { Width = w, Height = h, RadiusX = rx, RadiusY = rx, Margin = new Thickness(x, y, 0, 0) };
        var ring = R(4, 4, 232, 56, 28);
        ring.Stroke = (Brush)app.FindResource("MascotGlowRingBrush");
        ring.StrokeThickness = 7;
        ring.Opacity = 0.22;
        var body = R(4, 4, 232, 56, 28);
        body.Fill = (Brush)app.FindResource("MascotBodyBrush");
        var gloss = R(14, 8, 212, 20, 10);
        gloss.Fill = (Brush)app.FindResource("MascotGlossBrush");
        var outline = R(4, 4, 232, 56, 28);
        outline.Stroke = (Brush)app.FindResource("MascotOutlineBrush");
        outline.StrokeThickness = 2;
        Children.Add(ring);
        Children.Add(body);
        Children.Add(gloss);
        Children.Add(outline);
    }

    private Part AddPart(Point centre, bool accessory)
    {
        var part = new Part(centre, accessory);
        Children.Add(part.Root);
        return part;
    }

    private static T Frozen<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }

    /// <summary>One face part: transforms for micro-animations and the Mini pose, plus two slots for crossfades.</summary>
    private sealed class Part
    {
        public readonly Canvas Root = new() { Width = FrameWidth, Height = FrameHeight };
        public readonly ScaleTransform MicroScale;
        public readonly RotateTransform MicroRotate;
        public readonly TranslateTransform MicroMove = new();
        public readonly ScaleTransform PoseScale;
        public readonly TranslateTransform PoseMove = new();
        private readonly Slot[] _slots;
        private int _current;
        private PartShape _shape = PartShape.Empty;

        public Part(Point c, bool accessory)
        {
            MicroScale = new ScaleTransform(1, 1, c.X, c.Y);
            MicroRotate = new RotateTransform(0, c.X, c.Y);
            PoseScale = new ScaleTransform(1, 1, c.X, c.Y);
            var g = new TransformGroup();
            g.Children.Add(MicroScale);
            g.Children.Add(MicroRotate);
            g.Children.Add(MicroMove);
            g.Children.Add(PoseScale);
            g.Children.Add(PoseMove);
            Root.RenderTransform = g;
            _slots = [new Slot(c, accessory), new Slot(c, accessory)];
            foreach (var s in _slots) Root.Children.Add(s.Root);
            _slots[1].Root.Opacity = 0;
        }

        /// <summary>Old shape scales to 0.6 and fades (80 ms); new one scales 0.6 -> 1 and fades in (120 ms).</summary>
        public void Set(PartShape shape, Brush brush, bool animate, double delayMs)
        {
            var outgoing = _slots[_current];
            if (ReferenceEquals(shape, _shape) && ReferenceEquals(outgoing.Brush, brush)) return;
            _shape = shape;
            _current = 1 - _current;
            var incoming = _slots[_current];
            incoming.Apply(shape, brush);
            if (!animate)
            {
                outgoing.Hide();
                Anim.Stop(incoming.Root, OpacityProperty, 1);
                Anim.Stop(incoming.Scale, ScaleTransform.ScaleXProperty, 1);
                Anim.Stop(incoming.Scale, ScaleTransform.ScaleYProperty, 1);
                return;
            }
            double d = delayMs, outEnd = d + 80, inEnd = outEnd + 120;
            var ease = FluentEase.Decelerate;
            double o0 = outgoing.Root.Opacity;
            Anim.Keys(outgoing.Root, OpacityProperty, ease, null, (0, o0), (d, o0), (outEnd, 0));
            Anim.Keys(outgoing.Scale, ScaleTransform.ScaleXProperty, ease, null, (0, outgoing.Scale.ScaleX), (d, outgoing.Scale.ScaleX), (outEnd, 0.6));
            Anim.Keys(outgoing.Scale, ScaleTransform.ScaleYProperty, ease, null, (0, outgoing.Scale.ScaleY), (d, outgoing.Scale.ScaleY), (outEnd, 0.6));
            Anim.Keys(incoming.Root, OpacityProperty, ease, null, (0, 0), (outEnd, 0), (inEnd, 1));
            Anim.Keys(incoming.Scale, ScaleTransform.ScaleXProperty, ease, null, (0, 0.6), (outEnd, 0.6), (inEnd, 1));
            Anim.Keys(incoming.Scale, ScaleTransform.ScaleYProperty, ease, null, (0, 0.6), (outEnd, 0.6), (inEnd, 1));
        }
    }

    /// <summary>
    /// Paths for one shape: glow (wide translucent copy) under the face colour, plus the dark notch on top.
    /// Accessories use a blue outline layer instead of the glow.
    /// </summary>
    private sealed class Slot
    {
        public readonly Canvas Root = new() { Width = FrameWidth, Height = FrameHeight };
        public readonly ScaleTransform Scale;
        public Brush? Brush { get; private set; }
        private readonly bool _accessory;
        private readonly Path _underStroke, _underFill, _faceStroke, _faceFill, _dark;

        public Slot(Point c, bool accessory)
        {
            _accessory = accessory;
            Scale = new ScaleTransform(1, 1, c.X, c.Y);
            Root.RenderTransform = Scale;
            double underStroke = accessory ? 8 : 10, underFill = accessory ? 4 : 5, face = accessory ? 3.5 : 5;
            _underStroke = Line(underStroke);
            _underFill = Line(underFill);
            _faceStroke = Line(face);
            _faceFill = new Path();
            _dark = new Path { Fill = Notch };
            if (!accessory)
            {
                _underStroke.Opacity = 0.28;
                _underFill.Opacity = 0.28;
            }
            foreach (var p in new[] { _underStroke, _underFill, _faceStroke, _faceFill, _dark }) Root.Children.Add(p);
        }

        private static Path Line(double thickness) => new()
        {
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };

        public void Apply(PartShape shape, Brush brush)
        {
            Brush = brush;
            Brush under = _accessory ? AccOutline : (ReferenceEquals(brush, Feature) ? Glow : brush);
            _underStroke.Data = shape.Stroke;
            _underStroke.Stroke = under;
            _underFill.Data = shape.Fill;
            _underFill.Fill = under;
            _underFill.Stroke = under;
            _faceStroke.Data = shape.Stroke;
            _faceStroke.Stroke = brush;
            _faceFill.Data = shape.Fill;
            _faceFill.Fill = brush;
            _dark.Data = shape.Dark;
        }

        public void Hide()
        {
            Anim.Stop(Root, OpacityProperty, 0);
            Anim.Stop(Scale, ScaleTransform.ScaleXProperty, 1);
            Anim.Stop(Scale, ScaleTransform.ScaleYProperty, 1);
        }
    }
}
