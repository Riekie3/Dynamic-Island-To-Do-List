using TaskIsland.Core.Services;

namespace TaskIsland.Core.Tests;

public class DueLabelTests
{
    // Monday 2 Nov 2026, 10:30 local.
    private static readonly DateTime Now = new(2026, 11, 2, 10, 30, 0);

    [Fact]
    public void Today_with_time_in_the_future()
    {
        Assert.Equal("Today 5:00 PM", DueLabel.Format(new DateTime(2026, 11, 2, 17, 0, 0), true, false, Now));
    }

    [Fact]
    public void Yesterday_date_only_is_overdue()
    {
        Assert.Equal("Overdue · Yesterday", DueLabel.Format(new DateTime(2026, 11, 1), false, false, Now));
    }

    [Fact]
    public void Date_only_today_counts_as_09_00_so_it_is_overdue_at_10_30()
    {
        Assert.True(DueLabel.IsOverdue(new DateTime(2026, 11, 2), false, false, Now));
        Assert.False(DueLabel.IsOverdue(new DateTime(2026, 11, 2), false, false, Now.Date.AddHours(8)));
    }

    [Fact]
    public void Completed_tasks_are_never_overdue()
    {
        Assert.Equal("Yesterday", DueLabel.Format(new DateTime(2026, 11, 1), false, true, Now));
    }

    [Theory]
    [InlineData(2026, 11, 3, "Tomorrow")]
    [InlineData(2026, 11, 5, "Thu")]
    [InlineData(2026, 11, 20, "20 Nov")]
    [InlineData(2027, 1, 4, "4 Jan 2027")]
    public void Future_days(int y, int m, int d, string expected)
    {
        Assert.Equal(expected, DueLabel.Format(new DateTime(y, m, d), false, false, Now));
    }
}
