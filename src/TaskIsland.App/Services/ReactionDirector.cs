using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using TaskIsland.Core.Mascot;
using TaskIsland.Core.Models;

namespace TaskIsland.App.Services;

/// <summary>Reasons random pops are paused (PLAN.md section 6.8). Phase 4 adds full-screen, display-off and pause.</summary>
[Flags]
public enum Suppression
{
    None = 0,
    PanelOpen = 1,
    Hidden = 2,
    StartupQuiet = 4,
    Locked = 8,
    FullScreen = 16,
    DisplayOff = 32,
    Paused = 64,
}

/// <summary>
/// The one place that decides which face shows (PLAN.md section 6.8). Rules come from <see cref="ReactionRules"/>;
/// this class owns the one-shot timers: reaction hold, next random pop (re-armed after each pop), the 60-minute
/// Sleepy timer, the quiet minute after a <c>--startup</c> launch and the companion's next idle movement
/// (<see cref="CompanionRules"/>). No timer repeats; each one-shot is re-armed after it fires.
/// </summary>
public sealed class ReactionDirector
{
    private readonly AppSettings _settings;
    private readonly MotionPolicy _motion;
    private readonly bool _startupLaunch;
    private readonly Random _random;
    private readonly DispatcherTimer _hold, _pop, _sleepy, _quiet, _life;
    private Suppression _suppressed;
    private Reaction? _lastRandom;
    private LifeMove? _lastLife;
    private bool _currentOnPanel;

    public ReactionDirector(AppSettings settings, MotionPolicy motion, bool startupLaunch, Random? random = null)
    {
        _settings = settings;
        _motion = motion;
        _startupLaunch = startupLaunch;
        _random = random ?? new Random();
        _hold = OneShot((_, _) => End());
        _pop = OneShot((_, _) => OnPopTimer());
        _sleepy = OneShot((_, _) => SetSleepy(true));
        _quiet = OneShot((_, _) => SetSuppressed(Suppression.StartupQuiet, false));
        _life = OneShot((_, _) => OnLifeTimer());
        _motion.Changed += (_, _) => { UpdatePopTimer(); UpdateLifeTimer(); };
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    /// <summary>(reaction, shown on the panel face instead of the notch).</summary>
    public event Action<Reaction, bool>? ReactionStarted;
    /// <summary>(was on the panel face).</summary>
    public event Action<bool>? ReactionEnded;
    public event Action<bool>? SleepyChanged;
    /// <summary>(movement, for the panel face instead of the notch). Companion idle life.</summary>
    public event Action<LifeMove, bool>? LifeTick;

    public Reaction? Current { get; private set; }
    public bool CurrentIsRandom { get; private set; }
    /// <summary>The Curious face shown because the mouse is over the Mini notch.</summary>
    public bool CurrentIsHover { get; private set; }
    public bool IsSleepy { get; private set; }
    public Suppression Suppressed => _suppressed;
    public bool Enabled => _settings.MascotEnabled;
    public bool PopTimerRunning => _pop.IsEnabled;
    public bool LifeTimerRunning => _life.IsEnabled;

#if DEBUG
    /// <summary>Debug screenshots: keep the face until the next one.</summary>
    public bool HoldForever { get; set; }
    /// <summary>Debug: shorter random-pop interval for testing.</summary>
    public TimeSpan? PopIntervalOverride { get; set; }
#endif

    /// <summary>Starts the timers; greets with a Wink unless this is a <c>--startup</c> launch.</summary>
    public void Start()
    {
        RestartSleepyTimer();
        if (_startupLaunch)
        {
            _suppressed |= Suppression.StartupQuiet;
            _quiet.Interval = ReactionRules.StartupQuietPeriod;
            _quiet.Start();
        }
        else
        {
            Request(Reaction.Wink);
        }
        UpdatePopTimer();
        UpdateLifeTimer();
    }

    /// <summary>An event wants a face. Returns false when a higher-priority face is showing or the mascot is off.</summary>
    public bool Request(Reaction reaction) => Show(reaction, random: false, hover: false, Hold(reaction));

    /// <summary>Mouse over the Mini notch: Curious for 400 ms, then the window turns it into the Info pill.</summary>
    public bool RequestHover() => Show(Reaction.Curious, random: false, hover: true, TimeSpan.FromMilliseconds(400));

    /// <summary>Ends the current face now (for example when the panel opens).</summary>
    public void End()
    {
        _hold.Stop();
        if (Current is null) return;
        bool onPanel = _currentOnPanel;
        Current = null;
        CurrentIsRandom = CurrentIsHover = false;
        ReactionEnded?.Invoke(onPanel);
        UpdateLifeTimer(restart: true);
    }

    public void SetSuppressed(Suppression reason, bool on)
    {
        var before = _suppressed;
        _suppressed = on ? _suppressed | reason : _suppressed & ~reason;
        if (before != _suppressed) { UpdatePopTimer(); UpdateLifeTimer(); }
    }

    /// <summary>The user touched the island: wake up and restart the 60-minute Sleepy timer.</summary>
    public void NoteInteraction()
    {
        RestartSleepyTimer();
        if (IsSleepy) SetSleepy(false);
    }

    /// <summary>Re-read MascotEnabled / IdleReactions.</summary>
    public void SettingsChanged()
    {
        _pop.Stop();
        if (!Enabled) End();
        UpdatePopTimer();
        UpdateLifeTimer(restart: true);
    }

#if DEBUG
    /// <summary>Debug: show a face regardless of priorities.</summary>
    public void Force(Reaction reaction)
    {
        _hold.Stop();
        Current = null;
        Show(reaction, random: false, hover: false, Hold(reaction));
    }
#endif

    private bool Show(Reaction reaction, bool random, bool hover, TimeSpan hold)
    {
        if (!Enabled) return false;
        if (!ReactionRules.ShouldShow(reaction, random, Current, CurrentIsRandom)) return false;
        bool onPanel = (_suppressed & Suppression.PanelOpen) != 0;
        if (hover && onPanel) return false;
        Current = reaction;
        CurrentIsRandom = random;
        CurrentIsHover = hover;
        _currentOnPanel = onPanel;
        _hold.Stop();
#if DEBUG
        if (!HoldForever) { _hold.Interval = hold; _hold.Start(); }
#else
        _hold.Interval = hold;
        _hold.Start();
#endif
        ReactionStarted?.Invoke(reaction, onPanel);
        UpdateLifeTimer(restart: true);
        return true;
    }

    private bool PopsAllowed =>
        Enabled && _suppressed == Suppression.None && !_motion.IsReduced
        && ReactionRules.PopWindow(_settings.IdleReactions) is not null;

    private void UpdatePopTimer()
    {
        if (!PopsAllowed) { _pop.Stop(); return; }
        if (_pop.IsEnabled) return;
        var delay = ReactionRules.NextPopDelay(_settings.IdleReactions, _random)!.Value;
#if DEBUG
        if (PopIntervalOverride is { } o) delay = o;
#endif
        _pop.Interval = delay;
        _pop.Start();
    }

    private void OnPopTimer()
    {
        _pop.Stop();
        if (PopsAllowed && Current is null)
        {
            var pick = ReactionRules.PickRandom(_random, _lastRandom);
            if (Show(pick, random: true, hover: false, Hold(pick))) _lastRandom = pick;
        }
        UpdatePopTimer();
    }

    private TimeSpan Hold(Reaction reaction) => ReactionRules.HoldTime(reaction, _settings.Companion);

    // ---- companion idle life ----

    /// <summary>Like random pops, but the open panel and the quiet start-up minute do not stop it.</summary>
    private bool LifeAllowed =>
        Enabled && _settings.Companion != CompanionLevel.Off && !_motion.IsReduced
        && (_suppressed & ~(Suppression.PanelOpen | Suppression.StartupQuiet)) == Suppression.None;

    private bool PanelOpen => (_suppressed & Suppression.PanelOpen) != 0;

    /// <summary>Arms the next movement; <paramref name="restart"/> re-times it when a face appears or ends.</summary>
    private void UpdateLifeTimer(bool restart = false)
    {
        if (!LifeAllowed) { _life.Stop(); return; }
        if (_life.IsEnabled && !restart) return;
        _life.Stop();
        bool face = Current is not null || PanelOpen;
        _life.Interval = CompanionRules.NextDelay(_settings.Companion, face, _random)!.Value;
        _life.Start();
    }

    private void OnLifeTimer()
    {
        if (LifeAllowed)
        {
            bool onPanel = PanelOpen;
            Reaction? face = onPanel ? Current ?? Reaction.Neutral : Current;
            var move = CompanionRules.Pick(_random, face, sleepy: face is null && IsSleepy, _lastLife);
            _lastLife = move;
            LifeTick?.Invoke(move, onPanel);
        }
        UpdateLifeTimer();
    }

    private void RestartSleepyTimer()
    {
        _sleepy.Stop();
        _sleepy.Interval = ReactionRules.SleepyAfter;
        _sleepy.Start();
    }

    private void SetSleepy(bool sleepy)
    {
        if (sleepy) _sleepy.Stop();
        if (IsSleepy == sleepy) return;
        IsSleepy = sleepy;
        SleepyChanged?.Invoke(sleepy);
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        bool? locked = e.Reason switch
        {
            SessionSwitchReason.SessionLock => true,
            SessionSwitchReason.SessionUnlock => false,
            _ => null,
        };
        if (locked is { } l)
            Application.Current?.Dispatcher.BeginInvoke(() => SetSuppressed(Suppression.Locked, l));
    }

    private static DispatcherTimer OneShot(EventHandler tick)
    {
        var t = new DispatcherTimer(DispatcherPriority.Normal);
        t.Tick += (s, e) => { ((DispatcherTimer)s!).Stop(); tick(s, e); };
        return t;
    }
}
