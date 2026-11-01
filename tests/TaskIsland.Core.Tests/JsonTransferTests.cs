using System.Text;
using System.Text.Json;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;
using static TaskIsland.Core.Tests.TestData;

namespace TaskIsland.Core.Tests;

public class JsonTransferTests
{
    private static TransferData Parse(string json) => JsonTransfer.Parse(json, T0);

    private static string Doc(string tasks = "[]", string lists = "[{\"name\":\"Personal\",\"color\":\"#1E90FF\"}]",
        string app = "\"TaskIsland\"", string version = "1") =>
        $$"""{"app":{{app}},"formatVersion":{{version}},"lists":{{lists}},"tasks":{{tasks}}}""";

    private static string OneTask(string fields) => $"[{{{fields}}}]";

    private static ImportValidationException Fails(string json) =>
        Assert.Throws<ImportValidationException>(() => Parse(json));

    // ---- round trip ----

    [Fact]
    public void Export_then_parse_keeps_lists_and_task_to_list_mapping()
    {
        var lists = new[] { Personal, Office };
        var tasks = new[]
        {
            Task(1, "Buy milk", Priority.Low, listId: 1, sortOrder: 0, due: new DateTime(2026, 11, 5), hasTime: false),
            Task(2, "Quarterly report", Priority.High, listId: 2, sortOrder: 3, done: true, completedAt: T0.AddHours(2),
                due: new DateTime(2026, 11, 6, 14, 30, 0), hasTime: true),
        };
        tasks[0].Description = "2 litres";

        var json = JsonTransfer.Export(lists, tasks, T0);
        var data = Parse(json);

        Assert.Equal(["Personal", "Office Work"], data.Lists.Select(l => l.Name).ToArray());
        Assert.Equal("#A970FF", data.Lists[1].Color);
        Assert.Equal(2, data.Tasks.Count);

        var milk = data.Tasks.Single(t => t.Title == "Buy milk");
        Assert.Equal("Personal", milk.List);
        Assert.Equal(Priority.Low, milk.Priority);
        Assert.Equal("2 litres", milk.Description);
        Assert.Equal(new DateTime(2026, 11, 5), milk.DueDate);
        Assert.False(milk.DueHasTime);

        var report = data.Tasks.Single(t => t.Title == "Quarterly report");
        Assert.Equal("Office Work", report.List);
        Assert.True(report.IsCompleted);
        Assert.Equal(T0.AddHours(2), report.CompletedAt);
        Assert.Equal(new DateTime(2026, 11, 6, 14, 30, 0), report.DueDate);
        Assert.True(report.DueHasTime);
        Assert.Equal(T0.AddMinutes(2), report.CreatedAt);
        Assert.Equal(3, report.SortOrder);
    }

    [Fact]
    public void Export_writes_the_documented_header()
    {
        using var doc = JsonDocument.Parse(JsonTransfer.Export([Personal], [], T0));
        Assert.Equal("TaskIsland", doc.RootElement.GetProperty("app").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("formatVersion").GetInt32());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("tasks").ValueKind);
    }

    [Fact]
    public void Export_single_list_only_includes_that_list()
    {
        var tasks = new[] { Task(1, "A", listId: 1), Task(2, "B", listId: 2) };
        var data = Parse(JsonTransfer.Export([Personal, Office], tasks, T0, onlyListId: 2));
        Assert.Equal(["Office Work"], data.Lists.Select(l => l.Name).ToArray());
        Assert.Equal(["B"], data.Tasks.Select(t => t.Title).ToArray());
    }

    [Fact]
    public void Export_leaves_out_deleted_tasks()
    {
        var tasks = new[] { Task(1, "Keep"), Task(2, "Gone", deletedAt: T0) };
        var data = Parse(JsonTransfer.Export([Personal], tasks, T0));
        Assert.Equal(["Keep"], data.Tasks.Select(t => t.Title).ToArray());
    }

    // ---- accepted shapes ----

    [Fact]
    public void Minimal_task_gets_sensible_defaults()
    {
        var data = Parse(Doc(OneTask("\"title\":\"  Pay rent  \"")));
        var t = Assert.Single(data.Tasks);
        Assert.Equal("Pay rent", t.Title);
        Assert.Equal(Priority.Medium, t.Priority);
        Assert.False(t.IsCompleted);
        Assert.Null(t.List);
        Assert.Equal(T0, t.CreatedAt);
    }

    [Theory]
    [InlineData("high", Priority.High)]
    [InlineData("MEDIUM", Priority.Medium)]
    [InlineData("Low", Priority.Low)]
    public void Priority_is_case_insensitive(string text, Priority expected)
    {
        var data = Parse(Doc(OneTask($"\"title\":\"x\",\"priority\":\"{text}\"")));
        Assert.Equal(expected, data.Tasks[0].Priority);
    }

    [Fact]
    public void Task_list_is_matched_ignoring_case()
    {
        var data = Parse(Doc(OneTask("\"title\":\"x\",\"list\":\"personal\"")));
        Assert.Equal("personal", data.Tasks[0].List);
    }

    [Fact]
    public void A_file_without_lists_is_accepted_when_tasks_have_no_list()
    {
        var json = """{"app":"TaskIsland","formatVersion":1,"tasks":[{"title":"x"}]}""";
        var data = Parse(json);
        Assert.Empty(data.Lists);
        Assert.Single(data.Tasks);
    }

    [Fact]
    public void Colour_is_normalised_to_upper_case()
    {
        var data = Parse(Doc(lists: "[{\"name\":\"X\",\"color\":\"#abcdef\"}]"));
        Assert.Equal("#ABCDEF", data.Lists[0].Color);
    }

    // ---- rejected files ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_file_is_rejected(string json) => Assert.Contains("empty", Fails(json).Message);

    [Fact]
    public void Broken_json_is_rejected() => Assert.Contains("not valid JSON", Fails("{ nope").Message);

    [Fact]
    public void Root_must_be_an_object() => Assert.Contains("not a TaskIsland export", Fails("[1,2]").Message);

    [Theory]
    [InlineData("\"OtherApp\"")]
    [InlineData("42")]
    public void Wrong_app_name_is_rejected(string app) => Fails(Doc(app: app));

    [Theory]
    [InlineData("2")]
    [InlineData("0")]
    [InlineData("\"1\"")]
    public void Unsupported_format_version_is_rejected(string version) => Fails(Doc(version: version));

    [Fact]
    public void Missing_format_version_is_rejected() =>
        Fails("""{"app":"TaskIsland","tasks":[]}""");

    [Fact]
    public void Tasks_must_be_an_array()
    {
        Fails("""{"app":"TaskIsland","formatVersion":1,"tasks":{}}""");
        Fails("""{"app":"TaskIsland","formatVersion":1}""");
    }

    [Fact]
    public void More_than_ten_thousand_tasks_is_rejected()
    {
        var tasks = "[" + string.Join(",", Enumerable.Repeat("{\"title\":\"x\"}", JsonTransfer.MaxTasks + 1)) + "]";
        Assert.Contains("10,000", Fails(Doc(tasks)).Message.Replace("10 000", "10,000"));
    }

    [Fact]
    public void Exactly_ten_thousand_tasks_is_fine()
    {
        var tasks = "[" + string.Join(",", Enumerable.Repeat("{\"title\":\"x\"}", JsonTransfer.MaxTasks)) + "]";
        Assert.Equal(JsonTransfer.MaxTasks, Parse(Doc(tasks)).Tasks.Count);
    }

    [Fact]
    public void File_over_ten_megabytes_is_rejected()
    {
        var big = new string('x', JsonTransfer.MaxFileBytes + 1);
        Assert.Contains("10 MB", Fails(Doc(OneTask($"\"title\":\"a\",\"description\":\"{big}\""))).Message);
    }

    [Theory]
    [InlineData("\"title\":\"\"")]
    [InlineData("\"title\":\"   \"")]
    [InlineData("\"description\":\"no title\"")]
    [InlineData("\"title\":5")]
    public void Missing_or_blank_title_is_rejected(string fields) => Fails(Doc(OneTask(fields)));

    [Fact]
    public void Title_over_500_characters_is_rejected() =>
        Fails(Doc(OneTask($"\"title\":\"{new string('a', 501)}\"")));

    [Fact]
    public void Title_of_exactly_500_characters_is_fine() =>
        Assert.Single(Parse(Doc(OneTask($"\"title\":\"{new string('a', 500)}\""))).Tasks);

    [Theory]
    [InlineData("Urgent")]
    [InlineData("1")]
    [InlineData("")]
    public void Unknown_priority_is_rejected(string priority) =>
        Fails(Doc(OneTask($"\"title\":\"x\",\"priority\":\"{priority}\"")));

    [Theory]
    [InlineData("\"createdAt\":\"not a date\"")]
    [InlineData("\"createdAt\":12345")]
    [InlineData("\"completedAt\":\"tomorrow-ish\"")]
    [InlineData("\"dueDate\":\"31/12/2026\"")]
    [InlineData("\"dueDate\":\"soon\"")]
    public void Bad_dates_are_rejected(string field) => Fails(Doc(OneTask($"\"title\":\"x\",{field}")));

    [Fact]
    public void Task_pointing_at_an_unknown_list_is_rejected()
    {
        var ex = Fails(Doc(OneTask("\"title\":\"x\",\"list\":\"Nowhere\"")));
        Assert.Contains("Nowhere", ex.Message);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#FFF")]
    [InlineData("#GGGGGG")]
    [InlineData("1E90FF")]
    public void Bad_colour_is_rejected(string color) =>
        Fails(Doc(lists: $"[{{\"name\":\"X\",\"color\":\"{color}\"}}]"));

    [Fact]
    public void Duplicate_list_names_are_rejected_ignoring_case() =>
        Fails(Doc(lists: "[{\"name\":\"Work\",\"color\":\"#1E90FF\"},{\"name\":\"work\",\"color\":\"#1E90FF\"}]"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_list_name_is_rejected(string name) =>
        Fails(Doc(lists: $"[{{\"name\":\"{name}\",\"color\":\"#1E90FF\"}}]"));

    [Fact]
    public void List_name_over_40_characters_is_rejected() =>
        Fails(Doc(lists: $"[{{\"name\":\"{new string('n', 41)}\",\"color\":\"#1E90FF\"}}]"));

    [Fact]
    public void More_than_twenty_lists_is_rejected()
    {
        var lists = "[" + string.Join(",", Enumerable.Range(1, 21).Select(i => $"{{\"name\":\"L{i}\",\"color\":\"#1E90FF\"}}")) + "]";
        Fails(Doc(lists: lists));
    }

    [Fact]
    public void Notes_over_2000_characters_are_rejected() =>
        Fails(Doc(OneTask($"\"title\":\"x\",\"description\":\"{new string('d', 2001)}\"")));

    [Fact]
    public void One_bad_task_rejects_the_whole_file()
    {
        // The first task is fine, the second is not: nothing may be returned.
        Fails(Doc("[{\"title\":\"good\"},{\"title\":\"\"}]"));
    }

    [Fact]
    public void Validation_messages_are_readable_text()
    {
        var ex = Fails(Doc(OneTask("\"title\":\"Call mum\",\"priority\":\"Urgent\"")));
        Assert.Contains("Call mum", ex.Message);
        Assert.Contains("High, Medium or Low", ex.Message);
    }

    [Fact]
    public void Utf8_is_preserved()
    {
        var json = JsonTransfer.Export([Personal], [Task(1, "Beli kuih 🍰 untuk 妈妈")], T0);
        Assert.Equal("Beli kuih 🍰 untuk 妈妈", Parse(json).Tasks[0].Title);
        Assert.NotEmpty(Encoding.UTF8.GetBytes(json));
    }
}
