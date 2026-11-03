using TaskIsland.Core.Models;

namespace TaskIsland.App.ViewModels;

/// <summary>
/// Phase 2 only: in-memory sample lists and tasks (all three priorities, one done, one overdue, several lists) so the
/// island can be built and compared with the design reference. Phase 3 replaces this with the database.
/// </summary>
public static class SampleData
{
    public static (List<TodoList> Lists, List<TaskItem> Tasks) Create(DateTime nowUtc, DateTime nowLocal)
    {
        // "Due later today" stays in the future whatever time the app is started.
        var laterToday = nowLocal.Hour < 17 ? nowLocal.Date.AddHours(17)
            : nowLocal.Hour < 22 ? nowLocal.Date.AddHours(nowLocal.Hour + 2) : nowLocal.Date.AddDays(1).AddHours(9);
        var lists = new List<TodoList>
        {
            new() { Id = 1, Name = "Office Work", ColorHex = "#A970FF", SortOrder = 0 },
            new() { Id = 2, Name = "Personal", ColorHex = "#1E90FF", SortOrder = 1 },
            new() { Id = 3, Name = "Project Alpha", ColorHex = "#22C1C3", SortOrder = 2 },
        };
        var tasks = new List<TaskItem>
        {
            new() { Id = 1, ListId = 1, Title = "Finish project report", Priority = Priority.High,
                    DueDate = laterToday, DueHasTime = true, CreatedAt = nowUtc.AddHours(-5) },
            new() { Id = 2, ListId = 2, Title = "Renew car insurance", Priority = Priority.High,
                    DueDate = nowLocal.Date.AddDays(-1), CreatedAt = nowUtc.AddHours(-4) },
            new() { Id = 3, ListId = 1, Title = "Prepare slides for Monday", Priority = Priority.Medium,
                    CreatedAt = nowUtc.AddHours(-3) },
            new() { Id = 4, ListId = 2, Title = "Buy wedding gift", Priority = Priority.Low, IsCompleted = true,
                    CreatedAt = nowUtc.AddHours(-2), CompletedAt = nowUtc.AddHours(-1) },
        };
        foreach (var t in tasks) t.UpdatedAt = t.CreatedAt;
        return (lists, tasks);
    }
}
