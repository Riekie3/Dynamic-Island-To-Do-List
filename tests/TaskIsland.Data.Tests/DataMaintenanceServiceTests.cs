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

    [Fact]
    public async Task Reset_never_reuses_a_task_id()
    {
        using var t = await TestDb.CreateAsync();
        var old = await t.Tasks.AddAsync(P, "before", Priority.High);

        await new DataMaintenanceService(t.Db, t.Backup).ResetToFirstRunAsync();
        var fresh = await t.Tasks.AddAsync(P, "after", Priority.High);

        Assert.True(fresh.Id > old.Id, "a new task must not get the id of a deleted one (reminders remember ids)");
    }

    [Fact]
    public async Task Reset_leaves_no_old_text_in_the_database_file()
    {
        using var t = await TestDb.CreateAsync();
        await t.Tasks.AddAsync(P, "ZZ-secret-task-title-ZZ", Priority.High);

        await new DataMaintenanceService(t.Db, t.Backup).ResetToFirstRunAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (var file in Directory.GetFiles(t.Paths.Root, "taskisland.db*", SearchOption.AllDirectories))
        {
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var bytes = new byte[fs.Length];
            fs.ReadExactly(bytes);
            Assert.DoesNotContain("ZZ-secret-task-title-ZZ", System.Text.Encoding.UTF8.GetString(bytes));
        }
    }
}
