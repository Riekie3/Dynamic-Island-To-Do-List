using TaskIsland.App.Controls;
using TaskIsland.Core.Models;
using TaskIsland.Windows;

namespace TaskIsland.App.Services;

/// <summary>
/// Places the island window top-centre on the primary monitor in physical pixels (PLAN.md section 7.2, minimal
/// Phase 2 version; Phase 4 adds monitor choice, DPI changes and recovery). Both window sizes share the same left
/// edge, top edge and centre, so the pill never moves when the window grows or shrinks.
/// </summary>
public sealed class IslandPositioner(AppSettings settings)
{
    public const double WidthDip = IslandHost.HostWidth;
    public const double CollapsedHeightDip = 64;
    public const double ExpandedHeightDip = 600;
    public const double PanelWidthDip = 420;

    public MonitorInfo? Monitor { get; private set; }

    public void Apply(IntPtr hwnd, bool expanded)
    {
        var m = MonitorService.GetPrimary();
        Monitor = m;
        double s = m.Scale;
        double half = PanelWidthDip / 2 * s;
        double centreX = m.Left + m.Width / 2.0 + settings.HorizontalOffset * s;
        centreX = Math.Clamp(centreX, m.Left + half, m.Left + m.Width - half);
        int w = (int)Math.Round(WidthDip * s);
        int h = (int)Math.Round((expanded ? ExpandedHeightDip : CollapsedHeightDip) * s);
        int x = (int)Math.Round(centreX - w / 2.0);
        int y = m.Top + (int)Math.Round(settings.TopMargin * s);
        WindowStyler.Place(hwnd, x, y, w, h);
    }
}
