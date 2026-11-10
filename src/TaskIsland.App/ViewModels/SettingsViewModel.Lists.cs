using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskIsland.App.Logging;
using TaskIsland.App.Views;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;
using TaskIsland.Data;

namespace TaskIsland.App.ViewModels;

/// <summary>One list in Settings → Lists.</summary>
public sealed partial class ListRowViewModel(TodoList list, int taskCount, bool canUp, bool canDown, bool canDelete) : ObservableObject
{
    public long Id { get; } = list.Id;
    public string Name { get; } = list.Name;
    public string ColorHex { get; } = list.ColorHex;
    public Brush Brush { get; } = BrushCache.Frozen(BrushCache.Parse(list.ColorHex), 1);
    public string CountText { get; } = taskCount == 1 ? "1 task" : $"{taskCount} tasks";
    public bool CanUp { get; } = canUp;
    public bool CanDown { get; } = canDown;
    public bool CanDelete { get; } = canDelete;
}

/// <summary>Lists page: reorder, rename, colour, delete, new list, default list and the pill count scope (PLAN.md section 6.6).</summary>
public sealed partial class SettingsViewModel
{
    private readonly ITaskRepository _tasks;
    private readonly ITodoListRepository _listsRepo;
    private readonly ISettingsStore _store;
    private readonly BackupService _backup;
    private readonly ImportService _import;
    private readonly AppPaths _paths;
    private readonly IClock _clock;
    private List<TodoList> _lists = [];

    public ObservableCollection<ListRowViewModel> ListRows { get; } = [];
    public IReadOnlyList<string> ListNames { get; private set; } = [];

    [ObservableProperty] private string _listsMessage = "";

    private void RefreshLists()
    {
        _lists = Island.Lists.OrderBy(l => l.SortOrder).ToList();
        ListRows.Clear();
        for (int i = 0; i < _lists.Count; i++)
            ListRows.Add(new ListRowViewModel(_lists[i], Island.TaskCount(_lists[i].Id), i > 0, i < _lists.Count - 1, _lists.Count > 1));
        ListNames = _lists.Select(l => l.Name).ToList();
        OnPropertyChanged(nameof(ListNames));
        OnPropertyChanged(nameof(DefaultListIndex));
        OnPropertyChanged(nameof(CanAddList));
    }

    public bool CanAddList => _lists.Count < TodoList.MaxLists;

    public int DefaultListIndex
    {
        get => Math.Max(0, _lists.FindIndex(l => l.Id == _s.DefaultListId));
        set
        {
            if (value < 0 || value >= _lists.Count) return;
            Set(_s.DefaultListId, _lists[value].Id, v => _s.DefaultListId = v, AppSettings.Keys.DefaultListId);
        }
    }

    public int PillScopeIndex
    {
        get => (int)_s.PillCountScope;
        set => Set((int)_s.PillCountScope, value, v => _s.PillCountScope = (PillCountScope)v, AppSettings.Keys.PillCountScope);
    }

    private string? ValidateName(string name, long? ignoreId)
    {
        if (name.Length is < 1 or > TodoList.MaxNameLength) return $"The name must be 1 to {TodoList.MaxNameLength} characters.";
        if (_lists.Any(l => l.Id != ignoreId && string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)))
            return $"A list named \"{name}\" already exists.";
        return null;
    }

    /// <summary>Runs a list change, then reloads the island and this page. A failure becomes a message, never a crash.</summary>
    private async Task ListOpAsync(string what, Func<Task> op)
    {
        ListsMessage = "";
        try
        {
            await op();
            await Island.RefreshAsync();
        }
        catch (Exception ex)
        {
            FileLogger.Error($"settings: could not {what}", ex);
            ListsMessage = ex is ArgumentException or InvalidOperationException ? ex.Message : $"Couldn't {what}. Nothing was changed.";
        }
        RefreshLists();
    }

    [RelayCommand]
    private async Task NewListAsync()
    {
        if (_lists.Count >= TodoList.MaxLists)
        {
            ListsMessage = $"You can have at most {TodoList.MaxLists} lists.";
            return;
        }
        var used = _lists.Select(l => l.ColorHex.ToUpperInvariant()).ToHashSet();
        var color = TodoList.Palette.FirstOrDefault(c => !used.Contains(c.ToUpperInvariant())) ?? TodoList.Palette[0];
        if (DialogWindow.EditList(Owner, "New list", "Create", "", color, n => ValidateName(n, null)) is not { } r) return;
        await ListOpAsync("create the list", async () => await _listsRepo.CreateAsync(r.Name, r.Color));
    }

    [RelayCommand]
    private async Task EditListAsync(ListRowViewModel? row)
    {
        if (row is null) return;
        if (DialogWindow.EditList(Owner, "Edit list", "Save", row.Name, row.ColorHex, n => ValidateName(n, row.Id)) is not { } r) return;
        await ListOpAsync("save the list", async () =>
        {
            if (r.Name != row.Name) await _listsRepo.RenameAsync(row.Id, r.Name);
            if (!string.Equals(r.Color, row.ColorHex, StringComparison.OrdinalIgnoreCase)) await _listsRepo.SetColorAsync(row.Id, r.Color);
        });
    }

    [RelayCommand]
    private Task MoveListUpAsync(ListRowViewModel? row) => MoveAsync(row, -1);

    [RelayCommand]
    private Task MoveListDownAsync(ListRowViewModel? row) => MoveAsync(row, +1);

    private Task MoveAsync(ListRowViewModel? row, int direction)
    {
        if (row is null) return Task.CompletedTask;
        return ListOpAsync("move the list", async () =>
        {
            var ids = _lists.Select(l => l.Id).ToList();
            int from = ids.IndexOf(row.Id), to = from + direction;
            if (from < 0 || to < 0 || to >= ids.Count) return;
            (ids[from], ids[to]) = (ids[to], ids[from]);
            await _listsRepo.ReorderAsync(ids);
        });
    }

    [RelayCommand]
    private async Task DeleteListAsync(ListRowViewModel? row)
    {
        if (row is null) return;
        if (_lists.Count <= 1)
        {
            ListsMessage = "The last remaining list can't be deleted.";
            return;
        }
        int count = Island.TaskCount(row.Id);
        var others = _lists.Where(l => l.Id != row.Id).Select(l => (l.Id, l.Name)).ToList();
        var (ok, moveTo) = DialogWindow.DeleteList(Owner, row.Name, count, others);
        if (!ok) return;
        await ListOpAsync("delete the list", async () =>
        {
            // Deleting a list is not covered by Undo, so a backup comes first (PLAN.md section 4.6).
            var file = await _backup.BackupAsync(prefix: BackupService.PreDeletePrefix);
            FileLogger.Info($"backup before deleting list {row.Id}: {file}");
            await _listsRepo.DeleteAsync(row.Id, moveTo);
            if (long.TryParse(await _store.GetAsync(AppSettings.Keys.DefaultListId), out var def)) _s.DefaultListId = def;
        });
        OnPropertyChanged(nameof(DefaultListIndex));
    }
}
