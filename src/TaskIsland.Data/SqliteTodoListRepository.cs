using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;

namespace TaskIsland.Data;

public sealed partial class SqliteTodoListRepository(Database db, IClock clock) : ITodoListRepository
{
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColorRegex();

    public async Task<IReadOnlyList<TodoList>> GetAllAsync()
    {
        await using var conn = await db.OpenAsync();
        return await ReadAllAsync(conn, null);
    }

    public async Task<TodoList> CreateAsync(string name, string colorHex)
    {
        name = ValidateName(name);
        colorHex = ValidateColor(colorHex);
        var now = IsoDate.ToUtcString(clock.UtcNow);

        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginAsync();
        if (await conn.ScalarAsync<long>(tx, "SELECT COUNT(*) FROM Lists") >= TodoList.MaxLists)
            throw new InvalidOperationException($"You can have at most {TodoList.MaxLists} lists.");
        await EnsureNameFreeAsync(conn, tx, name, exceptId: null);

        long order = await conn.ScalarAsync<long?>(tx, "SELECT MAX(SortOrder) FROM Lists") + 1 ?? 0;
        long id = await conn.ScalarAsync<long>(tx, """
            INSERT INTO Lists (Name, ColorHex, SortOrder, CreatedAt, UpdatedAt) VALUES (@n, @c, @o, @now, @now);
            SELECT last_insert_rowid();
            """, ("@n", name), ("@c", colorHex), ("@o", order), ("@now", now));
        await tx.CommitAsync();
        return (await ReadAllAsync(conn, null)).First(l => l.Id == id);
    }

    public async Task RenameAsync(long id, string name)
    {
        name = ValidateName(name);
        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginAsync();
        await EnsureNameFreeAsync(conn, tx, name, exceptId: id);
        int n = await conn.ExecAsync(tx, "UPDATE Lists SET Name = @n, UpdatedAt = @now WHERE Id = @id",
            ("@n", name), ("@now", IsoDate.ToUtcString(clock.UtcNow)), ("@id", id));
        if (n == 0) throw new InvalidOperationException("List not found.");
        await tx.CommitAsync();
    }

    public async Task SetColorAsync(long id, string colorHex)
    {
        colorHex = ValidateColor(colorHex);
        await using var conn = await db.OpenAsync();
        int n = await conn.ExecAsync(null, "UPDATE Lists SET ColorHex = @c, UpdatedAt = @now WHERE Id = @id",
            ("@c", colorHex), ("@now", IsoDate.ToUtcString(clock.UtcNow)), ("@id", id));
        if (n == 0) throw new InvalidOperationException("List not found.");
    }

    public async Task ReorderAsync(IReadOnlyList<long> orderedIds)
    {
        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginAsync();
        var existing = (await ReadAllAsync(conn, tx)).Select(l => l.Id).ToHashSet();
        if (orderedIds.Count != existing.Count || !existing.SetEquals(orderedIds))
            throw new ArgumentException("The new order must contain every list exactly once.", nameof(orderedIds));
        for (int i = 0; i < orderedIds.Count; i++)
            await conn.ExecAsync(tx, "UPDATE Lists SET SortOrder = @i WHERE Id = @id", ("@i", i), ("@id", orderedIds[i]));
        await tx.CommitAsync();
    }

    public async Task DeleteAsync(long id, long? moveTasksToListId)
    {
        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginAsync();

        var all = await ReadAllAsync(conn, tx);
        if (all.All(l => l.Id != id)) throw new InvalidOperationException("List not found.");
        if (all.Count == 1) throw new InvalidOperationException("The last remaining list cannot be deleted.");

        if (moveTasksToListId is { } target)
        {
            if (target == id) throw new ArgumentException("Choose a different list to move the tasks to.", nameof(moveTasksToListId));
            if (all.All(l => l.Id != target)) throw new InvalidOperationException("The target list was not found.");
            await MoveAllTasksAsync(conn, tx, id, target);
        }
        // Without a target the ON DELETE CASCADE foreign key removes the list's tasks in the same transaction.

        await conn.ExecAsync(tx, "DELETE FROM Lists WHERE Id = @id", ("@id", id));

        var defaultId = await conn.ScalarAsync<string>(tx, "SELECT SettingValue FROM Settings WHERE SettingKey = 'DefaultListId'");
        if (defaultId == id.ToString())
        {
            var first = (await ReadAllAsync(conn, tx)).OrderBy(l => l.SortOrder).First();
            await conn.ExecAsync(tx, "UPDATE Settings SET SettingValue = @v WHERE SettingKey = 'DefaultListId'", ("@v", first.Id.ToString()));
        }
        await tx.CommitAsync();
    }

    /// <summary>Appends every task of <paramref name="from"/> to the end of its priority group in <paramref name="to"/>.</summary>
    private static async Task MoveAllTasksAsync(SqliteConnection conn, SqliteTransaction tx, long from, long to)
    {
        var rows = new List<(long Id, int Priority)>();
        await using (var cmd = conn.Cmd(tx, "SELECT Id, Priority FROM Tasks WHERE ListId = @f ORDER BY Priority, SortOrder, Id", ("@f", from)))
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync()) rows.Add((r.GetInt64(0), r.GetInt32(1)));

        var next = new Dictionary<int, long>();
        foreach (var (taskId, prio) in rows)
        {
            if (!next.TryGetValue(prio, out var n))
                n = await conn.ScalarAsync<long?>(tx, "SELECT MAX(SortOrder) FROM Tasks WHERE ListId = @t AND Priority = @p",
                    ("@t", to), ("@p", prio)) + 1 ?? 0;
            await conn.ExecAsync(tx, "UPDATE Tasks SET ListId = @t, SortOrder = @o WHERE Id = @id", ("@t", to), ("@o", n), ("@id", taskId));
            next[prio] = n + 1;
        }
    }

    private static async Task<List<TodoList>> ReadAllAsync(SqliteConnection conn, SqliteTransaction? tx)
    {
        var list = new List<TodoList>();
        await using var cmd = conn.Cmd(tx, $"SELECT {Sql.ListColumns} FROM Lists ORDER BY SortOrder, Id");
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(Sql.ReadList(r));
        return list;
    }

    private static async Task EnsureNameFreeAsync(SqliteConnection conn, SqliteTransaction tx, string name, long? exceptId)
    {
        var clash = await conn.ScalarAsync<long?>(tx,
            "SELECT Id FROM Lists WHERE Name = @n COLLATE NOCASE AND (@x IS NULL OR Id <> @x)", ("@n", name), ("@x", exceptId));
        if (clash is not null) throw new InvalidOperationException($"A list named \"{name}\" already exists.");
    }

    private static string ValidateName(string? name)
    {
        var n = (name ?? "").Trim();
        if (n.Length is < 1 or > TodoList.MaxNameLength)
            throw new ArgumentException($"The list name must be 1 to {TodoList.MaxNameLength} characters.", nameof(name));
        return n;
    }

    private static string ValidateColor(string? color)
    {
        if (color is null || !ColorRegex().IsMatch(color))
            throw new ArgumentException("The colour must look like #RRGGBB.", nameof(color));
        return color.ToUpperInvariant();
    }
}
