namespace TaskIsland.Core.Services;

/// <summary>What one reminder notification says (PLAN.md section 7.9).</summary>
public sealed record ReminderNotice(string Title, string Text, long? ListId);

/// <summary>Builds the balloon text for reminders that are due. Pure, unit-tested.</summary>
public static class ReminderText
{
    /// <summary>Longest wait for the reminder timer; it is recomputed then anyway.</summary>
    public static readonly TimeSpan MaxWait = TimeSpan.FromHours(24);

    /// <summary>
    /// One notice for a batch. A single task shows its title with its list name as the heading; several show a count.
    /// <paramref name="late"/>: the moment passed a while ago (TaskIsland was not running, or the PC was asleep), so the
    /// wording is "overdue" instead of "due now".
    /// Clicking the notice opens the task's list, or the list the whole batch shares, else All lists (null).
    /// </summary>
    public static ReminderNotice? Describe(IReadOnlyList<DueReminder> due, bool late, Func<long, string?> listName)
    {
        if (due.Count == 0) return null;
        var listIds = due.Select(d => d.Task.ListId).Distinct().ToList();
        long? list = listIds.Count == 1 ? listIds[0] : null;
        if (due.Count == 1)
        {
            var t = due[0].Task;
            string heading = listName(t.ListId) is { Length: > 0 } n ? n : "TaskIsland";
            return new ReminderNotice(heading, (late ? "⏰ Overdue: " : "⏰ Due now: ") + t.Title, list);
        }
        return new ReminderNotice("TaskIsland", late ? $"⏰ {due.Count} tasks overdue" : $"⏰ {due.Count} tasks due now", list);
    }

    /// <summary>A reminder this much past its moment counts as late ("overdue").</summary>
    public static readonly TimeSpan LateAfter = TimeSpan.FromSeconds(60);

    public static bool IsLate(DueReminder r, DateTime nowLocal) => nowLocal - r.Moment > LateAfter;

    /// <summary>How long the timer waits for <paramref name="nextLocal"/>: never negative, never more than 24 hours.</summary>
    public static TimeSpan Wait(DateTime nextLocal, DateTime nowLocal)
    {
        var wait = nextLocal - nowLocal;
        if (wait < TimeSpan.Zero) return TimeSpan.Zero;
        return wait > MaxWait ? MaxWait : wait;
    }
}
