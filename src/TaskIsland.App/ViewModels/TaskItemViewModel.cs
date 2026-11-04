using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;

namespace TaskIsland.App.ViewModels;

/// <summary>How a row leaves the list: collapses and fades (deleted) or slides out (filtered away, moved to another list).</summary>
public enum RowLeave { Delete, SlideOut }

/// <summary>Where the insertion line is drawn while a row is dragged over this one.</summary>
public enum DropMarker { None, Above, Below }

/// <summary>What a row needs from the view model that owns it.</summary>
public interface ITaskRowHost
{
    bool CanReorder { get; }
    void RowCompletedChanged(TaskItemViewModel row);
    void BeginEdit(TaskItemViewModel row);
    Task SaveEditAsync(TaskItemViewModel row);
    void CancelEdit(TaskItemViewModel row);
    Task DeleteAsync(TaskItemViewModel row);
    Task StepAsync(TaskItemViewModel row, int direction);
    void NoteTyping();
}

/// <summary>
/// One task row: display state plus the inline-edit draft (PLAN.md sections 6.4 and 6.5). The due label is computed
/// when the row is built or updated (no per-second bindings).
/// </summary>
public sealed partial class TaskItemViewModel : ObservableObject
{
    private readonly ITaskRowHost _host;
    private bool _silent;

    public TaskItemViewModel(TaskItem model, TodoList? list, bool showListName, DateTime nowLocal, ITaskRowHost host)
    {
        _host = host;
        Model = model;
        Update(model, list, showListName, nowLocal);
    }

    public TaskItem Model { get; private set; }
    public long Id => Model.Id;
    public long ListId => Model.ListId;

    /// <summary>Set by the view when the row is realised, so the view model can wait for a leave animation to finish.</summary>
    public Func<RowLeave, Task>? LeaveHandler { get; set; }

    /// <summary>Consumed by the view: play the fade-in / slide-in once (new row, restored row, row moved by a priority change).</summary>
    public bool PlayEnter { get; set; }

    public Task PlayLeaveAsync(RowLeave kind) => LeaveHandler?.Invoke(kind) ?? Task.CompletedTask;

    public bool CanReorder => _host.CanReorder;

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private Priority _priority = Priority.Medium;
    [ObservableProperty] private string _listName = "";
    [ObservableProperty] private Brush _listBrush = Brushes.Transparent;
    [ObservableProperty] private bool _showListName;
    [ObservableProperty] private bool _hasDue;
    [ObservableProperty] private string _dueText = "";
    [ObservableProperty] private bool _isOverdue;
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private DropMarker _dropMarker;

    partial void OnIsCompletedChanged(bool value)
    {
        if (_silent) return;
        _host.RowCompletedChanged(this);
    }

    /// <summary>Puts the tick back (or sets it) without telling the owner, for example after a failed save.</summary>
    public void SetCompletedSilently(bool value)
    {
        _silent = true;
        IsCompleted = value;
        _silent = false;
    }

    public void Update(TaskItem model, TodoList? list, bool showListName, DateTime nowLocal)
    {
        Model = model;
        Title = model.Title;
        Priority = model.Priority;
        ListName = list?.Name ?? "";
        ListBrush = BrushCache.Frozen(BrushCache.Parse(list?.ColorHex ?? TodoList.DefaultColor), 1.0);
        ShowListName = showListName;
        SetCompletedSilently(model.IsCompleted);
        HasDue = model.DueDate is not null;
        UpdateDue(nowLocal);
        OnPropertyChanged(nameof(CanReorder));
    }

    public void UpdateDue(DateTime nowLocal)
    {
        if (Model.DueDate is not { } due)
        {
            DueText = "";
            IsOverdue = false;
            return;
        }
        DueText = DueLabel.Format(due, Model.DueHasTime, Model.IsCompleted, nowLocal);
        IsOverdue = DueLabel.IsOverdue(due, Model.DueHasTime, Model.IsCompleted, nowLocal);
    }

    // ---- inline edit (PLAN.md section 6.5) ----

    public ObservableCollection<ListChoiceViewModel> ListChoices { get; } = [];

    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editTitle = "";
    [ObservableProperty] private string _editNotes = "";
    [ObservableProperty] private DateTime? _editDate;
    [ObservableProperty] private string _editTime = "";
    [ObservableProperty] private Priority _editPriority = Priority.Medium;
    [ObservableProperty] private long _editListId;
    [ObservableProperty] private string _editError = "";
    [ObservableProperty] private string _editDateText = "No date";

    public bool IsEditHigh => EditPriority == Priority.High;
    public bool IsEditMedium => EditPriority == Priority.Medium;
    public bool IsEditLow => EditPriority == Priority.Low;
    public bool HasEditDate => EditDate is not null;

    partial void OnEditPriorityChanged(Priority value)
    {
        OnPropertyChanged(nameof(IsEditHigh));
        OnPropertyChanged(nameof(IsEditMedium));
        OnPropertyChanged(nameof(IsEditLow));
    }

    partial void OnEditDateChanged(DateTime? value)
    {
        EditDateText = value is { } d ? d.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture) : "No date";
        OnPropertyChanged(nameof(HasEditDate));
    }

    partial void OnEditTitleChanged(string value)
    {
        if (IsEditing) _host.NoteTyping();
    }

    /// <summary>Fills the draft from the task and offers the given lists.</summary>
    public void StartEdit(IEnumerable<TodoList> lists)
    {
        EditTitle = Model.Title;
        EditNotes = Model.Description ?? "";
        EditPriority = Model.Priority;
        EditDate = Model.DueDate?.Date;
        EditTime = Model.DueDate is { } d && Model.DueHasTime ? DueInput.FormatTime(d) : "";
        EditListId = Model.ListId;
        EditError = "";
        ListChoices.Clear();
        foreach (var l in lists)
            ListChoices.Add(new ListChoiceViewModel(l.Id, l.Name, BrushCache.Frozen(BrushCache.Parse(l.ColorHex), 1.0))
            { IsSelected = l.Id == Model.ListId });
        IsEditing = true;
    }

    public void EndEdit()
    {
        IsEditing = false;
        EditError = "";
    }

    [RelayCommand]
    private void Edit() => _host.BeginEdit(this);

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task DeleteAsync() => _host.DeleteAsync(this);

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task SaveEditAsync() => _host.SaveEditAsync(this);

    [RelayCommand]
    private void CancelEdit() => _host.CancelEdit(this);

    [RelayCommand]
    private void SetEditPriority(Priority priority) => EditPriority = priority;

    [RelayCommand]
    private void ChooseList(ListChoiceViewModel? choice)
    {
        if (choice is null) return;
        EditListId = choice.Id;
        foreach (var c in ListChoices) c.IsSelected = c.Id == choice.Id;
    }

    [RelayCommand]
    private void SetDate(DateTime? date) => EditDate = date;

    [RelayCommand]
    private void ClearDue()
    {
        EditDate = null;
        EditTime = "";
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task StepAsync(string? direction) => _host.StepAsync(this, direction == "Up" ? -1 : 1);
}
