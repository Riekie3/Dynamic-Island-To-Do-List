using Microsoft.Data.Sqlite;
using TaskIsland.Core.Services;
using TaskIsland.Data;

// Every test uses its own temporary data folder; running them one at a time keeps SQLite file cleanup reliable.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TaskIsland.Data.Tests;

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
    public DateTime Now => UtcNow.ToLocalTime();
    public void Advance(TimeSpan by) => UtcNow += by;
}

/// <summary>A real SQLite database in a throw-away folder, wired up exactly like the app does it.</summary>
public sealed class TestDb : IDisposable
{
    public const long PersonalId = 1;
    public const long OfficeId = 2;

    private TestDb(string root)
    {
        Root = root;
        Paths = new AppPaths(root);
        Clock = new FakeClock();
        Db = new Database(Paths);
        Tasks = new SqliteTaskRepository(Db, Clock);
        Lists = new SqliteTodoListRepository(Db, Clock);
        Settings = new SqliteSettingsStore(Db);
        Backup = new BackupService(Db, Clock);
        Import = new ImportService(Tasks, Lists, Settings);
    }

    public string Root { get; }
    public AppPaths Paths { get; }
    public FakeClock Clock { get; }
    public Database Db { get; }
    public SqliteTaskRepository Tasks { get; }
    public SqliteTodoListRepository Lists { get; }
    public SqliteSettingsStore Settings { get; }
    public BackupService Backup { get; }
    public ImportService Import { get; }

    public static async Task<TestDb> CreateAsync()
    {
        var t = new TestDb(Path.Combine(Path.GetTempPath(), "TaskIslandTests-" + Guid.NewGuid().ToString("N")));
        await t.Db.InitializeAsync();
        return t;
    }

    public async Task<T?> ScalarAsync<T>(string sql, params (string, object?)[] args)
    {
        await using var conn = await Db.OpenAsync();
        return await conn.ScalarAsync<T>(null, sql, args);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        for (int i = 0; i < 5; i++)
        {
            try { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); return; }
            catch (IOException) { Thread.Sleep(50); }
            catch (UnauthorizedAccessException) { Thread.Sleep(50); }
        }
    }
}
