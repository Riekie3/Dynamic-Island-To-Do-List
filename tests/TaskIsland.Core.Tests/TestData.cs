using TaskIsland.Core.Models;

namespace TaskIsland.Core.Tests;

internal static class TestData
{
    public static readonly DateTime T0 = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    public static TaskItem Task(long id, string title = "Task", Priority priority = Priority.Medium, long listId = 1,
        int sortOrder = 0, bool done = false, DateTime? created = null, DateTime? completedAt = null,
        DateTime? deletedAt = null, DateTime? due = null, bool hasTime = false, DateTime? reminderShown = null, DateTime? earlyShown = null) => new()
    {
        Id = id,
        ListId = listId,
        Title = title,
        Priority = priority,
        SortOrder = sortOrder,
        IsCompleted = done,
        CreatedAt = created ?? T0.AddMinutes(id),
        UpdatedAt = created ?? T0.AddMinutes(id),
        CompletedAt = done ? completedAt ?? T0.AddHours(1) : null,
        DeletedAt = deletedAt,
        DueDate = due,
        DueHasTime = hasTime,
        ReminderShownAt = reminderShown,
        EarlyReminderShownAt = earlyShown,
    };

    public static TodoList List(long id, string name, int sortOrder, string color = "#1E90FF") =>
        new() { Id = id, Name = name, SortOrder = sortOrder, ColorHex = color, CreatedAt = T0, UpdatedAt = T0 };

    public static readonly TodoList Personal = List(1, "Personal", 0);
    public static readonly TodoList Office = List(2, "Office Work", 1, "#A970FF");
}
