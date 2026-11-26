namespace TaskIsland.Core.Services;

/// <summary>A rectangle in physical pixels (Right and Bottom exclusive, like Win32 RECT).</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;

    /// <summary>True when this rectangle covers all of <paramref name="other"/>.</summary>
    public bool Covers(PixelRect other) =>
        Left <= other.Left && Top <= other.Top && Right >= other.Right && Bottom >= other.Bottom;
}

/// <summary>A monitor as the placement rules see it.</summary>
public sealed record ScreenInfo(string DeviceName, PixelRect Bounds, double Scale, bool IsPrimary);

/// <summary>The screen edge the resting pill touches, if any (docs/DRAG-PLACE-PLAN.md).</summary>
public enum IslandEdge { None, Top, Bottom, Left, Right }

/// <summary>The resting pill on its monitor, in DIPs from the monitor's top-left.</summary>
public readonly record struct PillPlacement(double LeftDip, double TopDip, double WidthDip, double HeightDip, IslandEdge Edge)
{
    public double Right => LeftDip + WidthDip;
    public double Bottom => TopDip + HeightDip;
    public double CentreX => LeftDip + WidthDip / 2;
}

/// <summary>
/// One window rectangle in physical pixels, plus where the collapsed island sits inside it (host DIPs).
/// <see cref="BottomAnchored"/> is true when the island is drawn at the bottom of the window (a bottom-edge pill, or an
/// expanded panel that grows upwards); <see cref="OpenUp"/> is true only for the expanded panel.
/// </summary>
public readonly record struct IslandWindowPlacement(PixelRect Bounds, double CompactCentreDip, bool BottomAnchored,
    bool OpenUp, IslandEdge Edge);

/// <summary>
/// Free island placement and edge snapping (docs/DRAG-PLACE-PLAN.md), plus monitor choice (PLAN.md section 7.2).
/// The resting pill may sit anywhere on its monitor; saved offsets are in DIPs from the monitor centre (x) and top (y),
/// so a screen at 125 % puts the pill on the same pixel it was dropped on. Pure arithmetic, unit-tested.
/// </summary>
public static class IslandPlacement
{
    /// <summary>Within this many DIPs of a screen edge a placed pill is flush with that edge.</summary>
    private const double EdgeToleranceDip = 1.0;

    /// <summary>The monitor named in the settings, else the primary, else the first one. Null only for an empty list.</summary>
    public static ScreenInfo? PickMonitor(IReadOnlyList<ScreenInfo> monitors, string? deviceName)
    {
        if (monitors.Count == 0) return null;
        if (!string.IsNullOrEmpty(deviceName)
            && monitors.FirstOrDefault(m => string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase)) is { } named)
            return named;
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }

    /// <summary>
    /// The resting pill for the saved offsets: <paramref name="widthDip"/> x <paramref name="heightDip"/> with its centre
    /// at the monitor centre plus <paramref name="horizontalOffsetDip"/>, and its top at <paramref name="topMarginDip"/>.
    /// The pill is kept fully on the monitor; a value on an edge makes it flush with that edge.
    /// </summary>
    public static PillPlacement PillAt(PixelRect monitor, double scale, double widthDip, double heightDip,
        int horizontalOffsetDip, int topMarginDip)
    {
        scale = Normalize(scale);
        double monW = monitor.Width / scale;
        double left = monW / 2 + horizontalOffsetDip - widthDip / 2;
        return Normalize(monitor, scale, widthDip, heightDip, left, topMarginDip);
    }

    /// <summary>
    /// Drag to place: the pill follows the pointer (both axes), stays fully on the monitor and snaps to the nearest edge
    /// when it is within <paramref name="snapDip"/> of one. Positions are DIPs from the monitor's top-left.
    /// </summary>
    public static PillPlacement Snap(PixelRect monitor, double scale, double widthDip, double heightDip,
        double leftDip, double topDip, double snapDip)
    {
        scale = Normalize(scale);
        double monW = monitor.Width / scale;
        double monH = monitor.Height / scale;
        leftDip = widthDip >= monW ? (monW - widthDip) / 2 : Math.Clamp(leftDip, 0, monW - widthDip);
        topDip = heightDip >= monH ? (monH - heightDip) / 2 : Math.Clamp(topDip, 0, monH - heightDip);
        var edge = NearestEdge(leftDip, topDip, widthDip, heightDip, monW, monH, snapDip);
        return ApplyEdge(leftDip, topDip, widthDip, heightDip, edge, monW, monH);
    }

    /// <summary>The DIPs to save for <paramref name="pill"/>: horizontal offset from the monitor centre and the top.</summary>
    public static (int HorizontalOffsetDip, int TopMarginDip) OffsetsFor(PixelRect monitor, double scale, PillPlacement pill)
    {
        scale = Normalize(scale);
        double monW = monitor.Width / scale;
        return ((int)Math.Round(pill.CentreX - monW / 2), (int)Math.Round(pill.TopDip));
    }

    /// <summary>
    /// Whether the open panel should grow upwards from the pill. Down is preferred while the panel (max height
    /// <paramref name="panelHeightDip"/>) fits below the pill; otherwise the side with more room wins.
    /// </summary>
    public static bool OpensUp(PixelRect monitor, double scale, PillPlacement pill, double panelHeightDip)
    {
        scale = Normalize(scale);
        double monH = monitor.Height / scale;
        double below = monH - pill.TopDip;
        double above = pill.Bottom;
        if (below >= panelHeightDip) return false;
        if (above >= panelHeightDip) return true;
        return above > below;
    }

    /// <summary>
    /// Tallest the open panel can be while staying on the monitor. The panel starts at the pill's top edge when it
    /// opens down and ends at its bottom edge when it opens up, so the room is the distance to that screen edge.
    /// A free or side-attached pill near the middle may not have room for the full 550-DIP panel in either direction;
    /// the panel is then shortened and its task list scrolls instead of running off the screen.
    /// </summary>
    public static double PanelHeightLimit(PixelRect monitor, double scale, PillPlacement pill, bool openUp,
        double wantedDip)
    {
        scale = Normalize(scale);
        double monH = monitor.Height / scale;
        double room = openUp ? pill.Bottom : monH - pill.TopDip;
        return Math.Clamp(room, 0, wantedDip);
    }

    /// <summary>
    /// The window rectangle for the current state. Collapsed: 452 x 64 around the pill. Expanded: the panel should be
    /// visible, so the window slides inwards (the compact pill keeps its screen place inside the host) and grows up or
    /// down from the pill. A side-attached pill keeps the window flush with that side.
    /// </summary>
    public static IslandWindowPlacement WindowFor(PixelRect monitor, double scale, PillPlacement pill, double hostWidthDip,
        double hostHeightDip, double panelWidthDip, bool expanded, bool openUp)
    {
        scale = Normalize(scale);
        double monW = monitor.Width / scale;
        double windowLeft;
        if (pill.Edge == IslandEdge.Left) windowLeft = 0;
        else if (pill.Edge == IslandEdge.Right) windowLeft = monW - hostWidthDip;
        else
        {
            windowLeft = pill.CentreX - hostWidthDip / 2;
            if (expanded)
            {
                // Keep the 420-wide panel inside the monitor; the compact pill may then sit off-centre in the host.
                double lo = (panelWidthDip - hostWidthDip) / 2;
                double hi = monW - (hostWidthDip + panelWidthDip) / 2;
                windowLeft = lo <= hi ? Math.Clamp(windowLeft, lo, hi) : (monW - hostWidthDip) / 2;
            }
        }
        bool bottomAnchored = expanded ? openUp : pill.Edge == IslandEdge.Bottom;
        double windowTop = bottomAnchored ? pill.Bottom - hostHeightDip : pill.TopDip;
        // For a side edge this is the pill's own (small) centre; for a wide reminder pill the host stays flush and the
        // host-side anchoring in IslandHost grows it inwards.
        double compactCentre = pill.CentreX - windowLeft;
        int w = (int)Math.Round(hostWidthDip * scale);
        int h = (int)Math.Round(hostHeightDip * scale);
        int x = monitor.Left + (int)Math.Round(windowLeft * scale);
        int y = monitor.Top + (int)Math.Round(windowTop * scale);
        return new IslandWindowPlacement(new PixelRect(x, y, x + w, y + h), compactCentre, bottomAnchored,
            expanded && openUp, pill.Edge);
    }

    /// <summary>
    /// On-screen width (DIPs) of the reminder pill: <paramref name="percent"/> of the monitor width, kept on the monitor
    /// around the island's centre (with an 8 DIP gap to each edge) and never narrower than <paramref name="minDip"/>.
    /// A pill attached to the left or right edge widens inwards instead, so it may use almost the whole monitor.
    /// Percent 0 (or less) means <paramref name="minDip"/>.
    /// </summary>
    public static double AlertWidthDip(PixelRect monitor, double scale, int percent, int horizontalOffsetDip,
        double panelWidthDip, double minDip, IslandEdge edge = IslandEdge.None)
    {
        if (percent <= 0) return minDip;
        scale = Normalize(scale);
        double width = monitor.Width / scale;
        double half = panelWidthDip / 2;
        double centre = width / 2 + horizontalOffsetDip;
        centre = half <= width - half ? Math.Clamp(centre, half, width - half) : width / 2;
        double room = edge is IslandEdge.Left or IslandEdge.Right
            ? width - 16
            : 2 * Math.Min(centre, width - centre) - 16;
        double wanted = width * Math.Min(percent, 100) / 100.0;
        return Math.Max(minDip, Math.Min(wanted, room));
    }

    private static PillPlacement Normalize(PixelRect monitor, double scale, double widthDip, double heightDip,
        double leftDip, double topDip)
    {
        double monW = monitor.Width / scale;
        double monH = monitor.Height / scale;
        leftDip = widthDip >= monW ? (monW - widthDip) / 2 : Math.Clamp(leftDip, 0, monW - widthDip);
        topDip = heightDip >= monH ? (monH - heightDip) / 2 : Math.Clamp(topDip, 0, monH - heightDip);
        var edge = NearestEdge(leftDip, topDip, widthDip, heightDip, monW, monH, EdgeToleranceDip);
        return ApplyEdge(leftDip, topDip, widthDip, heightDip, edge, monW, monH);
    }

    private static PillPlacement ApplyEdge(double left, double top, double width, double height, IslandEdge edge,
        double monW, double monH)
    {
        switch (edge)
        {
            case IslandEdge.Top: top = 0; break;
            case IslandEdge.Bottom: top = monH - height; break;
            case IslandEdge.Left: left = 0; break;
            case IslandEdge.Right: left = monW - width; break;
        }
        return new PillPlacement(left, top, width, height, edge);
    }

    private static IslandEdge NearestEdge(double left, double top, double width, double height, double monW,
        double monH, double limit)
    {
        if (width >= monW || height >= monH) return IslandEdge.None;
        double dTop = top, dBottom = monH - (top + height), dLeft = left, dRight = monW - (left + width);
        double min = Math.Min(Math.Min(dTop, dBottom), Math.Min(dLeft, dRight));
        if (min > limit) return IslandEdge.None;
        if (dTop <= min + 0.001) return IslandEdge.Top;
        if (dBottom <= min + 0.001) return IslandEdge.Bottom;
        if (dLeft <= min + 0.001) return IslandEdge.Left;
        return IslandEdge.Right;
    }

    private static double Normalize(double scale) =>
        scale > 0 && !double.IsNaN(scale) && !double.IsInfinity(scale) ? scale : 1;
}

/// <summary>Values of <c>SHQueryUserNotificationState</c>.</summary>
public enum NotificationState
{
    Unknown = 0,
    NotPresent = 1,
    Busy = 2,
    RunningD3DFullScreen = 3,
    PresentationMode = 4,
    AcceptsNotifications = 5,
    QuietTime = 6,
    App = 7,
}

/// <summary>Full-screen decision (PLAN.md section 7.4). Pure, unit-tested.</summary>
public static class FullScreenRules
{
    private static readonly string[] ShellClasses = ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd"];

    /// <summary>
    /// True when the foreground window is a full-screen app on the island's monitor: it covers the whole monitor (and is not
    /// a maximised window, which only looks that way when the taskbar auto-hides), or Windows reports a D3D full-screen app
    /// or presentation mode while it is in front. The desktop, the taskbar and our own window never count.
    /// </summary>
    public static bool IsFullScreen(NotificationState state, PixelRect foreground, string className, bool isOwnWindow,
        bool isMaximized, bool onIslandMonitor, PixelRect islandMonitor)
    {
        if (isOwnWindow || !onIslandMonitor) return false;
        if (ShellClasses.Contains(className, StringComparer.Ordinal)) return false;
        if (state is NotificationState.RunningD3DFullScreen or NotificationState.PresentationMode) return true;
        return !isMaximized && foreground.Covers(islandMonitor);
    }
}
