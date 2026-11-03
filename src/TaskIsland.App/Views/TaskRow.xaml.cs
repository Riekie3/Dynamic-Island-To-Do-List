using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TaskIsland.App.Controls;
using TaskIsland.App.Services;

namespace TaskIsland.App.Views;

public partial class TaskRow : UserControl
{
    public TaskRow()
    {
        InitializeComponent();
        Check.Checked += OnChecked;
    }

    /// <summary>The tick scales in (fast token); instant under reduced motion.</summary>
    private void OnChecked(object sender, RoutedEventArgs e)
    {
        if (Check.Template.FindName("Done", Check) is not FrameworkElement done
            || done.RenderTransform is not ScaleTransform scale) return;
        var d = MotionPolicy.Current?.Fast ?? TimeSpan.Zero;
        if (d <= TimeSpan.Zero) return;
        // A transform declared in a control template is frozen and cannot be animated; give this row its own.
        if (scale.IsFrozen) done.RenderTransform = scale = new ScaleTransform();
        Anim.Keys(scale, ScaleTransform.ScaleXProperty, FluentEase.Decelerate, null, (0, 0.6), (d.TotalMilliseconds, 1));
        Anim.Keys(scale, ScaleTransform.ScaleYProperty, FluentEase.Decelerate, null, (0, 0.6), (d.TotalMilliseconds, 1));
    }
}
