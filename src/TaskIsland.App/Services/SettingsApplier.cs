using System.Windows;
using System.Windows.Threading;
using TaskIsland.App.Logging;
using TaskIsland.App.ViewModels;
using TaskIsland.App.Views;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;

namespace TaskIsland.App.Services;

/// <summary>
/// Makes a changed setting take effect at once and saves it (PLAN.md section 5: "Settings apply live and save
/// immediately"). One place knows which part of the app each key touches, so the Settings window only edits values.
/// Sliders save 300 ms after the last change; everything else saves immediately. Nothing runs when nothing changes.
/// </summary>
public sealed class SettingsApplier
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(300);

    private readonly AppSettings _settings;
    private readonly ISettingsStore _store;
    private readonly IslandWindow _window;
    private readonly RecoveryCoordinator _recovery;
    private readonly ReminderService _reminders;
    private readonly ReactionDirector _director;
    private readonly MotionPolicy _motion;
    private readonly IslandViewModel _island;
    private readonly Dictionary<string, DispatcherTimer> _pending = [];

    public SettingsApplier(AppSettings settings, ISettingsStore store, IslandWindow window, RecoveryCoordinator recovery,
        ReminderService reminders, ReactionDirector director, MotionPolicy motion, IslandViewModel island)
    {
        _settings = settings;
        _store = store;
        _window = window;
        _recovery = recovery;
        _reminders = reminders;
        _director = director;
        _motion = motion;
        _island = island;
    }

    /// <summary>A value in <see cref="AppSettings"/> was just changed: apply it now and save it.</summary>
    public void Changed(string key, bool debounceSave = false)
    {
        try { Apply(key); }
        catch (Exception ex) { FileLogger.Error($"could not apply setting {key}", ex); }
        if (debounceSave) SaveLater(key);
        else _ = SaveAsync(key);
        FileLogger.Info($"setting {key} = {Describe(key)}");
    }

    /// <summary>Saves anything still waiting for its 300 ms (the Settings window is closing).</summary>
    public void FlushPending()
    {
        foreach (var (key, timer) in _pending.ToList())
        {
            timer.Stop();
            _ = SaveAsync(key);
        }
        _pending.Clear();
    }

    /// <summary>After a restore the whole settings object was replaced: apply every setting that has an effect.</summary>
    public void ApplyAll()
    {
        foreach (var key in AllKeys)
        {
            try { Apply(key); }
            catch (Exception ex) { FileLogger.Error($"could not apply setting {key}", ex); }
        }
    }

    private static readonly string[] AllKeys =
    [
        AppSettings.Keys.Theme, AppSettings.Keys.AccentSource, AppSettings.Keys.Density, AppSettings.Keys.MascotEnabled,
        AppSettings.Keys.NotchWidth, AppSettings.Keys.PanelOpacity, AppSettings.Keys.TopMargin, AppSettings.Keys.MonitorDeviceName,
        AppSettings.Keys.AnimationsEnabled, AppSettings.Keys.FollowWindowsAnimations, AppSettings.Keys.IdleReactions,
        AppSettings.Keys.AlwaysShowNotch, AppSettings.Keys.HideInFullScreen, AppSettings.Keys.ToggleHotkey,
        AppSettings.Keys.RemindersEnabled, AppSettings.Keys.LaunchAtStartup, AppSettings.Keys.PillCountScope,
    ];

    private void Apply(string key)
    {
        var app = (App)Application.Current;
        switch (key)
        {
            case nameof(AppSettings.LaunchAtStartup):
                StartupManager.ApplyToggle(_settings.LaunchAtStartup, App.TestRoot);
                break;
            case nameof(AppSettings.Theme):
                app.ApplyTheme(_settings.Theme);
                break;
            case nameof(AppSettings.AccentSource):
            case nameof(AppSettings.Density):
                app.ApplyAppearance();
                break;
            case nameof(AppSettings.NotchWidth):
            case nameof(AppSettings.PanelOpacity):
            case nameof(AppSettings.MascotEnabled):
                _window.ApplyLookSettings();
                break;
            case nameof(AppSettings.TopMargin):
                _window.ApplyLookSettings();
                _window.Reposition();
                break;
            case nameof(AppSettings.HorizontalOffset):
            case nameof(AppSettings.MonitorDeviceName):
                _window.Reposition();
                break;
            case nameof(AppSettings.AnimationsEnabled):
                _motion.AnimationsEnabled = _settings.AnimationsEnabled;
                break;
            case nameof(AppSettings.FollowWindowsAnimations):
                _motion.FollowWindowsAnimations = _settings.FollowWindowsAnimations;
                break;
            case nameof(AppSettings.IdleReactions):
            case nameof(AppSettings.Companion):
                _director.SettingsChanged();
                break;
            case nameof(AppSettings.AlwaysShowNotch):
                _window.ApplyAlwaysShow();
                break;
            case nameof(AppSettings.HideInFullScreen):
                _recovery.ApplyFullScreenSetting();
                break;
            case nameof(AppSettings.ToggleHotkey):
                _recovery.RegisterHotkey();
                break;
            case nameof(AppSettings.RemindersEnabled):
            case nameof(AppSettings.ReminderLeadMinutes):
                _reminders.Recompute();
                break;
            case nameof(AppSettings.DefaultListId):
            case nameof(AppSettings.PillCountScope):
                _ = _island.ListSettingsChangedAsync();
                break;
        }
    }

    private string Describe(string key) =>
        _settings.ToDictionary().TryGetValue(key, out var v) ? v : "?";

    private void SaveLater(string key)
    {
        if (!_pending.TryGetValue(key, out var timer))
        {
            timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = SaveDelay };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                _pending.Remove(key);
                _ = SaveAsync(key);
            };
            _pending[key] = timer;
        }
        timer.Stop();
        timer.Start();
    }

    public async Task SaveAsync(string key)
    {
        try
        {
            if (_settings.ToDictionary().TryGetValue(key, out var value)) await _store.SetAsync(key, value);
        }
        catch (Exception ex)
        {
            FileLogger.Error($"could not save setting {key}", ex);
        }
    }

    /// <summary>Tries a new shortcut. Null = it is registered and saved; otherwise the reason it was refused (the old one is kept).</summary>
    public string? TryRebindHotkey(string text)
    {
        var old = _settings.ToggleHotkey;
        _settings.ToggleHotkey = text;
        if (_recovery.RegisterHotkey())
        {
            _ = SaveAsync(AppSettings.Keys.ToggleHotkey);
            FileLogger.Info($"setting ToggleHotkey = {text}");
            return null;
        }
        int error = _recovery.Hotkey.LastError;
        _settings.ToggleHotkey = old;
        _recovery.RegisterHotkey();
        return error == 1409
            ? $"{text} is in use by another app. Pick a different shortcut."
            : $"Windows would not register {text} (error {error}). Pick a different shortcut.";
    }

    /// <summary>The shortcut from the settings is not registered right now (another app holds it).</summary>
    public bool HotkeyUnavailable => !_recovery.Hotkey.IsRegistered;
}
