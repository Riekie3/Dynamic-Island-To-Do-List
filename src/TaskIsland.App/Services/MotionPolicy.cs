using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;
using TaskIsland.Windows;

namespace TaskIsland.App.Services;

/// <summary>
/// Duration tokens and the reduced-motion decision (PLAN.md sections 6.0 and 6.7 rule 8). Reduced motion turns on by
/// itself when Windows "Animation effects" is off, Battery saver is on or the session is remote; it is re-checked on
/// events only, never polled. <c>AnimationsEnabled=false</c> makes every duration zero.
/// </summary>
public sealed class MotionPolicy
{
    private bool _animationsEnabled;
    private bool _forceReduced;
    private bool _batterySaver;
    private bool _followWindows = true;

    public MotionPolicy(bool animationsEnabled, bool forceReduced = false)
    {
        Current = this;
        _animationsEnabled = animationsEnabled;
        _forceReduced = forceReduced;
        _batterySaver = SafeBatterySaver();
        SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    /// <summary>The app-wide instance, for views that animate in code-behind.</summary>
    public static MotionPolicy? Current { get; private set; }

    public event EventHandler? Changed;

    public bool AnimationsEnabled
    {
        get => _animationsEnabled;
        set { if (_animationsEnabled != value) { _animationsEnabled = value; Changed?.Invoke(this, EventArgs.Empty); } }
    }

    public bool BatterySaver => _batterySaver;

    /// <summary>Whether Windows "Animation effects" off means reduced motion here. Battery saver and remote sessions always do.</summary>
    public bool FollowWindowsAnimations
    {
        get => _followWindows;
        set { if (_followWindows != value) { _followWindows = value; Changed?.Invoke(this, EventArgs.Empty); } }
    }

    /// <summary>Reduced motion: no micro-animations, no random pops, short morph.</summary>
    public bool IsReduced => !_animationsEnabled || _forceReduced || _batterySaver
        || (_followWindows && !SystemParameters.ClientAreaAnimation) || SystemParameters.IsRemoteSession;

    public TimeSpan Fast => IsReduced ? TimeSpan.Zero : TimeSpan.FromMilliseconds(120);
    public TimeSpan Normal => IsReduced ? TimeSpan.Zero : TimeSpan.FromMilliseconds(200);
    public TimeSpan MorphExpand => Morph(260);
    public TimeSpan MorphCollapse => Morph(180);

    private TimeSpan Morph(int ms) =>
        !_animationsEnabled ? TimeSpan.Zero : IsReduced ? TimeSpan.FromMilliseconds(120) : TimeSpan.FromMilliseconds(ms);

    private void OnSystemParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SystemParameters.ClientAreaAnimation) or nameof(SystemParameters.IsRemoteSession))
            Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.StatusChange) return;
        // SystemEvents raises this on its own thread; hop to the UI thread before touching state.
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            bool now = SafeBatterySaver();
            if (now == _batterySaver) return;
            _batterySaver = now;
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }

    private static bool SafeBatterySaver()
    {
        try { return ProcessPower.IsBatterySaverOn(); }
        catch { return false; }
    }
}
