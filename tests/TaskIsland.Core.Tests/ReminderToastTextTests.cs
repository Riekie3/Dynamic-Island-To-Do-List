using TaskIsland.Core.Services;
using TaskIsland.Core.Toasts;

namespace TaskIsland.Core.Tests;

public class ReminderToastTextTests
{
    private static TaskIsland.Core.Models.TaskItem Task(long id, string title, long list = 1) =>
        new() { Id = id, Title = title, ListId = list };

    private static ReminderToastContent Due(string title, bool late, string listName = "Office Work")
    {
        var n = ReminderText.Describe([new DueReminder(Task(1, title), DateTime.Now)], late, _ => listName)!;
        return ReminderToastText.From(n);
    }

    [Fact]
    public void One_task_due_now_gets_a_due_now_tab_and_the_title_and_list_in_the_body()
    {
        var c = Due("Training ISO wei yee", late: false);
        Assert.Equal("Due now", c.Tab);
        Assert.Equal("Training ISO wei yee\nOffice Work", c.Body);
        Assert.Equal(ToastState.Info, c.State);
    }

    [Fact]
    public void One_overdue_task_uses_the_warning_look()
    {
        var c = Due("Pay rent", late: true);
        Assert.Equal("Overdue", c.Tab);
        Assert.Equal(ToastState.Warning, c.State);
        Assert.StartsWith("Pay rent", c.Body);
    }

    [Fact]
    public void Early_reminder_says_how_long_is_left()
    {
        var n = ReminderText.DescribeEarly([new DueReminder(Task(1, "Call bank"), DateTime.Now, Early: true)], 5, _ => "Personal")!;
        var c = ReminderToastText.From(n);
        Assert.Equal("In 5 minutes", c.Tab);
        Assert.Equal("Call bank\nPersonal", c.Body);
    }

    [Fact]
    public void A_notice_without_a_named_list_shows_only_the_task()
    {
        var c = ReminderToastText.From(new ReminderNotice("TaskIsland", "⏰ Due now: Solo", null));
        Assert.Equal("Solo", c.Body);
    }

    [Fact]
    public void Several_tasks_make_a_count_tab_and_a_hint_body()
    {
        var due = new[] { new DueReminder(Task(1, "a"), DateTime.Now), new DueReminder(Task(2, "b"), DateTime.Now) };
        var c = ReminderToastText.From(ReminderText.Describe(due, late: false, _ => "Personal")!);
        Assert.Equal("2 tasks due now", c.Tab);
        Assert.Contains("see them", c.Body);
        Assert.Equal(ToastState.Info, c.State);

        var late = ReminderToastText.From(ReminderText.Describe(due, late: true, _ => "Personal")!);
        Assert.Equal("2 tasks overdue", late.Tab);
        Assert.Equal(ToastState.Warning, late.State);
    }

    [Fact]
    public void A_task_title_containing_a_colon_keeps_everything_after_the_first_label()
    {
        var c = Due("Meeting: budget", late: false);
        Assert.Equal("Due now", c.Tab);
        Assert.StartsWith("Meeting: budget", c.Body);
    }
}
