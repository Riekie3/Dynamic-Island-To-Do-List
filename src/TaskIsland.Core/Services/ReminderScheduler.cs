using TaskIsland.Core.Models;

namespace TaskIsland.Core.Services;

/// <summary>A task together with the local moment its reminder is due.</summary>
public sealed record DueReminder(TaskItem Task, DateTime Moment);

/// <summary>Pure reminder logic (PLAN.md section 7.9). The UI layer owns the timer.</summary>
public static class ReminderScheduler
{
    /// <summary>Time of day used for a due date without a time.</summary>
    public static readonly TimeSpan DateOnlyDueTime = TimeSpan.FromHours(9);

    /// <summary>Local moment the task is due. Date-only due dates count as 09:00. Null when the task has no due date.</summary>
    public static DateTime? GetDueMoment(TaskItem task)
    {
        if (task.DueDate is not { } due) return null;
        return task.DueHasTime ? due : due.Date + DateOnlyDueTime;
    }

    /// <summary>Not completed, not deleted, has a due date, and its reminder was not shown yet.</summary>
    public static bool NeedsReminder(TaskItem task) =>
        task.DueDate is not null && !task.IsCompleted && task.DeletedAt is null && task.ReminderShownAt is null;

    /// <summary>Tasks whose reminder moment has passed (at or before <paramref name="nowLocal"/>), earliest first.</summary>
    public static IReadOnlyList<DueReminder> GetOverdue(IEnumerable<TaskItem> tasks, DateTime nowLocal) =>
        tasks.Where(NeedsReminder)
            .Select(t => new DueReminder(t, GetDueMoment(t)!.Value))
            .Where(r => r.Moment <= nowLocal)
            .OrderBy(r => r.Moment).ThenBy(r => r.Task.Id)
            .ToList();

    /// <summary>The earliest reminder strictly after <paramref name="nowLocal"/>, or null.</summary>
    public static DueReminder? GetNextUpcoming(IEnumerable<TaskItem> tasks, DateTime nowLocal) =>
        tasks.Where(NeedsReminder)
            .Select(t => new DueReminder(t, GetDueMoment(t)!.Value))
            .Where(r => r.Moment > nowLocal)
            .OrderBy(r => r.Moment).ThenBy(r => r.Task.Id)
            .FirstOrDefault();
}
