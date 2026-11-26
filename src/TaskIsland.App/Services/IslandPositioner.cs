using TaskIsland.App.Controls;
using TaskIsland.App.Logging;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;
using TaskIsland.Windows;

namespace TaskIsland.App.Services;

/// <summary>A computed island placement: the resting pill, the window around it and the monitor it is on.</summary>
public sealed record Placement(MonitorInfo Monitor, PillPlacement Pill, IslandWindowPlacement Window, PixelRect MonitorBounds);

/// <summary>
/// Places the island window on the chosen monitor in physical pixels (PLAN.md section 7.2, docs/DRAG-PLACE-PLAN.md):
/// the monitor named by <see cref="AppSettings.MonitorDeviceName"/>, else the primary. The resting pill can sit anywhere
/// on that monitor; the window around it either follows the pill (collapsed) or slides inwards so the open panel stays
/// visible and grows up or down from the pill. Monitors are enumerated on every call (cheap, and always current after
/// plug/unplug, DPI or resolution changes).
/// </summary>
public sealed class IslandPositioner(AppSettings settings)
{
    public const double WidthDip = IslandHost.HostWidth;
    public const double CollapsedHeightDip = 64;
    public const double ExpandedHeightDip = 600;
    public const double PanelWidthDip = 420;

    /// <summary>The tallest the open panel gets; the window has 50 DIP of transparent room around it.</summary>
    public const double MaxPanelHeightDip = 550;

    /// <summary>The monitor the island was last placed on.</summary>
    public MonitorInfo? Monitor { get; private set; }

    public PixelRect? LastRect { get; private set; }

    public Placement? LastPlacement { get; private set; }

    /// <summary>
    /// The size of the pill when it is resting (Mini with the face on, the Info pill with the face off). Snapping and
    /// saved positions use this size, not a temporary Reaction/peek width, so the resting island is the one flush with
    /// an edge.
    /// </summary>
    public (double WidthDip, double HeightDip) RestingPill() => settings.MascotEnabled
        ? (120 * IslandHost.CompactScale, 32 * IslandHost.CompactScale)
        : (settings.NotchWidth * IslandHost.CompactScale, 36 * IslandHost.CompactScale);

    /// <summary>Computes the placement for the current settings and window width. Null when no monitor can be read.</summary>
    public Placement? Compute(bool expanded, bool openUp, double widthDip = WidthDip)
    {
        if (!TryTarget(out var m, out var bounds)) return null;
        var (pillW, pillH) = RestingPill();
        var pill = IslandPlacement.PillAt(bounds, m.Scale, pillW, pillH, settings.HorizontalOffset, settings.TopMargin);
        var win = IslandPlacement.WindowFor(bounds, m.Scale, pill, widthDip,
            expanded ? ExpandedHeightDip : CollapsedHeightDip, PanelWidthDip, expanded, openUp);
        var placement = new Placement(m, pill, win, bounds);
        if (Monitor != m || LastRect != win.Bounds)
            FileLogger.Info(FormattableString.Invariant(
                $"island placed on {m.DeviceName} ({m.Width}x{m.Height} at {m.Left},{m.Top}, scale {m.Scale:0.##}) at {win.Bounds.Left},{win.Bounds.Top} {win.Bounds.Width}x{win.Bounds.Height}{(expanded ? openUp ? " expanded up" : " expanded down" : "")}; pill {pill.LeftDip:0.#},{pill.TopDip:0.#} {pill.WidthDip:0.#}x{pill.HeightDip:0.#} {pill.Edge}"));
        Monitor = m;
        LastRect = win.Bounds;
        LastPlacement = placement;
        return placement;
    }

    /// <summary>Moves the window to a placement computed a moment ago.</summary>
    public static void Place(IntPtr hwnd, Placement placement) =>
        WindowStyler.Place(hwnd, placement.Window.Bounds.Left, placement.Window.Bounds.Top,
            placement.Window.Bounds.Width, placement.Window.Bounds.Height);

    /// <summary>True when the open panel should grow upwards from the pill.</summary>
    public bool ShouldOpenUp(double panelHeightDip)
    {
        if (!TryTarget(out var m, out var bounds)) return false;
        var (pillW, pillH) = RestingPill();
        var pill = IslandPlacement.PillAt(bounds, m.Scale, pillW, pillH, settings.HorizontalOffset, settings.TopMargin);
        return IslandPlacement.OpensUp(bounds, m.Scale, pill, panelHeightDip);
    }

    /// <summary>The tallest open panel that fits between the resting pill and the monitor edge it opens towards.</summary>
    public double PanelHeightLimit(bool openUp, double wantedDip)
    {
        if (!TryTarget(out var m, out var bounds)) return wantedDip;
        var (pillW, pillH) = RestingPill();
        var pill = IslandPlacement.PillAt(bounds, m.Scale, pillW, pillH, settings.HorizontalOffset, settings.TopMargin);
        return IslandPlacement.PanelHeightLimit(bounds, m.Scale, pill, openUp, wantedDip);
    }

    public static PixelRect Bounds(MonitorInfo m) => new(m.Left, m.Top, m.Left + m.Width, m.Top + m.Height);

    private bool TryTarget(out MonitorInfo monitor, out PixelRect bounds)
    {
        monitor = null!;
        bounds = default;
        try
        {
            var all = MonitorService.GetAll();
            var screens = all.Select(ToScreen).ToList();
            var pick = IslandPlacement.PickMonitor(screens, settings.MonitorDeviceName);
            monitor = (pick is null ? null : all.FirstOrDefault(x => x.DeviceName == pick.DeviceName)) ?? MonitorService.GetPrimary();
            bounds = Bounds(monitor);
            return true;
        }
        catch (Exception ex)
        {
            // Displays can be briefly unavailable while waking; the follow-up repositions try again.
            FileLogger.Warn($"positioning skipped: {ex.Message}");
            return false;
        }
    }

    private static ScreenInfo ToScreen(MonitorInfo m) => new(m.DeviceName, Bounds(m), m.Scale, m.IsPrimary);
}
