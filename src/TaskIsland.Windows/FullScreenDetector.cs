using System.Runtime.InteropServices;
using System.Text;
using TaskIsland.Windows.Native;

namespace TaskIsland.Windows;

/// <summary>What the full-screen rule needs to know about the foreground window.</summary>
public readonly record struct ForegroundSnapshot(IntPtr Hwnd, int Left, int Top, int Right, int Bottom, string ClassName,
    bool IsMaximized, string? MonitorDevice, int NotificationState);

/// <summary>
/// Tells the App layer when the full-screen state may have changed (PLAN.md section 7.4); the App debounces and decides.
/// Two event sources, no polling and no keyboard/mouse hooks (section 7.11 rule 1):
/// <list type="bullet">
/// <item>an out-of-context <c>EVENT_SYSTEM_FOREGROUND</c> WinEvent: another window came to the front;</item>
/// <item>the shell's own full-screen notice (<c>ABN_FULLSCREENAPP</c>) on a hidden helper window registered as an app bar
/// without any screen space: catches F11 in a browser, where the foreground window does not change.</item>
/// </list>
/// Phase 4 change: the plan's <c>EVENT_OBJECT_LOCATIONCHANGE</c> hook did catch F11, but it woke TaskIsland for every
/// window, caret and cursor movement in the foreground app (measured 63 ms CPU per 45 s while the PC was in use, against
/// the 0.0 % idle target of section 6.7). The shell notice costs nothing until something really goes full screen.
/// Reads only the foreground window's rectangle and class name.
/// </summary>
public sealed class FullScreenDetector : IDisposable
{
    // Kept in a field: the native side holds only a function pointer, so the delegate must not be collected (GC!).
    private readonly User32.WinEventProc _foregroundProc;
    private IntPtr _foregroundHook;
    private IntPtr _appBarWindow;
    private uint _appBarMessage;
    private readonly HashSet<IntPtr> _own = [];

    public FullScreenDetector()
    {
        _foregroundProc = OnForeground;
    }

    /// <summary>Raised on the thread that called <see cref="Start"/> (it must pump messages).</summary>
    public event Action? Changed;

    public bool IsRunning => _foregroundHook != IntPtr.Zero;
    public bool ShellNoticesRegistered => _appBarWindow != IntPtr.Zero;

    /// <summary>Windows of our own process; they never count.</summary>
    public void AddOwnWindow(IntPtr hwnd) => _own.Add(hwnd);

    public bool IsOwn(IntPtr hwnd) => _own.Contains(hwnd);

    /// <param name="helperWindow">A hidden window of ours whose message hook calls <see cref="HandleMessage"/>.</param>
    public bool Start(IntPtr helperWindow)
    {
        if (IsRunning) return true;
        _foregroundHook = User32.SetWinEventHook(User32.EVENT_SYSTEM_FOREGROUND, User32.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero,
            _foregroundProc, 0, 0, User32.WINEVENT_OUTOFCONTEXT);
        if (helperWindow != IntPtr.Zero)
        {
            _appBarMessage = User32.RegisterWindowMessage("TaskIsland.FullScreenNotice");
            var data = new Shell32.APPBARDATA
            {
                cbSize = Marshal.SizeOf<Shell32.APPBARDATA>(),
                hWnd = helperWindow,
                uCallbackMessage = _appBarMessage,
            };
            if (Shell32.SHAppBarMessage(Shell32.ABM_NEW, ref data) != UIntPtr.Zero) _appBarWindow = helperWindow;
        }
        return IsRunning;
    }

    /// <summary>Called from the helper window's hook. True when it was the shell's full-screen notice.</summary>
    public bool HandleMessage(int msg, IntPtr wParam)
    {
        if (_appBarWindow == IntPtr.Zero || (uint)msg != _appBarMessage) return false;
        if (wParam.ToInt32() == Shell32.ABN_FULLSCREENAPP) Changed?.Invoke();
        return true;
    }

    public void Stop()
    {
        if (_foregroundHook != IntPtr.Zero) User32.UnhookWinEvent(_foregroundHook);
        _foregroundHook = IntPtr.Zero;
        if (_appBarWindow != IntPtr.Zero)
        {
            var data = new Shell32.APPBARDATA { cbSize = Marshal.SizeOf<Shell32.APPBARDATA>(), hWnd = _appBarWindow };
            Shell32.SHAppBarMessage(Shell32.ABM_REMOVE, ref data);
            _appBarWindow = IntPtr.Zero;
        }
    }

    public static ForegroundSnapshot Snapshot()
    {
        var hwnd = User32.GetForegroundWindow();
        Shell32.SHQueryUserNotificationState(out int quns);
        if (hwnd == IntPtr.Zero) return new ForegroundSnapshot(IntPtr.Zero, 0, 0, 0, 0, "", false, null, quns);
        User32.GetWindowRect(hwnd, out var r);
        var cls = new StringBuilder(64);
        User32.GetClassName(hwnd, cls, cls.Capacity);
        return new ForegroundSnapshot(hwnd, r.Left, r.Top, r.Right, r.Bottom, cls.ToString(), User32.IsZoomed(hwnd),
            MonitorService.DeviceOf(hwnd), quns);
    }

    private void OnForeground(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time) =>
        Changed?.Invoke();

    public void Dispose() => Stop();
}
