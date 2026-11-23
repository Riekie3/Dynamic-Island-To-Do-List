using TaskIsland.Core.Models;

namespace TaskIsland.Core.Services;

/// <summary>When the time-relative parts of a row ("Today", "Tomorrow", overdue styling) next need recomputing.</summary>
public static class DueRefresh
{
    /// <summary>
    /// The earliest upcoming due moment of an open task, or the next local midnight, whichever comes first. Midnight is
    /// always a change point because the day names move on. Completed tasks only need the midnight refresh.
    /// </summary>
    public static DateTime NextMoment(IEnumerable<TaskItem> tasks, DateTime nowLocal)
    {
        var next = nowLocal.Date.AddDays(1);
        foreach (var task in tasks)
        {
            if (task.IsCompleted || task.DeletedAt is not null) continue;
            if (ReminderScheduler.GetDueMoment(task) is not { } due) continue;
            if (due >= nowLocal && due < next) next = due;
        }
        return next;
    }
}
