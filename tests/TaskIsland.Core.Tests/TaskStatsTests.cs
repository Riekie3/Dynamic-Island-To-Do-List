using TaskIsland.Core.Services;
using static TaskIsland.Core.Tests.TestData;

namespace TaskIsland.Core.Tests;

public class TaskStatsTests
{
    [Fact]
    public void No_tasks_gives_all_zeros()
    {
        var s = TaskStats.Compute([]);
        Assert.Equal(new TaskStats(0, 0, 0, 0), s);
    }

    [Fact]
    public void Counts_total_completed_and_outstanding()
    {
        var s = TaskStats.Compute([Task(1), Task(2, done: true), Task(3)]);
        Assert.Equal(3, s.Total);
        Assert.Equal(1, s.Completed);
        Assert.Equal(2, s.Outstanding);
    }

    [Theory]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]
    [InlineData(1, 2, 50)]
    [InlineData(1, 8, 13)]   // 12.5 rounds away from zero
    [InlineData(3, 3, 100)]
    [InlineData(0, 5, 0)]
    public void Percent_is_rounded(int done, int total, int expected)
    {
        var tasks = Enumerable.Range(1, total).Select(i => Task(i, done: i <= done));
        Assert.Equal(expected, TaskStats.Compute(tasks).Percent);
    }

    [Fact]
    public void Deleted_tasks_are_not_counted()
    {
        var s = TaskStats.Compute([Task(1), Task(2, deletedAt: T0), Task(3, done: true, deletedAt: T0)]);
        Assert.Equal(1, s.Total);
        Assert.Equal(0, s.Completed);
    }

    [Fact]
    public void List_scope_counts_only_that_list()
    {
        var tasks = new[] { Task(1, listId: 1), Task(2, listId: 2, done: true), Task(3, listId: 2) };
        Assert.Equal(new TaskStats(1, 0, 1, 0), TaskStats.Compute(tasks, 1));
        Assert.Equal(new TaskStats(2, 1, 1, 50), TaskStats.Compute(tasks, 2));
        Assert.Equal(3, TaskStats.Compute(tasks).Total);
    }
}
