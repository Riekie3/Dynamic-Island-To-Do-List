using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskIsland.App.Logging;
using TaskIsland.Core.Models;

namespace TaskIsland.App.ViewModels;

/// <summary>Card for creating or renaming a list: name + the 8 palette swatches (PLAN.md sections 4.6 and 6.4).</summary>
public sealed partial class ListEditorViewModel : ObservableObject
{
    public ListEditorViewModel(long? listId, string name, string colorHex)
    {
        ListId = listId;
        _name = name;
        _colorHex = colorHex;
        foreach (var hex in TodoList.Palette)
            Swatches.Add(new SwatchViewModel(hex) { IsSelected = string.Equals(hex, colorHex, StringComparison.OrdinalIgnoreCase) });
    }

    public long? ListId { get; }
    public string Title => ListId is null ? "New list" : "Rename list";
    public string ConfirmText => ListId is null ? "Create" : "Save";
    public ObservableCollection<SwatchViewModel> Swatches { get; } = [];

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _colorHex;
    [ObservableProperty] private string _error = "";

    [RelayCommand]
    private void PickSwatch(SwatchViewModel? swatch)
    {
        if (swatch is null) return;
        ColorHex = swatch.Hex;
        foreach (var s in Swatches) s.IsSelected = s == swatch;
    }
}

/// <summary>Card shown before a list is deleted: move its tasks to another list (default) or delete them with it.</summary>
public sealed partial class DeleteListViewModel : ObservableObject
{
    public DeleteListViewModel(TodoList list, int taskCount, IEnumerable<ListChoiceViewModel> targets, long defaultTarget)
    {
        ListId = list.Id;
        Name = list.Name;
        TaskCount = taskCount;
        foreach (var t in targets)
        {
            t.IsSelected = t.Id == defaultTarget;
            Targets.Add(t);
        }
        _targetId = defaultTarget;
        _moveTasks = taskCount > 0;
    }

    public long ListId { get; }
    public string Name { get; }
    public int TaskCount { get; }
    public bool HasTasks => TaskCount > 0;
    public string Question => $"Delete \"{Name}\"?";
    public string MoveText => $"Move its {TaskCount} {(TaskCount == 1 ? "task" : "tasks")} to another list";
    public string DeleteText => $"Delete the list and its {TaskCount} {(TaskCount == 1 ? "task" : "tasks")}";
    public ObservableCollection<ListChoiceViewModel> Targets { get; } = [];

    [ObservableProperty] private bool _moveTasks;
    [ObservableProperty] private long _targetId;

    public bool DeleteTasks => !MoveTasks;

    partial void OnMoveTasksChanged(bool value) => OnPropertyChanged(nameof(DeleteTasks));

    [RelayCommand]
    private void ChooseTarget(ListChoiceViewModel? target)
    {
        if (target is null) return;
        TargetId = target.Id;
        MoveTasks = true;
        foreach (var t in Targets) t.IsSelected = t.Id == target.Id;
    }

    [RelayCommand]
    private void ChooseMove() => MoveTasks = true;

    [RelayCommand]
    private void ChooseDelete() => MoveTasks = false;
}

/// <summary>A yes/no card (Clear completed).</summary>
public sealed class ConfirmViewModel(string title, string message, string confirmText, bool isDestructive, Func<Task> action)
{
    public string Title { get; } = title;
    public string Message { get; } = message;
    public string ConfirmText { get; } = confirmText;
    public bool IsDestructive { get; } = isDestructive;
    public Func<Task> Action { get; } = action;
}

public sealed partial class IslandViewModel
{
    [ObservableProperty] private ListEditorViewModel? _listEditor;
    [ObservableProperty] private DeleteListViewModel? _deleteList;
    [ObservableProperty] private ConfirmViewModel? _confirm;

    /// <summary>A card is open over the panel; the panel underneath is disabled.</summary>
    public bool HasOverlay => ListEditor is not null || DeleteList is not null || Confirm is not null;

    partial void OnListEditorChanged(ListEditorViewModel? value) => OnPropertyChanged(nameof(HasOverlay));
    partial void OnDeleteListChanged(DeleteListViewModel? value) => OnPropertyChanged(nameof(HasOverlay));
    partial void OnConfirmChanged(ConfirmViewModel? value) => OnPropertyChanged(nameof(HasOverlay));

    /// <summary>The lists in switcher order (without "All"), for Settings and menus.</summary>
    public IReadOnlyList<TodoList> Lists => _lists;

    /// <summary>The list colour dot for a reminder pill (accent blue when the list is unknown or the notice covers several lists).</summary>
    public Brush ReminderDotBrush(long? listId) =>
        listId is { } id && _lists.FirstOrDefault(l => l.Id == id) is { } l
            ? BrushCache.Frozen(BrushCache.Parse(l.ColorHex), 1)
            : BrushCache.Frozen(AllColor, 1);

    /// <summary>One chip per list in order; chips of unchanged lists are kept so their bindings are not disturbed.</summary>
    private void RebuildChips()
    {
        var byId = Chips.Where(c => !c.IsAll).ToDictionary(c => c.Id);
        for (int i = Chips.Count - 1; i >= 1; i--)
            if (_lists.All(l => l.Id != Chips[i].Id)) Chips.RemoveAt(i);
        for (int i = 0; i < _lists.Count; i++)
        {
            var l = _lists[i];
            int at = i + 1;
            if (byId.TryGetValue(l.Id, out var chip) && Chips.Contains(chip))
            {
                chip.Update(l);
                int cur = Chips.IndexOf(chip);
                if (cur != at) Chips.Move(cur, at);
            }
            else
            {
                chip = new ListChipViewModel(l.Id, l.Name, BrushCache.Parse(l.ColorHex)) { ColorHex = l.ColorHex };
                Chips.Insert(at, chip);
            }
        }
        foreach (var c in Chips) c.IsSelected = c.Id == SelectedListId;
    }

    // ---- create / rename ----

    [RelayCommand]
    private void NewList()
    {
        CancelTransientUi();
        var used = _lists.Select(l => l.ColorHex.ToUpperInvariant()).ToHashSet();
        var color = TodoList.Palette.FirstOrDefault(c => !used.Contains(c.ToUpperInvariant())) ?? TodoList.Palette[0];
        ListEditor = new ListEditorViewModel(null, "", color);
    }

    [RelayCommand]
    private void RenameList(ListChipViewModel? chip)
    {
        if (chip is null || chip.IsAll) return;
        CancelTransientUi();
        ListEditor = new ListEditorViewModel(chip.Id, chip.Name, chip.ColorHex);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SaveListEditorAsync()
    {
        var editor = ListEditor;
        if (editor is null) return;
        var name = editor.Name.Trim();
        if (name.Length is < 1 or > TodoList.MaxNameLength)
        {
            editor.Error = $"The name must be 1 to {TodoList.MaxNameLength} characters.";
            return;
        }
        if (_lists.Any(l => l.Id != editor.ListId && string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            editor.Error = $"A list named \"{name}\" already exists.";
            return;
        }
        if (editor.ListId is null && _lists.Count >= TodoList.MaxLists)
        {
            editor.Error = $"You can have at most {TodoList.MaxLists} lists.";
            return;
        }

        long? createdId = null;
        bool ok = await RunAsync(editor.ListId is null ? "create the list" : "rename the list", async () =>
        {
            if (editor.ListId is { } id)
            {
                var old = _lists.FirstOrDefault(l => l.Id == id)
                    ?? throw new InvalidOperationException("That list no longer exists.");
                if (old.Name != name) await _listsRepo.RenameAsync(id, name);
                if (!string.Equals(old.ColorHex, editor.ColorHex, StringComparison.OrdinalIgnoreCase))
                    await _listsRepo.SetColorAsync(id, editor.ColorHex);
            }
            else
            {
                createdId = (await _listsRepo.CreateAsync(name, editor.ColorHex)).Id;
            }
            ListEditor = null;
            if (createdId is { } nid) SelectListInternal(nid); // show the new list
            await ReloadAsync();
        });
        if (!ok && ListEditor == editor) editor.Error = NoticeText;
    }

    [RelayCommand]
    private void CancelOverlay() => CancelTransientUi();

    // ---- colour, order ----

    public Task SetListColorAsync(ListChipViewModel chip, string hex) =>
        chip.IsAll ? Task.CompletedTask : RunAsync("change the colour", async () =>
        {
            await _listsRepo.SetColorAsync(chip.Id, hex);
            await ReloadAsync();
        });

    public Task MoveListAsync(ListChipViewModel chip, int direction) =>
        RunAsync("move the list", async () =>
        {
            var ids = _lists.Select(l => l.Id).ToList();
            int from = ids.IndexOf(chip.Id), to = from + Math.Sign(direction);
            if (from < 0 || to < 0 || to >= ids.Count) return;
            (ids[from], ids[to]) = (ids[to], ids[from]);
            await _listsRepo.ReorderAsync(ids);
            await ReloadAsync();
        });

    /// <summary>Drag and drop: puts the list at <paramref name="slot"/> (0 = first, Lists.Count = after the last).</summary>
    public Task MoveListToAsync(ListChipViewModel chip, int slot) =>
        chip.IsAll ? Task.CompletedTask : RunAsync("move the list", async () =>
        {
            var ids = _lists.Select(l => l.Id).ToList();
            int from = ids.IndexOf(chip.Id);
            if (from < 0) return;
            int to = Math.Clamp(slot, 0, ids.Count);
            if (to > from) to--;
            if (to == from) return;
            ids.RemoveAt(from);
            ids.Insert(to, chip.Id);
            await _listsRepo.ReorderAsync(ids);
            await ReloadAsync();
        });

    public bool CanMoveList(ListChipViewModel chip, int direction)
    {
        int from = _lists.FindIndex(l => l.Id == chip.Id), to = from + Math.Sign(direction);
        return from >= 0 && to >= 0 && to < _lists.Count;
    }

    // ---- delete ----

    [RelayCommand]
    private void OpenDeleteList(ListChipViewModel? chip)
    {
        if (chip is null || chip.IsAll) return;
        if (_lists.Count <= 1)
        {
            ShowNotice("The last remaining list can't be deleted.", error: true);
            return;
        }
        CancelTransientUi();
        var list = _lists.FirstOrDefault(l => l.Id == chip.Id);
        if (list is null) return;   // already gone
        int count = _tasks.Count(t => t.ListId == list.Id);
        var targets = _lists.Where(l => l.Id != list.Id)
            .Select(l => new ListChoiceViewModel(l.Id, l.Name, BrushCache.Frozen(BrushCache.Parse(l.ColorHex), 1))).ToList();
        long def = targets.Any(t => t.Id == _settings.DefaultListId) ? _settings.DefaultListId : targets[0].Id;
        DeleteList = new DeleteListViewModel(list, count, targets, def);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ConfirmDeleteListAsync()
    {
        var card = DeleteList;
        if (card is null) return;
        DeleteList = null;
        long? target = card.HasTasks && card.MoveTasks ? card.TargetId : null;
        await RunAsync("delete the list", async () =>
        {
            // Deleting a list is not covered by Undo, so make a backup first (PLAN.md section 4.6).
            var file = await _backup.BackupAsync(prefix: "pre-delete-");
            FileLogger.Info($"backup before deleting list {card.ListId}: {file}");
            await _listsRepo.DeleteAsync(card.ListId, target);
            if (SelectedListId == card.ListId) SelectListInternal(0);
            var def = await _settingsStore.GetAsync(AppSettings.Keys.DefaultListId);
            if (long.TryParse(def, out var id)) _settings.DefaultListId = id;
            await ReloadAsync();
        });
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ConfirmYesAsync()
    {
        var card = Confirm;
        if (card is null) return;
        Confirm = null;
        await card.Action();
    }
}
