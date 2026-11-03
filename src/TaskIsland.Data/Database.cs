using Microsoft.Data.Sqlite;

namespace TaskIsland.Data;

/// <summary>
/// Connection factory and schema migration. One short-lived connection per operation (pooled); WAL mode lets the
/// app read while it writes. Foreign keys and the busy timeout are applied to every connection.
/// </summary>
public sealed class Database
{
    private readonly string _connectionString;

    public Database(AppPaths paths)
    {
        Paths = paths;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
        }.ToString();
    }

    public AppPaths Paths { get; }

    public async Task<SqliteConnection> OpenAsync()
    {
        var conn = new SqliteConnection(_connectionString);
        try
        {
            await conn.OpenAsync();
            // cache_size -1024 = 1 MB page cache per connection (PLAN.md section 6.7 rule 9).
            await ExecAsync(conn, "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 3000; PRAGMA cache_size = -1024;");
            return conn;
        }
        catch
        {
            await conn.DisposeAsync();
            throw;
        }
    }

    /// <summary>Creates the folders and database file, switches to WAL and applies any missing migrations.</summary>
    public async Task InitializeAsync()
    {
        Paths.EnsureCreated();
        await using var conn = await OpenAsync();
        await ExecAsync(conn, "PRAGMA journal_mode = WAL;");

        int current = await GetUserVersionAsync(conn);
        if (current > Migrations.Latest)
            throw new InvalidOperationException(
                $"The database was created by a newer version of TaskIsland (schema {current}, this version supports {Migrations.Latest}).");

        foreach (var (version, sql) in Migrations.All.Where(m => m.Version > current).OrderBy(m => m.Version))
        {
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync();
            await ExecAsync(conn, sql, tx);
            await ExecAsync(conn, $"PRAGMA user_version = {version};", tx);
            await tx.CommitAsync();
        }
    }

    public static async Task<int> GetUserVersionAsync(SqliteConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    internal static async Task ExecAsync(SqliteConnection conn, string sql, SqliteTransaction? tx = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}
