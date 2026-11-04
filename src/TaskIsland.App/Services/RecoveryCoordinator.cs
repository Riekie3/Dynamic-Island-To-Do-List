using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using TaskIsland.App.Logging;
using TaskIsland.App.Views;
using TaskIsland.Core.Mascot;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;
using TaskIsland.Data;
using TaskIsland.Windows;

namespace TaskIsland.App.Services;

/// <summary>
/// Glue between Windows events and the island (PLAN.md sections 7.3, 7.4, 7.8). Everything is event-driven: window
/// messages through one <see cref="HwndSource"/> hook, <see cref="SystemEvents"/> for lock/unlock, display settings and
/// clock changes, and out-of-context WinEvents for full-screen detection. Timers here are one-shot (debounce and the two
/// follow-up repositions after a wake), never polling.
/// </summary>
public sealed class RecoveryCoordinator : IDisposable
{
    private static readonly TimeSpan RecoverDebounce = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan FullScreenDebounce = TimeSpan.FromMilliseconds(300);

    private readonly IslandWindow _window;
    private readonly AppSettings _settings;
    private readonly ReactionDirector _director;
    private readonly ReminderService _reminders;
    private readonly BackupService _backup;
    private readonly ISettingsStore _store;
    private readonly TrayIconService _tray;
    private readonly IslandPositioner _positioner;
    private readonly PowerEventsService _power = new();
    private readonly HotkeyService _hotkey = new();
    private readonly FullScreenDetector _fullScreen = new();
    private readonly DispatcherTimer _recover, _followUp2, _followUp6, _fsCheck;
    private HwndSource? _source;
    private HwndSource? _noticeWindow; // hidden helper for the shell's full-screen notice
    private string _pendingReasons = "";
    private bool _disposed;

    public RecoveryCoordinator(IslandWindow window, AppSettings settings, ReactionDirector director, ReminderService reminders,
        BackupService backup, ISettingsStore store, TrayIconService tray, IslandPositioner positioner)
    {
        _window = window;
        _settings = settings;
        _director = director;
        _reminders = reminders;
        _backup = backup;
        _store = store;
        _tray = tray;
        _positioner = positioner;
        _recover = OneShot(RecoverDebounce, () => _ = RecoverAsync());
        _followUp2 = OneShot(TimeSpan.FromSeconds(2), () => FollowUp("+2 s"));
        _followUp6 = OneShot(TimeSpan.FromSeconds(6), () => FollowUp("+6 s"));
        _fsCheck = OneShot(FullScreenDebounce, EvaluateFullScreen);
        _hotkey.Pressed += () => Guard("hotkey", _window.Toggle);
        _power.Raised += OnPower;
        _fullScreen.Changed += () => { _fsCheck.Stop(); _fsCheck.Start(); };
    }

    public HotkeyService Hotkey => _hotkey;
    public bool FullScreenWatching => _fullScreen.IsRunning;

    /// <summary>
    /// The WinEvent hooks run only while HideInFullScreen is on: with it off nothing is watched, so nothing wakes us.
    /// Settings (Phase 5) calls this when the toggle changes.
    /// </summary>
    public void ApplyFullScreenSetting()
    {
        if (_settings.HideInFullScreen)
        {
            if (_fullScreen.IsRunning) return;
            _noticeWindow ??= CreateNoticeWindow();
            FileLogger.Info("full-screen detection running");
            if (!_fullScreen.Start(_noticeWindow?.Handle ?? IntPtr.Zero)) FileLogger.Warn("full-screen detection unavailable (SetWinEventHook failed)");
            else if (!_fullScreen.ShellNoticesRegistered) FileLogger.Warn("shell full-screen notices unavailable; F11-style full screen is seen at the next window switch");
        }
        else
        {
            _fullScreen.Stop();
            _window.SetFullScreen(false);
            FileLogger.Info("full-screen detection stopped (HideInFullScreen off)");
        }
    }

    /// <summary>Call once the island window has its handle.</summary>
    public void Attach()
    {
        var hwnd = _window.Hwnd;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);
        if (!_power.Attach(hwnd)) FileLogger.Warn("display on/off notifications unavailable (RegisterPowerSettingNotification failed)");
        RegisterHotkey();
        _fullScreen.AddOwnWindow(hwnd);
        ApplyFullScreenSetting();
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.TimeChanged += OnTimeChanged;
        FileLogger.Info("windows integration attached (power, session, display, full-screen, hotkey)");
        EvaluateFullScreen();
    }

    /// <summary>Registers the toggle shortcut from the settings. Failure is logged; Settings (Phase 5) shows a warning.</summary>
    public bool RegisterHotkey()
    {
        var g = HotkeyGesture.ParseOrDefault(_settings.ToggleHotkey);
        bool ok = _hotkey.Register(_window.Hwnd, (uint)g.Modifiers, g.VirtualKey);
        if (ok) FileLogger.Info($"hotkey {g} registered");
        else FileLogger.Warn($"hotkey {g} could not be registered (error {_hotkey.LastError}; 1409 = in use by another app)");
        return ok;
    }

    /// <summary>A hidden, zero-size tool window (never shown) that receives the shell's full-screen notice.</summary>
    private HwndSource? CreateNoticeWindow()
    {
        try
        {
            var p = new HwndSourceParameters("TaskIsland full-screen notices")
            {
                WindowStyle = unchecked((int)0x80000000), // WS_POPUP, not visible
                ExtendedWindowStyle = 0x00000080,          // WS_EX_TOOLWINDOW: never in Alt-Tab or the taskbar
                Width = 0,
                Height = 0,
            };
            var source = new HwndSource(p);
            source.AddHook((IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) =>
            {
                if (_fullScreen.HandleMessage(msg, w)) handled = true;
                return IntPtr.Zero;
            });
            _fullScreen.AddOwnWindow(source.Handle);
            return source;
        }
        catch (Exception ex)
        {
            FileLogger.Error("could not create the full-screen notice window", ex);
            return null;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_hotkey.HandleMessage(msg, wParam)) { handled = true; return IntPtr.Zero; }
        _power.HandleMessage(msg, wParam, lParam);
        return IntPtr.Zero;
    }

    private void OnPower(PowerEvent e)
    {
        switch (e)
        {
            case PowerEvent.Suspend:
                FileLogger.Info("power: suspend");
                Guard("collapse on suspend", _window.Collapse);
                break;
            case PowerEvent.Resume:
                FileLogger.Info("power: resume");
                ScheduleRecovery("resume");
                break;
            case PowerEvent.DisplayOff:
                FileLogger.Info("power: display off");
                _director.SetSuppressed(Suppression.DisplayOff, true);
                break;
            case PowerEvent.DisplayDimmed:
                FileLogger.Info("power: display dimmed");
                break;
            case PowerEvent.DisplayOn:
                FileLogger.Info("power: display on");
                _director.SetSuppressed(Suppression.DisplayOff, false);
                ScheduleRecovery("display on");
                break;
            case PowerEvent.DisplayChanged:
                ScheduleRecovery("WM_DISPLAYCHANGE");
                break;
            case PowerEvent.DpiChanged:
                // WPF rescales first (we do not mark the message handled); then we place the window ourselves and ignore
                // the rectangle Windows suggested.
                FileLogger.Info("dpi changed");
                _window.Dispatcher.BeginInvoke(() => Guard("reposition after DPI change", _window.Reposition), DispatcherPriority.Background);
                break;
            case PowerEvent.TaskbarCreated:
                FileLogger.Info("explorer restarted (TaskbarCreated): tray icon and topmost again");
                Guard("tray after Explorer restart", () => _tray.EnsureVisible(force: true));
                Guard("topmost after Explorer restart", _window.ReapplyWindowStyles);
                break;
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e) =>
        _window.Dispatcher.BeginInvoke(() =>
        {
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLock:
                case SessionSwitchReason.ConsoleDisconnect:
                case SessionSwitchReason.RemoteDisconnect:
                    FileLogger.Info($"session: {e.Reason}");
                    _director.SetSuppressed(Suppression.Locked, true);
                    Guard("collapse on lock", _window.Collapse);
                    break;
                case SessionSwitchReason.SessionUnlock:
                case SessionSwitchReason.ConsoleConnect:
                case SessionSwitchReason.RemoteConnect:
                    FileLogger.Info($"session: {e.Reason}");
                    _director.SetSuppressed(Suppression.Locked, false);
                    ScheduleRecovery(e.Reason.ToString());
                    break;
            }
        });

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        _window.Dispatcher.BeginInvoke(() => ScheduleRecovery("display settings changed"));

    private void OnTimeChanged(object? sender, EventArgs e) =>
        _window.Dispatcher.BeginInvoke(() =>
        {
            FileLogger.Info("clock changed: reminders recomputed");
            Guard("reminders after clock change", _reminders.Recompute);
        });

    /// <summary>Several events usually arrive together after a wake; one recovery runs 750 ms after the last.</summary>
    public void ScheduleRecovery(string reason)
    {
        if (_disposed) return;
        _pendingReasons = _pendingReasons.Length == 0 ? reason : _pendingReasons + ", " + reason;
        _recover.Stop();
        _recover.Start();
    }

    /// <summary>PLAN.md section 7.3 steps 1–5. Each step is guarded, so one failure does not stop the others.</summary>
    public async Task RecoverAsync()
    {
        string why = _pendingReasons;
        _pendingReasons = "";
        FileLogger.Info($"recovery start ({why})");
        Guard("1 reposition", _window.Reposition);
        Guard("2 window styles and topmost", _window.ReapplyWindowStyles);
        if (_settings.RestoreAfterWake) Guard("2 restore visibility", _window.AutoShow);
        Guard("3 tray icon", () => _tray.EnsureVisible(force: false));
        if (!_hotkey.IsRegistered) Guard("4 hotkey", () => RegisterHotkey());
        Guard("4 reminders", _reminders.Recompute);
        try
        {
            var backup = await _backup.RunDailyBackupIfDueAsync(_store);
            if (backup is not null)
            {
                _settings.LastBackupAt = DateTime.UtcNow;
                FileLogger.Info($"4 daily backup written: {System.IO.Path.GetFileName(backup)}");
            }
        }
        catch (Exception ex) { FileLogger.Error("4 daily backup failed", ex); }
        EvaluateFullScreen();
        _followUp2.Stop(); _followUp2.Start();
        _followUp6.Stop(); _followUp6.Start();
        FileLogger.Info($"recovery done: visible={_window.IsVisible} paused={_window.IsPaused} hidden={_window.IsUserHidden} " +
            $"fullscreen={_window.IsFullScreenHidden} hotkey={_hotkey.IsRegistered} monitor={_positioner.Monitor?.DeviceName}");
    }

    /// <summary>Displays often report their final size late after a wake: one-shot repositions at +2 s and +6 s.</summary>
    private void FollowUp(string when)
    {
        Guard($"follow-up {when}", () =>
        {
            _window.Reposition();
            _window.ReapplyWindowStyles();
        });
        FileLogger.Info($"recovery follow-up {when}: monitor={_positioner.Monitor?.DeviceName} rect={_positioner.LastRect}");
    }

    /// <summary>PLAN.md section 7.4: hide only for a full-screen window on the island's own monitor.</summary>
    private void EvaluateFullScreen()
    {
        if (_disposed) return;
        if (!_settings.HideInFullScreen) { _window.SetFullScreen(false); return; }
        var snap = FullScreenDetector.Snapshot();
        if (snap.Hwnd == IntPtr.Zero || _fullScreen.IsOwn(snap.Hwnd)) return; // nothing new to judge; keep the state
        var monitor = _positioner.Monitor;
        if (monitor is null) return;
        bool full = FullScreenRules.IsFullScreen((NotificationState)snap.NotificationState,
            new PixelRect(snap.Left, snap.Top, snap.Right, snap.Bottom), snap.ClassName, isOwnWindow: false,
            snap.IsMaximized, string.Equals(snap.MonitorDevice, monitor.DeviceName, StringComparison.OrdinalIgnoreCase),
            IslandPositioner.Bounds(monitor));
        if (full != _window.IsFullScreenHidden)
            FileLogger.Info($"full-screen check: {(full ? "full-screen" : "normal")} (class {snap.ClassName}, state {snap.NotificationState}, " +
                $"rect {snap.Left},{snap.Top},{snap.Right},{snap.Bottom}, maximised {snap.IsMaximized})");
        _window.SetFullScreen(full);
    }

    private static DispatcherTimer OneShot(TimeSpan interval, Action tick)
    {
        var t = new DispatcherTimer(DispatcherPriority.Normal) { Interval = interval };
        t.Tick += (_, _) => { t.Stop(); tick(); };
        return t;
    }

    private static void Guard(string what, Action action)
    {
        try { action(); }
        catch (Exception ex) { FileLogger.Error($"{what} failed", ex); }
    }

    /// <summary>Exit path: unhook everything (static SystemEvents would otherwise keep us alive).</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var t in new[] { _recover, _followUp2, _followUp6, _fsCheck }) t.Stop();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.TimeChanged -= OnTimeChanged;
        _fullScreen.Dispose();
        _hotkey.Dispose();
        _power.Dispose();
        _source?.RemoveHook(WndProc);
        _noticeWindow?.Dispose();
    }
}
