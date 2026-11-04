using System.Runtime.InteropServices;
using TaskIsland.Windows.Native;

namespace TaskIsland.Windows;

/// <summary>
/// The global toggle shortcut (PLAN.md section 7.8) through <c>RegisterHotKey</c> only, never a keyboard hook (section
/// 7.11 rule 1). The island window's message hook passes <c>WM_HOTKEY</c> to <see cref="HandleMessage"/>.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int ToggleId = 0x7A01;
    private IntPtr _hwnd;

    public event Action? Pressed;

    public bool IsRegistered { get; private set; }
    /// <summary>Win32 error of the last failed registration (1409 = the shortcut is in use by another app).</summary>
    public int LastError { get; private set; }
    public uint Modifiers { get; private set; }
    public uint VirtualKey { get; private set; }

    /// <summary>Registers (or re-registers) the shortcut. False when Windows refused, usually because another app owns it.</summary>
    public bool Register(IntPtr hwnd, uint modifiers, uint virtualKey)
    {
        Unregister();
        _hwnd = hwnd;
        Modifiers = modifiers;
        VirtualKey = virtualKey;
        IsRegistered = User32.RegisterHotKey(hwnd, ToggleId, modifiers | User32.MOD_NOREPEAT, virtualKey);
        LastError = IsRegistered ? 0 : Marshal.GetLastWin32Error();
        return IsRegistered;
    }

    public void Unregister()
    {
        if (IsRegistered && _hwnd != IntPtr.Zero) User32.UnregisterHotKey(_hwnd, ToggleId);
        IsRegistered = false;
    }

    public bool HandleMessage(int msg, IntPtr wParam)
    {
        if (msg != User32.WM_HOTKEY || wParam.ToInt32() != ToggleId) return false;
        Pressed?.Invoke();
        return true;
    }

    public void Dispose() => Unregister();
}
