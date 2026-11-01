using TaskIsland.Core.Models;

namespace TaskIsland.Core.Services;

/// <summary>
/// Applies a validated <see cref="TransferData"/> to the database (PLAN.md section 4.4).
/// Merge adds what is missing; Replace swaps everything (the caller makes the safety backup and asks for confirmation).
/// </summary>
public sealed class ImportService(ITaskRepository tasks, ITodoListRepository lists, ISettingsStore settings)
{
    public async Task<ImportSummary> ImportAsync(TransferData data, ImportMode mode)
    {
        if (mode == ImportMode.Replace)
        {
            await tasks.ReplaceAllAsync(data);
            var all = await lists.GetAllAsync();
            var first = all.OrderBy(l => l.SortOrder).First();
            await settings.SetAsync(AppSettings.Keys.DefaultListId, first.Id.ToString());
            await settings.SetAsync(AppSettings.Keys.SelectedListId, "0");
            return new ImportSummary(data.Tasks.Count, 0, data.Lists.Count);
        }

        var existingLists = (await lists.GetAllAsync()).ToList();
        var byName = existingLists.ToDictionary(l => l.Name, l => l.Id, StringComparer.OrdinalIgnoreCase);
        var missing = data.Lists.Where(l => !byName.ContainsKey(l.Name)).ToList();
        if (existingLists.Count + missing.Count > TodoList.MaxLists)
            throw new InvalidOperationException(
                $"Importing would create more than {TodoList.MaxLists} lists. Delete or merge some lists first.");

        foreach (var l in missing)
        {
            var created = await lists.CreateAsync(l.Name, l.Color);
            byName[created.Name] = created.Id;
        }

        long defaultListId = await ResolveDefaultListAsync(existingLists);
        var known = new HashSet<(long, string, long)>(
            (await tasks.GetAllAsync()).Select(t => (t.ListId, t.Title, t.CreatedAt.Ticks)));

        int imported = 0, skipped = 0;
        foreach (var t in data.Tasks)
        {
            long listId = t.List is null ? defaultListId : byName[t.List];
            if (!known.Add((listId, t.Title, t.CreatedAt.Ticks))) { skipped++; continue; }
            await tasks.InsertAsync(new TaskItem
            {
                ListId = listId,
                Title = t.Title,
                Description = t.Description,
                Priority = t.Priority,
                IsCompleted = t.IsCompleted,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                CompletedAt = t.CompletedAt,
                DueDate = t.DueDate,
                DueHasTime = t.DueHasTime,
            });
            imported++;
        }
        return new ImportSummary(imported, skipped, missing.Count);
    }

    private async Task<long> ResolveDefaultListAsync(IReadOnlyList<TodoList> existing)
    {
        var current = await settings.LoadAsync();
        if (existing.Any(l => l.Id == current.DefaultListId)) return current.DefaultListId;
        return existing.OrderBy(l => l.SortOrder).First().Id;
    }
}
