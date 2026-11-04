using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TaskIsland.App.Controls;
using TaskIsland.App.Services;
using TaskIsland.App.ViewModels;
using TaskIsland.Core.Mascot;
using TaskIsland.Core.Models;

namespace TaskIsland.App.Views;

/// <summary>The expanded to-do panel. Created while the island rests after start-up (PLAN.md section 6.2) and then kept.</summary>
public partial class PanelView : UserControl
{
    private const double FaceScale = 28.0 / FaceRig.FrameHeight;
    private const double EdgeScroll = 28;
    private readonly IslandViewModel _vm;
    private TaskItemViewModel? _marked;

    public PanelView(IslandViewModel vm, bool mascot)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Face = new FaceRig(withShell: true) { RenderTransform = new ScaleTransform(FaceScale, FaceScale) };
        // The rig's pill (x 4..236, y 4..60) scaled into the 105 x 28 header slot.
        Canvas.SetLeft(Face, -4 * FaceScale);
        Canvas.SetTop(Face, -4 * FaceScale);
        FaceHost.Children.Add(Face);
        Face.Show(Reaction.Neutral, animate: false);
        SetMascot(mascot);

        vm.PropertyChanged += OnVmChanged;
        vm.AddRejected += (_, _) => Shake();
        SyncBar(UndoBar, UndoSlide, vm.HasUndo, animate: false);
        SyncBar(NoticeBar, NoticeSlide, vm.HasNotice, animate: false);
        Loaded += (_, _) =>
        {
            SyncFilter(animate: false);
            SyncProgress(animate: false);
        };
    }

    /// <summary>The 28 px face left of the title; reacts to events while the panel is open.</summary>
    public FaceRig Face { get; }

    public event EventHandler? CollapseRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? SettingsRequested;

    public bool QuickAddHasText => QuickAddBox.Text.Length > 0;

    /// <summary>A calendar popup (a window of its own) is open; the island must not treat that as a click outside.</summary>
    public bool IsPopupOpen => FindRows().Any(r => r.IsPopupOpen);

    public void ClearQuickAdd() => QuickAddBox.Clear();

#if DEBUG
    /// <summary>Debug screenshots: opens the due-date popup of the row that is being edited.</summary>
    public void OpenFirstCalendar() => FindRows().FirstOrDefault(r => r.DataContext is TaskItemViewModel { IsEditing: true })?.OpenCalendar();
#endif

    public void FocusQuickAdd()
    {
        QuickAddBox.Focus();
        Keyboard.Focus(QuickAddBox);
    }

    public void SetMascot(bool on)
    {
        FaceHost.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        TickIcon.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IslandViewModel.Filter):
                SyncFilter(animate: true);
                CrossfadeList();
                break;
            case nameof(IslandViewModel.ScopeFraction):
                SyncProgress(animate: true);
                break;
            case nameof(IslandViewModel.SelectedListId):
                CrossfadeList();
                break;
            case nameof(IslandViewModel.NewPriority):
                // Priority change: the chip's colour and label crossfade (150 ms).
                if (!(MotionPolicy.Current?.IsReduced ?? true))
                    Anim.Keys(NewPriorityChip, OpacityProperty, FluentEase.Decelerate, null, (0, 0.25), (150, 1));
                break;
            case nameof(IslandViewModel.HasUndo):
                SyncBar(UndoBar, UndoSlide, _vm.HasUndo, animate: true);
                break;
            case nameof(IslandViewModel.HasNotice):
                SyncBar(NoticeBar, NoticeSlide, _vm.HasNotice, animate: true);
                break;
            case nameof(IslandViewModel.ListEditor):
                if (_vm.ListEditor is not null)
                    Dispatcher.BeginInvoke(() =>
                    {
                        ListNameBox.Focus();
                        Keyboard.Focus(ListNameBox);
                        ListNameBox.SelectAll();
                    }, System.Windows.Threading.DispatcherPriority.Input);
                else if (!_vm.HasOverlay) FocusQuickAdd();
                break;
            case nameof(IslandViewModel.HasOverlay):
                if (!_vm.HasOverlay && _vm.Editing is null) FocusQuickAdd();
                break;
        }
    }

    /// <summary>Switching list or filter: the task list fades in again (fast token, opacity only).</summary>
    private void CrossfadeList()
    {
        if (MotionPolicy.Current?.IsReduced ?? true) return;
        Anim.Keys(TaskList, OpacityProperty, FluentEase.Decelerate, null, (0, 0.3), (MotionPolicy.Current!.Fast.TotalMilliseconds, 1));
    }

    /// <summary>The selected-segment pill slides between segments (normal token, TranslateTransform only).</summary>
    private void SyncFilter(bool animate)
    {
        RadioButton target = _vm.Filter switch
        {
            TaskFilter.Completed => FilterCompleted,
            TaskFilter.All => FilterAll,
            _ => FilterActive,
        };
        target.IsChecked = true;
        int index = _vm.Filter switch { TaskFilter.Completed => 1, TaskFilter.All => 2, _ => 0 };
        var d = animate ? MotionPolicy.Current?.Normal ?? TimeSpan.Zero : TimeSpan.Zero;
        Anim.To(FilterSlide, TranslateTransform.XProperty, index * FilterIndicator.Width, d, FluentEase.Decelerate);
    }

    /// <summary>Progress bar width tweens via ScaleX, never Width.</summary>
    private void SyncProgress(bool animate)
    {
        var d = animate ? MotionPolicy.Current?.Normal ?? TimeSpan.Zero : TimeSpan.Zero;
        Anim.To(ProgressScale, ScaleTransform.ScaleXProperty, _vm.ScopeFraction, d, FluentEase.Decelerate);
    }

    /// <summary>The Undo bar and the notice slide up and fade in (normal token) when they appear.</summary>
    private static void SyncBar(Border bar, TranslateTransform slide, bool show, bool animate)
    {
        if (!show)
        {
            bar.Visibility = Visibility.Collapsed;
            return;
        }
        bool wasHidden = bar.Visibility != Visibility.Visible;
        bar.Visibility = Visibility.Visible;
        var d = animate && wasHidden ? MotionPolicy.Current?.Normal ?? TimeSpan.Zero : TimeSpan.Zero;
        if (d <= TimeSpan.Zero) return;
        Anim.Keys(bar, OpacityProperty, FluentEase.Decelerate, null, (0, 0), (d.TotalMilliseconds, 1));
        Anim.Keys(slide, TranslateTransform.YProperty, FluentEase.Decelerate, null, (0, 8), (d.TotalMilliseconds, 0));
    }

    /// <summary>Empty title: a subtle shake instead of an error.</summary>
    private void Shake()
    {
        if (MotionPolicy.Current?.IsReduced ?? true) return;
        Anim.Keys(QuickAddShake, TranslateTransform.XProperty, null, null,
            (0, 0), (40, -4), (80, 4), (120, -3), (160, 3), (200, 0));
    }

    private void QuickAdd_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        _vm.AddCommand.Execute(null);
    }

    private void ListName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        _vm.SaveListEditorCommand.Execute(null);
    }

    private void Chips_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ChipScroller.ScrollableWidth <= 0) return;
        ChipScroller.ScrollToHorizontalOffset(ChipScroller.HorizontalOffset - e.Delta / 3.0);
        e.Handled = true;
    }

    // ---- list chip menu ----

    private static ListChipViewModel? ChipOf(object sender)
    {
        var owner = sender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement target } } ? target
            : sender as FrameworkElement;
        return owner?.DataContext as ListChipViewModel;
    }

    /// <summary>The All chip has no menu, and the move items are greyed out at the ends.</summary>
    private void Chip_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ListChipViewModel chip } el || chip.IsAll)
        {
            e.Handled = true;
            return;
        }
        if (el.ContextMenu is { } menu && menu.Items.Count >= 4)
        {
            ((MenuItem)menu.Items[2]).IsEnabled = _vm.CanMoveList(chip, -1);
            ((MenuItem)menu.Items[3]).IsEnabled = _vm.CanMoveList(chip, +1);
        }
    }

    private void ChipRename_Click(object sender, RoutedEventArgs e)
    {
        if (ChipOf(sender) is { } chip) _vm.RenameListCommand.Execute(chip);
    }

    private void ChipLeft_Click(object sender, RoutedEventArgs e)
    {
        if (ChipOf(sender) is { } chip) _ = _vm.MoveListAsync(chip, -1);
    }

    private void ChipRight_Click(object sender, RoutedEventArgs e)
    {
        if (ChipOf(sender) is { } chip) _ = _vm.MoveListAsync(chip, +1);
    }

    private void ChipDelete_Click(object sender, RoutedEventArgs e)
    {
        if (ChipOf(sender) is { } chip) _vm.OpenDeleteListCommand.Execute(chip);
    }

    /// <summary>In All lists the quick-add chip picks the list for new tasks.</summary>
    private void QuickAddList_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = QuickAddListButton, Placement = PlacementMode.Top };
        foreach (var chip in _vm.Chips.Where(c => !c.IsAll))
        {
            var item = new MenuItem { Header = chip.Name };
            System.Windows.Automation.AutomationProperties.SetName(item, "New tasks go to " + chip.Name);
            long id = chip.Id;
            item.Click += (_, _) => _vm.SetQuickAddList(id);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        MoreMenu.PlacementTarget = MoreButton;
        MoreMenu.Placement = PlacementMode.Bottom;
        MoreMenu.IsOpen = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CollapseRequested?.Invoke(this, EventArgs.Empty);

    private void Settings_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void Exit_Click(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);

    // ---- drag to reorder: drop target (PLAN.md section 6.4) ----

    private static bool TryDragged(DragEventArgs e, out TaskItemViewModel row)
    {
        row = null!;
        if (!e.Data.GetDataPresent(TaskRow.DragFormat) || e.Data.GetData(TaskRow.DragFormat) is not TaskItemViewModel r) return false;
        row = r;
        return true;
    }

    private void TaskList_DragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        var pos = e.GetPosition(TaskList);
        if (!TryDragged(e, out var dragged) || !_vm.CanReorder)
        {
            e.Effects = DragDropEffects.None;
            Mark(null, DropMarker.None);
            return;
        }
        AutoScroll(pos);
        var hit = HitRow(pos);
        if (hit is not { Row.DataContext: TaskItemViewModel target } h || !SameGroup(dragged, target))
        {
            e.Effects = DragDropEffects.None; // outside the priority group: the "no" cursor
            Mark(null, DropMarker.None);
            return;
        }
        e.Effects = DragDropEffects.Move;
        Mark(target, h.Below ? DropMarker.Below : DropMarker.Above);
    }

    private void TaskList_DragLeave(object sender, DragEventArgs e) => Mark(null, DropMarker.None);

    private void TaskList_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        Mark(null, DropMarker.None);
        if (!TryDragged(e, out var dragged) || !_vm.CanReorder) return;
        if (HitRow(e.GetPosition(TaskList)) is not { Row.DataContext: TaskItemViewModel target } h || !SameGroup(dragged, target)) return;
        var ids = _vm.GroupIds(dragged.ListId, dragged.Priority);
        int index = ids.IndexOf(target.Id) + (h.Below ? 1 : 0);
        _ = _vm.MoveAsync(dragged, index);
    }

    private static bool SameGroup(TaskItemViewModel a, TaskItemViewModel b) =>
        a.ListId == b.ListId && a.Priority == b.Priority && !b.Model.IsCompleted;

    private void Mark(TaskItemViewModel? row, DropMarker marker)
    {
        if (_marked is not null && _marked != row) _marked.DropMarker = DropMarker.None;
        _marked = row;
        if (row is not null) row.DropMarker = marker;
    }

    private (TaskRow Row, bool Below)? HitRow(Point inList)
    {
        foreach (var row in FindRows())
        {
            double top = row.TransformToAncestor(TaskList).Transform(new Point(0, 0)).Y;
            if (inList.Y >= top && inList.Y < top + row.ActualHeight)
                return (row, inList.Y > top + row.ActualHeight / 2);
        }
        return null;
    }

    private void AutoScroll(Point inList)
    {
        var scroller = Descendants<ScrollViewer>(TaskList).FirstOrDefault();
        if (scroller is null) return;
        if (inList.Y < EdgeScroll) scroller.ScrollToVerticalOffset(scroller.VerticalOffset - 12);
        else if (inList.Y > TaskList.ActualHeight - EdgeScroll) scroller.ScrollToVerticalOffset(scroller.VerticalOffset + 12);
    }

    private IEnumerable<TaskRow> FindRows() => Descendants<TaskRow>(TaskList);

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }
}
