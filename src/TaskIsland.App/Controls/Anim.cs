using System.Windows;
using System.Windows.Media.Animation;

namespace TaskIsland.App.Controls;

/// <summary>
/// The only way the app animates (PLAN.md section 6.2 rules): finite, <see cref="FillBehavior.Stop"/>, and the final
/// value is left as the local value, so no animation clock stays alive once it ends. Callers animate render
/// properties only (opacity, transforms, clip geometry).
/// </summary>
public static class Anim
{
    /// <summary>Animates from the current value to <paramref name="to"/>. Zero duration sets the value at once.</summary>
    public static void To(IAnimatable target, DependencyProperty property, double to, TimeSpan duration,
        IEasingFunction? ease = null, TimeSpan delay = default, Action? completed = null)
    {
        if (duration <= TimeSpan.Zero)
        {
            Stop(target, property, to);
            completed?.Invoke();
            return;
        }
        double from = (double)((DependencyObject)target).GetValue(property);
        double d = delay.TotalMilliseconds, total = d + duration.TotalMilliseconds;
        Keys(target, property, ease, completed, (0, from), (d, from), (total, to));
    }

    /// <summary>
    /// Plays key frames given as (milliseconds, value). The first key should be at 0. Holding a value between two keys
    /// replaces BeginTime, so a delayed animation never jumps to its end value first.
    /// </summary>
    public static void Keys(IAnimatable target, DependencyProperty property, IEasingFunction? ease, Action? completed,
        params (double Ms, double Value)[] keys)
    {
        var anim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        foreach (var (ms, value) in keys)
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(value, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms)), ease));
        if (completed is not null) anim.Completed += (_, _) => completed();
        target.BeginAnimation(property, anim);
        ((DependencyObject)target).SetValue(property, keys[^1].Value);
    }

    /// <summary>Stops any animation on the property and sets the value.</summary>
    public static void Stop(IAnimatable target, DependencyProperty property, double value)
    {
        target.BeginAnimation(property, null);
        ((DependencyObject)target).SetValue(property, value);
    }
}
