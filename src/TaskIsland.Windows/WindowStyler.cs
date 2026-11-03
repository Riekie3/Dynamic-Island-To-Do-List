using TaskIsland.Windows.Native;

namespace TaskIsland.Windows;

/// <summary>Tool-window / no-activate / topmost helpers for the island window (PLAN.md section 7.1).</summary>
public static class WindowStyler
{
    /// <summary>Hide from Alt-Tab and the taskbar, and never take focus on click or hover.</summary>
    public static void ApplyIslandStyles(IntPtr hwnd)
    {
        long ex = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE).ToInt64();
        ex = (ex | User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE) & ~User32.WS_EX_APPWINDOW;
        User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, new IntPtr(ex));
    }

    /// <summary>Expanded: the window may be activated so the quick-add box can take keyboard focus.</summary>
    public static void SetNoActivate(IntPtr hwnd, bool noActivate)
    {
        long ex = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE).ToInt64();
        ex = noActivate ? ex | User32.WS_EX_NOACTIVATE : ex & ~User32.WS_EX_NOACTIVATE;
        User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, new IntPtr(ex));
    }

    public static bool BringToForeground(IntPtr hwnd) => User32.SetForegroundWindow(hwnd);

    /// <summary>Moves and sizes the window in physical pixels, topmost, without activating it.</summary>
    public static void Place(IntPtr hwnd, int x, int y, int width, int height) =>
        User32.SetWindowPos(hwnd, User32.HWND_TOPMOST, x, y, width, height, User32.SWP_NOACTIVATE);

    public static void ReassertTopmost(IntPtr hwnd) =>
        User32.SetWindowPos(hwnd, User32.HWND_TOPMOST, 0, 0, 0, 0,
            User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
}
