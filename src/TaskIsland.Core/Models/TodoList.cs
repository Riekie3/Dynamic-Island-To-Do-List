namespace TaskIsland.Core.Models;

/// <summary>A named, coloured group of tasks (for example "Office Work" or "Personal").</summary>
public sealed class TodoList
{
    public const int MaxNameLength = 40;
    public const int MaxLists = 20;
    public const string DefaultColor = "#1E90FF";

    /// <summary>The 8 swatches offered in the UI.</summary>
    public static readonly IReadOnlyList<string> Palette =
    [
        "#1E90FF", "#A970FF", "#22C1C3", "#3DDC84", "#FFB020", "#FF7A45", "#FF5CA8", "#8C97A8",
    ];

    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string ColorHex { get; set; } = DefaultColor;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
