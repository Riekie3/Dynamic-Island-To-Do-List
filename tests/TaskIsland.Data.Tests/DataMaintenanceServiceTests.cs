using TaskIsland.Core.Models;
using TaskIsland.Data;

namespace TaskIsland.Data.Tests;

public class DataMaintenanceServiceTests
{
    private const long P = TestDb.PersonalId;

    [Fact]
    public async Task Reset_restores_starter_lists_default_settings_and_deletes_backups()
    {
        using var t = await TestDb.CreateAsync();
        await t.Tasks.AddAsync(P, "gone", Priority.High);
        await t.Lists.CreateAsync("Temporary", "#123456");
        await t.Settings.SetAsync(AppSettings.Keys.Theme, "Light");
        await t.Settings.SetAsync(AppSettings.Keys.LaunchAtStartup, "false");
        await t.Backup.BackupAsync();

        int removed = await new DataMaintenanceService(t.Db, t.Backup).ResetToFirstRunAsync();

        Assert.Equal(1, removed);
        Assert.Empty(await t.Tasks.GetAllAsync());
        var lists = await t.Lists.GetAllAsync();
        Assert.Equal(["Personal", "Office Work"], lists.Select(l => l.Name));
        Assert.Equal([TestDb.PersonalId, TestDb.OfficeId], lists.Select(l => l.Id));

        var settings = await t.Settings.LoadAsync();
        Assert.Equal(new AppSettings().Theme, settings.Theme);
        Assert.True(settings.LaunchAtStartup);
        Assert.Equal(TestDb.PersonalId, settings.DefaultListId);
        Assert.Null(settings.LastBackupAt);
        Assert.False(settings.FirstRunCompleted);
        Assert.Empty(Directory.GetFiles(t.Paths.BackupsDir));

        await t.Tasks.AddAsync(P, "fresh start", Priority.High);
        Assert.Equal("fresh start", (await t.Tasks.GetAllAsync()).Single().Title);
    }
}
