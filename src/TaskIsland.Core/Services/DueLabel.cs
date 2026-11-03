using System.Globalization;

namespace TaskIsland.Core.Services;

/// <summary>The due label on a task row (PLAN.md section 6.4): "Today 5:00 PM", "Overdue · Yesterday", "Mon", "3 Nov".</summary>
public static class DueLabel
{
    /// <summary>A date-only due counts as due at 09:00 local (PLAN.md section 3.3).</summary>
    public static readonly TimeSpan DateOnlyDueTime = TimeSpan.FromHours(9);

    public static DateTime DueMoment(DateTime due, bool hasTime) => hasTime ? due : due.Date + DateOnlyDueTime;

    public static bool IsOverdue(DateTime due, bool hasTime, bool isCompleted, DateTime nowLocal) =>
        !isCompleted && DueMoment(due, hasTime) < nowLocal;

    public static string Format(DateTime due, bool hasTime, bool isCompleted, DateTime nowLocal)
    {
        var c = CultureInfo.InvariantCulture;
        int days = (due.Date - nowLocal.Date).Days;
        string day = days switch
        {
            0 => "Today",
            1 => "Tomorrow",
            -1 => "Yesterday",
            > 1 and < 7 => due.ToString("ddd", c),
            _ when due.Year == nowLocal.Year => due.ToString("d MMM", c),
            _ => due.ToString("d MMM yyyy", c),
        };
        string text = hasTime ? $"{day} {due.ToString("h:mm tt", c)}" : day;
        return IsOverdue(due, hasTime, isCompleted, nowLocal) ? $"Overdue · {text}" : text;
    }
}
