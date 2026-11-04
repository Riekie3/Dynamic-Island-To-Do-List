using Microsoft.Data.Sqlite;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;

namespace TaskIsland.Data;

public sealed class SqliteTaskRepository(Database db, IClock clock) : ITaskRepository
{
    public async Task<IReadOnlyList<TaskItem>> GetAllAsync(bool includeDeleted = false)
    {
        await using var conn = await db.OpenAsync();
        await using var cmd = conn.Cmd(null,
            $"SELECT {Sql.TaskColumns} FROM Tasks WHERE (@all = 1 OR DeletedAt IS NULL) ORDER BY Id",
            ("@all", includeDeleted ? 1 : 0));
        var list = new List<TaskItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(Sql.ReadTask(r));
        return list;
    }

    public async Task<TaskItem> AddAsync(long listId, string title, Priority priority, DateTime? due = null,
        bool dueHasTime = false, string? description = null)
    {
        var now = clock.UtcNow;
        return await InsertAsync(new TaskItem
        {
            ListId = listId,
            Title = title,
            Priority = priority,
            DueDate = due,
            DueHasTime = due is not null && dueHasTime,
            Description = description,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }

    public async Task<TaskItem> InsertAsync(TaskItem item)
    {
        var title = ValidateTitle(item.Title);
        var description = ValidateDescription(item.Description);
        var created = item.CreatedAt == default ? clock.UtcNow : item.CreatedAt;
        var updated = item.UpdatedAt == default ? created : item.UpdatedAt;

        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginAsync();
        long order = await NextOrderAsync(conn, tx, item.ListId, item.Priority);
        long id = (await conn.ScalarAsync<long>(tx, """
            INSERT INTO Tasks (ListId, Title, Description, Priority, IsCompleted, CreatedAt, UpdatedAt, CompletedAt,
                               DueDate, DueHasTime, SortOrder)
            VALUES (@list, @title, @desc, @prio, @done, @created, @updated, @completedAt, @due, @hasTime, @order);
            SELECT last_insert_rowid();
            """,
            ("@list", item.ListId), ("@title", title), ("@desc", description), ("@prio", (int)item.Priority),
            ("@done", item.IsCompleted ? 1 : 0), ("@created", IsoDate.ToUtcString(created)),
            ("@updated", IsoDate.ToUtcString(updated)),
            ("@completedAt", item.IsCompleted ? Sql.Text(item.CompletedAt ?? updated) : null),
            ("@due", item.DueDate is { } d ? IsoDate.ToLocalString(d, item.DueHasTime) : null),
            ("@hasTime", item.DueDate is not null && item.DueHasTime ? 1 : 0), ("@order", order)));
        await tx.CommitAsync();
        return await GetAsync(conn, id) ?? throw new InvalidOperationException("The new task could not be read back.");
    }

    public async Task MoveToListAsync(long id, long targetListId)
    {
        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginAsync();
        var prio = await conn.ScalarAsync<int?>(tx, "SELECT Priority FROM Tasks WHERE Id = @id", ("@id", id))
            ?? throw new InvalidOperationException("Task not found.");
        long order = await NextOrderAsync(conn, tx, targetListId, (Priority)prio);
        await conn.ExecAsync(tx, "UPDATE Tasks SET ListId = @list, SortOrder = @order, UpdatedAt = @now WHERE Id = @id",
            ("@list", targetListId), ("@order", order), ("@now", IsoDate.ToUtcString(clock.UtcNow)), ("@id", id));
        await tx.CommitAsync();
    }

    public async Task UpdateAsync(TaskItem item)
    {
        var title = ValidateTitle(item.Title);
        var description = ValidateDescription(item.Description);

        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginAsync();
        TaskItem old;
        await using (var cmd = conn.Cmd(tx, $"SELECT {Sql.TaskColumns} FROM Tasks WHERE Id = @id", ("@id", item.Id)))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            if (!await r.ReadAsync()) throw new InvalidOperationException("Task not found.");
            old = Sql.ReadTask(r);
        }

        long order = old.SortOrder;
        if (old.ListId != item.ListId || old.Priority != item.Priority)
            order = await NextOrderAsync(conn, tx, item.ListId, item.Priority);

        bool hasTime = item.DueDate is not null && item.DueHasTime;
        bool dueChanged = old.DueDate != item.DueDate || old.DueHasTime != hasTime;

        await conn.ExecAsync(tx, """
            UPDATE Tasks SET ListId = @list, Title = @title, Description = @desc, Priority = @prio,
                             DueDate = @due, DueHasTime = @hasTime, SortOrder = @order, UpdatedAt = @now,
                             ReminderShownAt = CASE WHEN @dueChanged = 1 THEN NULL ELSE ReminderShownAt END,
                             EarlyReminderShownAt = CASE WHEN @dueChanged = 1 THEN NULL ELSE EarlyReminderShownAt END
            WHERE Id = @id
            """,
            ("@list", item.ListId), ("@title", title), ("@desc", description), ("@prio", (int)item.Priority),
            ("@due", item.DueDate is { } d ? IsoDate.ToLocalString(d, hasTime) : null), ("@hasTime", hasTime ? 1 : 0),
            ("@order", order), ("@now", IsoDate.ToUtcString(clock.UtcNow)), ("@dueChanged", dueChanged ? 1 : 0),
            ("@id", item.Id));
        await tx.CommitAsync();
    }

    public async Task SetCompletedAsync(long id, bool completed)
    {
        var now = IsoDate.ToUtcString(clock.UtcNow);
        await using var conn = await db.OpenAsync();
        int n = await conn.ExecAsync(null,
            "UPDATE Tasks SET IsCompleted = @done, CompletedAt = @at, UpdatedAt = @now WHERE Id = @id",
            ("@done", completed ? 1 : 0), ("@at", completed ? now : null), ("@now", now), ("@id", id));
        if (n == 0) throw new InvalidOperationException("Task not found.");
    }

    public async Task SoftDeleteAsync(long id)
    {
        var now = IsoDate.ToUtcString(clock.UtcNow);
        await using var conn = await db.OpenAsync();
        await conn.ExecAsync(null, "UPDATE Tasks SET DeletedAt = @now, UpdatedAt = @now WHERE Id = @id AND DeletedAt IS NULL",
            ("@now", now), ("@id", id));
    }

    public async Task RestoreAsync(long id)
    {
        await using var conn = await db.OpenAsync();
        await conn.ExecAsync(null, "UPDATE Tasks SET DeletedAt = NULL, UpdatedAt = @now WHERE Id = @id",
            ("@now", IsoDate.ToUtcString(clock.UtcNow)), ("@id", id));
    }

    public async Task PurgeDeletedAsync()
    {
        await using var conn = await db.OpenAsync();
        await conn.ExecAsync(null, "DELETE FROM Tasks WHERE DeletedAt IS NOT NULL");
    }

    public async Task ReorderAsync(long listId, Priority group, IReadOnlyList<long> orderedIds)
    {
        if (orderedIds.Distinct().Count() != orderedIds.Count)
            throw new ArgumentException("The order contains the same task twice.", nameof(orderedIds));

        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginAsync();
        for (int i = 0; i < orderedIds.Count; i++)
        {
            int n = await conn.ExecAsync(tx,
                "UPDATE Tasks SET SortOrder = @i WHERE Id = @id AND ListId = @list AND Priority = @prio",
                ("@i", i), ("@id", orderedIds[i]), ("@list", listId), ("@prio", (int)group));
            if (n == 0)
                throw new ArgumentException($"Task {orderedIds[i]} is not in that list and priority group.", nameof(orderedIds));
        }
        await tx.CommitAsync();
    }

    public async Task<int> ClearCompletedAsync(long? listId = null)
    {
        var now = IsoDate.ToUtcString(clock.UtcNow);
        await using var conn = await db.OpenAsync();
        return await conn.ExecAsync(null, """
            UPDATE Tasks SET DeletedAt = @now, UpdatedAt = @now
            WHERE IsCompleted = 1 AND DeletedAt IS NULL AND (@list IS NULL OR ListId = @list)
            """, ("@now", now), ("@list", listId));
    }

    public async Task MarkReminderShownAsync(long id)
    {
        await using var conn = await db.OpenAsync();
        await conn.ExecAsync(null, "UPDATE Tasks SET ReminderShownAt = @now WHERE Id = @id",
            ("@now", IsoDate.ToUtcString(clock.UtcNow)), ("@id", id));
    }

    public async Task MarkEarlyReminderShownAsync(long id)
    {
        await using var conn = await db.OpenAsync();
        await conn.ExecAsync(null, "UPDATE Tasks SET EarlyReminderShownAt = @now WHERE Id = @id",
            ("@now", IsoDate.ToUtcString(clock.UtcNow)), ("@id", id));
    }

    public async Task ReplaceAllAsync(TransferData data)
    {
        var now = IsoDate.ToUtcString(clock.UtcNow);
        await using var conn = await db.OpenAsync();
        await using var tx = await conn.BeginAsync();

        await conn.ExecAsync(tx, "DELETE FROM Tasks");
        await conn.ExecAsync(tx, "DELETE FROM Lists");

        var ids = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var lists = data.Lists.OrderBy(l => l.SortOrder).ToList();
        if (lists.Count == 0) lists.Add(new TransferList("Personal", TodoList.DefaultColor, 0));
        int order = 0;
        foreach (var l in lists)
        {
            ids[l.Name] = await conn.ScalarAsync<long>(tx, """
                INSERT INTO Lists (Name, ColorHex, SortOrder, CreatedAt, UpdatedAt) VALUES (@n, @c, @o, @now, @now);
                SELECT last_insert_rowid();
                """, ("@n", l.Name), ("@c", l.Color), ("@o", order++), ("@now", now));
        }
        long firstListId = ids[lists[0].Name];

        var counters = new Dictionary<(long, int), int>();
        foreach (var t in data.Tasks.OrderBy(t => t.SortOrder))
        {
            long listId = t.List is null ? firstListId : ids[t.List];
            int next = counters.GetValueOrDefault((listId, (int)t.Priority));
            counters[(listId, (int)t.Priority)] = next + 1;
            await conn.ExecAsync(tx, """
                INSERT INTO Tasks (ListId, Title, Description, Priority, IsCompleted, CreatedAt, UpdatedAt, CompletedAt,
                                   DueDate, DueHasTime, SortOrder)
                VALUES (@list, @title, @desc, @prio, @done, @created, @updated, @completedAt, @due, @hasTime, @order)
                """,
                ("@list", listId), ("@title", t.Title), ("@desc", t.Description), ("@prio", (int)t.Priority),
                ("@done", t.IsCompleted ? 1 : 0), ("@created", IsoDate.ToUtcString(t.CreatedAt)),
                ("@updated", IsoDate.ToUtcString(t.UpdatedAt)), ("@completedAt", Sql.Text(t.CompletedAt)),
                ("@due", t.DueDate is { } d ? IsoDate.ToLocalString(d, t.DueHasTime) : null),
                ("@hasTime", t.DueDate is not null && t.DueHasTime ? 1 : 0), ("@order", next));
        }
        await tx.CommitAsync();
    }

    private static async Task<TaskItem?> GetAsync(SqliteConnection conn, long id)
    {
        await using var cmd = conn.Cmd(null, $"SELECT {Sql.TaskColumns} FROM Tasks WHERE Id = @id", ("@id", id));
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? Sql.ReadTask(r) : null;
    }

    private static async Task<long> NextOrderAsync(SqliteConnection conn, SqliteTransaction tx, long listId, Priority priority) =>
        await conn.ScalarAsync<long?>(tx, "SELECT MAX(SortOrder) FROM Tasks WHERE ListId = @list AND Priority = @prio",
            ("@list", listId), ("@prio", (int)priority)) + 1 ?? 0;

    internal static string ValidateTitle(string? title)
    {
        var t = (title ?? "").Trim();
        if (t.Length is < 1 or > TaskItem.MaxTitleLength)
            throw new ArgumentException($"The title must be 1 to {TaskItem.MaxTitleLength} characters.", nameof(title));
        return t;
    }

    private static string? ValidateDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        if (description.Length > TaskItem.MaxDescriptionLength)
            throw new ArgumentException($"The notes can be at most {TaskItem.MaxDescriptionLength} characters.", nameof(description));
        return description;
    }
}
