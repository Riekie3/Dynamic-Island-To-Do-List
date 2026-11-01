using Microsoft.Data.Sqlite;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;

namespace TaskIsland.Data;

/// <summary>Small helpers that keep the repositories readable.</summary>
internal static class Sql
{
    public static SqliteCommand Cmd(this SqliteConnection conn, SqliteTransaction? tx, string sql,
        params (string Name, object? Value)[] args)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    public static async Task<int> ExecAsync(this SqliteConnection conn, SqliteTransaction? tx, string sql,
        params (string Name, object? Value)[] args)
    {
        await using var cmd = conn.Cmd(tx, sql, args);
        return await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<T?> ScalarAsync<T>(this SqliteConnection conn, SqliteTransaction? tx, string sql,
        params (string Name, object? Value)[] args)
    {
        await using var cmd = conn.Cmd(tx, sql, args);
        var v = await cmd.ExecuteScalarAsync();
        if (v is null or DBNull) return default;
        return (T)Convert.ChangeType(v, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    public static async Task<SqliteTransaction> BeginAsync(this SqliteConnection conn) =>
        (SqliteTransaction)await conn.BeginTransactionAsync();

    public const string TaskColumns =
        "Id, ListId, Title, Description, Priority, IsCompleted, CreatedAt, UpdatedAt, CompletedAt, DueDate, DueHasTime, ReminderShownAt, SortOrder, DeletedAt";

    public static TaskItem ReadTask(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        ListId = r.GetInt64(1),
        Title = r.GetString(2),
        Description = r.IsDBNull(3) ? null : r.GetString(3),
        Priority = (Priority)r.GetInt32(4),
        IsCompleted = r.GetInt32(5) != 0,
        CreatedAt = Utc(r.GetString(6)),
        UpdatedAt = Utc(r.GetString(7)),
        CompletedAt = r.IsDBNull(8) ? null : Utc(r.GetString(8)),
        DueDate = r.IsDBNull(9) ? null : IsoDate.TryParseLocal(r.GetString(9), out var due) ? due : null,
        DueHasTime = r.GetInt32(10) != 0,
        ReminderShownAt = r.IsDBNull(11) ? null : Utc(r.GetString(11)),
        SortOrder = r.GetInt32(12),
        DeletedAt = r.IsDBNull(13) ? null : Utc(r.GetString(13)),
    };

    public static TodoList ReadList(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        Name = r.GetString(1),
        ColorHex = r.GetString(2),
        SortOrder = r.GetInt32(3),
        CreatedAt = Utc(r.GetString(4)),
        UpdatedAt = Utc(r.GetString(5)),
    };

    public const string ListColumns = "Id, Name, ColorHex, SortOrder, CreatedAt, UpdatedAt";

    private static DateTime Utc(string text) =>
        IsoDate.TryParseUtc(text, out var v) ? v : DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

    public static string? Text(DateTime? utc) => utc is { } v ? IsoDate.ToUtcString(v) : null;
}
