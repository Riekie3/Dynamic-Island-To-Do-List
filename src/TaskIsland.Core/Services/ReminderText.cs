namespace TaskIsland.Core.Services;

/// <summary>What one reminder notification says (PLAN.md section 7.9).</summary>
public sealed record ReminderNotice(string Title, string Text, long? ListId, string? AlertText = null, bool Early = false)
{
    /// <summary>What the widened island shows: "⏰ title" for one task due now, else the same words as the notification.</summary>
    public string PillText => AlertText ?? Text;
}

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
            return new ReminderNotice(heading, (late ? "⏰ Overdue: " : "⏰ Due now: ") + t.Title, list,
                late ? null : "⏰ " + t.Title);
        }
        return new ReminderNotice("TaskIsland", late ? $"⏰ {due.Count} tasks overdue" : $"⏰ {due.Count} tasks due now", list);
    }

    /// <summary>"1 hour", "30 minutes", "1 day": the early-reminder lead as words.</summary>
    public static string LeadWords(int minutes) => minutes switch
    {
        1440 => "1 day",
        60 => "1 hour",
        120 => "2 hours",
        1 => "1 minute",
        _ when minutes > 0 && minutes % 1440 == 0 => $"{minutes / 1440} days",
        _ when minutes > 60 && minutes % 60 == 0 => $"{minutes / 60} hours",
        _ => $"{minutes} minutes",
    };

    /// <summary>
    /// Notice for early reminders: "⏰ In 1 hour: title" (list name as heading), or "⏰ 3 tasks due in 1 hour" for a batch.
    /// </summary>
    public static ReminderNotice? DescribeEarly(IReadOnlyList<DueReminder> early, int leadMinutes, Func<long, string?> listName)
    {
        if (early.Count == 0) return null;
        var listIds = early.Select(d => d.Task.ListId).Distinct().ToList();
        long? list = listIds.Count == 1 ? listIds[0] : null;
        string words = LeadWords(leadMinutes);
        if (early.Count == 1)
        {
            var t = early[0].Task;
            string heading = listName(t.ListId) is { Length: > 0 } n ? n : "TaskIsland";
            return new ReminderNotice(heading, $"⏰ In {words}: {t.Title}", list, Early: true);
        }
        return new ReminderNotice("TaskIsland", $"⏰ {early.Count} tasks due in {words}", list, Early: true);
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
