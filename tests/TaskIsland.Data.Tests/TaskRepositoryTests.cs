using Microsoft.Data.Sqlite;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;
using TaskIsland.Data;

namespace TaskIsland.Data.Tests;

public class TaskRepositoryTests
{
    private const long P = TestDb.PersonalId;
    private const long O = TestDb.OfficeId;

    private static async Task<long[]> OrderOf(TestDb t, long listId, Priority prio) =>
        (await t.Tasks.GetAllAsync()).Where(x => x.ListId == listId && x.Priority == prio)
            .OrderBy(x => x.SortOrder).Select(x => x.Id).ToArray();

    // ---- add ----

    [Fact]
    public async Task Add_returns_the_saved_task_with_defaults()
    {
        using var t = await TestDb.CreateAsync();
        var task = await t.Tasks.AddAsync(P, "  Buy milk  ", Priority.Medium);
        Assert.True(task.Id > 0);
        Assert.Equal("Buy milk", task.Title);
        Assert.Equal(Priority.Medium, task.Priority);
        Assert.False(task.IsCompleted);
        Assert.Equal(P, task.ListId);
        Assert.Equal(0, task.SortOrder);
        Assert.Equal(t.Clock.UtcNow, task.CreatedAt);
        Assert.Equal(t.Clock.UtcNow, task.UpdatedAt);
        Assert.Null(task.CompletedAt);
        Assert.Null(task.DeletedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Add_rejects_a_blank_title(string title)
    {
        using var t = await TestDb.CreateAsync();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => t.Tasks.AddAsync(P, title, Priority.Medium));
        Assert.Empty(await t.Tasks.GetAllAsync());
    }

    [Fact]
    public async Task Add_rejects_a_title_over_500_characters_but_accepts_exactly_500()
    {
        using var t = await TestDb.CreateAsync();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => t.Tasks.AddAsync(P, new string('a', 501), Priority.Low));
        var ok = await t.Tasks.AddAsync(P, new string('a', 500), Priority.Low);
        Assert.Equal(500, ok.Title.Length);
    }

    [Fact]
    public async Task Add_rejects_long_notes()
    {
        using var t = await TestDb.CreateAsync();
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            t.Tasks.AddAsync(P, "x", Priority.Low, description: new string('n', 2001)));
    }

    [Fact]
    public async Task Add_to_a_list_that_does_not_exist_fails()
    {
        using var t = await TestDb.CreateAsync();
        await Assert.ThrowsAsync<SqliteException>(() => t.Tasks.AddAsync(999, "x", Priority.Low));
        Assert.Empty(await t.Tasks.GetAllAsync());
    }

    [Fact]
    public async Task Add_appends_to_the_end_of_its_list_and_priority_group()
    {
        using var t = await TestDb.CreateAsync();
        var a = await t.Tasks.AddAsync(P, "a", Priority.High);
        var b = await t.Tasks.AddAsync(P, "b", Priority.High);
        var c = await t.Tasks.AddAsync(P, "c", Priority.Low);
        var d = await t.Tasks.AddAsync(O, "d", Priority.High);
        var e = await t.Tasks.AddAsync(P, "e", Priority.High);
        Assert.Equal([0, 1, 0, 0, 2], new[] { a, b, c, d, e }.Select(x => x.SortOrder).ToArray());
    }

    [Fact]
    public async Task Add_keeps_description_and_due_date()
    {
        using var t = await TestDb.CreateAsync();
        var timed = await t.Tasks.AddAsync(P, "timed", Priority.High, new DateTime(2026, 12, 24, 17, 45, 0), true, "Gift wrap");
        var dateOnly = await t.Tasks.AddAsync(P, "date", Priority.High, new DateTime(2026, 12, 25, 13, 0, 0), false);
        Assert.Equal("Gift wrap", timed.Description);
        Assert.Equal(new DateTime(2026, 12, 24, 17, 45, 0), timed.DueDate);
        Assert.True(timed.DueHasTime);
        Assert.False(dateOnly.DueHasTime);
        Assert.NotNull(dateOnly.DueDate);
    }

    // ---- read ----

    [Fact]
    public async Task GetAll_hides_deleted_tasks_unless_asked()
    {
        using var t = await TestDb.CreateAsync();
        var a = await t.Tasks.AddAsync(P, "a", Priority.Medium);
        await t.Tasks.AddAsync(P, "b", Priority.Medium);
        await t.Tasks.SoftDeleteAsync(a.Id);
        Assert.Single(await t.Tasks.GetAllAsync());
        Assert.Equal(2, (await t.Tasks.GetAllAsync(includeDeleted: true)).Count);
    }

    // ---- update ----

    [Fact]
    public async Task Update_saves_all_editable_fields_and_stamps_updated_at()
    {
        using var t = await TestDb.CreateAsync();
        var task = await t.Tasks.AddAsync(P, "old", Priority.Low);
        t.Clock.Advance(TimeSpan.FromHours(1));

        task.Title = "  new title ";
        task.Description = "some notes";
        task.Priority = Priority.High;
        task.DueDate = new DateTime(2026, 11, 30, 9, 15, 0);
        task.DueHasTime = true;
        task.ListId = O;
        await t.Tasks.UpdateAsync(task);

        var saved = (await t.Tasks.GetAllAsync()).Single();
        Assert.Equal("new title", saved.Title);
        Assert.Equal("some notes", saved.Description);
        Assert.Equal(Priority.High, saved.Priority);
        Assert.Equal(new DateTime(2026, 11, 30, 9, 15, 0), saved.DueDate);
        Assert.True(saved.DueHasTime);
        Assert.Equal(O, saved.ListId);
        Assert.Equal(t.Clock.UtcNow, saved.UpdatedAt);
        Assert.Equal(task.CreatedAt, saved.CreatedAt);
    }

    [Fact]
    public async Task Update_can_clear_notes_and_due_date()
    {
        using var t = await TestDb.CreateAsync();
        var task = await t.Tasks.AddAsync(P, "x", Priority.Low, new DateTime(2026, 11, 30), false, "notes");
        task.Description = "  ";
        task.DueDate = null;
        await t.Tasks.UpdateAsync(task);
        var saved = (await t.Tasks.GetAllAsync()).Single();
        Assert.Null(saved.Description);
        Assert.Null(saved.DueDate);
        Assert.False(saved.DueHasTime);
    }

    [Fact]
    public async Task Update_rejects_a_blank_title_and_keeps_the_old_one()
    {
        using var t = await TestDb.CreateAsync();
        var task = await t.Tasks.AddAsync(P, "keep", Priority.Low);
        task.Title = " ";
        await Assert.ThrowsAnyAsync<ArgumentException>(() => t.Tasks.UpdateAsync(task));
        Assert.Equal("keep", (await t.Tasks.GetAllAsync()).Single().Title);
    }

    [Fact]
    public async Task Update_of_an_unknown_task_fails()
    {
        using var t = await TestDb.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            t.Tasks.UpdateAsync(new TaskItem { Id = 42, ListId = P, Title = "ghost" }));
    }

    [Fact]
    public async Task Changing_priority_moves_the_task_to_the_end_of_the_new_group()
    {
        using var t = await TestDb.CreateAsync();
        var low1 = await t.Tasks.AddAsync(P, "l1", Priority.Low);
        var high1 = await t.Tasks.AddAsync(P, "h1", Priority.High);
        var high2 = await t.Tasks.AddAsync(P, "h2", Priority.High);

        low1.Priority = Priority.High;
        await t.Tasks.UpdateAsync(low1);

        Assert.Equal([high1.Id, high2.Id, low1.Id], await OrderOf(t, P, Priority.High));
        Assert.Empty(await OrderOf(t, P, Priority.Low));
    }

    [Fact]
    public async Task Keeping_the_priority_keeps_the_position()
    {
        using var t = await TestDb.CreateAsync();
        var a = await t.Tasks.AddAsync(P, "a", Priority.High);
        await t.Tasks.AddAsync(P, "b", Priority.High);
        a.Title = "renamed";
        await t.Tasks.UpdateAsync(a);
        Assert.Equal(0, (await t.Tasks.GetAllAsync()).Single(x => x.Id == a.Id).SortOrder);
    }

    [Fact]
    public async Task Changing_the_due_date_clears_the_reminder_shown_mark()
    {
        using var t = await TestDb.CreateAsync();
        var task = await t.Tasks.AddAsync(P, "x", Priority.High, new DateTime(2026, 11, 2, 9, 0, 0), true);
        await t.Tasks.MarkReminderShownAsync(task.Id);
        Assert.NotNull((await t.Tasks.GetAllAsync()).Single().ReminderShownAt);

        task.Title = "renamed only";
        await t.Tasks.UpdateAsync(task);
        Assert.NotNull((await t.Tasks.GetAllAsync()).Single().ReminderShownAt);

        task.DueDate = new DateTime(2026, 11, 3, 9, 0, 0);
        await t.Tasks.UpdateAsync(task);
        Assert.Null((await t.Tasks.GetAllAsync()).Single().ReminderShownAt);
    }

    [Fact]
    public async Task Early_reminder_mark_is_stored_and_survives_other_edits()
    {
        using var t = await TestDb.CreateAsync();
        var task = await t.Tasks.AddAsync(P, "x", Priority.High, new DateTime(2026, 11, 2, 9, 0, 0), true);
        Assert.Null((await t.Tasks.GetAllAsync()).Single().EarlyReminderShownAt);
        await t.Tasks.MarkEarlyReminderShownAsync(task.Id);
        Assert.Equal(t.Clock.UtcNow, (await t.Tasks.GetAllAsync()).Single().EarlyReminderShownAt);

        task.Title = "renamed only";
        await t.Tasks.UpdateAsync(task);
        Assert.NotNull((await t.Tasks.GetAllAsync()).Single().EarlyReminderShownAt);
    }

    [Fact]
    public async Task Changing_the_due_time_clears_both_reminder_marks()
    {
        using var t = await TestDb.CreateAsync();
        var task = await t.Tasks.AddAsync(P, "x", Priority.High, new DateTime(2026, 11, 2, 9, 0, 0), true);
        await t.Tasks.MarkEarlyReminderShownAsync(task.Id);
        await t.Tasks.MarkReminderShownAsync(task.Id);

        task.DueDate = new DateTime(2026, 11, 2, 10, 0, 0);
        await t.Tasks.UpdateAsync(task);
        var back = (await t.Tasks.GetAllAsync()).Single();
        Assert.Null(back.ReminderShownAt);
        Assert.Null(back.EarlyReminderShownAt);
    }

    [Fact]
    public async Task Removing_the_due_date_clears_both_reminder_marks()
    {
        using var t = await TestDb.CreateAsync();
        var task = await t.Tasks.AddAsync(P, "x", Priority.High, new DateTime(2026, 11, 2, 9, 0, 0), true);
        await t.Tasks.MarkEarlyReminderShownAsync(task.Id);
        task.DueDate = null;
        await t.Tasks.UpdateAsync(task);
        Assert.Null((await t.Tasks.GetAllAsync()).Single().EarlyReminderShownAt);
    }

    // ---- complete ----

    [Fact]
    public async Task Complete_sets_the_completion_time_and_uncomplete_clears_it()
    {
        using var t = await TestDb.CreateAsync();
        var task = await t.Tasks.AddAsync(P, "x", Priority.Medium);

        t.Clock.Advance(TimeSpan.FromMinutes(30));
        await t.Tasks.SetCompletedAsync(task.Id, true);
        var done = (await t.Tasks.GetAllAsync()).Single();
        Assert.True(done.IsCompleted);
        Assert.Equal(t.Clock.UtcNow, done.CompletedAt);
        Assert.Equal(t.Clock.UtcNow, done.UpdatedAt);

        await t.Tasks.SetCompletedAsync(task.Id, false);
        var undone = (await t.Tasks.GetAllAsync()).Single();
        Assert.False(undone.IsCompleted);
        Assert.Null(undone.CompletedAt);
    }

    [Fact]
    public async Task Completed_tasks_stay_in_the_database()
    {
        using var t = await TestDb.CreateAsync();
        var task = await t.Tasks.AddAsync(P, "x", Priority.Medium);
        await t.Tasks.SetCompletedAsync(task.Id, true);
        Assert.Single(await t.Tasks.GetAllAsync(includeDeleted: true));
    }

    [Fact]
    public async Task Completing_an_unknown_task_fails()
    {
        using var t = await TestDb.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.Tasks.SetCompletedAsync(404, true));
    }

    // ---- delete / restore / purge ----

    [Fact]
    public async Task Soft_delete_then_restore_brings_the_task_back_unchanged()
    {
        using var t = await TestDb.CreateAsync();
        var task = await t.Tasks.AddAsync(P, "x", Priority.High, description: "n");
        await t.Tasks.SoftDeleteAsync(task.Id);
        Assert.Empty(await t.Tasks.GetAllAsync());
        await t.Tasks.RestoreAsync(task.Id);
        var back = (await t.Tasks.GetAllAsync()).Single();
        Assert.Equal("x", back.Title);
        Assert.Equal("n", back.Description);
        Assert.Null(back.DeletedAt);
    }

    [Fact]
    public async Task Purge_removes_only_soft_deleted_tasks_for_good()
    {
        using var t = await TestDb.CreateAsync();
        var gone = await t.Tasks.AddAsync(P, "gone", Priority.High);
        var keep = await t.Tasks.AddAsync(P, "keep", Priority.High);
        await t.Tasks.SoftDeleteAsync(gone.Id);
        await t.Tasks.PurgeDeletedAsync();

        var all = await t.Tasks.GetAllAsync(includeDeleted: true);
        Assert.Equal([keep.Id], all.Select(x => x.Id).ToArray());
        await t.Tasks.RestoreAsync(gone.Id);   // nothing to restore any more
        Assert.Single(await t.Tasks.GetAllAsync(includeDeleted: true));
    }

    [Fact]
    public async Task Deleting_twice_does_not_move_the_deletion_time()
    {
        using var t = await TestDb.CreateAsync();
        var task = await t.Tasks.AddAsync(P, "x", Priority.High);
        await t.Tasks.SoftDeleteAsync(task.Id);
        var first = (await t.Tasks.GetAllAsync(true)).Single().DeletedAt;
        t.Clock.Advance(TimeSpan.FromMinutes(5));
        await t.Tasks.SoftDeleteAsync(task.Id);
        Assert.Equal(first, (await t.Tasks.GetAllAsync(true)).Single().DeletedAt);
    }

    // ---- reorder ----

    [Fact]
    public async Task Reorder_persists_the_new_order()
    {
        using var t = await TestDb.CreateAsync();
        var a = await t.Tasks.AddAsync(P, "a", Priority.High);
        var b = await t.Tasks.AddAsync(P, "b", Priority.High);
        var c = await t.Tasks.AddAsync(P, "c", Priority.High);

        await t.Tasks.ReorderAsync(P, Priority.High, [c.Id, a.Id, b.Id]);

        Assert.Equal([c.Id, a.Id, b.Id], await OrderOf(t, P, Priority.High));
    }

    [Fact]
    public async Task Reorder_does_not_touch_other_lists_or_priorities()
    {
        using var t = await TestDb.CreateAsync();
        var a = await t.Tasks.AddAsync(P, "a", Priority.High);
        var b = await t.Tasks.AddAsync(P, "b", Priority.High);
        var other = await t.Tasks.AddAsync(O, "o1", Priority.High);
        await t.Tasks.AddAsync(O, "o2", Priority.High);
        var low = await t.Tasks.AddAsync(P, "l", Priority.Low);
        var before = (await t.Tasks.GetAllAsync()).ToDictionary(x => x.Id, x => x.SortOrder);

        await t.Tasks.ReorderAsync(P, Priority.High, [b.Id, a.Id]);

        var after = (await t.Tasks.GetAllAsync()).ToDictionary(x => x.Id, x => x.SortOrder);
        Assert.Equal(before[other.Id], after[other.Id]);
        Assert.Equal(before[low.Id], after[low.Id]);
    }

    [Fact]
    public async Task Reorder_with_a_task_from_another_group_fails_and_changes_nothing()
    {
        using var t = await TestDb.CreateAsync();
        var a = await t.Tasks.AddAsync(P, "a", Priority.High);
        var b = await t.Tasks.AddAsync(P, "b", Priority.High);
        var stranger = await t.Tasks.AddAsync(O, "s", Priority.High);

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            t.Tasks.ReorderAsync(P, Priority.High, [b.Id, a.Id, stranger.Id]));

        Assert.Equal([a.Id, b.Id], await OrderOf(t, P, Priority.High));   // rolled back
    }

    [Fact]
    public async Task Reorder_with_a_duplicate_id_fails()
    {
        using var t = await TestDb.CreateAsync();
        var a = await t.Tasks.AddAsync(P, "a", Priority.High);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => t.Tasks.ReorderAsync(P, Priority.High, [a.Id, a.Id]));
    }

    // ---- clear completed ----

    [Fact]
    public async Task Clear_completed_soft_deletes_only_completed_tasks_and_can_be_undone()
    {
        using var t = await TestDb.CreateAsync();
        var open = await t.Tasks.AddAsync(P, "open", Priority.Medium);
        var done1 = await t.Tasks.AddAsync(P, "done1", Priority.Medium);
        var done2 = await t.Tasks.AddAsync(O, "done2", Priority.Medium);
        await t.Tasks.SetCompletedAsync(done1.Id, true);
        await t.Tasks.SetCompletedAsync(done2.Id, true);

        Assert.Equal(2, await t.Tasks.ClearCompletedAsync());
        Assert.Equal([open.Id], (await t.Tasks.GetAllAsync()).Select(x => x.Id).ToArray());

        await t.Tasks.RestoreAsync(done1.Id);
        await t.Tasks.RestoreAsync(done2.Id);
        Assert.Equal(3, (await t.Tasks.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Clear_completed_can_be_limited_to_one_list()
    {
        using var t = await TestDb.CreateAsync();
        var a = await t.Tasks.AddAsync(P, "a", Priority.Medium);
        var b = await t.Tasks.AddAsync(O, "b", Priority.Medium);
        await t.Tasks.SetCompletedAsync(a.Id, true);
        await t.Tasks.SetCompletedAsync(b.Id, true);

        Assert.Equal(1, await t.Tasks.ClearCompletedAsync(O));
        Assert.Equal([a.Id], (await t.Tasks.GetAllAsync()).Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task Clear_completed_with_nothing_completed_returns_zero()
    {
        using var t = await TestDb.CreateAsync();
        await t.Tasks.AddAsync(P, "a", Priority.Medium);
        Assert.Equal(0, await t.Tasks.ClearCompletedAsync());
    }

    // ---- move between lists ----

    [Fact]
    public async Task Move_to_list_appends_to_the_same_priority_group_in_the_target()
    {
        using var t = await TestDb.CreateAsync();
        var inP = await t.Tasks.AddAsync(P, "p", Priority.High);
        var o1 = await t.Tasks.AddAsync(O, "o1", Priority.High);
        var o2 = await t.Tasks.AddAsync(O, "o2", Priority.High);
        t.Clock.Advance(TimeSpan.FromMinutes(1));

        await t.Tasks.MoveToListAsync(inP.Id, O);

        Assert.Equal([o1.Id, o2.Id, inP.Id], await OrderOf(t, O, Priority.High));
        var moved = (await t.Tasks.GetAllAsync()).Single(x => x.Id == inP.Id);
        Assert.Equal(O, moved.ListId);
        Assert.Equal(t.Clock.UtcNow, moved.UpdatedAt);
    }

    [Fact]
    public async Task Move_of_an_unknown_task_fails()
    {
        using var t = await TestDb.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.Tasks.MoveToListAsync(77, O));
    }

    // ---- reminders ----

    [Fact]
    public async Task Mark_reminder_shown_stores_the_time()
    {
        using var t = await TestDb.CreateAsync();
        var task = await t.Tasks.AddAsync(P, "x", Priority.High, new DateTime(2026, 11, 2), false);
        t.Clock.Advance(TimeSpan.FromDays(1));
        await t.Tasks.MarkReminderShownAsync(task.Id);
        Assert.Equal(t.Clock.UtcNow, (await t.Tasks.GetAllAsync()).Single().ReminderShownAt);
    }

    // ---- import helper ----

    [Fact]
    public async Task Insert_keeps_the_given_timestamps_and_completion()
    {
        using var t = await TestDb.CreateAsync();
        var created = new DateTime(2026, 5, 1, 7, 0, 0, DateTimeKind.Utc);
        var item = await t.Tasks.InsertAsync(new TaskItem
        {
            ListId = O, Title = "Imported", Priority = Priority.Low, IsCompleted = true,
            CreatedAt = created, UpdatedAt = created.AddDays(1), CompletedAt = created.AddDays(1),
        });
        Assert.Equal(created, item.CreatedAt);
        Assert.Equal(created.AddDays(1), item.UpdatedAt);
        Assert.True(item.IsCompleted);
        Assert.Equal(created.AddDays(1), item.CompletedAt);
    }

    // ---- persistence ----

    [Fact]
    public async Task Tasks_survive_reopening_the_database()
    {
        using var t = await TestDb.CreateAsync();
        var a = await t.Tasks.AddAsync(P, "persisted", Priority.High, new DateTime(2026, 12, 1, 8, 30, 0), true, "notes");
        await t.Tasks.SetCompletedAsync(a.Id, true);

        SqliteConnection.ClearAllPools();   // as if the app had been closed
        var db2 = new Database(t.Paths);
        await db2.InitializeAsync();
        var repo2 = new SqliteTaskRepository(db2, t.Clock);

        var back = (await repo2.GetAllAsync()).Single();
        Assert.Equal("persisted", back.Title);
        Assert.Equal("notes", back.Description);
        Assert.Equal(Priority.High, back.Priority);
        Assert.True(back.IsCompleted);
        Assert.Equal(new DateTime(2026, 12, 1, 8, 30, 0), back.DueDate);
        Assert.True(back.DueHasTime);
    }

    [Fact]
    public async Task Data_is_committed_immediately_without_closing_anything()
    {
        using var t = await TestDb.CreateAsync();
        await t.Tasks.AddAsync(P, "crash-proof", Priority.High);

        // A completely separate connection (not from the pool) sees the row at once, so a crash right now would not lose it.
        var cs = new SqliteConnectionStringBuilder { DataSource = t.Paths.DatabasePath, Pooling = false }.ToString();
        await using var other = new SqliteConnection(cs);
        await other.OpenAsync();
        Assert.Equal(1L, await other.ScalarAsync<long>(null, "SELECT COUNT(*) FROM Tasks WHERE Title = 'crash-proof'"));
    }

    [Fact]
    public async Task Titles_with_quotes_and_unicode_are_stored_safely()
    {
        using var t = await TestDb.CreateAsync();
        const string title = "O'Brien; DROP TABLE Tasks;-- 光顾 🍰";
        await t.Tasks.AddAsync(P, title, Priority.Low);
        Assert.Equal(title, (await t.Tasks.GetAllAsync()).Single().Title);
    }
}
