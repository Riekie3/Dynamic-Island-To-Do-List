using Microsoft.Data.Sqlite;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;

namespace TaskIsland.Data;

/// <summary>Database backup, restore and daily auto-backup (PLAN.md section 4.5).</summary>
public sealed class BackupService(Database db, IClock clock)
{
    public const string AutoPrefix = "taskisland-";
    public const string ManualPrefix = "manual-";
    public const string PreRestorePrefix = "pre-restore-";
    public const int KeepAutoBackups = 7;

    private AppPaths Paths => db.Paths;

    /// <summary>
    /// Writes a consistent copy of the live database (safe while the app is running). Without a destination the file goes
    /// to the Backups folder as "taskisland-yyyyMMdd-HHmmss.db".
    /// </summary>
    public async Task<string> BackupAsync(string? destinationPath = null, string prefix = AutoPrefix)
    {
        Paths.EnsureCreated();
        var dest = destinationPath ?? UniquePath(Paths.BackupsDir, prefix);
        var dir = Path.GetDirectoryName(Path.GetFullPath(dest));
        if (dir is not null) Directory.CreateDirectory(dir);
        if (File.Exists(dest)) File.Delete(dest);

        await using var source = await db.OpenAsync();
        var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dest, Pooling = false }.ToString());
        await using (target)
        {
            await target.OpenAsync();
            source.BackupDatabase(target);
        }
        return dest;
    }

    /// <summary>
    /// Makes an automatic backup when enabled and the last one is older than 24 hours, then keeps only the newest
    /// <see cref="KeepAutoBackups"/> automatic backups. Returns the new file, or null when nothing was due.
    /// </summary>
    public async Task<string?> RunDailyBackupIfDueAsync(ISettingsStore settings)
    {
        var s = await settings.LoadAsync();
        if (!s.DailyBackupEnabled) return null;
        if (s.LastBackupAt is { } last && clock.UtcNow - last < TimeSpan.FromHours(24)) return null;

        var path = await BackupAsync();
        await settings.SetAsync(AppSettings.Keys.LastBackupAt, IsoDate.ToUtcString(clock.UtcNow));
        PruneAutoBackups();
        return path;
    }

    /// <summary>Deletes the oldest automatic backups beyond <paramref name="keep"/>. Manual and pre-restore files are never touched.</summary>
    public int PruneAutoBackups(int keep = KeepAutoBackups)
    {
        if (!Directory.Exists(Paths.BackupsDir)) return 0;
        var old = Directory.GetFiles(Paths.BackupsDir, AutoPrefix + "*.db")
            .OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal)   // names start with a sortable timestamp
            .Skip(keep).ToList();
        foreach (var f in old) File.Delete(f);
        return old.Count;
    }

    /// <summary>Checks that a file is a usable TaskIsland database. Throws <see cref="InvalidDataException"/> with a user-friendly message.</summary>
    public static async Task ValidateBackupAsync(string path)
    {
        if (!File.Exists(path)) throw new InvalidDataException("The file does not exist.");
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
            await using var conn = new SqliteConnection(cs);
            await conn.OpenAsync();

            var integrity = await conn.ScalarAsync<string>(null, "PRAGMA integrity_check");
            if (integrity != "ok") throw new InvalidDataException("The file is damaged (it failed the database integrity check).");

            int version = await Database.GetUserVersionAsync(conn);
            if (version < 1) throw new InvalidDataException("The file is not a TaskIsland database.");
            if (version > Migrations.Latest)
                throw new InvalidDataException("The backup was made by a newer version of TaskIsland. Update TaskIsland first.");

            var tables = await conn.ScalarAsync<long>(null,
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Tasks', 'Lists', 'Settings')");
            if (tables != 3) throw new InvalidDataException("The file is not a TaskIsland database (tables are missing).");
        }
        catch (SqliteException)
        {
            throw new InvalidDataException("The file is not a valid database.");
        }
    }

    /// <summary>
    /// Replaces the live database with the chosen backup. The current data is first saved as "pre-restore-*.db" in the
    /// Backups folder. Returns that safety copy's path.
    /// </summary>
    public async Task<string> RestoreAsync(string backupPath)
    {
        await ValidateBackupAsync(backupPath);
        var safety = await BackupAsync(prefix: PreRestorePrefix);

        SqliteConnection.ClearAllPools();   // release the pooled connections so the file can be replaced
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var side = Paths.DatabasePath + suffix;
            if (File.Exists(side)) File.Delete(side);
        }
        File.Copy(backupPath, Paths.DatabasePath, overwrite: true);
        await db.InitializeAsync();   // WAL mode again + migrations for older backups
        return safety;
    }

    public const string BrokenPrefix = "broken-";

    /// <summary>
    /// Start-up recovery when the live database cannot be opened (PLAN.md section 7.10): the broken file (and its -wal/-shm
    /// side files) is moved to the Backups folder as "broken-*.db" (never deleted), then the chosen backup is copied in and
    /// opened. Returns the path the broken file was kept at, or null when there was no database file.
    /// </summary>
    public async Task<string?> RestoreBrokenAsync(string backupPath)
    {
        await ValidateBackupAsync(backupPath);
        Paths.EnsureCreated();
        SqliteConnection.ClearAllPools();
        string? kept = null;
        if (File.Exists(Paths.DatabasePath))
        {
            kept = UniquePath(Paths.BackupsDir, BrokenPrefix);
            File.Move(Paths.DatabasePath, kept);
            foreach (var suffix in new[] { "-wal", "-shm" })
            {
                var side = Paths.DatabasePath + suffix;
                if (File.Exists(side)) File.Move(side, kept + suffix, overwrite: true);
            }
        }
        File.Copy(backupPath, Paths.DatabasePath, overwrite: true);
        await db.InitializeAsync();
        return kept;
    }

    private string UniquePath(string dir, string prefix)
    {
        var stamp = clock.Now.ToString("yyyyMMdd-HHmmss");
        var path = Path.Combine(dir, $"{prefix}{stamp}.db");
        for (int i = 2; File.Exists(path); i++) path = Path.Combine(dir, $"{prefix}{stamp}-{i}.db");
        return path;
    }
}
