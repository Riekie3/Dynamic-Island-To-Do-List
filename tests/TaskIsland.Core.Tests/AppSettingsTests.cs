using TaskIsland.Core.Models;
using static TaskIsland.Core.Tests.TestData;

namespace TaskIsland.Core.Tests;

public class AppSettingsTests
{
    private static AppSettings From(params (string Key, string Value)[] pairs) =>
        AppSettings.FromDictionary(pairs.ToDictionary(p => p.Key, p => p.Value));

    [Fact]
    public void Empty_dictionary_gives_the_documented_defaults()
    {
        var s = AppSettings.FromDictionary(new Dictionary<string, string>());
        Assert.True(s.LaunchAtStartup);
        Assert.True(s.StartCollapsed);
        Assert.True(s.CollapseOnClickOutside);
        Assert.True(s.CollapseOnEscape);
        Assert.True(s.AlwaysShowNotch);
        Assert.Equal(ThemeMode.Dark, s.Theme);
        Assert.Equal(200, s.NotchWidth);
        Assert.Equal(0, s.HorizontalOffset);
        Assert.Equal(0, s.TopMargin);
        Assert.Equal(0.94, s.PanelOpacity);
        Assert.True(s.AnimationsEnabled);
        Assert.True(s.MascotEnabled);
        Assert.Equal(IdleReactions.Normal, s.IdleReactions);
        Assert.Equal(DensityMode.Comfortable, s.Density);
        Assert.True(s.HideInFullScreen);
        Assert.Equal("", s.MonitorDeviceName);
        Assert.Equal(0, s.AutoCollapseSeconds);
        Assert.True(s.RestoreAfterWake);
        Assert.True(s.PeekOnHover);
        Assert.True(s.RemindersEnabled);
        Assert.Equal("Ctrl+Alt+T", s.ToggleHotkey);
        Assert.True(s.CompletedFadeOut);
        Assert.True(s.DailyBackupEnabled);
        Assert.Equal(PillCountScope.AllLists, s.PillCountScope);
        Assert.False(s.FirstRunCompleted);
        Assert.Null(s.LastBackupAt);
        Assert.Equal(TaskFilter.Active, s.CurrentFilter);
        Assert.Equal(0, s.SelectedListId);
    }

    [Fact]
    public void Round_trip_keeps_every_value()
    {
        var original = new AppSettings
        {
            LaunchAtStartup = false, StartCollapsed = false, CollapseOnClickOutside = false, CollapseOnEscape = false,
            AlwaysShowNotch = false, Theme = ThemeMode.System, NotchWidth = 240, HorizontalOffset = -120, TopMargin = 20,
            PanelOpacity = 0.8, AnimationsEnabled = false, MascotEnabled = false,
            IdleReactions = IdleReactions.Lively, Density = DensityMode.Compact, HideInFullScreen = false,
            MonitorDeviceName = @"\\.\DISPLAY2", AutoCollapseSeconds = 30, RestoreAfterWake = false, PeekOnHover = false,
            RemindersEnabled = false, ToggleHotkey = "Ctrl+Shift+Y", CompletedFadeOut = false, DailyBackupEnabled = false,
            DefaultListId = 7, PillCountScope = PillCountScope.SelectedList, FirstRunCompleted = true,
            LastBackupAt = T0, CurrentFilter = TaskFilter.Completed, SelectedListId = 3,
        };
        var copy = AppSettings.FromDictionary(original.ToDictionary());
        Assert.Equivalent(original, copy);
        Assert.Equal(T0, copy.LastBackupAt);
    }

    [Theory]
    [InlineData("NotchWidth", "50", 160)]
    [InlineData("NotchWidth", "9999", 280)]
    [InlineData("HorizontalOffset", "-9999", -400)]
    [InlineData("HorizontalOffset", "9999", 400)]
    [InlineData("TopMargin", "-5", 0)]
    [InlineData("TopMargin", "500", 40)]
    [InlineData("AutoCollapseSeconds", "-1", 0)]
    public void Numbers_are_clamped_to_their_range(string key, string value, int expected)
    {
        var s = From((key, value));
        var actual = key switch
        {
            "NotchWidth" => s.NotchWidth,
            "HorizontalOffset" => s.HorizontalOffset,
            "TopMargin" => s.TopMargin,
            _ => s.AutoCollapseSeconds,
        };
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("0.1", 0.70)]
    [InlineData("5", 1.00)]
    [InlineData("0.85", 0.85)]
    public void Opacity_is_clamped(string value, double expected)
    {
        Assert.Equal(expected, From(("PanelOpacity", value)).PanelOpacity);
    }

    [Fact]
    public void Garbage_values_fall_back_to_defaults()
    {
        var s = From(("LaunchAtStartup", "maybe"), ("Theme", "Neon"), ("NotchWidth", "wide"), ("PanelOpacity", "NaN"),
            ("Density", "99"), ("DefaultListId", "-4"), ("LastBackupAt", "yesterday"), ("ToggleHotkey", "   "));
        Assert.True(s.LaunchAtStartup);
        Assert.Equal(ThemeMode.Dark, s.Theme);
        Assert.Equal(200, s.NotchWidth);
        Assert.Equal(0.94, s.PanelOpacity);
        Assert.Equal(DensityMode.Comfortable, s.Density);
        Assert.Equal(0, s.DefaultListId);
        Assert.Null(s.LastBackupAt);
        Assert.Equal("Ctrl+Alt+T", s.ToggleHotkey);
    }

    [Theory]
    [InlineData("off", IdleReactions.Off)]
    [InlineData("Rare", IdleReactions.Rare)]
    [InlineData("LIVELY", IdleReactions.Lively)]
    [InlineData("often", IdleReactions.Normal)]
    [InlineData("7", IdleReactions.Normal)]
    [InlineData("", IdleReactions.Normal)]
    public void IdleReactions_parses_names_and_falls_back_to_Normal(string value, IdleReactions expected)
    {
        Assert.Equal(expected, From(("IdleReactions", value)).IdleReactions);
    }

    [Fact]
    public void MascotEnabled_garbage_keeps_the_default_and_false_is_kept()
    {
        Assert.True(From(("MascotEnabled", "yes")).MascotEnabled);
        Assert.False(From(("MascotEnabled", "false")).MascotEnabled);
    }

    [Fact]
    public void Enum_names_are_case_insensitive()
    {
        Assert.Equal(ThemeMode.Light, From(("Theme", "light")).Theme);
    }

    [Fact]
    public void Dictionary_uses_invariant_formatting()
    {
        var d = new AppSettings { PanelOpacity = 0.5 }.ToDictionary();
        Assert.Equal("0.5", d["PanelOpacity"]);
        Assert.Equal("true", d["LaunchAtStartup"]);
    }

    [Fact]
    public void Starter_palette_has_eight_valid_swatches()
    {
        Assert.Equal(8, TodoList.Palette.Count);
        Assert.All(TodoList.Palette, c => Assert.Matches("^#[0-9A-F]{6}$", c));
    }
}
