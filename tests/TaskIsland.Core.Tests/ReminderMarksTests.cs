using TaskIsland.Core.Services;
using static TaskIsland.Core.Tests.TestData;

namespace TaskIsland.Core.Tests;

public class ReminderMarksTests
{
    private static Dictionary<long, DateTime> Marks(long id, DateTime moment) => new() { [id] = moment };

    [Fact]
    public void Keeps_a_mark_while_the_database_stamp_and_due_moment_still_match()
    {
        var due = new DateTime(2026, 11, 5, 15, 0, 0);
        var marks = Marks(1, due);
        var tasks = new[] { Task(1, due: due, hasTime: true, reminderShown: T0) };

        ReminderMarks.Reconcile(marks, tasks, early: false);

        Assert.Equal(due, marks[1]);
    }

    [Fact]
    public void Drops_a_mark_when_the_due_date_was_changed_and_the_database_stamp_cleared()
    {
        var marks = Marks(1, new DateTime(2026, 11, 5, 15, 0, 0));
        var tasks = new[] { Task(1, due: new DateTime(2026, 11, 5, 17, 0, 0), hasTime: true) };

        ReminderMarks.Reconcile(marks, tasks, early: false);

        Assert.Empty(marks);
    }

    [Fact]
    public void Drops_a_mark_when_the_due_moment_changed_even_if_the_stamp_is_still_set()
    {
        var marks = Marks(1, new DateTime(2026, 11, 5, 15, 0, 0));
        var tasks = new[] { Task(1, due: new DateTime(2026, 11, 5, 17, 0, 0), hasTime: true, reminderShown: T0) };

        ReminderMarks.Reconcile(marks, tasks, early: false);

        Assert.Empty(marks);
    }

    [Fact]
    public void Drops_a_mark_for_a_task_that_no_longer_exists()
    {
        var dued = new DateTime(2026, 11, 5, 15, 0, 0);
        var marks = Marks(9, dued);
        var tasks = new[] { Task(1, due: dued, hasTime: true, reminderShown: T0) };

        ReminderMarks.Reconcile(marks, tasks, early: false);

        Assert.Empty(marks);
    }

    [Fact]
    public void Early_marks_use_the_early_stamp()
    {
        var due = new DateTime(2026, 11, 5, 15, 0, 0);
        var marks = Marks(1, due);
        var tasks = new[] { Task(1, due: due, hasTime: true, reminderShown: T0) };

        ReminderMarks.Reconcile(marks, tasks, early: true);

        Assert.Empty(marks);
        marks = Marks(1, due);
        tasks = [Task(1, due: due, hasTime: true, earlyShown: T0)];

        ReminderMarks.Reconcile(marks, tasks, early: true);

        Assert.Equal(due, marks[1]);
    }

    [Fact]
    public void A_task_without_a_due_date_keeps_no_mark()
    {
        var marks = Marks(1, new DateTime(2026, 11, 5, 15, 0, 0));
        var tasks = new[] { Task(1, reminderShown: T0) };

        ReminderMarks.Reconcile(marks, tasks, early: false);

        Assert.Empty(marks);
    }
}
