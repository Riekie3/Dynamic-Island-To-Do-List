using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TaskIsland.App.Services;
using TaskIsland.Core.Toasts;

namespace TaskIsland.App.Controls;

/// <summary>
/// Shows one <see cref="SileoToast"/> inside a panel: appears as a tab, opens its body 150 ms later (Sileo's auto-expand),
/// optionally hides itself after a while (longer while the mouse is on it) and, when hidden, folds the body and fades out
/// before it is collapsed. A second <see cref="Show"/> while visible swaps the content in place. Used by the panel (Undo
/// and notices) and the Settings window (data actions); the reminder toast in the island window has its own window-size
/// handling. Under reduced motion everything is instant. Must be used on the UI thread.
/// </summary>
public sealed class ToastPresenter
{
    private static readonly TimeSpan OpenDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan FoldAndFade = TimeSpan.FromMilliseconds(650);

    private readonly DispatcherTimer _open = new() { Interval = OpenDelay };
    private readonly DispatcherTimer _remove = new() { Interval = FoldAndFade };
    private readonly DispatcherTimer _auto = new();
    private TimeSpan _autoAfter;

    public ToastPresenter(Panel host, bool expandsUp, string fillKey, string strokeKey, ToastAlign align = ToastAlign.Center)
    {
        Toast = new SileoToast
        {
            ExpandsUp = expandsUp,
            FillKey = fillKey,
            StrokeKey = strokeKey,
            Align = align,
            HorizontalAlignment = align switch { ToastAlign.Left => HorizontalAlignment.Left, ToastAlign.Right => HorizontalAlignment.Right, _ => HorizontalAlignment.Center },
            VerticalAlignment = expandsUp ? VerticalAlignment.Bottom : VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
        };
        Toast.ButtonClick += (_, _) => ButtonClick?.Invoke(this, EventArgs.Empty);
        Toast.Clicked += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
        host.Children.Add(Toast);

        _open.Tick += (_, _) =>
        {
            _open.Stop();
            if (IsShown && Toast.HasBody) Toast.Open();
        };
        _remove.Tick += (_, _) =>
        {
            _remove.Stop();
            if (IsShown) return;   // shown again while folding
            Toast.Visibility = Visibility.Collapsed;
            Toast.Opacity = 1;
        };
        _auto.Tick += (_, _) =>
        {
            if (Toast.IsMouseOver) return;   // the mouse is on it: it stays until the mouse leaves and the timer fires again
            _auto.Stop();
            Hide();
        };
    }

    public SileoToast Toast { get; }

    /// <summary>True from <see cref="Show"/> until <see cref="Hide"/> (the fade-out afterwards does not count).</summary>
    public bool IsShown { get; private set; }

    /// <summary>The toast was clicked (not its button).</summary>
    public event EventHandler? Clicked;

    /// <summary>The toast's button was clicked.</summary>
    public event EventHandler? ButtonClick;

    private static bool Reduced => MotionPolicy.Current?.IsReduced ?? true;

    /// <param name="autoHide">Hide again after this long (null: stays until <see cref="Hide"/>).</param>
    /// <param name="animate">False: appear already open (screenshots, start-up).</param>
    public void Show(ToastState state, string title, string? body, string? button, TimeSpan? autoHide = null, bool animate = true)
    {
        bool wasVisible = IsShown && Toast.Visibility == Visibility.Visible;
        IsShown = true;
        _remove.Stop();
        Toast.IsHitTestVisible = true;
        if (!wasVisible)
        {
            Toast.Close(animate: false);
            Toast.Opacity = 1;
            Toast.SetContent(state, title, body, button, animate: false);
            Toast.Visibility = Visibility.Visible;
        }
        else
        {
            Toast.SetContent(state, title, body, button);   // swaps in place, like Sileo's refresh
        }

        _open.Stop();
        if (!Toast.HasBody) { /* loading or a bare tab: nothing to open */ }
        else if (!animate) Toast.Open(animate: false);
        else _open.Start();

        _auto.Stop();
        if (autoHide is { } after)
        {
            _autoAfter = after;
            _auto.Interval = after;
            _auto.Start();
        }
    }

    /// <summary>Folds the body, fades the toast out and collapses it. No-op when nothing is shown.</summary>
    public void Hide(bool animate = true)
    {
        if (!IsShown) return;
        IsShown = false;
        _open.Stop();
        _auto.Stop();
        Toast.IsHitTestVisible = false;
        if (!animate || Reduced)
        {
            _remove.Stop();
            Toast.Visibility = Visibility.Collapsed;
            return;
        }
        Toast.Close();
        Anim.To(Toast, UIElement.OpacityProperty, 0, TimeSpan.FromMilliseconds(350), FluentEase.Accelerate, TimeSpan.FromMilliseconds(250));
        _remove.Start();
    }
}
