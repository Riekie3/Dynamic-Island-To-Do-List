using Microsoft.Data.Sqlite;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;
using TaskIsland.Data;

namespace TaskIsland.Data.Tests;

public class BackupServiceTests
{
    private const long P = TestDb.PersonalId;

    private static async Task<long> CountTasksIn(string dbFile)
    {
        var cs = new SqliteConnectionStringBuilder { DataSource = dbFile, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        await using var conn = new SqliteConnection(cs);
        await conn.OpenAsync();
        return await conn.ScalarAsync<long>(null, "SELECT COUNT(*) FROM Tasks");
    }

    [Fact]
    public async Task Backup_creates_a_valid_copy_with_the_same_data()
    {
        using var t = await TestDb.CreateAsync();
        await t.Tasks.AddAsync(P, "one", Priority.High);
        await t.Tasks.AddAsync(P, "two", Priority.Low);

        var path = await t.Backup.BackupAsync();

        Assert.True(File.Exists(path));
        await BackupService.ValidateBackupAsync(path);
        Assert.Equal(2, await CountTasksIn(path));
    }

    [Fact]
    public async Task Backup_goes_to_the_backups_folder_with_a_timestamped_name()
    {
        using var t = await TestDb.CreateAsync();
        var path = await t.Backup.BackupAsync();
        Assert.Equal(t.Paths.BackupsDir, Path.GetDirectoryName(path));
        Assert.Matches(@"^taskisland-\d{8}-\d{6}\.db$", Path.GetFileName(path));
    }

    [Fact]
    public async Task Two_backups_in_the_same_second_get_different_files()
    {
        using var t = await TestDb.CreateAsync();
        var a = await t.Backup.BackupAsync();
        var b = await t.Backup.BackupAsync();
        Assert.NotEqual(a, b);
        Assert.True(File.Exists(a) && File.Exists(b));
    }

    [Fact]
    public async Task Backup_to_a_chosen_path_creates_the_folder()
    {
        using var t = await TestDb.CreateAsync();
        var dest = Path.Combine(t.Root, "somewhere", "else", "my-backup.db");
        var path = await t.Backup.BackupAsync(dest);
        Assert.Equal(dest, path);
        await BackupService.ValidateBackupAsync(dest);
    }

    [Fact]
    public async Task Backup_works_while_the_app_has_other_connections_open()
    {
        using var t = await TestDb.CreateAsync();
        await t.Tasks.AddAsync(P, "x", Priority.High);
        await using var busy = await t.Db.OpenAsync();
        var path = await t.Backup.BackupAsync();
        Assert.Equal(1, await CountTasksIn(path));
    }

    // ---- validate ----

    [Fact]
    public async Task Validate_rejects_a_missing_file()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => BackupService.ValidateBackupAsync(Path.Combine(Path.GetTempPath(), "nope-" + Guid.NewGuid() + ".db")));
    }

    [Fact]
    public async Task Validate_rejects_a_file_that_is_not_a_database()
    {
        using var t = await TestDb.CreateAsync();
        var junk = Path.Combine(t.Root, "junk.db");
        await File.WriteAllTextAsync(junk, "this is not sqlite, just some text that is long enough to look like a file");
        await Assert.ThrowsAsync<InvalidDataException>(() => BackupService.ValidateBackupAsync(junk));
    }

    [Fact]
    public async Task Validate_rejects_a_database_from_another_program()
    {
        using var t = await TestDb.CreateAsync();
        var other = Path.Combine(t.Root, "other.db");
        await using (var conn = new SqliteConnection($"Data Source={other};Pooling=False"))
        {
            await conn.OpenAsync();
            await Database.ExecAsync(conn, "CREATE TABLE Photos (Id INTEGER); PRAGMA user_version = 1;");
        }
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => BackupService.ValidateBackupAsync(other));
        Assert.Contains("tables are missing", ex.Message);
    }

    [Fact]
    public async Task Validate_rejects_a_backup_from_a_newer_version()
    {
        using var t = await TestDb.CreateAsync();
        var path = await t.Backup.BackupAsync();
        await using (var conn = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            await conn.OpenAsync();
            await Database.ExecAsync(conn, "PRAGMA user_version = 50;");
        }
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => BackupService.ValidateBackupAsync(path));
        Assert.Contains("newer version", ex.Message);
    }

    // ---- restore ----

    [Fact]
    public async Task Restore_replaces_the_data_and_keeps_a_pre_restore_copy()
    {
        using var t = await TestDb.CreateAsync();
        await t.Tasks.AddAsync(P, "before backup", Priority.High);
        var backup = await t.Backup.BackupAsync(Path.Combine(t.Root, "snap.db"));

        await t.Tasks.AddAsync(P, "after backup 1", Priority.High);
        await t.Tasks.AddAsync(P, "after backup 2", Priority.High);

        var safety = await t.Backup.RestoreAsync(backup);

        Assert.Equal(["before backup"], (await t.Tasks.GetAllAsync()).Select(x => x.Title).ToArray());
        Assert.StartsWith(BackupService.PreRestorePrefix, Path.GetFileName(safety));
        Assert.Equal(t.Paths.BackupsDir, Path.GetDirectoryName(safety));
        Assert.Equal(3, await CountTasksIn(safety));   // the safety copy still has the newer tasks
    }

    [Fact]
    public async Task Restore_brings_back_deleted_data_and_settings()
    {
        using var t = await TestDb.CreateAsync();
        await t.Tasks.AddAsync(P, "precious", Priority.High);
        await t.Settings.SetAsync(AppSettings.Keys.Theme, "Light");
        var backup = await t.Backup.BackupAsync(Path.Combine(t.Root, "snap.db"));

        await t.Tasks.SoftDeleteAsync((await t.Tasks.GetAllAsync()).Single().Id);
        await t.Tasks.PurgeDeletedAsync();
        await t.Settings.SetAsync(AppSettings.Keys.Theme, "Dark");
        Assert.Empty(await t.Tasks.GetAllAsync());

        await t.Backup.RestoreAsync(backup);

        Assert.Equal("precious", (await t.Tasks.GetAllAsync()).Single().Title);
        Assert.Equal(ThemeMode.Light, (await t.Settings.LoadAsync()).Theme);
    }

    [Fact]
    public async Task Restore_of_an_invalid_file_leaves_the_current_data_untouched()
    {
        using var t = await TestDb.CreateAsync();
        await t.Tasks.AddAsync(P, "safe", Priority.High);
        var junk = Path.Combine(t.Root, "junk.db");
        await File.WriteAllTextAsync(junk, "not a database at all, not even close, but long enough");

        await Assert.ThrowsAsync<InvalidDataException>(() => t.Backup.RestoreAsync(junk));

        Assert.Equal("safe", (await t.Tasks.GetAllAsync()).Single().Title);
        Assert.Empty(Directory.GetFiles(t.Paths.BackupsDir, "pre-restore-*"));
    }

    [Fact]
    public async Task Start_up_restore_keeps_the_broken_file_and_opens_the_backup()
    {
        using var t = await TestDb.CreateAsync();
        await t.Tasks.AddAsync(P, "saved", Priority.High);
        var backup = await t.Backup.BackupAsync(Path.Combine(t.Root, "good.db"));
        SqliteConnection.ClearAllPools();
        var garbage = "this file was damaged and is not a database any more";
        await File.WriteAllTextAsync(t.Paths.DatabasePath, garbage);
        await Assert.ThrowsAnyAsync<Exception>(() => t.Db.InitializeAsync());

        var kept = await t.Backup.RestoreBrokenAsync(backup);

        Assert.NotNull(kept);
        Assert.StartsWith(BackupService.BrokenPrefix, Path.GetFileName(kept));
        Assert.Equal(garbage, await File.ReadAllTextAsync(kept!)); // never deleted
        Assert.Equal("saved", (await t.Tasks.GetAllAsync()).Single().Title);
        Assert.Equal("wal", await t.ScalarAsync<string>("PRAGMA journal_mode"));
    }

    [Fact]
    public async Task Start_up_restore_refuses_an_invalid_backup_and_changes_nothing()
    {
        using var t = await TestDb.CreateAsync();
        SqliteConnection.ClearAllPools();
        await File.WriteAllTextAsync(t.Paths.DatabasePath, "broken but long enough to look like something");
        var junk = Path.Combine(t.Root, "junk.db");
        await File.WriteAllTextAsync(junk, "not a database at all, not even close, but long enough");

        await Assert.ThrowsAsync<InvalidDataException>(() => t.Backup.RestoreBrokenAsync(junk));

        Assert.Equal("broken but long enough to look like something", await File.ReadAllTextAsync(t.Paths.DatabasePath));
        Assert.Empty(Directory.GetFiles(t.Paths.BackupsDir, BackupService.BrokenPrefix + "*"));
    }

    [Fact]
    public async Task The_app_keeps_working_after_a_restore()
    {
        using var t = await TestDb.CreateAsync();
        var backup = await t.Backup.BackupAsync(Path.Combine(t.Root, "snap.db"));
        await t.Backup.RestoreAsync(backup);

        await t.Tasks.AddAsync(P, "new after restore", Priority.Medium);
        Assert.Single(await t.Tasks.GetAllAsync());
        Assert.Equal("wal", await t.ScalarAsync<string>("PRAGMA journal_mode"));
    }

    // ---- prune + daily backup ----

    private static void MakeFakeBackups(string dir, string prefix, int count)
    {
        for (int i = 0; i < count; i++)
            File.WriteAllText(Path.Combine(dir, $"{prefix}20260101-1200{i:00}.db"), "x");
    }

    [Fact]
    public async Task Prune_keeps_the_newest_three_automatic_backups()
    {
        using var t = await TestDb.CreateAsync();
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.AutoPrefix, 10);

        Assert.Equal(7, t.Backup.PruneAutoBackups());

        var left = Directory.GetFiles(t.Paths.BackupsDir, "taskisland-*.db").Select(Path.GetFileName).OrderBy(x => x).ToArray();
        Assert.Equal(3, left.Length);
        Assert.Equal("taskisland-20260101-120007.db", left[0]);   // the 7 oldest (00-06) are gone
    }

    [Fact]
    public async Task Prune_never_touches_manual_or_pre_restore_backups()
    {
        using var t = await TestDb.CreateAsync();
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.AutoPrefix, 9);
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.ManualPrefix, 9);
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.PreRestorePrefix, 9);

        t.Backup.PruneAutoBackups();

        Assert.Equal(3, Directory.GetFiles(t.Paths.BackupsDir, "taskisland-*").Length);
        Assert.Equal(9, Directory.GetFiles(t.Paths.BackupsDir, "manual-*").Length);
        Assert.Equal(9, Directory.GetFiles(t.Paths.BackupsDir, "pre-restore-*").Length);
    }

    [Fact]
    public async Task Prune_with_few_backups_deletes_nothing()
    {
        using var t = await TestDb.CreateAsync();
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.AutoPrefix, 3);
        Assert.Equal(0, t.Backup.PruneAutoBackups());
    }

    [Fact]
    public async Task Prune_removes_pre_delete_backups_after_24_hours_and_keeps_recent_ones()
    {
        using var t = await TestDb.CreateAsync();
        var old = Path.Combine(t.Paths.BackupsDir, "pre-delete-20261031-080000.db");
        var recent = Path.Combine(t.Paths.BackupsDir, "pre-delete-20261101-070000.db");
        await File.WriteAllTextAsync(old, "old");
        await File.WriteAllTextAsync(recent, "recent");
        File.SetLastWriteTimeUtc(old, t.Clock.UtcNow.AddHours(-25));
        File.SetLastWriteTimeUtc(recent, t.Clock.UtcNow.AddHours(-23));

        Assert.Equal(1, t.Backup.PruneExpiredPreDeleteBackups());

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
    }

    [Fact]
    public async Task Prune_never_touches_other_backup_kinds_however_old()
    {
        using var t = await TestDb.CreateAsync();
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.AutoPrefix, 1);
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.ManualPrefix, 1);
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.PreRestorePrefix, 1);
        MakeFakeBackups(t.Paths.BackupsDir, "pre-import-", 1);
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.BrokenPrefix, 1);
        var old = t.Clock.UtcNow.AddDays(-90);
        foreach (var f in Directory.GetFiles(t.Paths.BackupsDir)) File.SetLastWriteTimeUtc(f, old);

        Assert.Equal(0, t.Backup.PruneExpiredPreDeleteBackups());
        Assert.Equal(5, Directory.GetFiles(t.Paths.BackupsDir).Length);
    }

    [Fact]
    public async Task Daily_check_prunes_expired_pre_delete_backups_even_when_daily_backup_is_off()
    {
        using var t = await TestDb.CreateAsync();
        await t.Settings.SetAsync(AppSettings.Keys.DailyBackupEnabled, "false");
        var old = Path.Combine(t.Paths.BackupsDir, "pre-delete-20261031-080000.db");
        await File.WriteAllTextAsync(old, "old");
        File.SetLastWriteTimeUtc(old, t.Clock.UtcNow.AddHours(-25));

        Assert.Null(await t.Backup.RunDailyBackupIfDueAsync(t.Settings));
        Assert.False(File.Exists(old));
    }

    [Fact]
    public async Task Delete_all_backups_clears_the_backups_folder_but_not_the_live_database()
    {
        using var t = await TestDb.CreateAsync();
        await t.Tasks.AddAsync(P, "keep me", Priority.High);
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.AutoPrefix, 2);
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.ManualPrefix, 2);
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.PreDeletePrefix, 2);
        MakeFakeBackups(t.Paths.BackupsDir, "pre-import-", 2);
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.PreRestorePrefix, 2);
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.BrokenPrefix, 2);

        Assert.Equal(12, t.Backup.DeleteAllBackups());

        Assert.Empty(Directory.GetFiles(t.Paths.BackupsDir));
        Assert.Single(await t.Tasks.GetAllAsync());
        Assert.True(File.Exists(t.Paths.DatabasePath));
    }

    [Fact]
    public async Task Daily_backup_runs_the_first_time_and_records_when()
    {
        using var t = await TestDb.CreateAsync();
        var path = await t.Backup.RunDailyBackupIfDueAsync(t.Settings);
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.Equal(t.Clock.UtcNow, (await t.Settings.LoadAsync()).LastBackupAt);
    }

    [Fact]
    public async Task Daily_backup_waits_a_full_day()
    {
        using var t = await TestDb.CreateAsync();
        await t.Backup.RunDailyBackupIfDueAsync(t.Settings);

        t.Clock.Advance(TimeSpan.FromHours(23));
        Assert.Null(await t.Backup.RunDailyBackupIfDueAsync(t.Settings));

        t.Clock.Advance(TimeSpan.FromHours(2));
        Assert.NotNull(await t.Backup.RunDailyBackupIfDueAsync(t.Settings));
    }

    [Fact]
    public async Task Daily_backup_does_nothing_when_switched_off()
    {
        using var t = await TestDb.CreateAsync();
        await t.Settings.SetAsync(AppSettings.Keys.DailyBackupEnabled, "false");
        Assert.Null(await t.Backup.RunDailyBackupIfDueAsync(t.Settings));
        Assert.Empty(Directory.GetFiles(t.Paths.BackupsDir));
    }

    [Fact]
    public async Task Daily_backup_prunes_old_automatic_backups()
    {
        using var t = await TestDb.CreateAsync();
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.AutoPrefix, 12);
        await t.Backup.RunDailyBackupIfDueAsync(t.Settings);
        Assert.Equal(BackupService.KeepAutoBackups, Directory.GetFiles(t.Paths.BackupsDir, "taskisland-*.db").Length);
    }

    [Fact]
    public async Task Daily_check_shrinks_extra_automatic_backups_when_daily_backup_is_off()
    {
        using var t = await TestDb.CreateAsync();
        await t.Settings.SetAsync(AppSettings.Keys.DailyBackupEnabled, "false");
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.AutoPrefix, 8);

        Assert.Null(await t.Backup.RunDailyBackupIfDueAsync(t.Settings));

        Assert.Equal(BackupService.KeepAutoBackups, Directory.GetFiles(t.Paths.BackupsDir, "taskisland-*.db").Length);
    }

    [Fact]
    public async Task Daily_check_shrinks_extra_automatic_backups_when_no_backup_is_due()
    {
        using var t = await TestDb.CreateAsync();
        await t.Settings.SetAsync(AppSettings.Keys.LastBackupAt, IsoDate.ToUtcString(t.Clock.UtcNow));
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.AutoPrefix, 8);

        Assert.Null(await t.Backup.RunDailyBackupIfDueAsync(t.Settings));

        Assert.Equal(BackupService.KeepAutoBackups, Directory.GetFiles(t.Paths.BackupsDir, "taskisland-*.db").Length);
    }

    [Fact]
    public async Task A_locked_old_backup_does_not_stop_the_daily_backup()
    {
        using var t = await TestDb.CreateAsync();
        MakeFakeBackups(t.Paths.BackupsDir, BackupService.AutoPrefix, 6);
        var oldest = Directory.GetFiles(t.Paths.BackupsDir, "taskisland-*.db").OrderBy(f => f, StringComparer.Ordinal).First();

        string? path;
        using (new FileStream(oldest, FileMode.Open, FileAccess.Read, FileShare.None))   // e.g. an antivirus scan holds it
        {
            path = await t.Backup.RunDailyBackupIfDueAsync(t.Settings);
        }

        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.Equal(t.Clock.UtcNow, (await t.Settings.LoadAsync()).LastBackupAt);
    }

    // ---- safety guards (audit H-1) ----

    [Fact]
    public async Task Backup_onto_the_live_database_is_refused_and_the_data_survives()
    {
        using var t = await TestDb.CreateAsync();
        await t.Tasks.AddAsync(P, "keep me", Priority.High);
        await Assert.ThrowsAsync<InvalidDataException>(() => t.Backup.BackupAsync(t.Paths.DatabasePath));
        await Assert.ThrowsAsync<InvalidDataException>(() => t.Backup.BackupAsync(t.Paths.DatabasePath.ToUpperInvariant()));
        Assert.Single(await t.Tasks.GetAllAsync());
    }

    [Fact]
    public async Task Backup_over_an_existing_file_replaces_it_and_leaves_no_temp_files()
    {
        using var t = await TestDb.CreateAsync();
        var dest = Path.Combine(t.Root, "mine.db");
        await t.Tasks.AddAsync(P, "one", Priority.High);
        await t.Backup.BackupAsync(dest);
        await t.Tasks.AddAsync(P, "two", Priority.High);
        await t.Backup.BackupAsync(dest);
        Assert.Equal(2, await CountTasksIn(dest));
        Assert.Empty(Directory.GetFiles(t.Root, "mine.db.tmp-*"));
    }

    [Fact]
    public async Task A_failed_backup_keeps_the_old_file_and_cleans_up()
    {
        using var t = await TestDb.CreateAsync();
        var dest = Path.Combine(t.Root, "locked.db");
        await t.Tasks.AddAsync(P, "one", Priority.High);
        await t.Backup.BackupAsync(dest);
        using (new FileStream(dest, FileMode.Open, FileAccess.Read, FileShare.Read))   // holds the old backup open
        {
            await t.Tasks.AddAsync(P, "two", Priority.High);
            await Assert.ThrowsAnyAsync<Exception>(() => t.Backup.BackupAsync(dest));
        }
        Assert.Equal(1, await CountTasksIn(dest));
        Assert.Empty(Directory.GetFiles(t.Root, "locked.db.tmp-*"));
    }

    [Fact]
    public async Task Restore_from_the_live_database_is_refused_and_nothing_changes()
    {
        using var t = await TestDb.CreateAsync();
        await t.Tasks.AddAsync(P, "keep me", Priority.High);
        await Assert.ThrowsAsync<InvalidDataException>(() => t.Backup.RestoreAsync(t.Paths.DatabasePath));
        Assert.Single(await t.Tasks.GetAllAsync());
        Assert.Empty(Directory.GetFiles(t.Paths.BackupsDir, "pre-restore-*"));
    }

    [Fact]
    public async Task Broken_database_recovery_from_the_live_file_is_refused_and_the_file_stays()
    {
        using var t = await TestDb.CreateAsync();
        await t.Tasks.AddAsync(P, "keep me", Priority.High);
        await Assert.ThrowsAsync<InvalidDataException>(() => t.Backup.RestoreBrokenAsync(t.Paths.DatabasePath));
        Assert.True(File.Exists(t.Paths.DatabasePath));
        Assert.Empty(Directory.GetFiles(t.Paths.BackupsDir, "broken-*"));
    }

    [Fact]
    public async Task Restore_leaves_no_temp_files_behind()
    {
        using var t = await TestDb.CreateAsync();
        var backup = await t.Backup.BackupAsync();
        await t.Backup.RestoreAsync(backup);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(t.Paths.DatabasePath)!, "*.restore-*"));
    }
}
