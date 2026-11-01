namespace TaskIsland.Core.Models;

public sealed class TaskItem
{
    public const int MaxTitleLength = 500;
    public const int MaxDescriptionLength = 2000;

    public long Id { get; set; }
    public long ListId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;
    public bool IsCompleted { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>UTC.</summary>
    public DateTime UpdatedAt { get; set; }
    /// <summary>UTC.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Local wall-clock due date/time (no time zone). Date-only values have <see cref="DueHasTime"/> false.</summary>
    public DateTime? DueDate { get; set; }
    public bool DueHasTime { get; set; }

    /// <summary>UTC. Set once the reminder for the current due date has been shown.</summary>
    public DateTime? ReminderShownAt { get; set; }

    /// <summary>Order within (ListId, Priority).</summary>
    public int SortOrder { get; set; }

    /// <summary>UTC. Non-null means the task is soft-deleted (waiting for Undo to expire).</summary>
    public DateTime? DeletedAt { get; set; }

    public TaskItem Clone() => (TaskItem)MemberwiseClone();
}
