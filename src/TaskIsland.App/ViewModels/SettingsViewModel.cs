using System.Runtime.CompilerServices;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskIsland.App.Services;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;
using TaskIsland.Data;
using TaskIsland.Windows;

namespace TaskIsland.App.ViewModels;

/// <summary>
/// The Settings window's view model (PLAN.md sections 5 and 6.6). Every property is a thin view of one value in the shared
/// <see cref="AppSettings"/>; changing it applies the setting live and saves it through <see cref="SettingsApplier"/>.
/// Choices (theme, density, ...) are exposed as an index into a list of labels, which keeps the XAML free of converters.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private static readonly int[] AutoCollapseChoices = [0, 10, 30, 60, 120, 300];

    private readonly AppSettings _s;
    private readonly SettingsApplier _apply;
    private readonly DataMaintenanceService _maintenance;
    private List<string> _monitorNames = [];

    public SettingsViewModel(AppSettings settings, SettingsApplier applier, IslandViewModel island, ITaskRepository tasks,
        ITodoListRepository lists, ISettingsStore store, BackupService backup, DataMaintenanceService maintenance,
        ImportService import, AppPaths paths, IClock clock)
    {
        _s = settings;
        _apply = applier;
        Island = island;
        _tasks = tasks;
        _listsRepo = lists;
        _store = store;
        _backup = backup;
        _maintenance = maintenance;
        _import = import;
        _paths = paths;
        _clock = clock;
        BuildMonitors();
        BuildAutoCollapse();
        RefreshLists();
        UpdateBackupText();
    }

    /// <summary>The island view model: its Undo bar is shown on the Data page after "Clear completed".</summary>
    public IslandViewModel Island { get; }

    /// <summary>The Settings window, owner of every dialog. Set by the window.</summary>
    public Window? Owner { get; set; }

    // ---- option lists ----

    public IReadOnlyList<string> ThemeOptions { get; } = ["Dark", "Light", "System"];
    public IReadOnlyList<string> AccentOptions { get; } = ["TaskIsland blue", "Windows accent"];
    public IReadOnlyList<string> DensityOptions { get; } = ["Comfortable", "Compact"];
    public IReadOnlyList<string> IdleOptions { get; } = ["Off", "Rare", "Normal", "Lively"];
    public IReadOnlyList<string> CompanionOptions { get; } = ["Off", "Calm", "Lively"];
    public IReadOnlyList<string> PillScopeOptions { get; } = ["All lists", "Selected list"];
    public IReadOnlyList<string> LeadOptions { get; } = ["Off", "5 minutes", "15 minutes", "30 minutes", "1 hour", "2 hours", "1 day"];
    public IReadOnlyList<string> AlertWidthOptions { get; } =
        ["Compact", "30% of the screen", "40% of the screen", "50% of the screen", "60% of the screen", "75% of the screen", "Full width"];
    public IReadOnlyList<string> MonitorOptions { get; private set; } = [];
    public IReadOnlyList<string> AutoCollapseOptions { get; private set; } = [];

    private bool Set<T>(T current, T value, Action<T> assign, string key, bool debounce = false,
        [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(current, value)) return false;
        assign(value);
        OnPropertyChanged(property);
        _apply.Changed(key, debounce);
        return true;
    }

    // ---- General ----

    public bool LaunchAtStartup { get => _s.LaunchAtStartup; set => Set(_s.LaunchAtStartup, value, v => _s.LaunchAtStartup = v, AppSettings.Keys.LaunchAtStartup); }
    public bool StartCollapsed { get => _s.StartCollapsed; set => Set(_s.StartCollapsed, value, v => _s.StartCollapsed = v, AppSettings.Keys.StartCollapsed); }
    /// <summary>"Open expanded when I start it" is the opposite of StartCollapsed.</summary>
    public bool OpenExpanded { get => !_s.StartCollapsed; set => Set(!_s.StartCollapsed, value, v => _s.StartCollapsed = !v, AppSettings.Keys.StartCollapsed); }
    public bool CollapseOnClickOutside { get => _s.CollapseOnClickOutside; set => Set(_s.CollapseOnClickOutside, value, v => _s.CollapseOnClickOutside = v, AppSettings.Keys.CollapseOnClickOutside); }
    public bool CollapseOnEscape { get => _s.CollapseOnEscape; set => Set(_s.CollapseOnEscape, value, v => _s.CollapseOnEscape = v, AppSettings.Keys.CollapseOnEscape); }
    public bool AlwaysShowNotch { get => _s.AlwaysShowNotch; set => Set(_s.AlwaysShowNotch, value, v => _s.AlwaysShowNotch = v, AppSettings.Keys.AlwaysShowNotch); }

    // ---- Appearance ----

    public int ThemeIndex { get => (int)_s.Theme; set => Set((int)_s.Theme, value, v => _s.Theme = (TaskIsland.Core.Models.ThemeMode)v, AppSettings.Keys.Theme); }
    public int AccentIndex { get => (int)_s.AccentSource; set => Set((int)_s.AccentSource, value, v => _s.AccentSource = (AccentSource)v, AppSettings.Keys.AccentSource); }
    public int DensityIndex { get => (int)_s.Density; set => Set((int)_s.Density, value, v => _s.Density = (DensityMode)v, AppSettings.Keys.Density); }
    public int IdleIndex { get => (int)_s.IdleReactions; set => Set((int)_s.IdleReactions, value, v => _s.IdleReactions = (IdleReactions)v, AppSettings.Keys.IdleReactions); }
    public int CompanionIndex { get => (int)_s.Companion; set => Set((int)_s.Companion, value, v => _s.Companion = (CompanionLevel)v, AppSettings.Keys.Companion); }

    public double NotchWidth
    {
        get => _s.NotchWidth;
        set => Set(_s.NotchWidth, (int)Math.Round(Math.Clamp(value, 160, 280)), v => _s.NotchWidth = v, AppSettings.Keys.NotchWidth, debounce: true);
    }

    public double HorizontalOffset
    {
        get => _s.HorizontalOffset;
        set => Set(_s.HorizontalOffset, (int)Math.Round(Math.Clamp(value, -2000, 2000)), v => _s.HorizontalOffset = v, AppSettings.Keys.HorizontalOffset, debounce: true);
    }

    public double TopMargin
    {
        get => _s.TopMargin;
        set => Set(_s.TopMargin, (int)Math.Round(Math.Clamp(value, 0, 40)), v => _s.TopMargin = v, AppSettings.Keys.TopMargin, debounce: true);
    }

    public double PanelOpacity
    {
        get => _s.PanelOpacity;
        set => Set(_s.PanelOpacity, Math.Round(Math.Clamp(value, 0.70, 1.00), 2), v => _s.PanelOpacity = v, AppSettings.Keys.PanelOpacity, debounce: true);
    }

    public bool AnimationsEnabled { get => _s.AnimationsEnabled; set => Set(_s.AnimationsEnabled, value, v => _s.AnimationsEnabled = v, AppSettings.Keys.AnimationsEnabled); }
    public bool FollowWindowsAnimations { get => _s.FollowWindowsAnimations; set => Set(_s.FollowWindowsAnimations, value, v => _s.FollowWindowsAnimations = v, AppSettings.Keys.FollowWindowsAnimations); }
    public bool MascotEnabled { get => _s.MascotEnabled; set => Set(_s.MascotEnabled, value, v => _s.MascotEnabled = v, AppSettings.Keys.MascotEnabled); }

    // ---- Behaviour ----

    public bool HideInFullScreen { get => _s.HideInFullScreen; set => Set(_s.HideInFullScreen, value, v => _s.HideInFullScreen = v, AppSettings.Keys.HideInFullScreen); }
    public bool RestoreAfterWake { get => _s.RestoreAfterWake; set => Set(_s.RestoreAfterWake, value, v => _s.RestoreAfterWake = v, AppSettings.Keys.RestoreAfterWake); }
    public bool PeekOnHover { get => _s.PeekOnHover; set => Set(_s.PeekOnHover, value, v => _s.PeekOnHover = v, AppSettings.Keys.PeekOnHover); }
    public bool CompletedFadeOut { get => _s.CompletedFadeOut; set => Set(_s.CompletedFadeOut, value, v => _s.CompletedFadeOut = v, AppSettings.Keys.CompletedFadeOut); }
    public bool RemindersEnabled { get => _s.RemindersEnabled; set => Set(_s.RemindersEnabled, value, v => _s.RemindersEnabled = v, AppSettings.Keys.RemindersEnabled); }
    public bool ReminderPillAlert { get => _s.ReminderPillAlert; set => Set(_s.ReminderPillAlert, value, v => _s.ReminderPillAlert = v, AppSettings.Keys.ReminderPillAlert); }
    public bool ReminderBlink { get => _s.ReminderBlink; set => Set(_s.ReminderBlink, value, v => _s.ReminderBlink = v, AppSettings.Keys.ReminderBlink); }

    /// <summary>"Reminder width": index into <see cref="AlertWidthOptions"/> (Compact, 30 %, 40 %, 50 %, 60 %, 75 %, Full width).</summary>
    public int AlertWidthIndex
    {
        get => Math.Max(0, AppSettings.ReminderAlertWidthChoices.ToList().IndexOf(_s.ReminderAlertWidthPercent));
        set
        {
            int pct = AppSettings.ReminderAlertWidthChoices[Math.Clamp(value, 0, AppSettings.ReminderAlertWidthChoices.Count - 1)];
            Set(_s.ReminderAlertWidthPercent, pct, v => _s.ReminderAlertWidthPercent = v, AppSettings.Keys.ReminderAlertWidthPercent);
        }
    }

    /// <summary>The width and blink options only matter while the island widens for reminders.</summary>
    public bool AlertOptionsEnabled => ReminderPillAlert;

    /// <summary>"Remind me before due time": index into <see cref="LeadOptions"/> (Off, 5, 15, 30, 1 hour, 2 hours, 1 day).</summary>
    public int LeadIndex
    {
        get => Math.Max(0, AppSettings.ReminderLeadChoices.ToList().IndexOf(_s.ReminderLeadMinutes));
        set
        {
            int minutes = AppSettings.ReminderLeadChoices[Math.Clamp(value, 0, AppSettings.ReminderLeadChoices.Count - 1)];
            Set(_s.ReminderLeadMinutes, minutes, v => _s.ReminderLeadMinutes = v, AppSettings.Keys.ReminderLeadMinutes);
        }
    }

    /// <summary>Whether the lead picker is usable: only with reminders on.</summary>
    public bool LeadEnabled => RemindersEnabled;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(RemindersEnabled))
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(LeadEnabled)));
        if (e.PropertyName == nameof(ReminderPillAlert))
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(AlertOptionsEnabled)));
    }

    // ---- monitor ----

    private void BuildMonitors()
    {
        var names = new List<string> { "" };
        var labels = new List<string> { "Primary display (default)" };
        try
        {
            int n = 1;
            foreach (var m in MonitorService.GetAll())
            {
                names.Add(m.DeviceName);
                labels.Add($"Display {n++} — {m.Width}×{m.Height}{(m.IsPrimary ? " (Primary)" : "")}");
            }
        }
        catch (Exception ex)
        {
            Logging.FileLogger.Warn($"could not list monitors: {ex.Message}");
        }
        if (_s.MonitorDeviceName.Length > 0 && !names.Contains(_s.MonitorDeviceName))
        {
            names.Add(_s.MonitorDeviceName);
            labels.Add("Disconnected display (the primary is used)");
        }
        _monitorNames = names;
        MonitorOptions = labels;
    }

    public int MonitorIndex
    {
        get => Math.Max(0, _monitorNames.IndexOf(_s.MonitorDeviceName));
        set
        {
            if (value < 0 || value >= _monitorNames.Count) return;
            Set(_s.MonitorDeviceName, _monitorNames[value], v => _s.MonitorDeviceName = v, AppSettings.Keys.MonitorDeviceName);
        }
    }

    // ---- auto collapse ----

    private void BuildAutoCollapse()
    {
        var values = AutoCollapseChoices.ToList();
        if (!values.Contains(_s.AutoCollapseSeconds)) values.Add(_s.AutoCollapseSeconds);
        values.Sort();
        _autoValues = values;
        AutoCollapseOptions = values.Select(v => v == 0 ? "Off" : v < 60 ? $"After {v} seconds" : v == 60 ? "After 1 minute" : v % 60 == 0 ? $"After {v / 60} minutes" : $"After {v} seconds").ToList();
    }

    private List<int> _autoValues = [];

    public int AutoCollapseIndex
    {
        get => Math.Max(0, _autoValues.IndexOf(_s.AutoCollapseSeconds));
        set
        {
            if (value < 0 || value >= _autoValues.Count) return;
            Set(_s.AutoCollapseSeconds, _autoValues[value], v => _s.AutoCollapseSeconds = v, AppSettings.Keys.AutoCollapseSeconds);
        }
    }

    // ---- hotkey ----

    public string ToggleHotkey => _s.ToggleHotkey;

    /// <summary>Used by the capture box: null = accepted and registered, else why it was refused.</summary>
    public string? TryRebindHotkey(string text)
    {
        var problem = _apply.TryRebindHotkey(text);
        if (problem is null)
        {
            OnPropertyChanged(nameof(ToggleHotkey));
            OnPropertyChanged(nameof(HotkeyWarning));
        }
        return problem;
    }

    /// <summary>Shown when the saved shortcut could not be registered at start-up or after wake (another app owns it).</summary>
    public string HotkeyWarning => _apply.HotkeyUnavailable
        ? $"{_s.ToggleHotkey} is in use by another app, so the shortcut does not work right now. Choose another one."
        : "";

    /// <summary>After a restore or an import "Replace" the stored values changed under the window: show them all again.</summary>
    public void RefreshAll()
    {
        BuildMonitors();
        BuildAutoCollapse();
        OnPropertyChanged(string.Empty);
        RefreshLists();
        UpdateBackupText();
    }
}
