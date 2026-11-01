using System.Globalization;

namespace TaskIsland.Core.Services;

/// <summary>ISO-8601 helpers shared by the data layer and JSON transfer (PLAN.md section 3.3).</summary>
public static class IsoDate
{
    public const string LocalFormat = "yyyy-MM-ddTHH:mm:ss";
    public const string DateOnlyFormat = "yyyy-MM-dd";

    /// <summary>Round-trip ("o") text of the UTC instant.</summary>
    public static string ToUtcString(DateTime value) => value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    public static bool TryParseUtc(string? text, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var v)) return false;
        utc = DateTime.SpecifyKind(v, DateTimeKind.Utc);
        return true;
    }

    /// <summary>Local wall-clock text, no offset. Date-only values drop the time part.</summary>
    public static string ToLocalString(DateTime value, bool hasTime) =>
        value.ToString(hasTime ? LocalFormat : DateOnlyFormat, CultureInfo.InvariantCulture);

    public static bool TryParseLocal(string? text, out DateTime local)
    {
        local = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!DateTime.TryParseExact(text, [LocalFormat, DateOnlyFormat], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var v)) return false;
        local = DateTime.SpecifyKind(v, DateTimeKind.Unspecified);
        return true;
    }
}
