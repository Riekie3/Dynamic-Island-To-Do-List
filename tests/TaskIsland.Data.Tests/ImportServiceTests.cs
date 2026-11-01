using TaskIsland.Core.Models;
using TaskIsland.Core.Services;

namespace TaskIsland.Data.Tests;

public class ImportServiceTests
{
    private const long P = TestDb.PersonalId;
    private const long O = TestDb.OfficeId;
    private static readonly DateTime Created = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static TransferTask T(string title, string? list = null, Priority prio = Priority.Medium, bool done = false,
        DateTime? created = null, int order = 0) =>
        new(list, title, null, prio, done, created ?? Created, created ?? Created, done ? (created ?? Created).AddHours(1) : null,
            null, false, order);

    private static TransferData Data(TransferList[]? lists = null, params TransferTask[] tasks) =>
        new(lists ?? [], tasks);

    // ---- merge ----

    [Fact]
    public async Task Merge_creates_missing_lists_and_matches_existing_ones_ignoring_case()
    {
        using var t = await TestDb.CreateAsync();
        var data = Data(
            [new("office work", "#FFFFFF", 0), new("Project Alpha", "#22C1C3", 1)],
            T("a", "OFFICE WORK"), T("b", "Project Alpha"));

        var summary = await t.Import.ImportAsync(data, ImportMode.Merge);

        Assert.Equal(new ImportSummary(2, 0, 1), summary);
        var lists = await t.Lists.GetAllAsync();
        Assert.Equal(["Personal", "Office Work", "Project Alpha"], lists.Select(l => l.Name).ToArray());
        Assert.Equal("#A970FF", lists[1].ColorHex);   // the existing list keeps its own colour
        var alpha = lists[2];
        var tasks = await t.Tasks.GetAllAsync();
        Assert.Equal(O, tasks.Single(x => x.Title == "a").ListId);
        Assert.Equal(alpha.Id, tasks.Single(x => x.Title == "b").ListId);
    }

    [Fact]
    public async Task Merge_keeps_timestamps_priority_and_completion()
    {
        using var t = await TestDb.CreateAsync();
        var created = new DateTime(2025, 12, 24, 10, 30, 0, DateTimeKind.Utc);
        await t.Import.ImportAsync(Data(null, T("old one", "Personal", Priority.High, done: true, created: created)), ImportMode.Merge);

        var task = (await t.Tasks.GetAllAsync()).Single();
        Assert.Equal(created, task.CreatedAt);
        Assert.Equal(Priority.High, task.Priority);
        Assert.True(task.IsCompleted);
        Assert.Equal(created.AddHours(1), task.CompletedAt);
    }

    [Fact]
    public async Task Merge_skips_tasks_that_already_exist_so_importing_twice_adds_nothing()
    {
        using var t = await TestDb.CreateAsync();
        var data = Data(null, T("one", "Personal"), T("two", "Personal", created: Created.AddMinutes(1)));

        Assert.Equal(new ImportSummary(2, 0, 0), await t.Import.ImportAsync(data, ImportMode.Merge));
        Assert.Equal(new ImportSummary(0, 2, 0), await t.Import.ImportAsync(data, ImportMode.Merge));
        Assert.Equal(2, (await t.Tasks.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Merge_treats_the_same_title_with_a_different_creation_time_as_a_new_task()
    {
        using var t = await TestDb.CreateAsync();
        await t.Import.ImportAsync(Data(null, T("same title", "Personal")), ImportMode.Merge);
        var summary = await t.Import.ImportAsync(Data(null, T("same title", "Personal", created: Created.AddDays(1))), ImportMode.Merge);
        Assert.Equal(1, summary.TasksImported);
    }

    [Fact]
    public async Task Merge_skips_duplicates_inside_the_file_itself()
    {
        using var t = await TestDb.CreateAsync();
        var summary = await t.Import.ImportAsync(Data(null, T("twin", "Personal"), T("twin", "Personal")), ImportMode.Merge);
        Assert.Equal(new ImportSummary(1, 1, 0), summary);
    }

    [Fact]
    public async Task Merge_puts_tasks_without_a_list_into_the_default_list()
    {
        using var t = await TestDb.CreateAsync();
        await t.Settings.SetAsync(AppSettings.Keys.DefaultListId, O.ToString());
        await t.Import.ImportAsync(Data(null, T("homeless")), ImportMode.Merge);
        Assert.Equal(O, (await t.Tasks.GetAllAsync()).Single().ListId);
    }

    [Fact]
    public async Task Merge_falls_back_to_the_first_list_when_the_default_list_is_gone()
    {
        using var t = await TestDb.CreateAsync();
        await t.Settings.SetAsync(AppSettings.Keys.DefaultListId, "999");
        await t.Import.ImportAsync(Data(null, T("homeless")), ImportMode.Merge);
        Assert.Equal(P, (await t.Tasks.GetAllAsync()).Single().ListId);
    }

    [Fact]
    public async Task Merge_appends_after_the_existing_tasks_of_the_same_group()
    {
        using var t = await TestDb.CreateAsync();
        var existing = await t.Tasks.AddAsync(P, "existing", Priority.High);
        await t.Import.ImportAsync(Data(null, T("imported", "Personal", Priority.High)), ImportMode.Merge);
        var order = (await t.Tasks.GetAllAsync()).OrderBy(x => x.SortOrder).Select(x => x.Title).ToArray();
        Assert.Equal(["existing", "imported"], order);
        Assert.Equal(existing.Id, (await t.Tasks.GetAllAsync()).OrderBy(x => x.SortOrder).First().Id);
    }

    [Fact]
    public async Task Merge_that_would_exceed_twenty_lists_changes_nothing()
    {
        using var t = await TestDb.CreateAsync();
        var lists = Enumerable.Range(1, 19).Select(i => new TransferList($"New {i}", "#1E90FF", i)).ToArray();
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.Import.ImportAsync(Data(lists, T("x")), ImportMode.Merge));
        Assert.Equal(2, (await t.Lists.GetAllAsync()).Count);
        Assert.Empty(await t.Tasks.GetAllAsync());
    }

    // ---- replace ----

    [Fact]
    public async Task Replace_swaps_all_lists_and_tasks_for_the_imported_ones()
    {
        using var t = await TestDb.CreateAsync();
        await t.Tasks.AddAsync(P, "old task", Priority.High);

        var data = Data(
            [new("Home", "#3DDC84", 0), new("Work", "#FFB020", 1)],
            T("buy bread", "Home"), T("send invoice", "Work", Priority.High), T("call bank", "Work", Priority.Low));
        var summary = await t.Import.ImportAsync(data, ImportMode.Replace);

        Assert.Equal(new ImportSummary(3, 0, 2), summary);
        var lists = await t.Lists.GetAllAsync();
        Assert.Equal(["Home", "Work"], lists.Select(l => l.Name).ToArray());
        Assert.Equal(["#3DDC84", "#FFB020"], lists.Select(l => l.ColorHex).ToArray());
        var tasks = await t.Tasks.GetAllAsync();
        Assert.Equal(3, tasks.Count);
        Assert.DoesNotContain(tasks, x => x.Title == "old task");
        Assert.Equal(lists[1].Id, tasks.Single(x => x.Title == "send invoice").ListId);
    }

    [Fact]
    public async Task Replace_resets_the_default_and_selected_list()
    {
        using var t = await TestDb.CreateAsync();
        await t.Settings.SetAsync(AppSettings.Keys.SelectedListId, O.ToString());
        await t.Import.ImportAsync(Data([new("Only", "#1E90FF", 0)], T("x", "Only")), ImportMode.Replace);

        var only = (await t.Lists.GetAllAsync()).Single();
        var s = await t.Settings.LoadAsync();
        Assert.Equal(only.Id, s.DefaultListId);
        Assert.Equal(0, s.SelectedListId);
    }

    [Fact]
    public async Task Replace_without_any_lists_keeps_at_least_one_list()
    {
        using var t = await TestDb.CreateAsync();
        await t.Import.ImportAsync(Data(null, T("floating")), ImportMode.Replace);
        var list = Assert.Single(await t.Lists.GetAllAsync());
        Assert.Equal("Personal", list.Name);
        Assert.Equal(list.Id, (await t.Tasks.GetAllAsync()).Single().ListId);
    }

    [Fact]
    public async Task Replace_keeps_the_order_of_tasks_within_each_group()
    {
        using var t = await TestDb.CreateAsync();
        var data = Data([new("L", "#1E90FF", 0)],
            T("third", "L", order: 30), T("first", "L", order: 10), T("second", "L", order: 20));
        await t.Import.ImportAsync(data, ImportMode.Replace);
        var titles = (await t.Tasks.GetAllAsync()).OrderBy(x => x.SortOrder).Select(x => x.Title).ToArray();
        Assert.Equal(["first", "second", "third"], titles);
    }

    // ---- full round trip through JSON ----

    [Fact]
    public async Task Export_clear_import_gives_back_the_same_tasks()
    {
        using var source = await TestDb.CreateAsync();
        await source.Lists.CreateAsync("Project Alpha", "#FF5CA8");
        var alpha = (await source.Lists.GetAllAsync()).Single(l => l.Name == "Project Alpha");
        var a = await source.Tasks.AddAsync(P, "Pay rent", Priority.High, new DateTime(2026, 12, 1), false, "before the 5th");
        await source.Tasks.AddAsync(O, "Board slides", Priority.Medium, new DateTime(2026, 12, 3, 14, 0, 0), true);
        var c = await source.Tasks.AddAsync(alpha.Id, "Ship v1", Priority.Low);
        await source.Tasks.SetCompletedAsync(c.Id, true);
        await source.Tasks.SetCompletedAsync(a.Id, false);

        var json = JsonTransfer.Export(await source.Lists.GetAllAsync(), await source.Tasks.GetAllAsync(), source.Clock.UtcNow);

        using var target = await TestDb.CreateAsync();
        await target.Tasks.AddAsync(P, "will be replaced", Priority.High);
        await target.Import.ImportAsync(JsonTransfer.Parse(json, target.Clock.UtcNow), ImportMode.Replace);

        var lists = await target.Lists.GetAllAsync();
        Assert.Equal(["Personal", "Office Work", "Project Alpha"], lists.Select(l => l.Name).ToArray());
        Assert.Equal("#FF5CA8", lists[2].ColorHex);

        var back = await target.Tasks.GetAllAsync();
        Assert.Equal(["Pay rent", "Board slides", "Ship v1"], back.OrderBy(x => x.Id).Select(x => x.Title).ToArray());
        var rent = back.Single(x => x.Title == "Pay rent");
        Assert.Equal("before the 5th", rent.Description);
        Assert.Equal(Priority.High, rent.Priority);
        Assert.Equal(new DateTime(2026, 12, 1), rent.DueDate);
        Assert.False(rent.DueHasTime);
        var slides = back.Single(x => x.Title == "Board slides");
        Assert.Equal(new DateTime(2026, 12, 3, 14, 0, 0), slides.DueDate);
        Assert.True(slides.DueHasTime);
        Assert.Equal(lists[1].Id, slides.ListId);
        var ship = back.Single(x => x.Title == "Ship v1");
        Assert.True(ship.IsCompleted);
        Assert.Equal(lists[2].Id, ship.ListId);
    }

    [Fact]
    public async Task Exporting_one_list_and_merging_it_elsewhere_works()
    {
        using var source = await TestDb.CreateAsync();
        await source.Tasks.AddAsync(P, "personal stuff", Priority.Medium);
        await source.Tasks.AddAsync(O, "office stuff", Priority.Medium);
        var json = JsonTransfer.Export(await source.Lists.GetAllAsync(), await source.Tasks.GetAllAsync(), source.Clock.UtcNow, O);

        using var target = await TestDb.CreateAsync();
        var summary = await target.Import.ImportAsync(JsonTransfer.Parse(json, target.Clock.UtcNow), ImportMode.Merge);

        Assert.Equal(1, summary.TasksImported);
        Assert.Equal(["office stuff"], (await target.Tasks.GetAllAsync()).Select(x => x.Title).ToArray());
    }
}
