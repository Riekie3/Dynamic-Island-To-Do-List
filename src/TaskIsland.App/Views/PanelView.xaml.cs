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
using TaskIsland.Core.Toasts;

namespace TaskIsland.App.Views;

/// <summary>The expanded to-do panel. Created while the island rests after start-up (PLAN.md section 6.2) and then kept.</summary>
public partial class PanelView : UserControl
{
    private const double FaceScale = 28.0 / FaceRig.FrameHeight;
    private const double EdgeScroll = 28;
    private readonly IslandViewModel _vm;

    public PanelView(IslandViewModel vm, bool mascot)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        AddHandler(DragDrop.GiveFeedbackEvent, new GiveFeedbackEventHandler(OnDragFeedback));
        Face = new FaceRig(withShell: true) { RenderTransform = new ScaleTransform(FaceScale, FaceScale) };
        // The rig's pill (x 4..236, y 4..60) scaled into the 105 x 28 header slot.
        Canvas.SetLeft(Face, -4 * FaceScale);
        Canvas.SetTop(Face, -4 * FaceScale);
        FaceHost.Children.Add(Face);
        Face.Show(Reaction.Neutral, animate: false);
        SetMascot(mascot);

        vm.PropertyChanged += OnVmChanged;
        vm.AddRejected += (_, _) => Shake();
        BuildToast();
        SyncToast(animate: false);
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
            case nameof(IslandViewModel.UndoText):
            case nameof(IslandViewModel.HasNotice):
            case nameof(IslandViewModel.NoticeText):
            case nameof(IslandViewModel.NoticeIsError):
                SyncToast(animate: true);
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

    // ---- Undo and notices as a Sileo-style toast (SILEO-PLAN.md S3) ----

    private ToastPresenter? _toasts;
    private int _toastKind;   // 0 none, 1 undo, 2 notice: what the toast shows now (a Dismiss click must not undo)

    private void BuildToast()
    {
        var toasts = new ToastPresenter(ToastLayer, expandsUp: true, "PanelToastFillBrush", "PanelToastStrokeBrush");
        toasts.ButtonClick += (_, _) =>
        {
            if (_toastKind == 1) _vm.UndoCommand.Execute(null);
            else if (_toastKind == 2) _vm.DismissNoticeCommand.Execute(null);
        };
        toasts.Clicked += (_, _) =>
        {
            if (_toastKind == 2) _vm.DismissNoticeCommand.Execute(null);   // a tap on a message dismisses it; Undo needs its button
        };
        _toasts = toasts;
    }

    /// <summary>
    /// Shows the newest of: an error notice, the Undo for a delete, an informational notice. The view model owns the
    /// timers (5 s); this only mirrors them.
    /// </summary>
    private void SyncToast(bool animate)
    {
        if (_toasts is not { } toasts) return;
        if (_vm.HasNotice && _vm.NoticeIsError)
        {
            _toastKind = 2;
            toasts.Show(ToastState.Error, "Error", _vm.NoticeText, "Dismiss", animate: animate);
        }
        else if (_vm.HasUndo)
        {
            _toastKind = 1;
            toasts.Show(ToastState.Action, _vm.UndoText, null, "Undo", animate: animate);
        }
        else if (_vm.HasNotice)
        {
            _toastKind = 2;
            toasts.Show(ToastState.Info, "Notice", _vm.NoticeText, "Dismiss", animate: animate);
        }
        else
        {
            _toastKind = 0;
            toasts.Hide(animate);
        }
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

    // ---- drag a list chip to a new place ----

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
