using System.Runtime.InteropServices;

namespace TaskIsland.Windows;

/// <summary>
/// Window backdrop helpers for the Settings window (PLAN.md section 6.6): the Windows 11 Mica backdrop and the dark title
/// bar. Every call can fail (Windows 10, older builds); callers then use the solid Surface colour.
/// </summary>
public static class Dwm
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMSBT_MAINWINDOW = 2; // Mica

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS { public int Left, Right, Top, Bottom; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    /// <summary>Windows 11 22H2 (build 22621) is the first with the documented backdrop attribute.</summary>
    public static bool MicaSupported => Environment.OSVersion.Version.Build >= 22621;

    /// <summary>Dark or light caption (also tints the Mica material). Returns false when the call failed.</summary>
    public static bool SetDarkTitleBar(IntPtr hwnd, bool dark)
    {
        int v = dark ? 1 : 0;
        try { return DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int)) == 0; }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { return false; }
    }

    /// <summary>Mica behind the whole window. The WPF client area must be transparent. False when unsupported or refused.</summary>
    public static bool TryEnableMica(IntPtr hwnd)
    {
        if (!MicaSupported) return false;
        try
        {
            int type = DWMSBT_MAINWINDOW;
            if (DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref type, sizeof(int)) != 0) return false;
            var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            return DwmExtendFrameIntoClientArea(hwnd, ref margins) == 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }
}
