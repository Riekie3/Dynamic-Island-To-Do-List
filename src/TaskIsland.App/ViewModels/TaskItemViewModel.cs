using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;

namespace TaskIsland.App.ViewModels;

/// <summary>One task row. The due label is computed once when the row is built (no per-second bindings).</summary>
public sealed partial class TaskItemViewModel : ObservableObject
{
    private readonly Action<TaskItemViewModel> _completedChanged;

    public TaskItemViewModel(TaskItem model, TodoList? list, bool showListName, DateTime nowLocal,
        Action<TaskItemViewModel> completedChanged)
    {
        Model = model;
        _completedChanged = completedChanged;
        ListName = list?.Name ?? "";
        ListBrush = BrushCache.Frozen(BrushCache.Parse(list?.ColorHex ?? TodoList.DefaultColor), 1.0);
        ShowListName = showListName;
        _isCompleted = model.IsCompleted;
        UpdateDue(nowLocal);
    }

    public TaskItem Model { get; }
    public long Id => Model.Id;
    public string Title => Model.Title;
    public Priority Priority => Model.Priority;
    public string ListName { get; }
    public Brush ListBrush { get; }
    public bool ShowListName { get; }
    public bool HasDue => Model.DueDate is not null;

    [ObservableProperty]
    private string _dueText = "";

    [ObservableProperty]
    private bool _isOverdue;

    [ObservableProperty]
    private bool _isCompleted;

    partial void OnIsCompletedChanged(bool value)
    {
        if (Model.IsCompleted == value) return;
        _completedChanged(this);
    }

    public void UpdateDue(DateTime nowLocal)
    {
        if (Model.DueDate is not { } due) return;
        DueText = DueLabel.Format(due, Model.DueHasTime, Model.IsCompleted, nowLocal);
        IsOverdue = DueLabel.IsOverdue(due, Model.DueHasTime, Model.IsCompleted, nowLocal);
    }
}
