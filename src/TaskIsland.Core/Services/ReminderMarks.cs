using TaskIsland.Core.Models;

namespace TaskIsland.Core.Services;

/// <summary>
/// Keeps the in-memory "reminder already shown" marks in step with the database stamps after a reload (PLAN.md section 7.9).
/// The marks exist only to avoid a second notification between a mark and the next reload; the database is authoritative.
/// </summary>
public static class ReminderMarks
{
    /// <summary>
    /// Removes marks for tasks that no longer exist, whose database stamp was cleared, or whose due moment changed.
    /// This is what lets a due date moved away and back, or a restored backup, remind again.
    /// </summary>
    public static void Reconcile(Dictionary<long, DateTime> marks, IEnumerable<TaskItem> tasks, bool early)
    {
        if (marks.Count == 0) return;

        var keep = new HashSet<long>();
        foreach (var task in tasks)
        {
            if (early ? task.EarlyReminderShownAt is null : task.ReminderShownAt is null) continue;
            if (ReminderScheduler.GetDueMoment(task) is { } due
                && marks.TryGetValue(task.Id, out var marked)
                && marked == due)
            {
                keep.Add(task.Id);
            }
        }

        foreach (var id in marks.Keys.Where(id => !keep.Contains(id)).ToList())
            marks.Remove(id);
    }
}
