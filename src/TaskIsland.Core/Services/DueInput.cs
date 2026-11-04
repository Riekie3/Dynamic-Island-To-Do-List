using System.Globalization;

namespace TaskIsland.Core.Services;

/// <summary>Turns the inline-edit "Due" fields (a date and an optional "HH:mm" text) into a stored due value (PLAN.md section 6.5).</summary>
public static class DueInput
{
    /// <summary>Accepts "9:30", "09:30", "17:05" and "9.30"; rejects anything else, including "24:00".</summary>
    public static bool TryParseTime(string? text, out TimeSpan time)
    {
        time = default;
        var s = (text ?? "").Trim().Replace('.', ':');
        if (s.Length == 0) return false;
        var parts = s.Split(':');
        if (parts.Length != 2) return false;
        if (parts[0].Length is < 1 or > 2 || parts[1].Length != 2) return false;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int h)) return false;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int m)) return false;
        if (h > 23 || m > 59) return false;
        time = new TimeSpan(h, m, 0);
        return true;
    }

    /// <summary>"09:30" for the time box.</summary>
    public static string FormatTime(DateTime due) => due.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Combines the fields. No date means no due (a time without a date is an error). An empty time means a date-only due.
    /// Returns an error message, or null when the fields are valid.
    /// </summary>
    public static string? Compose(DateTime? date, string? timeText, out DateTime? due, out bool hasTime)
    {
        due = null;
        hasTime = false;
        bool timeGiven = !string.IsNullOrWhiteSpace(timeText);
        if (date is null)
            return timeGiven ? "Pick a date for the time, or clear the time." : null;
        if (!timeGiven)
        {
            due = date.Value.Date;
            return null;
        }
        if (!TryParseTime(timeText, out var t))
            return "Time must look like 09:30 (24-hour).";
        due = date.Value.Date + t;
        hasTime = true;
        return null;
    }
}
