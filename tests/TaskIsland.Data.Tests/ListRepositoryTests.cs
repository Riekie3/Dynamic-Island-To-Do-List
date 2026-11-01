using TaskIsland.Core.Models;

namespace TaskIsland.Data.Tests;

public class ListRepositoryTests
{
    private const long P = TestDb.PersonalId;
    private const long O = TestDb.OfficeId;

    [Fact]
    public async Task Create_appends_a_list_at_the_end()
    {
        using var t = await TestDb.CreateAsync();
        var list = await t.Lists.CreateAsync("  Project Alpha ", "#22c1c3");
        Assert.Equal("Project Alpha", list.Name);
        Assert.Equal("#22C1C3", list.ColorHex);
        Assert.Equal(2, list.SortOrder);
        Assert.Equal(t.Clock.UtcNow, list.CreatedAt);
        Assert.Equal(["Personal", "Office Work", "Project Alpha"], (await t.Lists.GetAllAsync()).Select(l => l.Name).ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_rejects_a_blank_name(string name)
    {
        using var t = await TestDb.CreateAsync();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => t.Lists.CreateAsync(name, "#1E90FF"));
    }

    [Fact]
    public async Task Create_rejects_a_name_over_40_characters_but_accepts_exactly_40()
    {
        using var t = await TestDb.CreateAsync();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => t.Lists.CreateAsync(new string('n', 41), "#1E90FF"));
        Assert.Equal(40, (await t.Lists.CreateAsync(new string('n', 40), "#1E90FF")).Name.Length);
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("#FFF")]
    [InlineData("#12345G")]
    [InlineData("")]
    public async Task Create_rejects_a_bad_colour(string color)
    {
        using var t = await TestDb.CreateAsync();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => t.Lists.CreateAsync("X", color));
    }

    [Theory]
    [InlineData("personal")]
    [InlineData("PERSONAL")]
    [InlineData("  Personal  ")]
    public async Task Create_rejects_a_duplicate_name_ignoring_case(string name)
    {
        using var t = await TestDb.CreateAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => t.Lists.CreateAsync(name, "#1E90FF"));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task The_twenty_first_list_is_rejected()
    {
        using var t = await TestDb.CreateAsync();
        for (int i = 3; i <= TodoList.MaxLists; i++) await t.Lists.CreateAsync($"List {i}", "#1E90FF");
        Assert.Equal(TodoList.MaxLists, (await t.Lists.GetAllAsync()).Count);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => t.Lists.CreateAsync("One too many", "#1E90FF"));
        Assert.Contains("at most", ex.Message);
        Assert.Equal(TodoList.MaxLists, (await t.Lists.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Rename_changes_the_name_and_stamps_updated_at()
    {
        using var t = await TestDb.CreateAsync();
        t.Clock.Advance(TimeSpan.FromHours(2));
        await t.Lists.RenameAsync(O, "Day Job");
        var list = (await t.Lists.GetAllAsync()).Single(l => l.Id == O);
        Assert.Equal("Day Job", list.Name);
        Assert.Equal(t.Clock.UtcNow, list.UpdatedAt);
    }

    [Fact]
    public async Task Rename_to_another_lists_name_is_rejected()
    {
        using var t = await TestDb.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.Lists.RenameAsync(O, "personal"));
    }

    [Fact]
    public async Task Rename_may_change_only_the_case_of_its_own_name()
    {
        using var t = await TestDb.CreateAsync();
        await t.Lists.RenameAsync(P, "PERSONAL");
        Assert.Equal("PERSONAL", (await t.Lists.GetAllAsync()).Single(l => l.Id == P).Name);
    }

    [Fact]
    public async Task Rename_of_an_unknown_list_fails()
    {
        using var t = await TestDb.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.Lists.RenameAsync(99, "Nope"));
    }

    [Fact]
    public async Task Set_colour_changes_only_the_colour()
    {
        using var t = await TestDb.CreateAsync();
        await t.Lists.SetColorAsync(P, "#ff7a45");
        var list = (await t.Lists.GetAllAsync()).Single(l => l.Id == P);
        Assert.Equal("#FF7A45", list.ColorHex);
        Assert.Equal("Personal", list.Name);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => t.Lists.SetColorAsync(P, "orange"));
    }

    [Fact]
    public async Task Reorder_persists_the_new_order()
    {
        using var t = await TestDb.CreateAsync();
        var c = await t.Lists.CreateAsync("Third", "#1E90FF");
        await t.Lists.ReorderAsync([c.Id, O, P]);
        Assert.Equal([c.Id, O, P], (await t.Lists.GetAllAsync()).Select(l => l.Id).ToArray());
    }

    [Fact]
    public async Task Reorder_must_name_every_list_exactly_once()
    {
        using var t = await TestDb.CreateAsync();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => t.Lists.ReorderAsync([P]));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => t.Lists.ReorderAsync([P, P]));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => t.Lists.ReorderAsync([P, 99]));
        var ids = (await t.Lists.GetAllAsync()).Select(l => l.Id).ToArray();
        Assert.Equal(new[] { P, O }, ids);
    }

    // ---- delete ----

    [Fact]
    public async Task Delete_with_move_keeps_the_tasks_in_the_target_list_at_the_end_of_each_group()
    {
        using var t = await TestDb.CreateAsync();
        var pHigh = await t.Tasks.AddAsync(P, "p-high", Priority.High);
        var pLow = await t.Tasks.AddAsync(P, "p-low", Priority.Low);
        var pHigh2 = await t.Tasks.AddAsync(P, "p-high2", Priority.High);
        var oHigh = await t.Tasks.AddAsync(O, "o-high", Priority.High);
        await t.Tasks.SetCompletedAsync(pLow.Id, true);

        await t.Lists.DeleteAsync(P, moveTasksToListId: O);

        var ids = (await t.Lists.GetAllAsync()).Select(l => l.Id).ToArray();
        Assert.Equal(new[] { O }, ids);
        var tasks = (await t.Tasks.GetAllAsync()).ToList();
        Assert.Equal(4, tasks.Count);
        Assert.All(tasks, x => Assert.Equal(O, x.ListId));
        var high = tasks.Where(x => x.Priority == Priority.High).OrderBy(x => x.SortOrder).Select(x => x.Id).ToArray();
        Assert.Equal([oHigh.Id, pHigh.Id, pHigh2.Id], high);
        Assert.True(tasks.Single(x => x.Id == pLow.Id).IsCompleted);
    }

    [Fact]
    public async Task Delete_without_move_removes_the_lists_tasks_too()
    {
        using var t = await TestDb.CreateAsync();
        await t.Tasks.AddAsync(P, "doomed", Priority.High);
        var survivor = await t.Tasks.AddAsync(O, "survivor", Priority.High);

        await t.Lists.DeleteAsync(P, moveTasksToListId: null);

        Assert.Equal([survivor.Id], (await t.Tasks.GetAllAsync(includeDeleted: true)).Select(x => x.Id).ToArray());
        Assert.Single(await t.Lists.GetAllAsync());
    }

    [Fact]
    public async Task The_last_remaining_list_cannot_be_deleted()
    {
        using var t = await TestDb.CreateAsync();
        await t.Lists.DeleteAsync(O, moveTasksToListId: P);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => t.Lists.DeleteAsync(P, null));
        Assert.Contains("last remaining list", ex.Message);
        Assert.Single(await t.Lists.GetAllAsync());
    }

    [Fact]
    public async Task Deleting_the_default_list_makes_the_first_remaining_list_the_default()
    {
        using var t = await TestDb.CreateAsync();
        Assert.Equal(P, (await t.Settings.LoadAsync()).DefaultListId);
        await t.Lists.DeleteAsync(P, moveTasksToListId: O);
        Assert.Equal(O, (await t.Settings.LoadAsync()).DefaultListId);
    }

    [Fact]
    public async Task Deleting_another_list_leaves_the_default_alone()
    {
        using var t = await TestDb.CreateAsync();
        await t.Lists.DeleteAsync(O, moveTasksToListId: P);
        Assert.Equal(P, (await t.Settings.LoadAsync()).DefaultListId);
    }

    [Fact]
    public async Task Delete_cannot_move_tasks_into_the_list_being_deleted_or_into_a_missing_list()
    {
        using var t = await TestDb.CreateAsync();
        var task = await t.Tasks.AddAsync(P, "x", Priority.High);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => t.Lists.DeleteAsync(P, P));
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.Lists.DeleteAsync(P, 99));
        // nothing was lost by the failed attempts
        Assert.Equal(2, (await t.Lists.GetAllAsync()).Count);
        Assert.Equal(P, (await t.Tasks.GetAllAsync()).Single(x => x.Id == task.Id).ListId);
    }

    [Fact]
    public async Task Delete_of_an_unknown_list_fails()
    {
        using var t = await TestDb.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.Lists.DeleteAsync(99, null));
    }

    [Fact]
    public async Task Soft_deleted_tasks_also_move_with_their_list()
    {
        using var t = await TestDb.CreateAsync();
        var task = await t.Tasks.AddAsync(P, "undo-able", Priority.High);
        await t.Tasks.SoftDeleteAsync(task.Id);
        await t.Lists.DeleteAsync(P, O);
        await t.Tasks.RestoreAsync(task.Id);
        Assert.Equal(O, (await t.Tasks.GetAllAsync()).Single().ListId);
    }
}
