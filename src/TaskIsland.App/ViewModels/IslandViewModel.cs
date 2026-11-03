using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;

namespace TaskIsland.App.ViewModels;

/// <summary>
/// State behind the pill and the panel. Phase 2 runs on in-memory sample data (<see cref="SampleData"/>) through the
/// real Core rules (<see cref="TaskSorter"/>, <see cref="TaskStats"/>); nothing is saved. Phase 3 connects the
/// repositories, Undo, inline edit and list management.
/// </summary>
public sealed partial class IslandViewModel : ObservableObject
{
    private static readonly Color AllColor = Color.FromRgb(0x2F, 0x8C, 0xFF);
    private readonly List<TodoList> _lists;
    private readonly List<TaskItem> _tasks;
    private readonly AppSettings _settings;
    private readonly IClock _clock;
    private long _nextId;

    public IslandViewModel(AppSettings settings, IClock clock)
    {
        _settings = settings;
        _clock = clock;
        (_lists, _tasks) = SampleData.Create(clock.UtcNow, clock.UtcNow.ToLocalTime());
        _nextId = _tasks.Max(t => t.Id) + 1;
        _filter = settings.CurrentFilter;
        _selectedListId = _lists.Any(l => l.Id == settings.SelectedListId) ? settings.SelectedListId : 0;
        Chips.Add(new ListChipViewModel(0, "All", AllColor));
        foreach (var l in _lists.OrderBy(l => l.SortOrder))
            Chips.Add(new ListChipViewModel(l.Id, l.Name, BrushCache.Parse(l.ColorHex)));
        Refresh();
    }

    public ObservableCollection<ListChipViewModel> Chips { get; } = [];
    public ObservableCollection<TaskItemViewModel> Tasks { get; } = [];

    /// <summary>Raised when Add is pressed with an empty title (the view shakes the input).</summary>
    public event EventHandler? AddRejected;

    [ObservableProperty] private TaskFilter _filter;
    [ObservableProperty] private long _selectedListId;
    [ObservableProperty] private string _newTitle = "";
    [ObservableProperty] private Priority _newPriority = Priority.Medium;

    // Selected-list scope (header badge, footer, progress bar, empty state)
    [ObservableProperty] private int _scopeOutstanding;
    [ObservableProperty] private string _footerText = "";
    [ObservableProperty] private double _scopeFraction;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _emptyTitle = "";
    [ObservableProperty] private string _emptyHint = "";

    // Pill scope (PillCountScope)
    [ObservableProperty] private string _pillText = "";
    [ObservableProperty] private double _pillFraction;
    [ObservableProperty] private bool _allDone;
    [ObservableProperty] private bool _hasOverdue;
    [ObservableProperty] private bool _hasTopTask;
    [ObservableProperty] private string _topTaskTitle = "";
    [ObservableProperty] private Brush _topTaskBrush = BrushCache.Frozen(AllColor, 1);

    partial void OnFilterChanged(TaskFilter value)
    {
        _settings.CurrentFilter = value;
        Refresh();
    }

    partial void OnSelectedListIdChanged(long value)
    {
        _settings.SelectedListId = value;
        Refresh();
    }

    [RelayCommand]
    private void SelectList(ListChipViewModel? chip)
    {
        if (chip is not null) SelectedListId = chip.Id;
    }

    [RelayCommand]
    private void SetFilter(string? name)
    {
        if (Enum.TryParse<TaskFilter>(name, out var f)) Filter = f;
    }

    /// <summary>Quick-add chip: High → Medium → Low → High.</summary>
    [RelayCommand]
    private void CyclePriority() => NewPriority = NewPriority switch
    {
        Priority.High => Priority.Medium,
        Priority.Medium => Priority.Low,
        _ => Priority.High,
    };

    [RelayCommand]
    private void SetPriority(Priority priority) => NewPriority = priority;

    [RelayCommand]
    private void Add()
    {
        var title = NewTitle.Trim();
        if (title.Length == 0)
        {
            AddRejected?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (title.Length > TaskItem.MaxTitleLength) title = title[..TaskItem.MaxTitleLength];
        long listId = SelectedListId != 0 ? SelectedListId : DefaultListId();
        var now = _clock.UtcNow;
        int order = _tasks.Where(t => t.ListId == listId && t.Priority == NewPriority).Select(t => t.SortOrder + 1)
            .DefaultIfEmpty(0).Max();
        _tasks.Add(new TaskItem
        {
            Id = _nextId++, ListId = listId, Title = title, Priority = NewPriority,
            CreatedAt = now, UpdatedAt = now, SortOrder = order,
        });
        NewTitle = "";
        Refresh();
    }

    [RelayCommand]
    private void Delete(TaskItemViewModel? item)
    {
        if (item is null) return;
        _tasks.Remove(item.Model);
        Refresh();
    }

    /// <summary>Ticking keeps the row where it is (the 2 s slide-out arrives in Phase 3); counters update at once.</summary>
    private void OnCompletedChanged(TaskItemViewModel row)
    {
        var t = row.Model;
        t.IsCompleted = row.IsCompleted;
        t.CompletedAt = row.IsCompleted ? _clock.UtcNow : null;
        t.UpdatedAt = _clock.UtcNow;
        row.UpdateDue(_clock.UtcNow.ToLocalTime());
        RefreshCounts();
    }

    private long DefaultListId() =>
        _lists.Any(l => l.Id == _settings.DefaultListId) ? _settings.DefaultListId
        : _lists.FirstOrDefault(l => l.Name == "Personal")?.Id ?? _lists[0].Id;

    private void Refresh()
    {
        var nowLocal = _clock.UtcNow.ToLocalTime();
        long? scope = SelectedListId == 0 ? null : SelectedListId;
        var byId = _lists.ToDictionary(l => l.Id);
        Tasks.Clear();
        foreach (var t in TaskSorter.Apply(_tasks, Filter, scope, _lists))
            Tasks.Add(new TaskItemViewModel(t, byId.GetValueOrDefault(t.ListId), scope is null, nowLocal, OnCompletedChanged));
        foreach (var chip in Chips) chip.IsSelected = chip.Id == SelectedListId;

        string listName = scope is { } id && byId.TryGetValue(id, out var sel) ? sel.Name : "all lists";
        EmptyTitle = Filter == TaskFilter.Completed ? "Nothing completed yet." : $"All done in {listName}";
        EmptyHint = Filter == TaskFilter.Completed ? "" : "Add something new above.";
        IsEmpty = Tasks.Count == 0;
        RefreshCounts();
    }

    private void RefreshCounts()
    {
        var nowLocal = _clock.UtcNow.ToLocalTime();
        long? scope = SelectedListId == 0 ? null : SelectedListId;
        var s = TaskStats.Compute(_tasks, scope);
        ScopeOutstanding = s.Outstanding;
        ScopeFraction = s.Total == 0 ? 0 : s.Percent / 100.0;
        FooterText = $"{s.Total} total · {s.Completed} done · {s.Percent}%";

        foreach (var chip in Chips)
            chip.Count = TaskStats.Compute(_tasks, chip.IsAll ? null : chip.Id).Outstanding;

        long? pillScope = _settings.PillCountScope == PillCountScope.SelectedList ? scope : null;
        var p = TaskStats.Compute(_tasks, pillScope);
        AllDone = p.Outstanding == 0;
        PillText = p.Outstanding switch { 0 => "All done", 1 => "1 task", var n => $"{n} tasks" };
        PillFraction = p.Total == 0 ? 1 : p.Percent / 100.0;

        var active = TaskSorter.Apply(_tasks, TaskFilter.Active, pillScope, _lists);
        HasOverdue = active.Any(t => t.DueDate is { } d && DueLabel.IsOverdue(d, t.DueHasTime, false, nowLocal));
        var top = active.FirstOrDefault();
        HasTopTask = top is not null;
        TopTaskTitle = top?.Title ?? "";
        if (top is not null && _lists.FirstOrDefault(l => l.Id == top.ListId) is { } tl)
            TopTaskBrush = BrushCache.Frozen(BrushCache.Parse(tl.ColorHex), 1);
    }
}
