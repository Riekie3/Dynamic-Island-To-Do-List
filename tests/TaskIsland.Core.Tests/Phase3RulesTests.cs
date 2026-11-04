using TaskIsland.Core.Mascot;
using TaskIsland.Core.Services;

namespace TaskIsland.Core.Tests;

public class DueInputTests
{
    [Theory]
    [InlineData("09:30", 9, 30)]
    [InlineData("9:30", 9, 30)]
    [InlineData("17:05", 17, 5)]
    [InlineData("9.30", 9, 30)]
    [InlineData(" 00:00 ", 0, 0)]
    [InlineData("23:59", 23, 59)]
    public void Valid_times_parse(string text, int h, int m)
    {
        Assert.True(DueInput.TryParseTime(text, out var t));
        Assert.Equal(new TimeSpan(h, m, 0), t);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("24:00")]
    [InlineData("12:60")]
    [InlineData("9")]
    [InlineData("9:5")]
    [InlineData("ab:cd")]
    [InlineData("-1:30")]
    [InlineData("1:2:3")]
    [InlineData("123:30")]
    public void Invalid_times_are_rejected(string? text) => Assert.False(DueInput.TryParseTime(text, out _));

    [Fact]
    public void No_date_and_no_time_means_no_due()
    {
        Assert.Null(DueInput.Compose(null, "", out var due, out var hasTime));
        Assert.Null(due);
        Assert.False(hasTime);
    }

    [Fact]
    public void A_time_without_a_date_is_an_error()
    {
        Assert.NotNull(DueInput.Compose(null, "09:30", out var due, out _));
        Assert.Null(due);
    }

    [Fact]
    public void A_date_without_a_time_is_date_only()
    {
        Assert.Null(DueInput.Compose(new DateTime(2026, 11, 5, 14, 22, 0), "  ", out var due, out var hasTime));
        Assert.Equal(new DateTime(2026, 11, 5), due);
        Assert.False(hasTime);
    }

    [Fact]
    public void A_date_with_a_time_keeps_both()
    {
        Assert.Null(DueInput.Compose(new DateTime(2026, 11, 5), "17:30", out var due, out var hasTime));
        Assert.Equal(new DateTime(2026, 11, 5, 17, 30, 0), due);
        Assert.True(hasTime);
    }

    [Fact]
    public void A_bad_time_gives_a_message_and_no_due()
    {
        var msg = DueInput.Compose(new DateTime(2026, 11, 5), "25:00", out var due, out var hasTime);
        Assert.NotNull(msg);
        Assert.Null(due);
        Assert.False(hasTime);
    }

    [Fact]
    public void FormatTime_is_24_hour()
    {
        Assert.Equal("07:05", DueInput.FormatTime(new DateTime(2026, 1, 1, 7, 5, 0)));
        Assert.Equal("17:30", DueInput.FormatTime(new DateTime(2026, 1, 1, 17, 30, 0)));
    }
}

public class ReorderRulesTests
{
    private static readonly long[] Ids = [10, 20, 30, 40];

    [Theory]
    [InlineData(10L, 2, new long[] { 20, 10, 30, 40 })]   // drop between 20 and 30
    [InlineData(10L, 4, new long[] { 20, 30, 40, 10 })]   // drop after the last
    [InlineData(40L, 0, new long[] { 40, 10, 20, 30 })]   // drop before the first
    [InlineData(30L, 1, new long[] { 10, 30, 20, 40 })]
    public void Move_puts_the_task_at_the_drop_position(long moving, int insertIndex, long[] expected)
    {
        Assert.Equal(expected, ReorderRules.Move(Ids, moving, insertIndex));
    }

    [Theory]
    [InlineData(20L, 1)]  // directly above itself
    [InlineData(20L, 2)]  // directly below itself
    [InlineData(10L, 0)]
    [InlineData(40L, 4)]
    public void Dropping_next_to_itself_changes_nothing(long moving, int insertIndex)
    {
        Assert.Null(ReorderRules.Move(Ids, moving, insertIndex));
    }

    [Fact]
    public void Unknown_id_and_out_of_range_index_are_handled()
    {
        Assert.Null(ReorderRules.Move(Ids, 99, 1));
        Assert.Equal([20L, 30, 40, 10], ReorderRules.Move(Ids, 10, 99));
        Assert.Equal([40L, 10, 20, 30], ReorderRules.Move(Ids, 40, -5));
    }

    [Fact]
    public void Move_does_not_change_the_input()
    {
        long[] copy = [.. Ids];
        ReorderRules.Move(Ids, 10, 3);
        Assert.Equal(copy, Ids);
    }

    [Theory]
    [InlineData(20L, -1, new long[] { 20, 10, 30, 40 })]
    [InlineData(20L, 1, new long[] { 10, 30, 20, 40 })]
    public void Step_swaps_with_the_neighbour(long moving, int dir, long[] expected)
    {
        Assert.Equal(expected, ReorderRules.Step(Ids, moving, dir));
    }

    [Fact]
    public void Step_at_the_ends_does_nothing()
    {
        Assert.Null(ReorderRules.Step(Ids, 10, -1));
        Assert.Null(ReorderRules.Step(Ids, 40, 1));
        Assert.Null(ReorderRules.Step(Ids, 99, 1));
    }
}

public class TickStreakTests
{
    private static readonly DateTime T0 = new(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Fifth_tick_inside_thirty_minutes_triggers_then_resets()
    {
        var s = new TickStreak();
        for (int i = 0; i < 4; i++) Assert.False(s.Record(T0.AddMinutes(i * 5)));
        Assert.True(s.Record(T0.AddMinutes(25)));
        Assert.False(s.Record(T0.AddMinutes(26)));
    }

    [Fact]
    public void Ticks_older_than_the_window_do_not_count()
    {
        var s = new TickStreak();
        for (int i = 0; i < 4; i++) s.Record(T0.AddMinutes(i));
        Assert.False(s.Record(T0.AddMinutes(40)));   // the first four expired
    }

    [Fact]
    public void Undo_takes_the_last_tick_back()
    {
        var s = new TickStreak();
        for (int i = 0; i < 4; i++) s.Record(T0.AddMinutes(i));
        s.Undo();
        Assert.False(s.Record(T0.AddMinutes(5)));    // only four now
        Assert.True(s.Record(T0.AddMinutes(6)));
    }
}
