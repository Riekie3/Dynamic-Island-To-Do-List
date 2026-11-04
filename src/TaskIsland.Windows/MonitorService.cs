using System.Runtime.InteropServices;
using TaskIsland.Windows.Native;

namespace TaskIsland.Windows;

/// <summary>A monitor's full bounds in physical pixels and its effective DPI scale (1.0 = 96 DPI).</summary>
public sealed record MonitorInfo(string DeviceName, int Left, int Top, int Width, int Height, double Scale, bool IsPrimary);

/// <summary>Monitor enumeration and lookup (PLAN.md section 7.2). Called on demand; nothing is cached or polled.</summary>
public static class MonitorService
{
    public static IReadOnlyList<MonitorInfo> GetAll()
    {
        var list = new List<MonitorInfo>();
        User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (h, _, _, _) =>
        {
            if (TryDescribe(h, out var m)) list.Add(m);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static MonitorInfo GetPrimary()
    {
        var hMon = User32.MonitorFromPoint(new User32.POINT { X = 0, Y = 0 }, User32.MONITOR_DEFAULTTOPRIMARY);
        if (!TryDescribe(hMon, out var m)) throw new InvalidOperationException("GetMonitorInfo failed for the primary monitor.");
        return m;
    }

    /// <summary>Device name of the monitor that holds most of <paramref name="hwnd"/>.</summary>
    public static string? DeviceOf(IntPtr hwnd)
    {
        var hMon = User32.MonitorFromWindow(hwnd, User32.MONITOR_DEFAULTTONEAREST);
        return TryDescribe(hMon, out var m) ? m.DeviceName : null;
    }

    private static bool TryDescribe(IntPtr hMon, out MonitorInfo monitor)
    {
        monitor = null!;
        if (hMon == IntPtr.Zero) return false;
        var info = new User32.MONITORINFOEX { cbSize = Marshal.SizeOf<User32.MONITORINFOEX>() };
        if (!User32.GetMonitorInfo(hMon, ref info)) return false;
        double scale = Shcore.GetDpiForMonitor(hMon, Shcore.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0
            ? dpiX / 96.0 : 1.0;
        var r = info.rcMonitor;
        monitor = new MonitorInfo(info.szDevice, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, scale,
            (info.dwFlags & User32.MONITORINFOF_PRIMARY) != 0);
        return true;
    }
}
