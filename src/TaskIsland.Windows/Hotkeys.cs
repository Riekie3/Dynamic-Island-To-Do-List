using TaskIsland.Windows.Native;

namespace TaskIsland.Windows;

/// <summary>
/// Thin RegisterHotKey wrapper (never a keyboard hook, PLAN.md section 7.11). The window's message hook checks
/// <see cref="WM_HOTKEY"/>. Phase 2 uses it for Debug-only shortcuts; Phase 4 builds HotkeyService on top.
/// </summary>
public static class Hotkeys
{
    public const int WM_HOTKEY = User32.WM_HOTKEY;
    public const uint Alt = User32.MOD_ALT, Control = User32.MOD_CONTROL, Shift = User32.MOD_SHIFT;

    public static bool Register(IntPtr hwnd, int id, uint modifiers, uint virtualKey) =>
        User32.RegisterHotKey(hwnd, id, modifiers | User32.MOD_NOREPEAT, virtualKey);

    public static void Unregister(IntPtr hwnd, int id) => User32.UnregisterHotKey(hwnd, id);
}
