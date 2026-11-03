using System.Windows;
using System.Windows.Media;
using TaskIsland.Core.Mascot;

namespace TaskIsland.App.Mascot;

/// <summary>
/// The 12 expressions plus the Mini notch dots, copied 1:1 from docs/design/mascot-faces.html (the geometry source of
/// truth). Frame 240 x 64: eye centres (86,32) and (154,32), mouth centre (120,42), accessories top right.
/// Geometries are parsed once and frozen.
/// </summary>
public static class Expressions
{
    public static readonly Point LeftEyeCentre = new(86, 32);
    public static readonly Point RightEyeCentre = new(154, 32);
    public static readonly Point MouthCentre = new(120, 42);
    public static readonly Point AccessoryCentre = new(220, 14);

    public static readonly Color FeatureColor = Color.FromRgb(0xEA, 0xF2, 0xFF);
    public static readonly Color GlowColor = Color.FromRgb(0x7F, 0xB6, 0xFF);
    public static readonly Color HeartColor = Color.FromRgb(0x8F, 0xD3, 0xFF);
    public static readonly Color AccessoryOutlineColor = Color.FromRgb(0x3D, 0x5A, 0xFE);
    public static readonly Color NotchColor = Color.FromRgb(0x0B, 0x0C, 0x16);

    private static readonly Dictionary<Reaction, Expression> ByReaction = Build();

    /// <summary>Mini notch: two small dots at the eye centres (the rig moves them into the small pill).</summary>
    public static readonly PartShape DotLeft = new(null, Circle(86, 32, 3.2));
    public static readonly PartShape DotRight = new(null, Circle(154, 32, 3.2));

    public static Expression Get(Reaction reaction) => ByReaction[reaction];

    private static Dictionary<Reaction, Expression> Build()
    {
        // Eyes
        var neutralL = F(Capsule(81, 21, 10, 22, 5));
        var neutralR = F(Capsule(149, 21, 10, 22, 5));
        var happyL = S(P("M75,34 Q86,22 97,34"));
        var happyR = S(P("M143,34 Q154,22 165,34"));
        var laughL = S(P("M79,23 L94,32 L79,41"));
        var laughR = S(P("M161,23 L146,32 L161,41"));
        var winkR = S(P("M162,24 L148,32 L162,40"));
        var surprisedL = F(Circle(86, 30, 9));
        var surprisedR = F(Circle(154, 30, 9));
        var curiousL = new PartShape(null, Ellipse(86, 33, 11, 7.5, -28), Circle(93, 29, 4.2));
        var curiousR = new PartShape(null, Ellipse(154, 33, 11, 7.5, -28), Circle(161, 29, 4.2));
        var sleepyL = S(P("M75,30 Q86,41 97,30"));
        var sleepyR = S(P("M143,30 Q154,41 165,30"));
        var thinkL = F(Capsule(95, 22, 9, 15, 4.5));
        var thinkR = F(Capsule(149, 17, 9, 15, 4.5));
        const string heart = "M0,5 C-9,-2 -7,-10 0,-5.5 C7,-10 9,-2 0,5 z";
        var loveL = F(Moved(P(heart), 86, 31, 1.45));
        var loveR = F(Moved(P(heart), 154, 31, 1.45));
        var focusL = new PartShape(P("M77,22 L94,28"), P("M80,27 L93,31 v7 a6.5,6.5 0 0 1 -13,0 z"));
        var focusR = new PartShape(P("M163,22 L146,28"), P("M160,27 L147,31 v7 a6.5,6.5 0 0 0 13,0 z"));
        var sadL = F(P("M74,40 Q79,27 97,27 Q93,37 74,40 z"));
        var sadR = F(P("M166,40 Q161,27 143,27 Q147,37 166,40 z"));

        // Mouths
        var none = PartShape.Empty;
        var open = F(P("M111,38 h18 a9,9 0 0 1 -18,0 z"));
        var smile = S(P("M113,40 Q120,46 127,40"));
        var o = S(Circle(120, 44, 4.5));
        var think = S(P("M132,38 Q122,33 117,40 Q113,48 121,49 Q128,49 127,43"));
        var frown = S(P("M112,46 Q120,38 128,46"));

        // Accessories
        var lines = S(Group(P("M212,22 L221,7"), P("M222,24 L233,11")));
        var question = new PartShape(P("M216,9 q0,-6 7,-6 q7,0 7,6 q0,4 -6,7 v2.5"), Circle(224, 23.5, 2.4));
        var zz = S(Group(P("M205,14 h7 l-7,8 h7"), P("M217,4 h12 l-12,13 h12")));
        var bubbles = S(Group(Circle(210, 21, 2), Circle(218, 14, 3.5), Circle(230, 7, 5.5)));
        const string star10 = "M0,-10 Q1.6,-1.6 10,0 Q1.6,1.6 0,10 Q-1.6,1.6 -10,0 Q-1.6,-1.6 0,-10 z";
        const string star5 = "M0,-5 Q0.8,-0.8 5,0 Q0.8,0.8 0,5 Q-0.8,0.8 -5,0 Q-0.8,-0.8 0,-5 z";
        const string star9 = "M0,-9 Q1.4,-1.4 9,0 Q1.4,1.4 0,9 Q-1.4,1.4 -9,0 Q-1.4,-1.4 0,-9 z";
        var sparkles = F(Group(Moved(P(star10), 219, 11, 1), Moved(P(star5), 233, 24, 1)));
        var sparkle = F(Moved(P(star9), 224, 12, 1));
        var drop = F(Moved(P("M0,-8 Q5.5,-1 5.5,2.5 a5.5,5.5 0 0 1 -11,0 Q-5.5,-1 0,-8 z"), 206, 26, 1));

        var list = new[]
        {
            new Expression(Reaction.Neutral, neutralL, neutralR, none, none),
            new Expression(Reaction.Happy, happyL, happyR, open, none),
            new Expression(Reaction.Laughing, laughL, laughR, open, lines),
            new Expression(Reaction.Wink, neutralL, winkR, smile, none),
            new Expression(Reaction.Surprised, surprisedL, surprisedR, o, lines),
            new Expression(Reaction.Curious, curiousL, curiousR, none, question),
            new Expression(Reaction.Sleepy, sleepyL, sleepyR, none, zz),
            new Expression(Reaction.Thinking, thinkL, thinkR, think, bubbles),
            new Expression(Reaction.Love, loveL, loveR, smile, none, HeartColor),
            new Expression(Reaction.Excited, laughL, laughR, open, sparkles),
            new Expression(Reaction.Focused, focusL, focusR, none, sparkle),
            new Expression(Reaction.Sad, sadL, sadR, frown, drop),
        };
        return list.ToDictionary(e => e.Reaction);
    }

    private static PartShape S(Geometry g) => new(g, null);
    private static PartShape F(Geometry g) => new(null, g);

    private static Geometry P(string data)
    {
        var g = Geometry.Parse(data);
        g.Freeze();
        return g;
    }

    private static Geometry Capsule(double x, double y, double w, double h, double r) =>
        Frozen(new RectangleGeometry(new Rect(x, y, w, h), r, r));

    private static Geometry Circle(double cx, double cy, double r) =>
        Frozen(new EllipseGeometry(new Point(cx, cy), r, r));

    private static Geometry Ellipse(double cx, double cy, double rx, double ry, double degrees) =>
        Frozen(new EllipseGeometry(new Point(cx, cy), rx, ry) { Transform = new RotateTransform(degrees, cx, cy) });

    /// <summary>SVG <c>transform="translate(x y) scale(s)"</c>: scale first, then move.</summary>
    private static Geometry Moved(Geometry g, double x, double y, double scale)
    {
        var copy = g.Clone();
        var t = new TransformGroup();
        t.Children.Add(new ScaleTransform(scale, scale));
        t.Children.Add(new TranslateTransform(x, y));
        copy.Transform = t;
        return Frozen(copy);
    }

    private static Geometry Group(params Geometry[] parts)
    {
        var g = new GeometryGroup { FillRule = FillRule.Nonzero };
        foreach (var p in parts) g.Children.Add(p);
        return Frozen(g);
    }

    private static Geometry Frozen(Geometry g)
    {
        g.Freeze();
        return g;
    }

}
