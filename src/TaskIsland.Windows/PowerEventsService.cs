using System.Runtime.InteropServices;
using TaskIsland.Windows.Native;

namespace TaskIsland.Windows;

public enum PowerEvent { Suspend, Resume, DisplayOff, DisplayOn, DisplayDimmed, DisplayChanged, DpiChanged, TaskbarCreated }

/// <summary>
/// Turns window messages into power/display events (PLAN.md section 7.3). Event-driven only, no polling: the island
/// window's message hook passes every message to <see cref="HandleMessage"/>. Lock/unlock and display-settings events come
/// from <c>SystemEvents</c> in the App layer. <c>GUID_CONSOLE_DISPLAY_STATE</c> also catches Modern Standby laptops,
/// where the classic suspend/resume messages may never arrive.
/// </summary>
public sealed class PowerEventsService : IDisposable
{
    private static Guid ConsoleDisplayState = new("6FE69556-704A-47A0-8F24-C28D936FDA47");
    private readonly uint _taskbarCreated = User32.RegisterWindowMessage("TaskbarCreated");
    private IntPtr _notify;
    private int _lastDisplayState = -1;

    public event Action<PowerEvent>? Raised;

    /// <summary>Registers for display on/off/dim notifications on the island window. False if Windows refused.</summary>
    public bool Attach(IntPtr hwnd)
    {
        _notify = User32.RegisterPowerSettingNotification(hwnd, ref ConsoleDisplayState, User32.DEVICE_NOTIFY_WINDOW_HANDLE);
        return _notify != IntPtr.Zero;
    }

    /// <summary>Returns true when the message was one of ours (it is never marked handled: others may need it too).</summary>
    public bool HandleMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == User32.WM_POWERBROADCAST)
        {
            switch (wParam.ToInt32())
            {
                case User32.PBT_APMSUSPEND: Raised?.Invoke(PowerEvent.Suspend); return true;
                case User32.PBT_APMRESUMESUSPEND:
                case User32.PBT_APMRESUMEAUTOMATIC: Raised?.Invoke(PowerEvent.Resume); return true;
                case User32.PBT_POWERSETTINGCHANGE when lParam != IntPtr.Zero:
                    var s = Marshal.PtrToStructure<User32.POWERBROADCAST_SETTING>(lParam);
                    if (s.PowerSetting != ConsoleDisplayState || s.DataLength < 4) return false;
                    int state = (int)s.Data; // 0 off, 1 on, 2 dimmed
                    if (state == _lastDisplayState) return true; // Windows sends the current state once on registration
                    bool first = _lastDisplayState < 0;
                    _lastDisplayState = state;
                    if (first && state == 1) return true;
                    Raised?.Invoke(state switch { 0 => PowerEvent.DisplayOff, 2 => PowerEvent.DisplayDimmed, _ => PowerEvent.DisplayOn });
                    return true;
            }
            return false;
        }
        if (msg == User32.WM_DISPLAYCHANGE) { Raised?.Invoke(PowerEvent.DisplayChanged); return true; }
        if (msg == User32.WM_DPICHANGED) { Raised?.Invoke(PowerEvent.DpiChanged); return true; }
        if (_taskbarCreated != 0 && (uint)msg == _taskbarCreated) { Raised?.Invoke(PowerEvent.TaskbarCreated); return true; }
        return false;
    }

    public void Dispose()
    {
        if (_notify == IntPtr.Zero) return;
        User32.UnregisterPowerSettingNotification(_notify);
        _notify = IntPtr.Zero;
    }
}
