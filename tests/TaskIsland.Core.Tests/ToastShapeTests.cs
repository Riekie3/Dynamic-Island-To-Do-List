using TaskIsland.Core.Toasts;

namespace TaskIsland.Core.Tests;

/// <summary>SILEO-PLAN.md phase S1: the toast outline and the spring curve.</summary>
public class ToastShapeTests
{
    private const double W = 350, TabH = 40, R = 16;

    private static List<(double X, double Y)> Points(Outline o) =>
        [(o.StartX, o.StartY), .. o.Steps.Select(s => (s.X, s.Y))];

    /// <summary>Signed area by the shoelace formula on the step end points (arcs as chords): positive = clockwise on screen.</summary>
    private static double Area(Outline o)
    {
        var p = Points(o);
        double a = 0;
        for (int i = 0; i < p.Count; i++)
        {
            var (x1, y1) = p[i];
            var (x2, y2) = p[(i + 1) % p.Count];
            a += x1 * y2 - x2 * y1;
        }
        return a / 2;
    }

    private static void AssertClosed(Outline o)
    {
        var last = o.Steps[^1];
        Assert.Equal(o.StartX, last.X, 6);
        Assert.Equal(o.StartY, last.Y, 6);
    }

    private static void AssertInside(Outline o, double width, double height)
    {
        foreach (var (x, y) in Points(o))
        {
            Assert.InRange(x, -1e-9, width + 1e-9);
            Assert.InRange(y, -1e-9, height + 1e-9);
        }
    }

    [Fact]
    public void Closed_toast_is_just_the_rounded_tab()
    {
        var o = ToastOutline.Build(W, 0, 120, TabH, 0, R);
        AssertClosed(o);
        AssertInside(o, 120, TabH);
        Assert.Equal(4, o.Steps.Count(s => s.IsArc));
        Assert.All(o.Steps.Where(s => s.IsArc), s => Assert.True(s.Clockwise));
        Assert.True(Area(o) > 0);
    }

    [Theory]
    [InlineData(ToastAlign.Left)]
    [InlineData(ToastAlign.Center)]
    [InlineData(ToastAlign.Right)]
    public void Open_toast_stays_closed_inside_its_box_and_clockwise(ToastAlign align)
    {
        double tabW = 140, body = 80;
        var o = ToastOutline.Build(W, ToastOutline.TabX(align, W, tabW), tabW, TabH, body, R);
        AssertClosed(o);
        AssertInside(o, W, TabH + body);
        Assert.True(Area(o) > 0);
    }

    [Fact]
    public void Centred_tab_has_two_concave_fillets()
    {
        double tabW = 140;
        var o = ToastOutline.Build(W, ToastOutline.TabX(ToastAlign.Center, W, tabW), tabW, TabH, 80, R);
        var concave = o.Steps.Where(s => s.IsArc && !s.Clockwise).ToList();
        Assert.Equal(2, concave.Count);
        Assert.All(concave, s => Assert.Equal(R, s.Radius, 6));
        // Each fillet ends on the body's top edge or on the tab's side.
        Assert.All(concave, s => Assert.True(Math.Abs(s.Y - TabH) < 1e-9 || Math.Abs(s.Y - (TabH - R)) < 1e-9));
    }

    [Fact]
    public void Left_tab_has_a_straight_left_side_and_one_fillet()
    {
        var o = ToastOutline.Build(W, 0, 140, TabH, 80, R);
        Assert.Single(o.Steps, s => s.IsArc && !s.Clockwise);
        // Some line runs straight up the left edge from the body's bottom corner to the tab's top corner.
        Assert.Contains(o.Steps, s => !s.IsArc && s.X == 0 && Math.Abs(s.Y - R) < 1e-9);
    }

    [Fact]
    public void Fillets_and_corners_grow_with_the_body_so_the_join_melts_open()
    {
        double tabW = 140, x = ToastOutline.TabX(ToastAlign.Center, W, tabW);
        var small = ToastOutline.Build(W, x, tabW, TabH, 4, R);
        var big = ToastOutline.Build(W, x, tabW, TabH, 120, R);
        double SmallestRadius(Outline o) => o.Steps.Where(s => s.IsArc && !s.Clockwise).Min(s => s.Radius);
        Assert.True(SmallestRadius(small) <= 4 + 1e-9);
        Assert.Equal(R, SmallestRadius(big), 6);
        Assert.All(small.Steps.Where(s => s.IsArc), s => Assert.True(s.Radius <= R + 1e-9));
    }

    [Fact]
    public void A_narrow_gap_shares_its_room_so_corner_and_fillet_never_overlap()
    {
        // Tab almost as wide as the body: 6 px on each side.
        double tabW = W - 12;
        var o = ToastOutline.Build(W, 6, tabW, TabH, 80, R);
        AssertClosed(o);
        // On both sides the walk along the body's top edge heads right; a step going left would mean the corner and
        // the fillet overlap. Touching (a zero-length line between them) is fine.
        var p = Points(o);
        for (int i = 1; i < p.Count; i++)
        {
            if (Math.Abs(p[i].Y - TabH) > 1e-9 || Math.Abs(p[i - 1].Y - TabH) > 1e-9) continue;
            Assert.True(p[i].X >= p[i - 1].X - 1e-9, $"step {i} goes left: {p[i - 1].X} -> {p[i].X}");
        }
        // The joins (fillets) fit in the 6 px gap; the tab's and body's own outer corners keep their full radius.
        foreach (var s in o.Steps.Where(s => s.IsArc && !s.Clockwise)) Assert.True(s.Radius <= 6 + 1e-9, $"fillet radius {s.Radius}");
    }

    [Fact]
    public void Too_wide_tab_is_clamped_to_the_body()
    {
        var o = ToastOutline.Build(W, 50, 999, TabH, 60, R);
        AssertClosed(o);
        AssertInside(o, W, TabH + 60);
    }

    [Fact]
    public void Tab_position_for_each_alignment()
    {
        Assert.Equal(0, ToastOutline.TabX(ToastAlign.Left, W, 100));
        Assert.Equal(125, ToastOutline.TabX(ToastAlign.Center, W, 100));
        Assert.Equal(250, ToastOutline.TabX(ToastAlign.Right, W, 100));
        Assert.Equal(0, ToastOutline.TabX(ToastAlign.Right, W, 999));
    }

    // ---- spring ----

    [Fact]
    public void Spring_starts_at_zero_and_ends_at_one()
    {
        Assert.Equal(0, Spring.Ease(0));
        Assert.Equal(1, Spring.Ease(1));
        Assert.Equal(0, Spring.Ease(-1));
        Assert.Equal(1, Spring.Ease(2));
        Assert.InRange(Spring.Ease(0.999), 0.998, 1.002);   // settled before the end: no jump on the last frame
    }

    [Fact]
    public void Sileo_bounce_overshoots_a_little()
    {
        double peak = Spring.Peak(Spring.DefaultBounce);
        Assert.InRange(peak, 1.01, 1.05);
    }

    [Fact]
    public void Zero_bounce_never_overshoots_and_rises_steadily()
    {
        Assert.True(Spring.Peak(0) <= 1 + 1e-9);
        double prev = 0;
        for (int i = 1; i <= 100; i++)
        {
            double v = Spring.Ease(i / 100.0, 0);
            Assert.True(v >= prev - 1e-12);
            prev = v;
        }
    }

    [Fact]
    public void More_bounce_means_more_overshoot()
    {
        Assert.True(Spring.Peak(0.5) > Spring.Peak(0.25));
    }
}
