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
/// Reorder drag and drop (PLAN.md section 6.4). A drag lifts a snapshot of the chip or row into the panel's adorner
/// layer, dims the source and slides the other items aside so the destination gap is visible. All preview motion is a
/// render transform (section 6.2); the source dim/opacity is restored when the drop finishes.
/// </summary>
public partial class PanelView
{
    private const string ChipDragFormat = "TaskIsland.ListChip";
    private const double ChipLiftScale = 1.05;
    private const double RowLiftScale = 1.03;
    private const double DragDimOpacity = 0.2;

    private Point? _chipDragStart;
    private ChipDrag? _chipDrag;
    private RowDrag? _rowDrag;

    // ---- list chips: drag source ----

    private void Chip_PreviewMouseDown(object sender, MouseButtonEventArgs e) =>
        _chipDragStart = sender is FrameworkElement { DataContext: ListChipViewModel { IsAll: false } } && !_vm.HasOverlay
            ? e.GetPosition(ChipStrip)
            : null;

    private void Chip_PreviewMouseUp(object sender, MouseButtonEventArgs e) => _chipDragStart = null;

    private void Chip_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_chipDragStart is not { } start) return;
        if (e.LeftButton != MouseButtonState.Pressed) { _chipDragStart = null; return; }
        var delta = e.GetPosition(ChipStrip) - start;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance + 2) return;
        _chipDragStart = null;
        if (sender is not ButtonBase { DataContext: ListChipViewModel chip } button) return;
        button.ReleaseMouseCapture();   // so the button does not also count this as a click

        var state = BeginChipDrag(button, chip, e.GetPosition(this));
        if (state is null) return;
        try
        {
            DragDrop.DoDragDrop(button, new DataObject(ChipDragFormat, chip), DragDropEffects.Move);
        }
        finally
        {
            // A drop hands cleanup to the async move; every other ending (Esc, drop outside) cleans up here.
            if (!state.Dropped) EndChipDrag(state);
        }
    }

    private ChipDrag? BeginChipDrag(ButtonBase button, ListChipViewModel chip, Point pointer)
    {
        if (_chipDrag is not null || _rowDrag is not null) return null;

        var items = ChipSlots();
        int from = items.FindIndex(i => i.Chip.Id == chip.Id);
        if (from < 0) return null;
        var source = items[from];

        var lift = DragLift.Start(button, this, pointer, 14, ChipLiftScale, DragDimOpacity);
        var state = new ChipDrag(items, from, lift);
        _chipDrag = state;
        UpdateChipPreview(state, from + 1);
        return state;
    }

    /// <summary>Every list chip in switcher order, with the transform used to move its slot aside.</summary>
    private List<ChipSlot> ChipSlots()
    {
        var slots = new List<ChipSlot>();
        for (int i = 1; i < ChipItems.Items.Count; i++)
        {
            if (ChipItems.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement element) continue;
            if (element.DataContext is not ListChipViewModel chip) continue;
            double width = element.ActualWidth > 0 ? element.ActualWidth : element.DesiredSize.Width;
            slots.Add(new ChipSlot(element, chip, EnsureShift(element), width));
        }
        return slots;
    }

    // ---- list chips: preview + drop ----

    private void Chips_DragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        var pointer = e.GetPosition(this);
        _rowDrag?.Lift?.MoveTo(pointer);
        var state = _chipDrag;
        if (state is null || e.Data.GetData(ChipDragFormat) is not ListChipViewModel dragged || dragged.Id != state.Chip.Id)
        {
            e.Effects = DragDropEffects.None;
            return;
        }
        e.Effects = DragDropEffects.Move;
        ScrollChips(e.GetPosition(ChipScroller));
        state.Lift?.MoveTo(pointer);
        UpdateChipPreview(state, ChipSlotAt(e.GetPosition(ChipStrip).X));
    }

    private void Chips_DragLeave(object sender, DragEventArgs e)
    {
        if (_chipDrag is { } state) UpdateChipPreview(state, state.From + 1);
    }

    private void Chips_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        var state = _chipDrag;
        if (state is null || e.Data.GetData(ChipDragFormat) is not ListChipViewModel dragged || dragged.Id != state.Chip.Id) return;

        int slot = ChipSlotAt(e.GetPosition(ChipStrip).X);
        state.Dropped = true;
        UpdateChipPreview(state, slot);
        _ = FinishChipDropAsync(state, slot);
    }

    private async Task FinishChipDropAsync(ChipDrag state, int slot)
    {
        try
        {
            await _vm.MoveListToAsync(state.Chip, slot);
        }
        finally
        {
            EndChipDrag(state);
        }
    }

    /// <summary>Where a drop at <paramref name="x"/> puts the list: 0 = first, Lists.Count = last.</summary>
    private int ChipSlotAt(double x)
    {
        var state = _chipDrag;
        if (state is null) return 0;
        for (int i = 0; i < state.Items.Count; i++)
        {
            var item = state.Items[i];
            if (x < BaseLeft(item.Element, ChipStrip) + item.Width / 2) return i;
        }
        return state.Items.Count;
    }

    /// <summary>
    /// Moves the placeholder to the projected slot and slides the chips in between by one slot. Positions are read
    /// without the current preview translation, so an animation in flight can never feed back into the next update.
    /// </summary>
    private void UpdateChipPreview(ChipDrag state, int slot)
    {
        slot = Math.Clamp(slot, 0, state.Items.Count);
        if (slot == state.Slot) return;
        state.Slot = slot;

        int from = state.From;
        double width = state.Source.Width;
        double sourceBase = BaseLeft(state.Source.Element, ChipStrip);
        double boundary = slot < state.Items.Count
            ? BaseLeft(state.Items[slot].Element, ChipStrip)
            : state.Items.Count > 0
                ? BaseLeft(state.Items[^1].Element, ChipStrip) + state.Items[^1].Width
                : sourceBase + width;
        bool noChange = slot == from || slot == from + 1;
        double sourceShift = noChange ? 0 : (slot > from ? boundary - width : boundary) - sourceBase;

        for (int i = 0; i < state.Items.Count; i++)
        {
            var item = state.Items[i];
            double shift = i == from
                ? sourceShift
                : !noChange && slot > from && i > from && i < slot ? -width
                : !noChange && slot < from && i >= slot && i < from ? width
                : 0;
            MoveShift(item.Shift, shift);
        }
    }

    private void EndChipDrag(ChipDrag state)
    {
        if (ReferenceEquals(_chipDrag, state)) _chipDrag = null;
        foreach (var item in state.Items) Anim.Stop(item.Shift, TranslateTransform.XProperty, 0);
        state.Lift?.Dispose();
    }

    private void ScrollChips(Point inScroller)
    {
        if (inScroller.X < EdgeScroll) ChipScroller.ScrollToHorizontalOffset(ChipScroller.HorizontalOffset - 8);
        else if (inScroller.X > ChipScroller.ActualWidth - EdgeScroll)
            ChipScroller.ScrollToHorizontalOffset(ChipScroller.HorizontalOffset + 8);
    }

    // ---- task rows: drag source ----

    /// <summary>Starts a row drag after the 6 px threshold was crossed (called by <see cref="TaskRow"/>).</summary>
    public void BeginTaskDrag(TaskRow row, TaskItemViewModel item, Point pointer)
    {
        if (_rowDrag is not null || _chipDrag is not null || !_vm.CanReorder || item.IsEditing || item.IsCompleted) return;
        if (!ReferenceEquals(row.DataContext, item)) return;
        var ids = _vm.GroupIds(item.ListId, item.Priority);
        int from = ids.IndexOf(item.Id);
        if (from < 0 || ids.Count < 2) return;

        var lift = DragLift.Start(row, this, pointer, 12, RowLiftScale, DragDimOpacity);
        var state = new RowDrag(row, item, ids, from, row.ActualHeight, lift);
        _rowDrag = state;
        UpdateRowPreview(state, from + 1, 0);

        try
        {
            DragDrop.DoDragDrop(row, new DataObject(TaskRow.DragFormat, item), DragDropEffects.Move);
        }
        finally
        {
            if (!state.Dropped) EndRowDrag(state);
        }
    }

    // ---- task rows: preview + drop ----

    private void TaskList_DragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        var pointer = e.GetPosition(this);
        _chipDrag?.Lift?.MoveTo(pointer);
        var state = _rowDrag;
        if (state is null || !_vm.CanReorder ||
            e.Data.GetData(TaskRow.DragFormat) is not TaskItemViewModel dragged || dragged.Id != state.Item.Id)
        {
            e.Effects = DragDropEffects.None;
            if (state is not null) UpdateRowPreview(state, state.From + 1, 0);
            return;
        }

        var pos = e.GetPosition(TaskList);
        AutoScroll(pos);
        state.Lift?.MoveTo(pointer);

        var hit = HitRow(pos);
        if (hit is not { } h || !SameGroup(dragged, h.Item))
        {
            e.Effects = DragDropEffects.None;   // outside the priority group: the "no" cursor
            UpdateRowPreview(state, state.From + 1, 0);
            return;
        }

        int index = state.Ids.IndexOf(h.Item.Id);
        if (index < 0)
        {
            e.Effects = DragDropEffects.None;
            UpdateRowPreview(state, state.From + 1, 0);
            return;
        }

        e.Effects = DragDropEffects.Move;
        int slot = index + (h.Below ? 1 : 0);
        UpdateRowPreview(state, slot, h.Below ? h.Top + h.Height : h.Top);
    }

    private void TaskList_DragLeave(object sender, DragEventArgs e)
    {
        if (_rowDrag is { } state) UpdateRowPreview(state, state.From + 1, 0);
    }

    private void TaskList_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        var state = _rowDrag;
        if (state is null || !_vm.CanReorder) return;
        if (e.Data.GetData(TaskRow.DragFormat) is not TaskItemViewModel dragged || dragged.Id != state.Item.Id) return;
        if (HitRow(e.GetPosition(TaskList)) is not { } h || !SameGroup(dragged, h.Item)) return;

        int index = state.Ids.IndexOf(h.Item.Id);
        if (index < 0) return;
        int slot = index + (h.Below ? 1 : 0);
        state.Dropped = true;
        UpdateRowPreview(state, slot, h.Below ? h.Top + h.Height : h.Top);
        _ = FinishRowDropAsync(state, slot);
    }

    private async Task FinishRowDropAsync(RowDrag state, int slot)
    {
        try
        {
            await _vm.MoveAsync(state.Item, slot);
        }
        finally
        {
            EndRowDrag(state);
        }
    }

    /// <summary>
    /// Slides the rows between the source and the projected slot by one row height. The placeholder goes to the
    /// boundary measured in base layout coordinates; only rows inside the same (list, priority) group move.
    /// </summary>
    private void UpdateRowPreview(RowDrag state, int slot, double boundary)
    {
        slot = Math.Clamp(slot, 0, state.Ids.Count);
        if (slot == state.Slot) return;
        state.Slot = slot;

        int from = state.From;
        bool noChange = slot == from || slot == from + 1;
        double sourceBase = BaseTop(state.Source, TaskList);
        double sourceShift = noChange ? 0 : (slot > from ? boundary - state.SourceHeight : boundary) - sourceBase;

        foreach (var row in FindRows())
        {
            if (row.DataContext is not TaskItemViewModel item) continue;
            int index = state.Ids.IndexOf(item.Id);
            if (index < 0) continue;

            double shift = !noChange && slot > from && index > from && index < slot ? -state.SourceHeight
                : !noChange && slot < from && index >= slot && index < from ? state.SourceHeight
                : 0;
            if (ReferenceEquals(row, state.Source) && ReferenceEquals(row.DataContext, state.Item)) shift = sourceShift;
            row.ShiftForDrag(shift);
        }
    }

    private void EndRowDrag(RowDrag state)
    {
        if (ReferenceEquals(_rowDrag, state)) _rowDrag = null;
        foreach (var row in FindRows())
        {
            if (row.DataContext is TaskItemViewModel item && state.Ids.Contains(item.Id)) row.ResetDragShift();
        }
        if (ReferenceEquals(state.Source.DataContext, state.Item)) state.Source.ResetDragShift();
        state.Lift?.Dispose();
    }

    // ---- shared ----

    /// <summary>
    /// GiveFeedback runs for the whole drag, even between drop targets, so the floating copy can follow the pointer
    /// without a gap. Mouse.GetPosition is unreliable inside the OLE loop, but the screen cursor plus PointFromScreen
    /// is exact (and independent of DPI).
    /// </summary>
    private void OnDragFeedback(object sender, GiveFeedbackEventArgs e)
    {
        var screen = System.Windows.Forms.Cursor.Position;
        var pointer = PointFromScreen(new Point(screen.X, screen.Y));
        if (pointer.X < -8 || pointer.Y < -8 ||
            pointer.X > ActualWidth + 8 || pointer.Y > ActualHeight + 8) return;
        _chipDrag?.Lift?.MoveTo(pointer);
        _rowDrag?.Lift?.MoveTo(pointer);
    }

    private static bool SameGroup(TaskItemViewModel a, TaskItemViewModel b) =>
        a.ListId == b.ListId && a.Priority == b.Priority && !b.Model.IsCompleted;

    private readonly record struct RowHit(TaskItemViewModel Item, double Top, double Height, bool Below);

    /// <summary>The row under <paramref name="inList"/> (TaskList coordinates), and which half of it.</summary>
    private RowHit? HitRow(Point inList)
    {
        foreach (var row in FindRows())
        {
            if (row.DataContext is not TaskItemViewModel item || !row.IsVisible) continue;
            double top = BaseTop(row, TaskList);
            double height = row.ActualHeight;
            if (inList.Y >= top && inList.Y < top + height)
                return new RowHit(item, top, height, inList.Y > top + height / 2);
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

    private static TranslateTransform EnsureShift(FrameworkElement element)
    {
        if (element.RenderTransform is TranslateTransform shift) return shift;
        shift = new TranslateTransform();
        element.RenderTransform = shift;
        return shift;
    }

    private static void MoveShift(TranslateTransform shift, double to)
    {
        var duration = MotionPolicy.Current?.Fast ?? TimeSpan.Zero;
        Anim.To(shift, TranslateTransform.XProperty, to, duration, FluentEase.Decelerate);
    }

    /// <summary>Layout position without the preview translation currently applied to the element.</summary>
    private static double BaseLeft(FrameworkElement element, Visual ancestor) =>
        element.TransformToAncestor(ancestor).Transform(new Point(0, 0)).X -
        (element.RenderTransform is TranslateTransform shift ? shift.X : 0);

    /// <summary>Layout position without the enter/leave translation currently applied to the row.</summary>
    private static double BaseTop(TaskRow row, Visual ancestor) =>
        row.TransformToAncestor(ancestor).Transform(new Point(0, 0)).Y - row.DragShiftY;

    private sealed record ChipSlot(FrameworkElement Element, ListChipViewModel Chip, TranslateTransform Shift, double Width);

    private sealed class ChipDrag(List<ChipSlot> items, int from, DragLift? lift)
    {
        public List<ChipSlot> Items { get; } = items;
        public int From { get; } = from;
        public ChipSlot Source => Items[From];
        public ListChipViewModel Chip => Source.Chip;
        public DragLift? Lift { get; } = lift;
        public int Slot { get; set; } = -1;
        public bool Dropped { get; set; }
    }

    private sealed class RowDrag(TaskRow source, TaskItemViewModel item, List<long> ids, int from, double sourceHeight, DragLift? lift)
    {
        public TaskRow Source { get; } = source;
        public TaskItemViewModel Item { get; } = item;
        public List<long> Ids { get; } = ids;
        public int From { get; } = from;
        public double SourceHeight { get; } = sourceHeight;
        public DragLift? Lift { get; } = lift;
        public int Slot { get; set; } = -1;
        public bool Dropped { get; set; }
    }
}
