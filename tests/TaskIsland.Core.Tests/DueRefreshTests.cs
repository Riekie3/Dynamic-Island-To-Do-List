using TaskIsland.Core.Services;
using static TaskIsland.Core.Tests.TestData;

namespace TaskIsland.Core.Tests;

public class DueRefreshTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 10, 0, 0);
    private static readonly DateTime Midnight = new(2026, 11, 6);

    [Fact]
    public void Midnight_is_the_fallback_with_nothing_due()
    {
        Assert.Equal(Midnight, DueRefresh.NextMoment([Task(1)], Now));
    }

    [Fact]
    public void A_later_due_moment_today_comes_first()
    {
        var due = new DateTime(2026, 11, 5, 17, 0, 0);
        Assert.Equal(due, DueRefresh.NextMoment([Task(1, due: due, hasTime: true)], Now));
    }

    [Fact]
    public void The_earliest_upcoming_due_moment_wins()
    {
        var tasks = new[]
        {
            Task(1, due: new DateTime(2026, 11, 5, 18, 0, 0), hasTime: true),
            Task(2, due: new DateTime(2026, 11, 5, 14, 0, 0), hasTime: true),
            Task(3, due: new DateTime(2026, 11, 5, 16, 0, 0), hasTime: true),
        };

        Assert.Equal(new DateTime(2026, 11, 5, 14, 0, 0), DueRefresh.NextMoment(tasks, Now));
    }

    [Fact]
    public void A_date_only_due_counts_as_nine_in_the_morning()
    {
        Assert.Equal(new DateTime(2026, 11, 5, 9, 0, 0),
            DueRefresh.NextMoment([Task(1, due: new DateTime(2026, 11, 5))], new DateTime(2026, 11, 5, 8, 0, 0)));
    }

    [Fact]
    public void Past_due_moments_are_ignored()
    {
        Assert.Equal(Midnight,
            DueRefresh.NextMoment([Task(1, due: new DateTime(2026, 11, 5, 9, 0, 0), hasTime: true)], Now));
    }

    [Fact]
    public void Completed_and_deleted_tasks_do_not_arm_the_timer()
    {
        var tasks = new[]
        {
            Task(1, due: new DateTime(2026, 11, 5, 14, 0, 0), hasTime: true, done: true),
            Task(2, due: new DateTime(2026, 11, 5, 15, 0, 0), hasTime: true, deletedAt: T0),
        };

        Assert.Equal(Midnight, DueRefresh.NextMoment(tasks, Now));
    }

    [Fact]
    public void A_due_moment_after_midnight_waits_for_midnight()
    {
        Assert.Equal(Midnight,
            DueRefresh.NextMoment([Task(1, due: new DateTime(2026, 11, 6, 9, 0, 0), hasTime: true)], Now));
    }
}
