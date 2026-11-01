using TaskIsland.Core.Models;

namespace TaskIsland.Core.Services;

/// <summary>Counters shown on the pill, in the header, in the footer and on list chips.</summary>
public sealed record TaskStats(int Total, int Completed, int Outstanding, int Percent)
{
    /// <summary>Counts non-deleted tasks. <paramref name="listId"/> null means all lists.</summary>
    public static TaskStats Compute(IEnumerable<TaskItem> tasks, long? listId = null)
    {
        int total = 0, completed = 0;
        foreach (var t in tasks)
        {
            if (t.DeletedAt is not null) continue;
            if (listId is not null && t.ListId != listId) continue;
            total++;
            if (t.IsCompleted) completed++;
        }
        int percent = total == 0 ? 0 : (int)Math.Round(completed * 100.0 / total, MidpointRounding.AwayFromZero);
        return new TaskStats(total, completed, total - completed, percent);
    }
}
