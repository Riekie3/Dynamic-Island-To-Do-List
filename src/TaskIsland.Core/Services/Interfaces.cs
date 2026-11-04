using TaskIsland.Core.Models;

namespace TaskIsland.Core.Services;

/// <summary>Task storage (PLAN.md section 4.2). Every call is committed immediately.</summary>
public interface ITaskRepository
{
    /// <summary>All tasks of all lists, ordered by Id. Filtering and display order live in <see cref="TaskSorter"/>.</summary>
    Task<IReadOnlyList<TaskItem>> GetAllAsync(bool includeDeleted = false);

    /// <summary>Adds a task at the end of its (list, priority) group.</summary>
    Task<TaskItem> AddAsync(long listId, string title, Priority priority, DateTime? due = null,
        bool dueHasTime = false, string? description = null);

    /// <summary>Inserts a complete task, keeping its timestamps (used by import). Id and SortOrder are assigned.</summary>
    Task<TaskItem> InsertAsync(TaskItem item);

    /// <summary>Moves the task to the end of its priority group in another list.</summary>
    Task MoveToListAsync(long id, long targetListId);

    /// <summary>
    /// Saves title, description, priority, due date and list. A changed priority or list moves the task to the end of
    /// its new group; a changed due date clears both "reminder shown" marks. Completion is not changed here.
    /// </summary>
    Task UpdateAsync(TaskItem item);

    Task SetCompletedAsync(long id, bool completed);

    Task SoftDeleteAsync(long id);
    Task RestoreAsync(long id);
    /// <summary>Permanently removes all soft-deleted tasks.</summary>
    Task PurgeDeletedAsync();

    /// <summary>Stores the given order (index = SortOrder) for one (list, priority) group in one transaction.</summary>
    Task ReorderAsync(long listId, Priority group, IReadOnlyList<long> orderedIds);

    /// <summary>Soft-deletes completed tasks (all lists, or one list) so the action can be undone. Returns the count.</summary>
    Task<int> ClearCompletedAsync(long? listId = null);

    Task MarkReminderShownAsync(long id);

    /// <summary>Marks the early reminder as shown (or deliberately skipped) so it never fires twice, also after a restart.</summary>
    Task MarkEarlyReminderShownAsync(long id);

    /// <summary>Replaces every list and task with the imported data in one transaction (JSON import "Replace").</summary>
    Task ReplaceAllAsync(TransferData data);
}

/// <summary>List (group) storage (PLAN.md section 4.6).</summary>
public interface ITodoListRepository
{
    Task<IReadOnlyList<TodoList>> GetAllAsync();
    Task<TodoList> CreateAsync(string name, string colorHex);
    Task RenameAsync(long id, string name);
    Task SetColorAsync(long id, string colorHex);
    Task ReorderAsync(IReadOnlyList<long> orderedIds);

    /// <summary>
    /// Deletes a list. <paramref name="moveTasksToListId"/> null deletes its tasks too. The last remaining list cannot
    /// be deleted.
    /// </summary>
    Task DeleteAsync(long id, long? moveTasksToListId);
}

public interface ISettingsStore
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    Task<IReadOnlyDictionary<string, string>> GetAllAsync();
    Task<AppSettings> LoadAsync();
    /// <summary>Writes every setting in one transaction.</summary>
    Task SaveAsync(AppSettings settings);
}
