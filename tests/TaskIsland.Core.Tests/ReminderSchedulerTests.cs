using TaskIsland.Core.Services;
using static TaskIsland.Core.Tests.TestData;

namespace TaskIsland.Core.Tests;

public class ReminderSchedulerTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0);

    [Fact]
    public void Date_only_due_counts_as_nine_in_the_morning()
    {
        var t = Task(1, due: new DateTime(2026, 11, 5), hasTime: false);
        Assert.Equal(new DateTime(2026, 11, 5, 9, 0, 0), ReminderScheduler.GetDueMoment(t));
    }

    [Fact]
    public void Due_with_time_keeps_its_time()
    {
        var t = Task(1, due: new DateTime(2026, 11, 5, 14, 30, 0), hasTime: true);
        Assert.Equal(new DateTime(2026, 11, 5, 14, 30, 0), ReminderScheduler.GetDueMoment(t));
    }

    [Fact]
    public void No_due_date_has_no_moment()
    {
        Assert.Null(ReminderScheduler.GetDueMoment(Task(1)));
    }

    [Fact]
    public void Next_upcoming_is_the_earliest_future_reminder()
    {
        var tasks = new[]
        {
            Task(1, due: new DateTime(2026, 11, 3, 8, 0, 0), hasTime: true),
            Task(2, due: new DateTime(2026, 11, 2, 8, 0, 0), hasTime: true),
            Task(3, due: new DateTime(2026, 11, 4), hasTime: false),
        };
        var next = ReminderScheduler.GetNextUpcoming(tasks, Now);
        Assert.NotNull(next);
        Assert.Equal(2, next.Task.Id);
        Assert.Equal(new DateTime(2026, 11, 2, 8, 0, 0), next.Moment);
    }

    [Fact]
    public void Past_reminders_are_overdue_not_upcoming()
    {
        var tasks = new[] { Task(1, due: new DateTime(2026, 11, 1, 9, 0, 0), hasTime: true) };
        Assert.Null(ReminderScheduler.GetNextUpcoming(tasks, Now));
        Assert.Single(ReminderScheduler.GetOverdue(tasks, Now));
    }

    [Fact]
    public void Overdue_lists_earliest_first()
    {
        var tasks = new[]
        {
            Task(1, due: new DateTime(2026, 10, 30), hasTime: false),
            Task(2, due: new DateTime(2026, 10, 28), hasTime: false),
        };
        Assert.Equal([2L, 1L], ReminderScheduler.GetOverdue(tasks, Now).Select(r => r.Task.Id).ToArray());
    }

    [Fact]
    public void A_reminder_exactly_now_is_overdue()
    {
        var tasks = new[] { Task(1, due: Now, hasTime: true) };
        Assert.Single(ReminderScheduler.GetOverdue(tasks, Now));
    }

    [Fact]
    public void Skips_tasks_that_do_not_need_a_reminder()
    {
        var due = new DateTime(2026, 11, 1, 9, 0, 0);
        var tasks = new[]
        {
            Task(1, due: due, hasTime: true, reminderShown: T0),
            Task(2, due: due, hasTime: true, done: true),
            Task(3, due: due, hasTime: true, deletedAt: T0),
            Task(4),
        };
        Assert.Empty(ReminderScheduler.GetOverdue(tasks, Now));
        Assert.Null(ReminderScheduler.GetNextUpcoming(tasks, Now));
        Assert.All(tasks, t => Assert.False(ReminderScheduler.NeedsReminder(t)));
    }

    [Fact]
    public void Task_with_due_date_and_nothing_else_needs_a_reminder()
    {
        Assert.True(ReminderScheduler.NeedsReminder(Task(1, due: Now)));
    }
}
