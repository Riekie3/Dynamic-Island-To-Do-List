namespace TaskIsland.Core.Toasts;

/// <summary>The six toast looks (SILEO-PLAN.md section 1). Each has its own colour; Loading shows a spinner and never opens.</summary>
public enum ToastState { Success, Error, Warning, Info, Action, Loading }

/// <summary>Where the tab (the small pill with the title) sits along the top edge of the body.</summary>
public enum ToastAlign { Left, Center, Right }

/// <summary>One step of an outline: a straight line or a circular arc to (X, Y).</summary>
public readonly record struct OutlineStep(bool IsArc, double X, double Y, double Radius = 0, bool Clockwise = true);

/// <summary>A closed outline: start point plus steps. Pure data, so the shape can be unit-tested without WPF.</summary>
public sealed record Outline(double StartX, double StartY, IReadOnlyList<OutlineStep> Steps);

/// <summary>
/// The Sileo toast shape (SILEO-PLAN.md section 3): a rounded tab on top of a wider rounded body, joined by concave
/// fillets so the tab flows into the body like a folder tab. Sileo gets this curve from a gooey SVG filter; here the
/// same shape is drawn directly, which costs nothing per frame beyond rebuilding one geometry.
/// Coordinates: x right, y down, the tab's top edge at y = 0. Clockwise, starting on the tab's top edge.
/// </summary>
public static class ToastOutline
{
    /// <summary>Gaps narrower than this are drawn as one straight side instead of a corner plus a fillet.</summary>
    private const double Flush = 0.5;

    /// <param name="width">Width of the body (the toast's full width).</param>
    /// <param name="tabX">Left edge of the tab.</param>
    /// <param name="tabWidth">Width of the tab (clamped to the body width).</param>
    /// <param name="tabHeight">Height of the tab; the body starts at this y.</param>
    /// <param name="bodyHeight">Current height of the body; 0 = closed (only the tab is drawn).</param>
    /// <param name="radius">Corner radius of the body and the largest fillet radius.</param>
    public static Outline Build(double width, double tabX, double tabWidth, double tabHeight, double bodyHeight, double radius)
    {
        width = Math.Max(1, width);
        tabWidth = Math.Clamp(tabWidth, 1, width);
        tabX = Math.Clamp(tabX, 0, width - tabWidth);
        tabHeight = Math.Max(1, tabHeight);
        radius = Math.Max(0, radius);
        double b = Math.Max(0, bodyHeight);
        double tabR = Math.Min(radius, Math.Min(tabHeight, tabWidth) / 2);

        var steps = new List<OutlineStep>();
        double left = tabX, right = tabX + tabWidth;

        if (b < Flush)
        {
            // Closed: just the rounded tab.
            steps.Add(Line(right - tabR, 0));
            steps.Add(Arc(right, tabR, tabR, true));
            steps.Add(Line(right, tabHeight - tabR));
            steps.Add(Arc(right - tabR, tabHeight, tabR, true));
            steps.Add(Line(left + tabR, tabHeight));
            steps.Add(Arc(left, tabHeight - tabR, tabR, true));
            steps.Add(Line(left, tabR));
            steps.Add(Arc(left + tabR, 0, tabR, true));
            return new Outline(left + tabR, 0, steps);
        }

        // The body's corners and the fillets grow with the body, so the join "melts" open instead of popping in.
        double bodyR = Math.Min(radius, b / 2);
        // A fillet runs up the tab's side, below the tab's own top corner.
        double filletMax = Math.Min(Math.Min(radius, b), Math.Max(0, tabHeight - tabR));
        double bottom = tabHeight + b;

        double gapR = width - right, gapL = left;
        var (cornerR, filletR) = Join(gapR, bodyR, filletMax);
        var (cornerL, filletL) = Join(gapL, bodyR, filletMax);

        // Top edge of the tab, top-right corner.
        steps.Add(Line(right - tabR, 0));
        steps.Add(Arc(right, tabR, tabR, true));

        if (gapR < Flush)
        {
            // Tab flush with the right edge: one straight side down to the body's bottom corner.
            steps.Add(Line(width, bottom - bodyR));
        }
        else
        {
            steps.Add(Line(right, tabHeight - filletR));
            if (filletR > 0) steps.Add(Arc(right + filletR, tabHeight, filletR, false));   // concave join
            steps.Add(Line(width - cornerR, tabHeight));
            if (cornerR > 0) steps.Add(Arc(width, tabHeight + cornerR, cornerR, true));
            steps.Add(Line(width, bottom - bodyR));
        }

        // Bottom of the body.
        if (bodyR > 0) steps.Add(Arc(width - bodyR, bottom, bodyR, true));
        steps.Add(Line(bodyR, bottom));
        if (bodyR > 0) steps.Add(Arc(0, bottom - bodyR, bodyR, true));

        if (gapL < Flush)
        {
            steps.Add(Line(0, tabR));
        }
        else
        {
            steps.Add(Line(0, tabHeight + cornerL));
            if (cornerL > 0) steps.Add(Arc(cornerL, tabHeight, cornerL, true));
            steps.Add(Line(left - filletL, tabHeight));
            if (filletL > 0) steps.Add(Arc(left, tabHeight - filletL, filletL, false));   // concave join
            steps.Add(Line(left, tabR));
        }
        steps.Add(Arc(left + tabR, 0, tabR, true));
        return new Outline(left + tabR, 0, steps);
    }

    /// <summary>
    /// Splits the gap beside the tab between the body's top corner and the fillet, so the two never overlap: the
    /// corner takes at most half the gap, the fillet what is left (up to its own limit).
    /// </summary>
    private static (double Corner, double Fillet) Join(double gap, double bodyR, double filletMax)
    {
        if (gap < Flush) return (0, 0);
        double corner = Math.Min(bodyR, gap / 2);
        double fillet = Math.Min(filletMax, gap - corner);
        return (corner, Math.Max(0, fillet));
    }

    /// <summary>Where the tab goes for an alignment: left edge, centred, or right edge of the body.</summary>
    public static double TabX(ToastAlign align, double width, double tabWidth) => align switch
    {
        ToastAlign.Right => Math.Max(0, width - tabWidth),
        ToastAlign.Center => Math.Max(0, (width - tabWidth) / 2),
        _ => 0,
    };

    private static OutlineStep Line(double x, double y) => new(false, x, y);

    private static OutlineStep Arc(double x, double y, double r, bool clockwise) => new(true, x, y, r, clockwise);
}
