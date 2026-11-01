using Microsoft.Data.Sqlite;
using TaskIsland.Core.Models;
using TaskIsland.Data;

namespace TaskIsland.Data.Tests;

public class MigrationTests
{
    [Fact]
    public async Task Fresh_database_is_at_schema_version_1()
    {
        using var t = await TestDb.CreateAsync();
        Assert.Equal(1, await t.ScalarAsync<int>("PRAGMA user_version"));
        Assert.Equal(1, Migrations.Latest);
    }

    [Fact]
    public async Task Fresh_database_has_the_two_starter_lists()
    {
        using var t = await TestDb.CreateAsync();
        var lists = await t.Lists.GetAllAsync();
        Assert.Equal(["Personal", "Office Work"], lists.Select(l => l.Name).ToArray());
        Assert.Equal(["#1E90FF", "#A970FF"], lists.Select(l => l.ColorHex).ToArray());
        Assert.Equal([0, 1], lists.Select(l => l.SortOrder).ToArray());
        Assert.Equal(TestDb.PersonalId, lists[0].Id);
    }

    [Fact]
    public async Task Fresh_database_has_no_sample_tasks()
    {
        using var t = await TestDb.CreateAsync();
        Assert.Empty(await t.Tasks.GetAllAsync(includeDeleted: true));
    }

    [Fact]
    public async Task Default_list_setting_points_at_personal()
    {
        using var t = await TestDb.CreateAsync();
        var s = await t.Settings.LoadAsync();
        Assert.Equal(TestDb.PersonalId, s.DefaultListId);
    }

    [Fact]
    public async Task Database_uses_wal_journal_mode()
    {
        using var t = await TestDb.CreateAsync();
        Assert.Equal("wal", await t.ScalarAsync<string>("PRAGMA journal_mode"));
    }

    [Fact]
    public async Task Every_connection_enforces_foreign_keys()
    {
        using var t = await TestDb.CreateAsync();
        Assert.Equal(1, await t.ScalarAsync<int>("PRAGMA foreign_keys"));
    }

    [Fact]
    public async Task Initialising_twice_keeps_the_data_and_adds_nothing()
    {
        using var t = await TestDb.CreateAsync();
        await t.Tasks.AddAsync(TestDb.PersonalId, "Keep me", Priority.High);
        await t.Db.InitializeAsync();
        Assert.Equal(2, (await t.Lists.GetAllAsync()).Count);
        Assert.Single(await t.Tasks.GetAllAsync());
    }

    [Fact]
    public async Task Creates_data_backups_and_logs_folders()
    {
        using var t = await TestDb.CreateAsync();
        Assert.True(Directory.Exists(t.Paths.DataDir));
        Assert.True(Directory.Exists(t.Paths.BackupsDir));
        Assert.True(Directory.Exists(t.Paths.LogsDir));
        Assert.True(File.Exists(t.Paths.DatabasePath));
        Assert.EndsWith(Path.Combine("Data", "taskisland.db"), t.Paths.DatabasePath);
    }

    [Fact]
    public async Task Default_paths_live_under_local_app_data()
    {
        var p = new AppPaths();
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.Equal(Path.Combine(local, "TaskIsland", "Data", "taskisland.db"), p.DatabasePath);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task A_database_from_a_newer_version_is_refused()
    {
        using var t = await TestDb.CreateAsync();
        await using (var conn = await t.Db.OpenAsync())
            await Database.ExecAsync(conn, "PRAGMA user_version = 99;");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => t.Db.InitializeAsync());
        Assert.Contains("newer version", ex.Message);
    }

    [Fact]
    public async Task Schema_rejects_bad_values()
    {
        using var t = await TestDb.CreateAsync();
        await using var conn = await t.Db.OpenAsync();
        await Assert.ThrowsAsync<SqliteException>(() => Database.ExecAsync(conn,
            "INSERT INTO Tasks (ListId, Title, Priority, CreatedAt, UpdatedAt) VALUES (1, 'x', 7, 'a', 'a')"));
        await Assert.ThrowsAsync<SqliteException>(() => Database.ExecAsync(conn,
            "INSERT INTO Tasks (ListId, Title, CreatedAt, UpdatedAt) VALUES (999, 'x', 'a', 'a')"));
    }
}
