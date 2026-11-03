using TaskIsland.Windows.Native;

namespace TaskIsland.Windows;

/// <summary>A monitor's full bounds in physical pixels and its effective DPI scale (1.0 = 96 DPI).</summary>
public sealed record MonitorInfo(string DeviceName, int Left, int Top, int Width, int Height, double Scale, bool IsPrimary);

/// <summary>
/// Monitor lookup. Phase 2 needs only the primary monitor; enumeration, the chosen monitor and change events
/// are added in Phase 4 (PLAN.md section 7.2).
/// </summary>
public static class MonitorService
{
    public static MonitorInfo GetPrimary()
    {
        var hMon = User32.MonitorFromPoint(new User32.POINT { X = 0, Y = 0 }, User32.MONITOR_DEFAULTTOPRIMARY);
        var info = new User32.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<User32.MONITORINFOEX>() };
        if (!User32.GetMonitorInfo(hMon, ref info))
            throw new InvalidOperationException("GetMonitorInfo failed for the primary monitor.");
        double scale = Shcore.GetDpiForMonitor(hMon, Shcore.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0
            ? dpiX / 96.0 : 1.0;
        var r = info.rcMonitor;
        return new MonitorInfo(info.szDevice, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, scale, true);
    }
}
