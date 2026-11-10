using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TaskIsland.App.Controls;
using TaskIsland.App.Services;
using TaskIsland.App.ViewModels;

namespace TaskIsland.App.Views;

/// <summary>
/// One task row: tick animation, inline edit form, enter / leave animations and the drag source. Every animation is
/// finite and render-only (opacity, transforms; PLAN.md section 6.2 rules).
/// </summary>
public partial class TaskRow : UserControl
{
    private const double DragThreshold = 6;
    private TaskItemViewModel? _vm;
    private bool _userToggle;
    private Point? _dragStart;

    public TaskRow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Check.PreviewMouseLeftButtonDown += (_, _) => _userToggle = true;
        Check.PreviewKeyDown += (_, e) => { if (e.Key == Key.Space) _userToggle = true; };
        Check.Click += (_, _) => _userToggle = false;
    }

    // ---- DataContext (rows are recycled, so every visual state is reset here) ----

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.LeaveHandler = null;
            _vm.PropertyChanged -= OnVmChanged;
        }
        _vm = e.NewValue as TaskItemViewModel;
        ResetVisuals();
        if (_vm is null) return;
        _vm.LeaveHandler = PlayLeaveAsync;
        _vm.PropertyChanged += OnVmChanged;
        StrikeLine.Opacity = _vm.IsCompleted ? 1 : 0;
        UpdateStrikeMode();
        if (_vm.PlayEnter)
        {
            _vm.PlayEnter = false;
            PlayEnter();
        }
        if (_vm.IsEditing) FocusTitle();
    }

    private void ResetVisuals()
    {
        Anim.Stop(this, OpacityProperty, 1);
        Anim.Stop(RowScale, ScaleTransform.ScaleYProperty, 1);
        Anim.Stop(RowSlide, TranslateTransform.XProperty, 0);
        Anim.Stop(RowSlide, TranslateTransform.YProperty, 0);
        Anim.Stop(StrikeLine, OpacityProperty, 0);
        DatePopup.IsOpen = false;
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TaskItemViewModel.IsCompleted):
                SyncCompleted();
                break;
            case nameof(TaskItemViewModel.IsEditing):
                if (_vm!.IsEditing) FocusTitle();
                else DatePopup.IsOpen = false;
                break;
        }
    }

    // ---- tick: done circle scales in, the strikethrough line fades in (200 ms) ----

    // A long title wraps onto several lines. A single line through the middle of a wrapped block looks wrong, so wrapped
    // titles use a normal text strikethrough instead (instant); one-line titles keep the fading line.
    private void TitleText_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateStrikeMode();

    private void UpdateStrikeMode()
    {
        bool multi = TitleText.ActualHeight > TitleText.FontSize * 2.0;
        StrikeLine.Visibility = multi ? Visibility.Collapsed : Visibility.Visible;
        TitleText.TextDecorations = multi && _vm?.IsCompleted == true ? TextDecorations.Strikethrough : null;
    }

    private void SyncCompleted()
    {
        if (_vm is null) return;
        UpdateStrikeMode();
        double to = _vm.IsCompleted ? 1 : 0;
        var d = MotionPolicy.Current?.Normal ?? TimeSpan.Zero;
        if (!_userToggle || d <= TimeSpan.Zero)
        {
            Anim.Stop(StrikeLine, OpacityProperty, to);
            return;
        }
        Anim.To(StrikeLine, OpacityProperty, to, d, FluentEase.Decelerate);
        if (_vm.IsCompleted) PlayTickScale();
    }

    /// <summary>The tick scales in (fast token); instant under reduced motion.</summary>
    private void PlayTickScale()
    {
        if (Check.Template.FindName("Done", Check) is not FrameworkElement done || done.RenderTransform is not ScaleTransform scale) return;
        var d = MotionPolicy.Current?.Fast ?? TimeSpan.Zero;
        if (d <= TimeSpan.Zero) return;
        // A transform declared in a control template is frozen and cannot be animated; give this row its own.
        if (scale.IsFrozen) done.RenderTransform = scale = new ScaleTransform();
        Anim.Keys(scale, ScaleTransform.ScaleXProperty, FluentEase.Decelerate, null, (0, 0.6), (d.TotalMilliseconds, 1));
        Anim.Keys(scale, ScaleTransform.ScaleYProperty, FluentEase.Decelerate, null, (0, 0.6), (d.TotalMilliseconds, 1));
    }

    // ---- enter / leave ----

    /// <summary>New row: fades in while sliding down from -8 px (200 ms).</summary>
    private void PlayEnter()
    {
        var d = MotionPolicy.Current?.Normal ?? TimeSpan.Zero;
        if (d <= TimeSpan.Zero) return;
        double ms = d.TotalMilliseconds;
        Anim.Keys(this, OpacityProperty, FluentEase.Decelerate, null, (0, 0), (ms, 1));
        Anim.Keys(RowSlide, TranslateTransform.YProperty, FluentEase.Decelerate, null, (0, -8), (ms, 0));
    }

    /// <summary>
    /// Delete: the row fades and shrinks (180 ms). Slide-out: it slides right and fades (220 ms). The returned task
    /// also ends after a safety delay, so a row that is no longer on screen can never hold up the list.
    /// </summary>
    private async Task PlayLeaveAsync(RowLeave kind)
    {
        if (MotionPolicy.Current?.IsReduced ?? true) return;
        var tcs = new TaskCompletionSource();
        double ms = kind == RowLeave.Delete ? 180 : 220;
        if (kind == RowLeave.Delete)
        {
            Anim.Keys(this, OpacityProperty, FluentEase.Accelerate, null, (0, 1), (ms, 0));
            Anim.Keys(RowScale, ScaleTransform.ScaleYProperty, FluentEase.Accelerate, () => tcs.TrySetResult(), (0, 1), (ms, 0.6));
        }
        else
        {
            Anim.Keys(this, OpacityProperty, FluentEase.Accelerate, null, (0, 1), (ms, 0));
            Anim.Keys(RowSlide, TranslateTransform.XProperty, FluentEase.Accelerate, () => tcs.TrySetResult(), (0, 0), (ms, 48));
        }
        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMilliseconds(ms + 150)));
    }

    // ---- inline edit ----

    private void FocusTitle() => Dispatcher.BeginInvoke(() =>
    {
        BringIntoView(); // the form is taller than a row: scroll so Save and Cancel are reachable
        TitleBox.Focus();
        Keyboard.Focus(TitleBox);
        TitleBox.SelectAll();
    }, System.Windows.Threading.DispatcherPriority.Input);

    private void TitleBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _vm is null) return;
        e.Handled = true;
        _vm.SaveEditCommand.Execute(null);
    }

    private void DateButton_Click(object sender, RoutedEventArgs e)
    {
        Calendar.SelectedDate = _vm?.EditDate;
        Calendar.ShowSelectedMonth();
        DatePopup.IsOpen = !DatePopup.IsOpen;
    }

    /// <summary>Opens the due-date popup (also used by the Debug screenshot demo).</summary>
    public void OpenCalendar() => DateButton_Click(this, new RoutedEventArgs());

    private void SetDate(DateTime? date)
    {
        _vm?.SetDateCommand.Execute(date);
        DatePopup.IsOpen = false;
    }

    private void Today_Click(object sender, RoutedEventArgs e) => SetDate(DateTime.Today);
    private void Tomorrow_Click(object sender, RoutedEventArgs e) => SetDate(DateTime.Today.AddDays(1));
    private void NoDate_Click(object sender, RoutedEventArgs e)
    {
        _vm?.ClearDueCommand.Execute(null);
        DatePopup.IsOpen = false;
    }
    private void Calendar_DateChosen(DateTime date) => SetDate(date);

    /// <summary>The popup is a window of its own; give the focus back to the island so click-outside still works.</summary>
    private void DatePopup_Closed(object? sender, EventArgs e)
    {
        if (Window.GetWindow(this) is { } w && _vm?.IsEditing == true) w.Activate();
    }

    public bool IsPopupOpen => DatePopup.IsOpen;

    // ---- keyboard: F2 edit, Del delete, Alt+Up / Alt+Down reorder ----

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (_vm is null || e.Handled || _vm.IsEditing) return;
        if (e.OriginalSource is TextBoxBase) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.F2)
        {
            e.Handled = true;
            _vm.EditCommand.Execute(null);
        }
        else if (key == Key.Delete)
        {
            e.Handled = true;
            _vm.DeleteCommand.Execute(null);
        }
        else if (key is Key.Up or Key.Down && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) && _vm.CanReorder)
        {
            e.Handled = true;
            _vm.StepCommand.Execute(key == Key.Up ? "Up" : "Down");
        }
    }

    // ---- drag source (PLAN.md section 6.4: the whole row is the handle, after a 6 px move) ----

    /// <summary>Format of the dragged row inside this process.</summary>
    public static readonly string DragFormat = typeof(TaskItemViewModel).FullName!;

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        _dragStart = _vm is { CanReorder: true, IsEditing: false, IsCompleted: false } && !OnControl(e.OriginalSource as DependencyObject)
            ? e.GetPosition(this)
            : null;
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (_dragStart is not { } start || _vm is null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { _dragStart = null; return; }
        if ((e.GetPosition(this) - start).Length < DragThreshold) return;
        _dragStart = null;
        if (FindPanel() is not { } panel) return;
        panel.BeginTaskDrag(this, _vm, e.GetPosition(panel));
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        _dragStart = null;
    }

    private bool OnControl(DependencyObject? source)
    {
        while (source is not null && source != this)
        {
            if (source is ButtonBase or TextBoxBase) return true;
            source = source is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return false;
    }

    private PanelView? FindPanel()
    {
        DependencyObject? current = this;
        while (current is not null)
        {
            if (current is PanelView panel) return panel;
            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return null;
    }

    // ---- drag gap preview ----

    /// <summary>The row's current vertical preview offset (0 when no drag is in progress).</summary>
    public double DragShiftY => RowSlide.Y;

    /// <summary>Slides the row out of the way for the drag preview (fast token; instant under reduced motion).</summary>
    public void ShiftForDrag(double y)
    {
        var d = MotionPolicy.Current?.Fast ?? TimeSpan.Zero;
        Anim.To(RowSlide, TranslateTransform.YProperty, y, d, FluentEase.Decelerate);
    }

    /// <summary>Puts the row back at once when the drop finishes or the drag is cancelled.</summary>
    public void ResetDragShift() => Anim.Stop(RowSlide, TranslateTransform.YProperty, 0);
}
