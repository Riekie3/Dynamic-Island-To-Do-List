using TaskIsland.Core.Models;

namespace TaskIsland.Core.Services;

/// <summary>A task together with the local moment its reminder is due. <paramref name="Early"/>: the early reminder (before the due time).</summary>
public sealed record DueReminder(TaskItem Task, DateTime Moment, bool Early = false);

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

    /// <summary>
    /// Local moment of the early reminder (due time minus the lead). Null when the lead is off or the task has no due
    /// *time*: date-only tasks get no early reminder.
    /// </summary>
    public static DateTime? GetEarlyMoment(TaskItem task, int leadMinutes)
    {
        if (leadMinutes <= 0 || task.DueDate is not { } due || !task.DueHasTime) return null;
        return due - TimeSpan.FromMinutes(leadMinutes);
    }

    /// <summary>The task could still get an early reminder: it has one scheduled, it was not shown, the due reminder has not fired.</summary>
    public static bool NeedsEarlyReminder(TaskItem task, int leadMinutes) =>
        GetEarlyMoment(task, leadMinutes) is not null && NeedsReminder(task) && task.EarlyReminderShownAt is null;

    /// <summary>Tasks whose reminder moment has passed (at or before <paramref name="nowLocal"/>), earliest first.</summary>
    public static IReadOnlyList<DueReminder> GetOverdue(IEnumerable<TaskItem> tasks, DateTime nowLocal) =>
        tasks.Where(NeedsReminder)
            .Select(t => new DueReminder(t, GetDueMoment(t)!.Value))
            .Where(r => r.Moment <= nowLocal)
            .OrderBy(r => r.Moment).ThenBy(r => r.Task.Id)
            .ToList();

    /// <summary>
    /// Early reminders whose moment has come (at or before <paramref name="nowLocal"/>) while the task is not yet due.
    /// A task already due is left to <see cref="GetOverdue"/>. Earliest first.
    /// </summary>
    public static IReadOnlyList<DueReminder> GetEarlyDue(IEnumerable<TaskItem> tasks, DateTime nowLocal, int leadMinutes) =>
        tasks.Where(t => NeedsEarlyReminder(t, leadMinutes))
            .Select(t => new DueReminder(t, GetEarlyMoment(t, leadMinutes)!.Value, Early: true))
            .Where(r => r.Moment <= nowLocal && nowLocal < GetDueMoment(r.Task)!.Value)
            .OrderBy(r => r.Moment).ThenBy(r => r.Task.Id)
            .ToList();

    /// <summary>
    /// The earliest reminder strictly after <paramref name="nowLocal"/>, or null: either a due moment or, when a lead is set,
    /// an early moment.
    /// </summary>
    public static DueReminder? GetNextUpcoming(IEnumerable<TaskItem> tasks, DateTime nowLocal, int leadMinutes = 0)
    {
        var all = tasks as IReadOnlyCollection<TaskItem> ?? tasks.ToList();
        var due = all.Where(NeedsReminder)
            .Select(t => new DueReminder(t, GetDueMoment(t)!.Value))
            .Where(r => r.Moment > nowLocal);
        var early = all.Where(t => NeedsEarlyReminder(t, leadMinutes))
            .Select(t => new DueReminder(t, GetEarlyMoment(t, leadMinutes)!.Value, Early: true))
            .Where(r => r.Moment > nowLocal);
        return due.Concat(early).OrderBy(r => r.Moment).ThenBy(r => r.Task.Id).FirstOrDefault();
    }
}
