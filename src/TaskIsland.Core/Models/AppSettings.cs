using System.Globalization;
using TaskIsland.Core.Services;

namespace TaskIsland.Core.Models;

public enum ThemeMode { Dark, Light, System }
public enum DensityMode { Comfortable, Compact }
public enum PillCountScope { AllLists, SelectedList }
/// <summary>How often the mascot pops up on its own (PLAN.md section 6.8).</summary>
public enum IdleReactions { Off, Rare, Normal, Lively }
/// <summary>Small idle movements of the face between reactions: blinks, glances, breathing (PLAN.md section 6.8).</summary>
public enum CompanionLevel { Off, Calm, Lively }
/// <summary>Where the accent colour comes from (PLAN.md section 5).</summary>
public enum AccentSource { Default, Windows }

/// <summary>
/// Strongly typed view of the key/value Settings table (PLAN.md section 5). Unknown, missing or invalid
/// values fall back to the defaults below; numeric values are clamped to their allowed range.
/// </summary>
public sealed class AppSettings
{
    public static class Keys
    {
        public const string LaunchAtStartup = nameof(LaunchAtStartup);
        public const string StartCollapsed = nameof(StartCollapsed);
        public const string CollapseOnClickOutside = nameof(CollapseOnClickOutside);
        public const string CollapseOnEscape = nameof(CollapseOnEscape);
        public const string AlwaysShowNotch = nameof(AlwaysShowNotch);
        public const string Theme = nameof(Theme);
        public const string NotchWidth = nameof(NotchWidth);
        public const string HorizontalOffset = nameof(HorizontalOffset);
        public const string TopMargin = nameof(TopMargin);
        public const string PanelOpacity = nameof(PanelOpacity);
        public const string AnimationsEnabled = nameof(AnimationsEnabled);
        public const string FollowWindowsAnimations = nameof(FollowWindowsAnimations);
        public const string MascotEnabled = nameof(MascotEnabled);
        public const string IdleReactions = nameof(IdleReactions);
        public const string Companion = nameof(Companion);
        public const string Density = nameof(Density);
        public const string HideInFullScreen = nameof(HideInFullScreen);
        public const string MonitorDeviceName = nameof(MonitorDeviceName);
        public const string AutoCollapseSeconds = nameof(AutoCollapseSeconds);
        public const string RestoreAfterWake = nameof(RestoreAfterWake);
        public const string PeekOnHover = nameof(PeekOnHover);
        public const string RemindersEnabled = nameof(RemindersEnabled);
        public const string ReminderLeadMinutes = nameof(ReminderLeadMinutes);
        public const string ReminderPillAlert = nameof(ReminderPillAlert);
        public const string ReminderAlertWidthPercent = nameof(ReminderAlertWidthPercent);
        public const string ReminderBlink = nameof(ReminderBlink);
        public const string AccentSource = nameof(AccentSource);
        public const string ToggleHotkey = nameof(ToggleHotkey);
        public const string DailyBackupEnabled = nameof(DailyBackupEnabled);
        public const string CompletedFadeOut = nameof(CompletedFadeOut);
        public const string DefaultListId = nameof(DefaultListId);
        public const string PillCountScope = nameof(PillCountScope);
        public const string FirstRunCompleted = nameof(FirstRunCompleted);
        public const string LastBackupAt = nameof(LastBackupAt);
        public const string CurrentFilter = nameof(CurrentFilter);
        public const string SelectedListId = nameof(SelectedListId);
    }

    // General
    public bool LaunchAtStartup { get; set; } = true;
    public bool StartCollapsed { get; set; } = true;
    public bool CollapseOnClickOutside { get; set; } = true;
    public bool CollapseOnEscape { get; set; } = true;
    public bool AlwaysShowNotch { get; set; } = true;

    // Appearance
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    public int NotchWidth { get; set; } = 200;
    public int HorizontalOffset { get; set; } = 0;
    public int TopMargin { get; set; } = 0;
    public double PanelOpacity { get; set; } = 0.94;
    public bool AnimationsEnabled { get; set; } = true;
    /// <summary>Windows "Animation effects" off also means reduced motion here. Off by default (user's choice, 2026-11-04).</summary>
    public bool FollowWindowsAnimations { get; set; } = false;
    /// <summary>Face notch (section 6.8). Off = the classic count pill.</summary>
    public bool MascotEnabled { get; set; } = true;
    public IdleReactions IdleReactions { get; set; } = IdleReactions.Normal;
    public CompanionLevel Companion { get; set; } = CompanionLevel.Calm;
    public DensityMode Density { get; set; } = DensityMode.Comfortable;
    public AccentSource AccentSource { get; set; } = AccentSource.Default;

    // Behaviour
    public bool HideInFullScreen { get; set; } = true;
    /// <summary>Empty = primary monitor.</summary>
    public string MonitorDeviceName { get; set; } = "";
    public int AutoCollapseSeconds { get; set; } = 0;
    public bool RestoreAfterWake { get; set; } = true;
    public bool PeekOnHover { get; set; } = true;
    public bool RemindersEnabled { get; set; } = true;
    /// <summary>The minutes offered for the early reminder (0 = off). Anything else in the database falls back to off.</summary>
    public static readonly IReadOnlyList<int> ReminderLeadChoices = [0, 5, 15, 30, 60, 120, 1440];
    /// <summary>Early reminder this many minutes before the due time (tasks with a due time only). 0 = off.</summary>
    public int ReminderLeadMinutes { get; set; } = 0;
    /// <summary>The island widens and shows the task title when a reminder fires (PLAN.md section 7.9).</summary>
    public bool ReminderPillAlert { get; set; } = true;
    /// <summary>The widths offered for the reminder pill, in percent of the screen width (0 = compact, the old fixed width).</summary>
    public static readonly IReadOnlyList<int> ReminderAlertWidthChoices = [0, 30, 40, 50, 60, 75, 100];
    /// <summary>How wide the reminder pill grows, in percent of the screen width. 0 = compact. Anything else in the database falls back to 50.</summary>
    public int ReminderAlertWidthPercent { get; set; } = 50;
    /// <summary>A red border blinks around the reminder pill while it shows (steady when animations are reduced).</summary>
    public bool ReminderBlink { get; set; } = true;
    public string ToggleHotkey { get; set; } = "Ctrl+Alt+T";
    public bool CompletedFadeOut { get; set; } = true;

    // Data
    public bool DailyBackupEnabled { get; set; } = true;

    // Lists
    /// <summary>0 = not set yet (the data layer sets it to the starter "Personal" list).</summary>
    public long DefaultListId { get; set; } = 0;
    public PillCountScope PillCountScope { get; set; } = PillCountScope.AllLists;

    // Internal
    public bool FirstRunCompleted { get; set; } = false;
    public DateTime? LastBackupAt { get; set; }
    public TaskFilter CurrentFilter { get; set; } = TaskFilter.Active;
    /// <summary>0 = "All lists".</summary>
    public long SelectedListId { get; set; } = 0;

    public static AppSettings FromDictionary(IReadOnlyDictionary<string, string> values)
    {
        var s = new AppSettings();
        string? Get(string key) => values.TryGetValue(key, out var v) ? v : null;
        bool B(string key, bool def) => bool.TryParse(Get(key), out var v) ? v : def;
        int I(string key, int def, int min, int max) =>
            int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? Math.Clamp(v, min, max) : def;
        long L(string key, long def) =>
            long.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v >= 0 ? v : def;
        double D(string key, double def, double min, double max) =>
            double.TryParse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v)
                ? Math.Clamp(v, min, max) : def;
        T E<T>(string key, T def) where T : struct, Enum =>
            Enum.TryParse<T>(Get(key), ignoreCase: true, out var v) && Enum.IsDefined(v) ? v : def;

        s.LaunchAtStartup = B(Keys.LaunchAtStartup, s.LaunchAtStartup);
        s.StartCollapsed = B(Keys.StartCollapsed, s.StartCollapsed);
        s.CollapseOnClickOutside = B(Keys.CollapseOnClickOutside, s.CollapseOnClickOutside);
        s.CollapseOnEscape = B(Keys.CollapseOnEscape, s.CollapseOnEscape);
        s.AlwaysShowNotch = B(Keys.AlwaysShowNotch, s.AlwaysShowNotch);
        s.Theme = E(Keys.Theme, s.Theme);
        s.NotchWidth = I(Keys.NotchWidth, s.NotchWidth, 160, 280);
        s.HorizontalOffset = I(Keys.HorizontalOffset, s.HorizontalOffset, -2000, 2000);
        s.TopMargin = I(Keys.TopMargin, s.TopMargin, 0, 40);
        s.PanelOpacity = D(Keys.PanelOpacity, s.PanelOpacity, 0.70, 1.00);
        s.AnimationsEnabled = B(Keys.AnimationsEnabled, s.AnimationsEnabled);
        s.FollowWindowsAnimations = B(Keys.FollowWindowsAnimations, s.FollowWindowsAnimations);
        s.MascotEnabled = B(Keys.MascotEnabled, s.MascotEnabled);
        s.IdleReactions = E(Keys.IdleReactions, s.IdleReactions);
        s.Companion = E(Keys.Companion, s.Companion);
        s.Density = E(Keys.Density, s.Density);
        s.AccentSource = E(Keys.AccentSource, s.AccentSource);
        s.HideInFullScreen = B(Keys.HideInFullScreen, s.HideInFullScreen);
        s.MonitorDeviceName = Get(Keys.MonitorDeviceName) ?? s.MonitorDeviceName;
        s.AutoCollapseSeconds = I(Keys.AutoCollapseSeconds, s.AutoCollapseSeconds, 0, 3600);
        s.RestoreAfterWake = B(Keys.RestoreAfterWake, s.RestoreAfterWake);
        s.PeekOnHover = B(Keys.PeekOnHover, s.PeekOnHover);
        s.RemindersEnabled = B(Keys.RemindersEnabled, s.RemindersEnabled);
        // Only the offered values count; anything else (also out-of-range numbers) means off, it is not clamped to 1 day.
        s.ReminderLeadMinutes = int.TryParse(Get(Keys.ReminderLeadMinutes), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lead)
            && ReminderLeadChoices.Contains(lead) ? lead : 0;
        s.ReminderPillAlert = B(Keys.ReminderPillAlert, s.ReminderPillAlert);
        s.ReminderAlertWidthPercent = int.TryParse(Get(Keys.ReminderAlertWidthPercent), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pct)
            && ReminderAlertWidthChoices.Contains(pct) ? pct : s.ReminderAlertWidthPercent;
        s.ReminderBlink = B(Keys.ReminderBlink, s.ReminderBlink);
        var hotkey = Get(Keys.ToggleHotkey);
        s.ToggleHotkey = string.IsNullOrWhiteSpace(hotkey) ? s.ToggleHotkey : hotkey.Trim();
        s.CompletedFadeOut = B(Keys.CompletedFadeOut, s.CompletedFadeOut);
        s.DailyBackupEnabled = B(Keys.DailyBackupEnabled, s.DailyBackupEnabled);
        s.DefaultListId = L(Keys.DefaultListId, s.DefaultListId);
        s.PillCountScope = E(Keys.PillCountScope, s.PillCountScope);
        s.FirstRunCompleted = B(Keys.FirstRunCompleted, s.FirstRunCompleted);
        s.LastBackupAt = IsoDate.TryParseUtc(Get(Keys.LastBackupAt), out var lb) ? lb : null;
        s.CurrentFilter = E(Keys.CurrentFilter, s.CurrentFilter);
        s.SelectedListId = L(Keys.SelectedListId, s.SelectedListId);
        return s;
    }

    /// <summary>
    /// Copies every value from <paramref name="other"/> into this live instance (after a restore from backup the settings
    /// come from the restored database, but the rest of the app holds this object).
    /// </summary>
    public void CopyFrom(AppSettings other)
    {
        foreach (var p in typeof(AppSettings).GetProperties())
            if (p.CanWrite && p.GetIndexParameters().Length == 0) p.SetValue(this, p.GetValue(other));
    }

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        static string B(bool v) => v ? "true" : "false";
        static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        var d = new Dictionary<string, string>
        {
            [Keys.LaunchAtStartup] = B(LaunchAtStartup),
            [Keys.StartCollapsed] = B(StartCollapsed),
            [Keys.CollapseOnClickOutside] = B(CollapseOnClickOutside),
            [Keys.CollapseOnEscape] = B(CollapseOnEscape),
            [Keys.AlwaysShowNotch] = B(AlwaysShowNotch),
            [Keys.Theme] = Theme.ToString(),
            [Keys.NotchWidth] = NotchWidth.ToString(CultureInfo.InvariantCulture),
            [Keys.HorizontalOffset] = HorizontalOffset.ToString(CultureInfo.InvariantCulture),
            [Keys.TopMargin] = TopMargin.ToString(CultureInfo.InvariantCulture),
            [Keys.PanelOpacity] = N(PanelOpacity),
            [Keys.AnimationsEnabled] = B(AnimationsEnabled),
            [Keys.FollowWindowsAnimations] = B(FollowWindowsAnimations),
            [Keys.MascotEnabled] = B(MascotEnabled),
            [Keys.IdleReactions] = IdleReactions.ToString(),
            [Keys.Companion] = Companion.ToString(),
            [Keys.Density] = Density.ToString(),
            [Keys.AccentSource] = AccentSource.ToString(),
            [Keys.HideInFullScreen] = B(HideInFullScreen),
            [Keys.MonitorDeviceName] = MonitorDeviceName ?? "",
            [Keys.AutoCollapseSeconds] = AutoCollapseSeconds.ToString(CultureInfo.InvariantCulture),
            [Keys.RestoreAfterWake] = B(RestoreAfterWake),
            [Keys.PeekOnHover] = B(PeekOnHover),
            [Keys.RemindersEnabled] = B(RemindersEnabled),
            [Keys.ReminderLeadMinutes] = (ReminderLeadChoices.Contains(ReminderLeadMinutes) ? ReminderLeadMinutes : 0).ToString(CultureInfo.InvariantCulture),
            [Keys.ReminderPillAlert] = B(ReminderPillAlert),
            [Keys.ReminderAlertWidthPercent] = (ReminderAlertWidthChoices.Contains(ReminderAlertWidthPercent) ? ReminderAlertWidthPercent : 50).ToString(CultureInfo.InvariantCulture),
            [Keys.ReminderBlink] = B(ReminderBlink),
            [Keys.ToggleHotkey] = ToggleHotkey,
            [Keys.CompletedFadeOut] = B(CompletedFadeOut),
            [Keys.DailyBackupEnabled] = B(DailyBackupEnabled),
            [Keys.DefaultListId] = DefaultListId.ToString(CultureInfo.InvariantCulture),
            [Keys.PillCountScope] = PillCountScope.ToString(),
            [Keys.FirstRunCompleted] = B(FirstRunCompleted),
            [Keys.CurrentFilter] = CurrentFilter.ToString(),
            [Keys.SelectedListId] = SelectedListId.ToString(CultureInfo.InvariantCulture),
        };
        if (LastBackupAt is { } lb) d[Keys.LastBackupAt] = IsoDate.ToUtcString(lb);
        return d;
    }
}
