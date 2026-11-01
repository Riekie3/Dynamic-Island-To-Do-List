using Microsoft.Data.Sqlite;
using TaskIsland.Core.Models;

namespace TaskIsland.Data.Tests;

public class SettingsStoreTests
{
    [Fact]
    public async Task A_missing_key_reads_as_null()
    {
        using var t = await TestDb.CreateAsync();
        Assert.Null(await t.Settings.GetAsync("Nope"));
    }

    [Fact]
    public async Task Set_then_get_and_overwrite()
    {
        using var t = await TestDb.CreateAsync();
        await t.Settings.SetAsync("Theme", "Light");
        Assert.Equal("Light", await t.Settings.GetAsync("Theme"));
        await t.Settings.SetAsync("Theme", "Dark");
        Assert.Equal("Dark", await t.Settings.GetAsync("Theme"));
        Assert.Equal(1L, await t.ScalarAsync<long>("SELECT COUNT(*) FROM Settings WHERE SettingKey = 'Theme'"));
    }

    [Fact]
    public async Task Load_returns_defaults_for_everything_not_stored()
    {
        using var t = await TestDb.CreateAsync();
        var s = await t.Settings.LoadAsync();
        Assert.Equal(ThemeMode.Dark, s.Theme);
        Assert.Equal(200, s.NotchWidth);
        Assert.True(s.LaunchAtStartup);
        Assert.Equal(TestDb.PersonalId, s.DefaultListId);   // the only value migration 1 stores
    }

    [Fact]
    public async Task Save_then_load_round_trips_every_setting()
    {
        using var t = await TestDb.CreateAsync();
        var s = new AppSettings
        {
            Theme = ThemeMode.Light, NotchWidth = 260, HorizontalOffset = 55, TopMargin = 12, PanelOpacity = 0.77,
            AnimationsEnabled = false, Density = DensityMode.Compact, LaunchAtStartup = false, MonitorDeviceName = @"\\.\DISPLAY3",
            AutoCollapseSeconds = 45, ToggleHotkey = "Ctrl+Shift+K", DefaultListId = TestDb.OfficeId,
            PillCountScope = PillCountScope.SelectedList, FirstRunCompleted = true, CurrentFilter = TaskFilter.All,
            SelectedListId = TestDb.OfficeId, LastBackupAt = t.Clock.UtcNow,
        };
        await t.Settings.SaveAsync(s);
        Assert.Equivalent(s, await t.Settings.LoadAsync());
    }

    [Fact]
    public async Task Settings_survive_reopening_the_database()
    {
        using var t = await TestDb.CreateAsync();
        await t.Settings.SetAsync(AppSettings.Keys.Theme, "System");
        SqliteConnection.ClearAllPools();

        var db2 = new Database(t.Paths);
        await db2.InitializeAsync();
        Assert.Equal(ThemeMode.System, (await new SqliteSettingsStore(db2).LoadAsync()).Theme);
    }

    [Fact]
    public async Task Saving_without_a_backup_time_removes_the_stored_one()
    {
        using var t = await TestDb.CreateAsync();
        await t.Settings.SetAsync(AppSettings.Keys.LastBackupAt, "2026-01-01T00:00:00.0000000Z");
        await t.Settings.SaveAsync(new AppSettings { LastBackupAt = null });
        Assert.Null(await t.Settings.GetAsync(AppSettings.Keys.LastBackupAt));
    }

    [Fact]
    public async Task Invalid_stored_values_load_as_safe_defaults()
    {
        using var t = await TestDb.CreateAsync();
        await t.Settings.SetAsync(AppSettings.Keys.NotchWidth, "banana");
        await t.Settings.SetAsync(AppSettings.Keys.Theme, "Neon");
        var s = await t.Settings.LoadAsync();
        Assert.Equal(200, s.NotchWidth);
        Assert.Equal(ThemeMode.Dark, s.Theme);
    }

    [Fact]
    public async Task Get_all_returns_every_stored_pair()
    {
        using var t = await TestDb.CreateAsync();
        await t.Settings.SetAsync("A", "1");
        await t.Settings.SetAsync("B", "2");
        var all = await t.Settings.GetAllAsync();
        Assert.Equal("1", all["A"]);
        Assert.Equal("2", all["B"]);
        Assert.True(all.ContainsKey("DefaultListId"));
    }
}
