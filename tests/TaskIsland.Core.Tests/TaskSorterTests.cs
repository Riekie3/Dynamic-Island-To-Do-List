using TaskIsland.Core.Models;
using TaskIsland.Core.Services;
using static TaskIsland.Core.Tests.TestData;

namespace TaskIsland.Core.Tests;

public class TaskSorterTests
{
    private static readonly TodoList[] Lists = [Personal, Office];

    private static long[] Ids(IEnumerable<TaskItem> tasks) => tasks.Select(t => t.Id).ToArray();

    [Fact]
    public void Active_orders_high_then_medium_then_low()
    {
        var tasks = new[]
        {
            Task(1, priority: Priority.Low),
            Task(2, priority: Priority.High),
            Task(3, priority: Priority.Medium),
        };
        Assert.Equal([2L, 3L, 1L], Ids(TaskSorter.Apply(tasks, TaskFilter.Active, 1, Lists)));
    }

    [Fact]
    public void Within_a_priority_sort_order_wins()
    {
        var tasks = new[]
        {
            Task(1, sortOrder: 2), Task(2, sortOrder: 0), Task(3, sortOrder: 1),
        };
        Assert.Equal([2L, 3L, 1L], Ids(TaskSorter.Apply(tasks, TaskFilter.Active, 1, Lists)));
    }

    [Fact]
    public void Equal_sort_order_falls_back_to_created_then_id()
    {
        var tasks = new[]
        {
            Task(5, created: T0.AddMinutes(10)),
            Task(4, created: T0.AddMinutes(10)),
            Task(9, created: T0.AddMinutes(1)),
        };
        Assert.Equal([9L, 4L, 5L], Ids(TaskSorter.Apply(tasks, TaskFilter.Active, 1, Lists)));
    }

    [Fact]
    public void Active_filter_hides_completed_tasks()
    {
        var tasks = new[] { Task(1), Task(2, done: true) };
        Assert.Equal([1L], Ids(TaskSorter.Apply(tasks, TaskFilter.Active, null, Lists)));
    }

    [Fact]
    public void Completed_filter_shows_newest_completion_first()
    {
        var tasks = new[]
        {
            Task(1, done: true, completedAt: T0.AddHours(1)),
            Task(2, done: true, completedAt: T0.AddHours(3)),
            Task(3, done: true, completedAt: T0.AddHours(2)),
            Task(4),
        };
        Assert.Equal([2L, 3L, 1L], Ids(TaskSorter.Apply(tasks, TaskFilter.Completed, null, Lists)));
    }

    [Fact]
    public void All_filter_lists_active_block_then_completed_block()
    {
        var tasks = new[]
        {
            Task(1, done: true, completedAt: T0.AddHours(1)),
            Task(2, priority: Priority.Low),
            Task(3, priority: Priority.High),
            Task(4, done: true, completedAt: T0.AddHours(2)),
        };
        Assert.Equal([3L, 2L, 4L, 1L], Ids(TaskSorter.Apply(tasks, TaskFilter.All, null, Lists)));
    }

    [Theory]
    [InlineData(TaskFilter.Active)]
    [InlineData(TaskFilter.Completed)]
    [InlineData(TaskFilter.All)]
    public void Deleted_tasks_are_never_shown(TaskFilter filter)
    {
        var tasks = new[]
        {
            Task(1, deletedAt: T0), Task(2, done: true, deletedAt: T0), Task(3), Task(4, done: true),
        };
        Assert.DoesNotContain(Apply(tasks, filter), t => t.DeletedAt is not null);
    }

    private static IReadOnlyList<TaskItem> Apply(IEnumerable<TaskItem> tasks, TaskFilter f) =>
        TaskSorter.Apply(tasks, f, null, Lists);

    [Fact]
    public void A_single_list_scope_only_returns_that_list()
    {
        var tasks = new[] { Task(1, listId: 1), Task(2, listId: 2), Task(3, listId: 1) };
        Assert.Equal([1L, 3L], Ids(TaskSorter.Apply(tasks, TaskFilter.Active, 1, Lists)));
        Assert.Equal([2L], Ids(TaskSorter.Apply(tasks, TaskFilter.Active, 2, Lists)));
    }

    [Fact]
    public void All_lists_view_sorts_by_priority_first_then_list_order()
    {
        var tasks = new[]
        {
            Task(1, priority: Priority.Medium, listId: 1),
            Task(2, priority: Priority.High, listId: 2),
            Task(3, priority: Priority.High, listId: 1),
            Task(4, priority: Priority.Medium, listId: 2),
        };
        // High tasks first (Personal before Office Work), then the Medium ones in the same list order.
        Assert.Equal([3L, 2L, 1L, 4L], Ids(TaskSorter.Apply(tasks, TaskFilter.Active, null, Lists)));
    }

    [Fact]
    public void List_order_not_list_id_decides_between_lists()
    {
        var reversed = new[] { List(1, "Personal", 5), List(2, "Office Work", 0) };
        var tasks = new[] { Task(1, listId: 1), Task(2, listId: 2) };
        Assert.Equal([2L, 1L], Ids(TaskSorter.Apply(tasks, TaskFilter.Active, null, reversed)));
    }

    [Fact]
    public void Empty_input_gives_empty_result()
    {
        Assert.Empty(TaskSorter.Apply([], TaskFilter.All, null, Lists));
    }
}
