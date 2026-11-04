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

/// <summary>Monitor choice and window placement (PLAN.md section 7.2). Pure arithmetic, unit-tested.</summary>
public static class IslandPlacement
{
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
    /// Window rectangle in physical pixels: top-centred on the monitor (plus the horizontal offset), with the centre clamped
    /// so the 420-wide panel stays on the screen. Both window sizes share the same centre and top edge, so the pill
    /// does not move when the window switches size.
    /// </summary>
    public static PixelRect Compute(PixelRect monitor, double scale, double widthDip, double heightDip, double panelWidthDip,
        int horizontalOffsetDip, int topMarginDip)
    {
        if (scale <= 0 || double.IsNaN(scale)) scale = 1;
        double half = panelWidthDip / 2 * scale;
        double centreX = monitor.Left + monitor.Width / 2.0 + horizontalOffsetDip * scale;
        double lo = monitor.Left + half, hi = monitor.Right - half;
        centreX = lo <= hi ? Math.Clamp(centreX, lo, hi) : monitor.Left + monitor.Width / 2.0;
        int w = (int)Math.Round(widthDip * scale);
        int h = (int)Math.Round(heightDip * scale);
        int x = (int)Math.Round(centreX - w / 2.0);
        int y = monitor.Top + (int)Math.Round(topMarginDip * scale);
        return new PixelRect(x, y, x + w, y + h);
    }
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
