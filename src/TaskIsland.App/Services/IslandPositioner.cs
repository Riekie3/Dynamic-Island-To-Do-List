using TaskIsland.App.Controls;
using TaskIsland.App.Logging;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;
using TaskIsland.Windows;

namespace TaskIsland.App.Services;

/// <summary>
/// Places the island window top-centre on the chosen monitor in physical pixels (PLAN.md section 7.2): the monitor named
/// by <see cref="AppSettings.MonitorDeviceName"/>, else the primary. Monitors are enumerated on every call (cheap, and
/// always current after plug/unplug, DPI or resolution changes). Both window sizes share the same left edge, top edge and
/// centre, so the pill never moves when the window grows or shrinks.
/// </summary>
public sealed class IslandPositioner(AppSettings settings)
{
    public const double WidthDip = IslandHost.HostWidth;
    public const double CollapsedHeightDip = 64;
    public const double ExpandedHeightDip = 600;
    public const double PanelWidthDip = 420;

    /// <summary>The monitor the island was last placed on.</summary>
    public MonitorInfo? Monitor { get; private set; }

    public PixelRect? LastRect { get; private set; }

    public void Apply(IntPtr hwnd, bool expanded)
    {
        MonitorInfo m;
        try
        {
            var all = MonitorService.GetAll();
            var screens = all.Select(ToScreen).ToList();
            var pick = IslandPlacement.PickMonitor(screens, settings.MonitorDeviceName);
            m = (pick is null ? null : all.FirstOrDefault(x => x.DeviceName == pick.DeviceName)) ?? MonitorService.GetPrimary();
        }
        catch (Exception ex)
        {
            // Displays can be briefly unavailable while waking; the follow-up repositions try again.
            FileLogger.Warn($"positioning skipped: {ex.Message}");
            return;
        }
        var rect = IslandPlacement.Compute(Bounds(m), m.Scale, WidthDip, expanded ? ExpandedHeightDip : CollapsedHeightDip,
            PanelWidthDip, settings.HorizontalOffset, settings.TopMargin);
        if (Monitor != m || LastRect != rect)
            FileLogger.Info($"island placed on {m.DeviceName} ({m.Width}x{m.Height} at {m.Left},{m.Top}, scale {m.Scale:0.##}) " +
                $"at {rect.Left},{rect.Top} {rect.Width}x{rect.Height}{(expanded ? " expanded" : "")}");
        Monitor = m;
        LastRect = rect;
        WindowStyler.Place(hwnd, rect.Left, rect.Top, rect.Width, rect.Height);
    }

    public static PixelRect Bounds(MonitorInfo m) => new(m.Left, m.Top, m.Left + m.Width, m.Top + m.Height);

    private static ScreenInfo ToScreen(MonitorInfo m) => new(m.DeviceName, Bounds(m), m.Scale, m.IsPrimary);
}
