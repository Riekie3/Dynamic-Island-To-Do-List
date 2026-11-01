namespace TaskIsland.Core.Models;

/// <summary>Task priority. The numeric value is stored in the database and drives sort order (High first).</summary>
public enum Priority
{
    High = 0,
    Medium = 1,
    Low = 2,
}
