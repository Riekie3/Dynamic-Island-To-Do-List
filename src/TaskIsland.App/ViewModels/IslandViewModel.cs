using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskIsland.App.Controls;
using TaskIsland.App.Logging;
using TaskIsland.App.Services;
using TaskIsland.Core.Mascot;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;
using TaskIsland.Data;

namespace TaskIsland.App.ViewModels;

/// <summary>
/// State behind the pill and the panel, working against the real repositories (PLAN.md sections 4 and 6.4). Every action
/// is awaited against the database first and the UI follows only on success; on failure the error is logged, the UI is
/// reloaded from the database (which reverts it) and an inline notice is shown. Operations run one at a time
/// (<see cref="RunAsync"/>), so the in-memory copy is always the database's.
/// </summary>
public sealed partial class IslandViewModel : ObservableObject, ITaskRowHost
{
    private static readonly Color AllColor = Color.FromRgb(0x2F, 0x8C, 0xFF);
    private static readonly TimeSpan UndoTime = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan NoticeTime = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan SlideOutDelay = TimeSpan.FromSeconds(2);

    private readonly ITaskRepository _tasksRepo;
    private readonly ITodoListRepository _listsRepo;
    private readonly ISettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly IClock _clock;
    private readonly BackupService _backup;
    private readonly ReactionDirector _director;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TickStreak _streak = new();
    private readonly HashSet<long> _lingering = [];
    private readonly Dictionary<long, DispatcherTimer> _lingerTimers = [];
    private readonly DispatcherTimer _undoTimer, _noticeTimer;
    private List<TodoList> _lists = [];
    private List<TaskItem> _tasks = [];
    private Dictionary<long, TaskItem> _tasksById = [];
    private readonly List<(string Text, List<long> Ids)> _undoBatches = [];
    private bool _internalSelect;

    public IslandViewModel(ITaskRepository tasks, ITodoListRepository lists, ISettingsStore settingsStore,
        AppSettings settings, IClock clock, BackupService backup, ReactionDirector director)
    {
        _tasksRepo = tasks;
        _listsRepo = lists;
        _settingsStore = settingsStore;
        _settings = settings;
        _clock = clock;
        _backup = backup;
        _director = director;
        _filter = settings.CurrentFilter;
        _selectedListId = settings.SelectedListId;
        _quickAddListId = settings.DefaultListId;
        Chips.Add(new ListChipViewModel(0, "All", AllColor) { IsSelected = _selectedListId == 0 });
        _undoTimer = OneShot(UndoTime, () => _ = ExpireUndoAsync());
        _noticeTimer = OneShot(NoticeTime, HideNotice);
    }

    public ObservableCollection<ListChipViewModel> Chips { get; } = [];
    public ObservableCollection<TaskItemViewModel> Tasks { get; } = [];

    /// <summary>Raised when Add is pressed with an empty title (the view shakes the input).</summary>
    public event EventHandler? AddRejected;

    /// <summary>Raised after every reload with the active (not deleted) tasks of all lists; reminders recompute from it.</summary>
    public event Action<IReadOnlyList<TaskItem>>? DataChanged;

    [ObservableProperty] private TaskFilter _filter;
    [ObservableProperty] private long _selectedListId;
    [ObservableProperty] private string _newTitle = "";
    [ObservableProperty] private Priority _newPriority = Priority.Medium;
    [ObservableProperty] private long _quickAddListId;
    [ObservableProperty] private string _quickAddListName = "";
    [ObservableProperty] private Brush _quickAddListBrush = BrushCache.Frozen(AllColor, 1);

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

    // Notices: Undo bar and inline messages above the footer
    [ObservableProperty] private bool _hasUndo;
    [ObservableProperty] private string _undoText = "";
    [ObservableProperty] private bool _hasNotice;
    [ObservableProperty] private string _noticeText = "";
    [ObservableProperty] private bool _noticeIsError;

    public bool ShowQuickAddList => SelectedListId == 0;

    /// <summary>Dragging is allowed in the Active filter of a single list only (PLAN.md section 6.4).</summary>
    public bool CanReorder => Filter == TaskFilter.Active && SelectedListId != 0;

    /// <summary>The row whose edit form is open (only one at a time).</summary>
    public TaskItemViewModel? Editing { get; private set; }

    // ---- start-up and reload ----

    /// <summary>Loads lists and tasks. Soft-deleted rows left over from a previous run are removed for good first.</summary>
    public async Task InitializeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await _tasksRepo.PurgeDeletedAsync();
            await ReloadAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ReloadAsync(RowLeave? leave = null, ISet<long>? enter = null)
    {
        _lists = (await _listsRepo.GetAllAsync()).ToList();
        _tasks = (await _tasksRepo.GetAllAsync()).ToList();
        _tasksById = _tasks.ToDictionary(t => t.Id);
        if (_lists.Count > 0 && _settings.DefaultListId != 0 && _lists.All(l => l.Id != _settings.DefaultListId))
            _settings.DefaultListId = _lists[0].Id;
        if (SelectedListId != 0 && _lists.All(l => l.Id != SelectedListId)) SelectListInternal(0);
        if (_lists.All(l => l.Id != QuickAddListId)) QuickAddListId = DefaultListId();
        RebuildChips();
        UpdateQuickAddList();
        RefreshCounts();
        await ReconcileAsync(leave, enter);
        DataChanged?.Invoke(_tasks);
    }

    /// <summary>
    /// Reloads lists and tasks after something outside the view model changed the data (Settings: list changes, import,
    /// restore, clear completed). <paramref name="reloadSettings"/> also re-reads the default list and the selection, which an
    /// import "Replace" or a restore changes in the database.
    /// </summary>
    public async Task RefreshAsync(bool reloadSettings = false)
    {
        await _gate.WaitAsync();
        try
        {
            CancelTransientUi();
            if (reloadSettings)
            {
                var fresh = await _settingsStore.LoadAsync();
                _settings.DefaultListId = fresh.DefaultListId;
                SelectListInternal(fresh.SelectedListId);
                QuickAddListId = DefaultListId();
            }
            await ReloadAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Settings → Lists: the default list or the pill count scope changed.</summary>
    public async Task ListSettingsChangedAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_lists.Any(l => l.Id == _settings.DefaultListId)) QuickAddListId = _settings.DefaultListId;
            RefreshCounts();
            UpdateQuickAddList();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Open task count per list id (Settings → Lists).</summary>
    public int TaskCount(long listId) => _tasks.Count(t => t.ListId == listId);

    /// <summary>Every task that is not deleted, as last loaded (reminders start from it).</summary>
    public IReadOnlyList<TaskItem> AllTasks => _tasks;

    /// <summary>Name of a list, or null when it no longer exists.</summary>
    public string? ListName(long listId) => _lists.FirstOrDefault(l => l.Id == listId)?.Name;

    /// <summary>A reminder was clicked: show that list (or All lists) in the Active view.</summary>
    public void ShowList(long? listId)
    {
        long id = listId is { } l && _lists.Any(x => x.Id == l) ? l : 0;
        if (Filter != TaskFilter.Active) Filter = TaskFilter.Active;
        if (SelectedListId != id) SelectedListId = id;
    }

    /// <summary>An unexpected error was caught by the global handler (PLAN.md section 7.10): a small notice and the Sad face.</summary>
    public void ReportUnexpectedError()
    {
        ShowNotice("Something went wrong. It was written to the log; your tasks are safe.", error: true);
        _director.Request(Reaction.Sad);
    }

    private long DefaultListId() =>
        _lists.Any(l => l.Id == _settings.DefaultListId) ? _settings.DefaultListId : _lists.Count > 0 ? _lists[0].Id : 0;

    /// <summary>
    /// Runs one operation at a time. A failure is logged, shown as an inline notice with the Sad face, and the UI is
    /// reloaded from the database, which reverts anything the failed action had changed on screen.
    /// </summary>
    private async Task<bool> RunAsync(string what, Func<Task> op)
    {
        await _gate.WaitAsync();
        try
        {
            HideNotice();
            await op();
            return true;
        }
        catch (Exception ex)
        {
            FileLogger.Error($"{what} failed", ex);
            ShowNotice(Friendly(ex, what), error: true);
            _director.Request(Reaction.Sad);
            try { await ReloadAsync(); }
            catch (Exception ex2) { FileLogger.Error("reload after a failure also failed", ex2); }
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Messages written for the user (validation in the repositories) pass through; anything else gets a plain sentence.</summary>
    private static string Friendly(Exception ex, string what) =>
        ex is ArgumentException or InvalidOperationException && !ex.Message.Contains("SQLite", StringComparison.OrdinalIgnoreCase)
            ? ex.Message
            : $"Couldn't {what}. Nothing was changed.";

    // ---- filter and list selection ----

    partial void OnFilterChanged(TaskFilter value)
    {
        _settings.CurrentFilter = value;
        OnPropertyChanged(nameof(CanReorder));
        _ = PersistAsync(AppSettings.Keys.CurrentFilter, value.ToString());
        CancelAllLingering();
        _ = RunAsync("show that view", () => { RefreshCounts(); return ReconcileAsync(); });
    }

    partial void OnSelectedListIdChanged(long value)
    {
        _settings.SelectedListId = value;
        OnPropertyChanged(nameof(ShowQuickAddList));
        OnPropertyChanged(nameof(CanReorder));
        foreach (var chip in Chips) chip.IsSelected = chip.Id == value;
        if (_internalSelect) return;
        _ = PersistAsync(AppSettings.Keys.SelectedListId, value.ToString());
        CancelAllLingering();
        _ = RunAsync("show that list", () => { RefreshCounts(); return ReconcileAsync(); });
    }

    /// <summary>Changes the selection from inside an operation (which already holds the gate).</summary>
    private void SelectListInternal(long id)
    {
        _internalSelect = true;
        SelectedListId = id;
        _internalSelect = false;
        _ = PersistAsync(AppSettings.Keys.SelectedListId, id.ToString());
    }

    private async Task PersistAsync(string key, string value)
    {
        try { await _settingsStore.SetAsync(key, value); }
        catch (Exception ex) { FileLogger.Warn($"could not save setting {key}: {ex.Message}"); }
    }

    [RelayCommand]
    private void SelectList(ListChipViewModel? chip)
    {
        if (chip is not null) SelectedListId = chip.Id;
    }

    /// <summary>Ctrl+1…9 / Ctrl+0 / Ctrl+Tab (PLAN.md section 6.4). Index 0 is All.</summary>
    public void SelectChipAt(int index)
    {
        if (index >= 0 && index < Chips.Count) SelectedListId = Chips[index].Id;
    }

    public void SelectNextChip(int step)
    {
        int i = Chips.ToList().FindIndex(c => c.Id == SelectedListId);
        if (i < 0) i = 0;
        SelectedListId = Chips[((i + step) % Chips.Count + Chips.Count) % Chips.Count].Id;
    }

    [RelayCommand]
    private void SetFilter(string? name)
    {
        if (Enum.TryParse<TaskFilter>(name, out var f)) Filter = f;
    }

    // ---- quick add ----

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

    public void SetQuickAddList(long id)
    {
        QuickAddListId = id;
        UpdateQuickAddList();
    }

    private void UpdateQuickAddList()
    {
        var l = _lists.FirstOrDefault(x => x.Id == QuickAddListId);
        if (l is null) return;
        QuickAddListName = l.Name;
        QuickAddListBrush = BrushCache.Frozen(BrushCache.Parse(l.ColorHex), 1);
    }

    partial void OnNewTitleChanged(string value)
    {
        if (value.Length > 0) _director.Keep(Reaction.Focused);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AddAsync()
    {
        var title = NewTitle.Trim();
        if (title.Length == 0)
        {
            AddRejected?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (title.Length > TaskItem.MaxTitleLength) title = title[..TaskItem.MaxTitleLength];
        long listId = SelectedListId != 0 ? SelectedListId : QuickAddListId;
        var priority = NewPriority;
        await RunAsync("add the task", async () =>
        {
            if (_lists.All(l => l.Id != listId))
                throw new InvalidOperationException("That list no longer exists. Pick another list.");
            var added = await _tasksRepo.AddAsync(listId, title, priority);
            if (NewTitle.Trim() == title) NewTitle = "";
            await ReloadAsync(enter: new HashSet<long> { added.Id });
            _director.Request(Reaction.Wink);
        });
    }

    // ---- ticking, with the 2 s slide-out ----

    void ITaskRowHost.RowCompletedChanged(TaskItemViewModel row) => _ = ToggleAsync(row, row.IsCompleted);

    private async Task ToggleAsync(TaskItemViewModel row, bool completed)
    {
        bool ok = await RunAsync(completed ? "tick the task" : "untick the task", async () =>
        {
            await _tasksRepo.SetCompletedAsync(row.Id, completed);
            if (!completed) _streak.Undo();

            // Does the row still belong in this view? If not it lingers for 2 s (tick + strikethrough), then slides out.
            bool belongs = Filter == TaskFilter.All || (Filter == TaskFilter.Active) == !completed;
            if (belongs) CancelLinger(row.Id);
            else if (_settings.CompletedFadeOut) StartLinger(row.Id);
            else CancelLinger(row.Id);

            bool high = row.Priority == Priority.High;
            await ReloadAsync();
            if (completed) ReactToTick(high);
        });
        if (!ok) row.SetCompletedSilently(!completed);
    }

    private void ReactToTick(bool high)
    {
        bool streak = _streak.Record(_clock.UtcNow);
        var reaction = ScopeOutstanding == 0 ? Reaction.Excited
            : high || streak ? Reaction.Love
            : Reaction.Happy;
        _director.Request(reaction);
    }

    private bool InScope(TaskItem t) => SelectedListId == 0 || t.ListId == SelectedListId;

    private void StartLinger(long id)
    {
        _lingering.Add(id);
        if (_lingerTimers.TryGetValue(id, out var old)) old.Stop();
        var timer = OneShot(SlideOutDelay, () => _ = FinishLingerAsync(id));
        _lingerTimers[id] = timer;
        timer.Start();
    }

    private void CancelLinger(long id)
    {
        _lingering.Remove(id);
        if (_lingerTimers.Remove(id, out var t)) t.Stop();
    }

    private void CancelAllLingering()
    {
        foreach (var t in _lingerTimers.Values) t.Stop();
        _lingerTimers.Clear();
        _lingering.Clear();
    }

    private async Task FinishLingerAsync(long id)
    {
        await RunAsync("update the list", async () =>
        {
            if (!_lingering.Remove(id)) return; // cancelled meanwhile
            _lingerTimers.Remove(id);
            await ReconcileAsync(RowLeave.SlideOut);
        });
    }

    // ---- delete and Undo ----

    async Task ITaskRowHost.DeleteAsync(TaskItemViewModel row)
    {
        await RunAsync("delete the task", async () =>
        {
            if (Editing == row) CancelEditCore(row);
            await _tasksRepo.SoftDeleteAsync(row.Id);
            CancelLinger(row.Id);
            await ReloadAsync(leave: RowLeave.Delete);
            ShowUndo("Task deleted", [row.Id]);
        });
    }

    // Every delete in the 5 s window is its own batch: Undo brings back the newest one, an earlier one stays undoable
    // until the timer ends (the timer restarts with each delete).
    private void ShowUndo(string text, List<long> ids)
    {
        _undoBatches.Add((text, ids));
        UndoText = text;
        HasUndo = true;
        _undoTimer.Stop();
        _undoTimer.Start();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task UndoAsync()
    {
        _undoTimer.Stop();
        if (_undoBatches.Count == 0) { HasUndo = false; return; }
        var ids = _undoBatches[^1].Ids;
        _undoBatches.RemoveAt(_undoBatches.Count - 1);
        if (_undoBatches.Count > 0)
        {
            UndoText = _undoBatches[^1].Text;   // the previous delete can still be undone
            _undoTimer.Start();
        }
        else HasUndo = false;
        if (ids.Count == 0) return;
        await RunAsync("restore the tasks", async () =>
        {
            foreach (var id in ids) await _tasksRepo.RestoreAsync(id);
            await ReloadAsync(enter: ids.ToHashSet());
        });
    }

    /// <summary>The 5 s are over: the deleted tasks go for good.</summary>
    private async Task ExpireUndoAsync()
    {
        HasUndo = false;
        _undoBatches.Clear();
        await RunAsync("remove the deleted tasks", () => _tasksRepo.PurgeDeletedAsync());
    }

    // ---- clear completed (Settings -> Data) ----

    /// <summary>Completed tasks in all lists.</summary>
    public int CompletedCount => _tasks.Count(t => t.IsCompleted);

    /// <summary>Settings -> Data -> Clear completed tasks (after its own confirmation): all lists, with the Undo bar.</summary>
    public async Task<int> ClearAllCompletedAsync()
    {
        int cleared = 0;
        await RunAsync("clear the completed tasks", async () =>
        {
            var ids = _tasks.Where(t => t.IsCompleted).Select(t => t.Id).ToList();
            cleared = await _tasksRepo.ClearCompletedAsync(null);
            await ReloadAsync(leave: RowLeave.SlideOut);
            if (cleared > 0) ShowUndo(cleared == 1 ? "1 completed task cleared" : $"{cleared} completed tasks cleared", ids);
        });
        return cleared;
    }

    // ---- clear completed ----

    [RelayCommand]
    private void ClearCompleted()
    {
        var done = _tasks.Where(t => t.IsCompleted && InScope(t)).ToList();
        if (done.Count == 0)
        {
            ShowNotice("No completed tasks to clear.", error: false);
            return;
        }
        string where = SelectedListId == 0 ? "in all lists" : $"in {_lists.FirstOrDefault(l => l.Id == SelectedListId)?.Name}";
        Confirm = new ConfirmViewModel(
            "Clear completed tasks?",
            $"{done.Count} completed {(done.Count == 1 ? "task" : "tasks")} {where} will be removed. You can undo this for 5 seconds.",
            "Clear", isDestructive: true, ClearCompletedCoreAsync);
    }

    private async Task ClearCompletedCoreAsync()
    {
        var ids = _tasks.Where(t => t.IsCompleted && InScope(t)).Select(t => t.Id).ToList();
        await RunAsync("clear the completed tasks", async () =>
        {
            int n = await _tasksRepo.ClearCompletedAsync(SelectedListId == 0 ? null : SelectedListId);
            await ReloadAsync(leave: RowLeave.SlideOut);
            if (n > 0) ShowUndo(n == 1 ? "1 completed task cleared" : $"{n} completed tasks cleared", ids);
        });
    }

    // ---- inline edit ----

    void ITaskRowHost.BeginEdit(TaskItemViewModel row)
    {
        if (Editing is { } other && other != row) CancelEditCore(other);
        Editing = row;
        row.StartEdit(_lists);
        _director.Keep(Reaction.Focused);
    }

    void ITaskRowHost.CancelEdit(TaskItemViewModel row) => CancelEditCore(row);

    private void CancelEditCore(TaskItemViewModel row)
    {
        row.EndEdit();
        if (Editing == row) Editing = null;
    }

    /// <summary>Esc / collapse: drops an open edit form and any dialog card without saving.</summary>
    public void CancelTransientUi()
    {
        if (Editing is { } r) CancelEditCore(r);
        ListEditor = null;
        DeleteList = null;
        Confirm = null;
    }

    async Task ITaskRowHost.SaveEditAsync(TaskItemViewModel row)
    {
        var title = row.EditTitle.Trim();
        if (title.Length is < 1 or > TaskItem.MaxTitleLength)
        {
            row.EditError = $"The title must be 1 to {TaskItem.MaxTitleLength} characters.";
            return;
        }
        var notes = row.EditNotes.Trim();
        if (notes.Length > TaskItem.MaxDescriptionLength)
        {
            row.EditError = $"Notes can be at most {TaskItem.MaxDescriptionLength} characters.";
            return;
        }
        var dueError = DueInput.Compose(row.EditDate, row.EditTime, out var due, out var hasTime);
        if (dueError is not null)
        {
            row.EditError = dueError;
            return;
        }

        bool saved = await RunAsync("save the task", async () =>
        {
            var item = row.Model.Clone();
            bool priorityChanged = item.Priority != row.EditPriority;
            bool listChanged = item.ListId != row.EditListId;
            item.Title = title;
            item.Description = notes.Length == 0 ? null : notes;
            item.Priority = row.EditPriority;
            item.ListId = row.EditListId;
            item.DueDate = due;
            item.DueHasTime = hasTime;
            if (_lists.All(l => l.Id != item.ListId))
                throw new InvalidOperationException("That list no longer exists. Pick another list.");

            await _tasksRepo.UpdateAsync(item);
            CancelEditCore(row);
            // A different single list is on screen: the row slides out after saving. A new priority fades the row in at its new place.
            if (priorityChanged && !listChanged) row.PlayEnter = true;
            await ReloadAsync(leave: RowLeave.SlideOut);
        });
        if (!saved && row.IsEditing) row.EditError = NoticeText;
    }

    // ---- reorder (drag, or Alt+Up / Alt+Down) ----

    /// <summary>Ids of the rows shown in this row's (list, priority) group, top to bottom.</summary>
    public List<long> GroupIds(long listId, Priority priority) =>
        Tasks.Where(r => r.ListId == listId && r.Priority == priority && !r.Model.IsCompleted).Select(r => r.Id).ToList();

    public Task MoveAsync(TaskItemViewModel row, int insertIndex)
    {
        var ids = GroupIds(row.ListId, row.Priority);
        var order = ReorderRules.Move(ids, row.Id, insertIndex);
        return order is null ? Task.CompletedTask : ApplyOrderAsync(row.ListId, row.Priority, order);
    }

    Task ITaskRowHost.StepAsync(TaskItemViewModel row, int direction)
    {
        if (!CanReorder) return Task.CompletedTask;
        var order = ReorderRules.Step(GroupIds(row.ListId, row.Priority), row.Id, direction);
        return order is null ? Task.CompletedTask : ApplyOrderAsync(row.ListId, row.Priority, order);
    }

    private Task ApplyOrderAsync(long listId, Priority priority, IReadOnlyList<long> visibleOrder) =>
        RunAsync("reorder the tasks", async () =>
        {
            // The visible tasks get the new order; the group's other tasks (done ones) follow in their old order.
            var rest = _tasks.Where(t => t.ListId == listId && t.Priority == priority && !visibleOrder.Contains(t.Id))
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Id).Select(t => t.Id);
            await _tasksRepo.ReorderAsync(listId, priority, visibleOrder.Concat(rest).ToList());
            await ReloadAsync();
        });

    void ITaskRowHost.NoteTyping() => _director.Keep(Reaction.Focused);

    // ---- one-shot notices ----

    private void ShowNotice(string text, bool error)
    {
        NoticeText = text;
        NoticeIsError = error;
        HasNotice = true;
        _noticeTimer.Stop();
        _noticeTimer.Start();
    }

    private void HideNotice()
    {
        _noticeTimer.Stop();
        HasNotice = false;
    }

    [RelayCommand]
    private void DismissNotice() => HideNotice();

    private static DispatcherTimer OneShot(TimeSpan interval, Action tick)
    {
        var t = new DispatcherTimer(DispatcherPriority.Normal) { Interval = interval };
        t.Tick += (_, _) => { t.Stop(); tick(); };
        return t;
    }

    // ---- reconciling the visible rows with the data ----

    /// <summary>The tasks to show now. A row that was just ticked (or unticked) stays for its 2 s even though it no longer matches.</summary>
    private IReadOnlyList<TaskItem> VisibleTasks()
    {
        long? scope = SelectedListId == 0 ? null : SelectedListId;
        if (_lingering.Count == 0) return TaskSorter.Apply(_tasks, Filter, scope, _lists);
        var projected = _tasks.Select(t =>
        {
            if (!_lingering.Contains(t.Id)) return t;
            var c = t.Clone();
            c.IsCompleted = Filter != TaskFilter.Active;
            c.CompletedAt ??= c.UpdatedAt;
            return c;
        });
        return TaskSorter.Apply(projected, Filter, scope, _lists).Select(t => _tasksById[t.Id]).ToList();
    }

    /// <summary>
    /// Makes <see cref="Tasks"/> match the data with the fewest changes: rows that left animate out first, new rows are
    /// inserted, moved rows are moved, the rest are updated in place (so animations on them are not interrupted).
    /// </summary>
    private async Task ReconcileAsync(RowLeave? leave = null, ISet<long>? enter = null)
    {
        var nowLocal = _clock.Now;
        var listsById = _lists.ToDictionary(l => l.Id);
        bool showList = SelectedListId == 0;
        var desired = VisibleTasks();
        var wanted = desired.Select(t => t.Id).ToHashSet();

        var gone = Tasks.Where(r => !wanted.Contains(r.Id)).ToList();
        if (gone.Count > 0)
        {
            if (leave is { } kind && !(MotionPolicy.Current?.IsReduced ?? true))
                await Task.WhenAll(gone.Select(r => r.PlayLeaveAsync(kind)));
            foreach (var r in gone) Tasks.Remove(r);
        }

        var rows = Tasks.ToDictionary(r => r.Id);
        for (int i = 0; i < desired.Count; i++)
        {
            var t = desired[i];
            listsById.TryGetValue(t.ListId, out var list);
            if (i < Tasks.Count && Tasks[i].Id == t.Id)
            {
                Tasks[i].Update(t, list, showList, nowLocal);
                continue;
            }
            if (rows.TryGetValue(t.Id, out var existing))
            {
                // A moved row gets a new view model: WPF keeps one UI Automation node per item, and that node goes stale
                // when the same item is given a new container (screen readers would then see an empty row).
                bool priorityChanged = existing.Priority != t.Priority;
                Tasks.RemoveAt(Tasks.IndexOf(existing));
                var moved = new TaskItemViewModel(t, list, showList, nowLocal, this) { PlayEnter = priorityChanged };
                Tasks.Insert(i, moved);
                rows[t.Id] = moved;
            }
            else
            {
                var row = new TaskItemViewModel(t, list, showList, nowLocal, this);
                if (enter?.Contains(t.Id) == true) row.PlayEnter = true;
                Tasks.Insert(i, row);
                rows[t.Id] = row;
            }
        }

        EmptyTitle = Filter == TaskFilter.Completed ? "Nothing completed yet." : $"All done in {ScopeName()}";
        EmptyHint = Filter == TaskFilter.Completed ? "" : "Add something new above.";
        IsEmpty = Tasks.Count == 0;
    }

    private string ScopeName() =>
        SelectedListId != 0 && _lists.FirstOrDefault(l => l.Id == SelectedListId) is { } l ? l.Name : "all lists";

    private void RefreshCounts()
    {
        var nowLocal = _clock.Now;
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
