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

/// <summary>The expanded to-do panel. Created on first expand (PLAN.md section 6.2) and then kept.</summary>
public partial class PanelView : UserControl
{
    private const double FaceScale = 28.0 / FaceRig.FrameHeight;
    private readonly IslandViewModel _vm;

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

    public bool QuickAddHasText => QuickAddBox.Text.Length > 0;

    public void ClearQuickAdd() => QuickAddBox.Clear();

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
        if (e.PropertyName == nameof(IslandViewModel.Filter)) SyncFilter(animate: true);
        else if (e.PropertyName == nameof(IslandViewModel.ScopeFraction)) SyncProgress(animate: true);
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

    private void Chips_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ChipScroller.ScrollableWidth <= 0) return;
        ChipScroller.ScrollToHorizontalOffset(ChipScroller.HorizontalOffset - e.Delta / 3.0);
        e.Handled = true;
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        MoreMenu.PlacementTarget = MoreButton;
        MoreMenu.Placement = PlacementMode.Bottom;
        MoreMenu.IsOpen = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CollapseRequested?.Invoke(this, EventArgs.Empty);

    private void Exit_Click(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);
}
