using TaskIsland.Core.Models;
using TaskIsland.Core.Services;

namespace TaskIsland.Core.Tests;

/// <summary>The reminder pill width setting, the red border setting and the width rule.</summary>
public class ReminderAlertTests
{
    private static readonly PixelRect FullHd = new(0, 0, 1920, 1080);

    [Fact]
    public void Defaults_are_half_the_screen_and_blinking()
    {
        var s = AppSettings.FromDictionary(new Dictionary<string, string>());
        Assert.Equal(50, s.ReminderAlertWidthPercent);
        Assert.True(s.ReminderBlink);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("30", 30)]
    [InlineData("50", 50)]
    [InlineData("75", 75)]
    [InlineData("100", 100)]
    [InlineData("45", 50)]   // not offered: the default
    [InlineData("-10", 50)]
    [InlineData("500", 50)]
    [InlineData("abc", 50)]
    [InlineData("", 50)]
    public void Width_only_accepts_the_offered_values(string text, int expected)
    {
        var s = AppSettings.FromDictionary(new Dictionary<string, string> { [AppSettings.Keys.ReminderAlertWidthPercent] = text });
        Assert.Equal(expected, s.ReminderAlertWidthPercent);
    }

    [Fact]
    public void Settings_survive_a_round_trip()
    {
        var s = new AppSettings { ReminderAlertWidthPercent = 75, ReminderBlink = false };
        var back = AppSettings.FromDictionary(s.ToDictionary());
        Assert.Equal(75, back.ReminderAlertWidthPercent);
        Assert.False(back.ReminderBlink);
    }

    [Fact]
    public void Half_of_a_full_hd_screen()
    {
        Assert.Equal(960, IslandPlacement.AlertWidthDip(FullHd, 1, 50, 0, 420, 320), 3);
    }

    [Fact]
    public void Scaled_screen_is_measured_in_dips()
    {
        // 2560 px at 150 % = 1706.67 DIPs wide; half of that.
        Assert.Equal(2560 / 1.5 / 2, IslandPlacement.AlertWidthDip(new PixelRect(0, 0, 2560, 1440), 1.5, 50, 0, 420, 320), 3);
    }

    [Fact]
    public void Compact_returns_the_minimum()
    {
        Assert.Equal(320, IslandPlacement.AlertWidthDip(FullHd, 1, 0, 0, 420, 320));
    }

    [Fact]
    public void Full_width_keeps_an_edge_gap()
    {
        Assert.Equal(1920 - 16, IslandPlacement.AlertWidthDip(FullHd, 1, 100, 0, 420, 320), 3);
    }

    [Fact]
    public void An_offset_island_stays_on_the_screen()
    {
        // Centre 300 DIPs right of the middle: 660 DIPs to the right edge, so at most 2 * 660 - 16 wide.
        Assert.Equal(1304, IslandPlacement.AlertWidthDip(FullHd, 1, 75, 300, 420, 320), 3);
    }

    [Fact]
    public void Never_narrower_than_the_minimum()
    {
        Assert.Equal(320, IslandPlacement.AlertWidthDip(new PixelRect(0, 0, 800, 600), 1, 30, 0, 420, 320));
    }

    [Fact]
    public void Second_monitor_uses_its_own_width()
    {
        Assert.Equal(640, IslandPlacement.AlertWidthDip(new PixelRect(1920, 0, 3200, 1024), 1, 50, 0, 420, 320), 3);
    }
}
