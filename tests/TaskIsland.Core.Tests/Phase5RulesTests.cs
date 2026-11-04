using TaskIsland.Core.Models;
using TaskIsland.Core.Services;
using static TaskIsland.Core.Tests.TestData;

namespace TaskIsland.Core.Tests;

/// <summary>Phase 5: early reminder rules, the new reminder settings and the accent source.</summary>
public class Phase5RulesTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 10, 0, 0);

    private static TaskItem Due(long id, DateTime due, bool hasTime = true, bool done = false, DateTime? deleted = null,
        DateTime? shown = null, DateTime? early = null) =>
        Task(id, due: due, hasTime: hasTime, done: done, deletedAt: deleted, reminderShown: shown, earlyShown: early);

    // ---- settings ----

    [Fact]
    public void New_reminder_settings_default_to_off_and_on()
    {
        var s = AppSettings.FromDictionary(new Dictionary<string, string>());
        Assert.Equal(0, s.ReminderLeadMinutes);
        Assert.True(s.ReminderPillAlert);
        Assert.Equal(AccentSource.Default, s.AccentSource);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("5", 5)]
    [InlineData("15", 15)]
    [InlineData("30", 30)]
    [InlineData("60", 60)]
    [InlineData("120", 120)]
    [InlineData("1440", 1440)]
    [InlineData("45", 0)]       // not one of the offered values
    [InlineData("-60", 0)]
    [InlineData("100000", 0)]
    [InlineData("abc", 0)]
    [InlineData("", 0)]
    public void Reminder_lead_only_accepts_the_offered_values(string text, int expected)
    {
        var s = AppSettings.FromDictionary(new Dictionary<string, string> { [AppSettings.Keys.ReminderLeadMinutes] = text });
        Assert.Equal(expected, s.ReminderLeadMinutes);
    }

    [Fact]
    public void New_settings_survive_a_round_trip()
    {
        var s = new AppSettings { ReminderLeadMinutes = 60, ReminderPillAlert = false, AccentSource = AccentSource.Windows };
        var back = AppSettings.FromDictionary(s.ToDictionary());
        Assert.Equal(60, back.ReminderLeadMinutes);
        Assert.False(back.ReminderPillAlert);
        Assert.Equal(AccentSource.Windows, back.AccentSource);
    }

    [Theory]
    [InlineData("Windows", AccentSource.Windows)]
    [InlineData("windows", AccentSource.Windows)]
    [InlineData("Default", AccentSource.Default)]
    [InlineData("Rainbow", AccentSource.Default)]
    [InlineData("99", AccentSource.Default)]
    public void Accent_source_parses_or_falls_back(string text, AccentSource expected)
    {
        var s = AppSettings.FromDictionary(new Dictionary<string, string> { [AppSettings.Keys.AccentSource] = text });
        Assert.Equal(expected, s.AccentSource);
    }

    [Fact]
    public void An_invalid_lead_is_never_written_back()
    {
        var s = new AppSettings { ReminderLeadMinutes = 45 };
        Assert.Equal("0", s.ToDictionary()[AppSettings.Keys.ReminderLeadMinutes]);
    }

    // ---- early moment ----

    [Fact]
    public void Early_moment_is_due_minus_lead()
    {
        var t = Due(1, new DateTime(2026, 11, 4, 14, 0, 0));
        Assert.Equal(new DateTime(2026, 11, 4, 13, 0, 0), ReminderScheduler.GetEarlyMoment(t, 60));
    }

    [Fact]
    public void Lead_off_gives_no_early_moment()
    {
        Assert.Null(ReminderScheduler.GetEarlyMoment(Due(1, new DateTime(2026, 11, 4, 14, 0, 0)), 0));
    }

    [Fact]
    public void Date_only_task_has_no_early_moment()
    {
        var t = Due(1, new DateTime(2026, 11, 5), hasTime: false);
        Assert.Null(ReminderScheduler.GetEarlyMoment(t, 60));
        Assert.False(ReminderScheduler.NeedsEarlyReminder(t, 60));
    }

    [Fact]
    public void Task_without_a_due_date_has_no_early_moment()
    {
        Assert.Null(ReminderScheduler.GetEarlyMoment(Task(1), 60));
    }

    [Fact]
    public void Completed_deleted_or_already_reminded_tasks_get_no_early_reminder()
    {
        var due = new DateTime(2026, 11, 4, 14, 0, 0);
        Assert.False(ReminderScheduler.NeedsEarlyReminder(Due(1, due, done: true), 60));
        Assert.False(ReminderScheduler.NeedsEarlyReminder(Due(2, due, deleted: DateTime.UtcNow), 60));
        Assert.False(ReminderScheduler.NeedsEarlyReminder(Due(3, due, shown: DateTime.UtcNow), 60));
        Assert.False(ReminderScheduler.NeedsEarlyReminder(Due(4, due, early: DateTime.UtcNow), 60));
        Assert.True(ReminderScheduler.NeedsEarlyReminder(Due(5, due), 60));
    }

    // ---- GetEarlyDue / GetNextUpcoming ----

    [Fact]
    public void Task_due_in_two_hours_is_not_early_due_yet_but_is_upcoming_at_one_hour_before()
    {
        var t = Due(1, Now.AddHours(2));
        Assert.Empty(ReminderScheduler.GetEarlyDue([t], Now, 60));
        var next = ReminderScheduler.GetNextUpcoming([t], Now, 60);
        Assert.NotNull(next);
        Assert.True(next.Early);
        Assert.Equal(Now.AddHours(1), next.Moment);
    }

    [Fact]
    public void Early_moment_reached_while_not_yet_due_is_early_due()
    {
        var t = Due(1, Now.AddMinutes(60));
        var early = ReminderScheduler.GetEarlyDue([t], Now, 60);
        Assert.Single(early);
        Assert.True(early[0].Early);
    }

    [Fact]
    public void Task_due_in_thirty_minutes_with_a_sixty_minute_lead_is_inside_the_window()
    {
        var t = Due(1, Now.AddMinutes(30));
        // The early moment is already 30 minutes old: the service marks it quietly (older than the late limit).
        var early = ReminderScheduler.GetEarlyDue([t], Now, 60);
        Assert.Single(early);
        Assert.True(ReminderText.IsLate(early[0], Now));
    }

    [Fact]
    public void Task_that_is_already_due_is_not_early_due()
    {
        Assert.Empty(ReminderScheduler.GetEarlyDue([Due(1, Now.AddMinutes(-5))], Now, 60));
        Assert.Empty(ReminderScheduler.GetEarlyDue([Due(1, Now)], Now, 60));
    }

    [Fact]
    public void Task_due_in_the_past_has_only_the_overdue_reminder()
    {
        var t = Due(1, Now.AddHours(-3));
        Assert.Empty(ReminderScheduler.GetEarlyDue([t], Now, 60));
        Assert.Single(ReminderScheduler.GetOverdue([t], Now));
        Assert.Null(ReminderScheduler.GetNextUpcoming([t], Now, 60));
    }

    [Fact]
    public void Lead_off_changes_nothing_for_the_next_upcoming()
    {
        var t = Due(1, Now.AddHours(2));
        var next = ReminderScheduler.GetNextUpcoming([t], Now, 0);
        Assert.NotNull(next);
        Assert.False(next.Early);
        Assert.Equal(Now.AddHours(2), next.Moment);
        Assert.Empty(ReminderScheduler.GetEarlyDue([t], Now.AddHours(1), 0));
    }

    [Fact]
    public void Upcoming_picks_the_earliest_of_early_and_due_moments_across_tasks()
    {
        var a = Due(1, Now.AddMinutes(150));       // early at +90
        var b = Due(2, Now.AddMinutes(100));       // early at +40
        var next = ReminderScheduler.GetNextUpcoming([a, b], Now, 60);
        Assert.Equal(2, next!.Task.Id);
        Assert.True(next.Early);
        Assert.Equal(Now.AddMinutes(40), next.Moment);
    }

    [Fact]
    public void After_the_early_reminder_the_next_upcoming_is_the_due_moment()
    {
        var t = Due(1, Now.AddHours(2), early: DateTime.UtcNow);
        var next = ReminderScheduler.GetNextUpcoming([t], Now, 60);
        Assert.False(next!.Early);
        Assert.Equal(Now.AddHours(2), next.Moment);
    }

    [Fact]
    public void Editing_the_due_date_re_arms_the_early_reminder()
    {
        // The data layer clears EarlyReminderShownAt when the due date changes; the rule then sees the task again.
        var edited = Due(1, Now.AddHours(3), early: null);
        Assert.True(ReminderScheduler.NeedsEarlyReminder(edited, 60));
    }

    [Fact]
    public void Changing_the_lead_after_the_early_one_fired_does_not_repeat_it()
    {
        var t = Due(1, Now.AddMinutes(50), early: DateTime.UtcNow);
        Assert.Empty(ReminderScheduler.GetEarlyDue([t], Now, 30));
        Assert.Empty(ReminderScheduler.GetEarlyDue([t], Now, 120));
    }

    [Fact]
    public void Completed_or_deleted_tasks_are_not_upcoming()
    {
        var due = Now.AddHours(2);
        Assert.Null(ReminderScheduler.GetNextUpcoming([Due(1, due, done: true), Due(2, due, deleted: DateTime.UtcNow)], Now, 60));
    }

    // ---- text ----

    [Theory]
    [InlineData(5, "5 minutes")]
    [InlineData(15, "15 minutes")]
    [InlineData(30, "30 minutes")]
    [InlineData(60, "1 hour")]
    [InlineData(120, "2 hours")]
    [InlineData(1440, "1 day")]
    public void Lead_words(int minutes, string words) => Assert.Equal(words, ReminderText.LeadWords(minutes));

    [Fact]
    public void Early_notice_for_one_task_names_the_lead_and_the_list()
    {
        var t = Task(1, "Send report", listId: 2, due: Now.AddHours(1), hasTime: true);
        var n = ReminderText.DescribeEarly([new DueReminder(t, Now, Early: true)], 60, id => id == 2 ? "Office Work" : null);
        Assert.NotNull(n);
        Assert.Equal("Office Work", n.Title);
        Assert.Equal("⏰ In 1 hour: Send report", n.Text);
        Assert.Equal("⏰ In 1 hour: Send report", n.PillText);
        Assert.True(n.Early);
        Assert.Equal(2, n.ListId);
    }

    [Fact]
    public void Early_notice_for_several_tasks_is_a_count()
    {
        var a = new DueReminder(Task(1, listId: 1), Now, Early: true);
        var b = new DueReminder(Task(2, listId: 2), Now, Early: true);
        var n = ReminderText.DescribeEarly([a, b], 30, _ => "x");
        Assert.Equal("⏰ 2 tasks due in 30 minutes", n!.Text);
        Assert.Null(n.ListId);
    }

    [Fact]
    public void Early_notice_for_nothing_is_null() => Assert.Null(ReminderText.DescribeEarly([], 60, _ => null));

    [Fact]
    public void Pill_text_for_a_task_due_now_is_just_the_title()
    {
        var t = Task(1, "Call mum", due: Now, hasTime: true);
        var n = ReminderText.Describe([new DueReminder(t, Now)], late: false, _ => "Personal");
        Assert.Equal("⏰ Due now: Call mum", n!.Text);
        Assert.Equal("⏰ Call mum", n.PillText);
        Assert.False(n.Early);
    }

    [Fact]
    public void Pill_text_for_overdue_and_batches_matches_the_notification()
    {
        var t = Task(1, "Call mum", due: Now, hasTime: true);
        var late = ReminderText.Describe([new DueReminder(t, Now)], late: true, _ => "Personal");
        Assert.Equal(late!.Text, late.PillText);
        var many = ReminderText.Describe([new DueReminder(t, Now), new DueReminder(Task(2), Now), new DueReminder(Task(3), Now)],
            late: false, _ => null);
        Assert.Equal("⏰ 3 tasks due now", many!.PillText);
    }
}
