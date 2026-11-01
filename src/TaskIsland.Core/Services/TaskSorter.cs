using TaskIsland.Core.Models;

namespace TaskIsland.Core.Services;

/// <summary>Display order and filtering rules (PLAN.md section 4.3).</summary>
public static class TaskSorter
{
    /// <summary>
    /// Returns the tasks to display. <paramref name="listId"/> null means "All lists". Deleted tasks are never shown.
    /// Active: Priority, list order, task SortOrder, CreatedAt, Id. Completed: newest CompletedAt first.
    /// All: the active block followed by the completed block.
    /// </summary>
    public static IReadOnlyList<TaskItem> Apply(IEnumerable<TaskItem> tasks, TaskFilter filter, long? listId,
        IEnumerable<TodoList> lists)
    {
        var listOrder = lists.ToDictionary(l => l.Id, l => l.SortOrder);
        var scoped = tasks.Where(t => t.DeletedAt is null && (listId is null || t.ListId == listId)).ToList();

        IEnumerable<TaskItem> Active() => scoped.Where(t => !t.IsCompleted)
            .OrderBy(t => t.Priority)
            .ThenBy(t => listOrder.GetValueOrDefault(t.ListId, int.MaxValue))
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.Id);

        IEnumerable<TaskItem> Completed() => scoped.Where(t => t.IsCompleted)
            .OrderByDescending(t => t.CompletedAt ?? DateTime.MinValue)
            .ThenByDescending(t => t.Id);

        return filter switch
        {
            TaskFilter.Active => Active().ToList(),
            TaskFilter.Completed => Completed().ToList(),
            _ => Active().Concat(Completed()).ToList(),
        };
    }
}
