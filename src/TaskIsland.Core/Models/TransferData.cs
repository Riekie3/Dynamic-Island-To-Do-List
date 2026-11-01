namespace TaskIsland.Core.Models;

/// <summary>A list as it appears in an export file (referenced by name, never by database id).</summary>
public sealed record TransferList(string Name, string Color, int SortOrder);

/// <summary>A task as it appears in an export file. <see cref="List"/> null means "the default list".</summary>
public sealed record TransferTask(
    string? List,
    string Title,
    string? Description,
    Priority Priority,
    bool IsCompleted,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt,
    DateTime? DueDate,
    bool DueHasTime,
    int SortOrder);

/// <summary>Validated contents of a TaskIsland JSON export.</summary>
public sealed record TransferData(IReadOnlyList<TransferList> Lists, IReadOnlyList<TransferTask> Tasks);

public enum ImportMode { Merge, Replace }

public sealed record ImportSummary(int TasksImported, int DuplicatesSkipped, int ListsCreated);
