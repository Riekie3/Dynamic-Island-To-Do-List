using System.Runtime.InteropServices;

namespace TaskIsland.Windows.Native;

internal static class Shcore
{
    public const int MDT_EFFECTIVE_DPI = 0;

    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);
}
