using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TaskIsland.App.Controls;
using TaskIsland.App.Logging;
using TaskIsland.App.Services;
using TaskIsland.App.ViewModels;
using TaskIsland.Core.Mascot;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;
using TaskIsland.Core.Toasts;
using TaskIsland.Windows;

namespace TaskIsland.App.Views;

/// <summary>
/// The island window: Mini ↔ Reaction ↔ Info ↔ Panel (PLAN.md sections 6.2, 6.3, 6.8 and 7.1). Code-behind holds only
/// view concerns: window styles, the state machine that drives <see cref="IslandHost"/>, hover and focus.
/// The window is resized only while nothing moves: grown just before the expand, shrunk just after the collapse.
/// </summary>
public partial class IslandWindow : Window
{
    private const double MaxPanelHeight = 550;
    private static readonly TimeSpan InfoPeekDelay = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan LegacyPeekDelay = TimeSpan.FromMilliseconds(400);

    private readonly IslandViewModel _vm;
    private readonly AppSettings _settings;
    private readonly MotionPolicy _motion;
    private readonly ReactionDirector _director;
    private readonly EfficiencyMode _efficiency;
    private readonly IslandPositioner _positioner;
    private readonly FaceRig _face = new();
    private readonly DispatcherTimer _peekTimer, _alertTimer, _tuckTimer, _autoCollapseTimer;
    private readonly TranslateTransform _tuckSlide = new();
    private bool _tucked;                          // AlwaysShowNotch off: the notch is slid up to a 6 px sliver
    private PanelView? _panel;
    private IntPtr _hwnd;
    private bool _hovering, _expanded, _peeking, _allowClose;
    private bool _windowLarge;                    // the window currently has the expanded size
    private double _windowWidth = IslandPositioner.WidthDip; // wider only while a wide reminder pill shows
    private bool _userHidden, _paused, _fullScreen; // reasons the notch is hidden (PLAN.md sections 7.4 and 7.5)
    private DateTime _lastAutoCollapse = DateTime.MinValue;
    private ReminderNotice? _alert;                // the widening reminder pill is showing (PLAN.md section 7.9)
    private bool _alertHeld;                       // 5 s are over but the mouse is on it
    private bool _alertHover;                      // the mouse is on the reminder toast
    private SileoToast? _toast;                    // the Sileo-style reminder toast (SILEO-PLAN.md S2), created on first use
    private DispatcherTimer? _toastOpenTimer, _toastHideTimer;
    private bool _toastWindow;                     // the window was grown to hold the toast
    private static readonly TimeSpan AlertTime = TimeSpan.FromSeconds(5);

    public IslandWindow(IslandViewModel vm, AppSettings settings, MotionPolicy motion, ReactionDirector director,
        EfficiencyMode efficiency, IslandPositioner positioner)
    {
        _vm = vm;
        _settings = settings;
        _motion = motion;
        _director = director;
        _efficiency = efficiency;
        _positioner = positioner;
        InitializeComponent();
        DataContext = vm;

        Host.Face = _face;
        Host.PanelOpacity = settings.PanelOpacity;
        Host.Flush = settings.TopMargin == 0;
        Host.SetMascotLook(settings.MascotEnabled);
        Host.MouseEnter += OnHostMouseEnter;
        Host.MouseLeave += OnHostMouseLeave;
        Host.MouseLeftButtonUp += OnHostClick;

        _peekTimer = new DispatcherTimer(DispatcherPriority.Normal);
        _peekTimer.Tick += (_, _) => { _peekTimer.Stop(); StartPeek(); };

        _alertTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = AlertTime };
        _alertTimer.Tick += (_, _) =>
        {
            _alertTimer.Stop();
            if (_alertHover) _alertHeld = true; // hover keeps it open until the mouse leaves
            else EndAlert();
        };

        // AlwaysShowNotch off (PLAN.md section 5): after 500 ms with the mouse away the notch slides up to a 6 px sliver and
        // comes back when the mouse touches it. A one-shot timer; nothing runs while AlwaysShowNotch is on.
        _tuckTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(500) };
        _tuckTimer.Tick += (_, _) => { _tuckTimer.Stop(); if (WantsTuck()) SlideTo(tucked: true); };
        Host.RenderTransform = _tuckSlide;

        // AutoCollapseSeconds (PLAN.md section 5): one-shot while the panel is open.
        _autoCollapseTimer = new DispatcherTimer(DispatcherPriority.Normal);
        _autoCollapseTimer.Tick += (_, _) => OnAutoCollapseTick();

        director.ReactionStarted += OnReactionStarted;
        director.ReactionEnded += OnReactionEnded;
        director.SleepyChanged += OnSleepyChanged;
        director.LifeTick += OnLifeTick;
        vm.PropertyChanged += OnVmChanged;

        SourceInitialized += OnSourceInitialized;
        Deactivated += (_, _) =>
        {
            // A calendar popup is a window of its own: opening it is not a click outside.
            if (_expanded && _settings.CollapseOnClickOutside && !IsDemo && _panel is not { IsPopupOpen: true })
            {
                _lastAutoCollapse = DateTime.UtcNow;
                Collapse();
            }
        };
        IsVisibleChanged += (_, _) => director.SetSuppressed(Suppression.Hidden, !IsVisible);
    }

    public bool IsExpanded => _expanded;
    public IntPtr Hwnd => _hwnd;
    public bool IsUserHidden => _userHidden;
    public bool IsPaused => _paused;
    public bool IsFullScreenHidden => _fullScreen;

    /// <summary>Show/Hide, Pause or full-screen changed (the tray updates its icon and menu).</summary>
    public event Action? VisibilityStateChanged;

    // ---- Windows integration (PLAN.md section 7): visibility, toggles, placement ----

    /// <summary>Tray "Show / Hide": hides the notch now. Automatic events (wake, full-screen end) may show it again.</summary>
    public void SetUserHidden(bool hidden)
    {
        _userHidden = hidden;
        FileLogger.Info($"notch {(hidden ? "hidden" : "shown")} by the user");
        if (hidden && _expanded) Collapse();
        ApplyVisibility();
    }

    /// <summary>Tray "Pause visibility": hidden, and no automatic event shows it until Resume.</summary>
    public void SetPaused(bool paused)
    {
        _paused = paused;
        if (!paused) _userHidden = false;
        FileLogger.Info(paused ? "visibility paused" : "visibility resumed");
        if (paused && _expanded) Collapse();
        ApplyVisibility();
    }

    /// <summary>A full-screen app on the island's monitor started or ended (only when HideInFullScreen is on).</summary>
    public void SetFullScreen(bool fullScreen)
    {
        if (_fullScreen == fullScreen) return;
        _fullScreen = fullScreen;
        _director.SetSuppressed(Suppression.FullScreen, fullScreen);
        FileLogger.Info(fullScreen ? "full-screen app: notch hidden" : "full-screen ended: notch back");
        if (!fullScreen && !_paused) _userHidden = false; // an automatic event may show it again
        ApplyVisibility();
    }

    /// <summary>Wake/unlock with RestoreAfterWake: show the notch unless paused or covered by a full-screen app.</summary>
    public void AutoShow()
    {
        if (_paused) return;
        _userHidden = false;
        ApplyVisibility();
    }

    /// <summary>
    /// The window is visible while the panel is open or when nothing hides the notch. Opening the panel from the tray,
    /// the hotkey or a second launch always works; when it collapses, the notch hides again if a reason is still set.
    /// </summary>
    private void ApplyVisibility()
    {
        bool show = _expanded || !(_userHidden || _paused || _fullScreen);
        if (show && !IsVisible)
        {
            Show();
            if (_hwnd != IntPtr.Zero) WindowStyler.ReassertTopmost(_hwnd);
        }
        else if (!show && IsVisible)
        {
            Hide();
        }
        VisibilityStateChanged?.Invoke();
    }

    /// <summary>Tray Open, double-click, the hotkey and a second launch: show the window if needed, then expand.</summary>
    public void OpenFromOutside()
    {
        if (!IsVisible) Show();
        if (_expanded)
        {
            WindowStyler.BringToForeground(_hwnd);
            _panel?.FocusQuickAdd();
            return;
        }
        Expand();
    }

    /// <summary>Global hotkey: toggle.</summary>
    public void Toggle()
    {
        if (_expanded) Collapse();
        else OpenFromOutside();
    }

    /// <summary>
    /// Tray left-click: toggle. Clicking the tray while the panel is open first takes the focus away, which already
    /// collapsed it (click outside), so that click must not open it again.
    /// </summary>
    public void ToggleFromTray()
    {
        if (_expanded) { Collapse(); return; }
        if (DateTime.UtcNow - _lastAutoCollapse < TimeSpan.FromMilliseconds(500)) return;
        OpenFromOutside();
    }

    /// <summary>Moves the window to its place on the chosen monitor (also after DPI, display and wake changes).</summary>
    public void Reposition()
    {
        if (_hwnd == IntPtr.Zero) return;
        _positioner.Apply(_hwnd, _windowLarge, _windowWidth);
    }

    /// <summary>After wake, display change or an Explorer restart: our window styles and topmost again.</summary>
    public void ReapplyWindowStyles()
    {
        if (_hwnd == IntPtr.Zero) return;
        WindowStyler.ApplyIslandStyles(_hwnd);
        if (_expanded) WindowStyler.SetNoActivate(_hwnd, false);
        WindowStyler.ReassertTopmost(_hwnd);
    }

    // ---- AlwaysShowNotch off: the 6 px sliver ----

    private const double SliverHeight = 6;

    /// <summary>The notch may hide: setting off, panel closed, nothing hovering, no alert or face on screen.</summary>
    private bool WantsTuck() =>
        !_settings.AlwaysShowNotch && !_expanded && !_hovering && _alert is null && _director.Current is null
        && Host.State is IslandState.Mini or IslandState.Info && !Host.IsAnimating;

    private void SlideTo(bool tucked)
    {
        if (_tucked == tucked) return;
        _tucked = tucked;
        double y = tucked ? -Math.Max(0, Host.IslandRect.Height - SliverHeight) : 0;
        Anim.To(_tuckSlide, TranslateTransform.YProperty, y, _motion.IsReduced ? TimeSpan.Zero : TimeSpan.FromMilliseconds(160),
            tucked ? FluentEase.Accelerate : FluentEase.Decelerate);
        FileLogger.Info(tucked ? "notch tucked to a sliver" : "notch revealed");
    }

    /// <summary>Re-evaluates the sliver after anything that changes what is on screen. Costs nothing while the setting is on.</summary>
    private void RefreshTuck()
    {
        if (_settings.AlwaysShowNotch)
        {
            _tuckTimer.Stop();
            if (_tucked) SlideTo(tucked: false);
            return;
        }
        if (WantsTuck())
        {
            if (!_tucked && !_tuckTimer.IsEnabled) _tuckTimer.Start();
        }
        else
        {
            _tuckTimer.Stop();
            if (_tucked) SlideTo(tucked: false);
        }
    }

    /// <summary>Settings: AlwaysShowNotch changed.</summary>
    public void ApplyAlwaysShow()
    {
        if (!_settings.AlwaysShowNotch && !_expanded && !_hovering) _tuckTimer.Start();
        else RefreshTuck();
    }

    /// <summary>The notch is tucked away right now (tests read it from the log).</summary>
    public bool IsTucked => _tucked;

    // ---- AutoCollapseSeconds ----

    private void StartAutoCollapse()
    {
        _autoCollapseTimer.Stop();
        if (_settings.AutoCollapseSeconds <= 0 || IsDemo) return;
        _autoCollapseTimer.Interval = TimeSpan.FromSeconds(_settings.AutoCollapseSeconds);
        _autoCollapseTimer.Start();
    }

    /// <summary>Collapses only when the mouse is away and nobody is typing or editing; otherwise waits another full period.</summary>
    private void OnAutoCollapseTick()
    {
        _autoCollapseTimer.Stop();
        if (!_expanded) return;
        bool busy = IsMouseOver || _vm.HasOverlay || _vm.Editing is not null || _panel is { QuickAddHasText: true }
            || _panel is { IsPopupOpen: true };
        if (busy || _settings.AutoCollapseSeconds <= 0)
        {
            StartAutoCollapse();
            return;
        }
        FileLogger.Info($"auto-collapse after {_settings.AutoCollapseSeconds} s");
        Collapse();
    }

    // ---- reminder pill alert (PLAN.md section 7.9) ----

    public bool IsAlerting => _alert is not null;
    public string? AlertContent => _alert?.PillText;
    /// <summary>Current island width in design units (tests read it from the log).</summary>
    public double IslandWidth => Host.IslandRect.Width / IslandHost.CompactScale;

    /// <summary>
    /// The reminder appears as a Sileo-style toast (SILEO-PLAN.md S2) in place of the notch: a tab with the state ("Due now",
    /// "Overdue", "In 5 minutes") and, 150 ms later, a body melting out of it with the task title, its list and an Open
    /// button. It holds 5 s (longer while the mouse is on it) and folds away; a click opens the task's list. Returns false
    /// (the caller then falls back to the face) when the setting is off, the panel is open or the island is hidden.
    /// While it shows, the resting island is hidden underneath and the window is tall enough for the body; both are put
    /// back after the toast has faded (never resized during an animation). With <see cref="AppSettings.ReminderBlink"/>
    /// the toast also flashes twice. Reduced motion: no animation, the toast just appears.
    /// </summary>
    public bool ShowReminderAlert(ReminderNotice notice)
    {
        if (!_settings.ReminderPillAlert || _expanded || !IsVisible || _userHidden || _paused || _fullScreen) return false;
        _peekTimer.Stop();
        EndPeekVisual();
        _tuckTimer.Stop();
        if (_tucked) SlideTo(tucked: false);
        _director.End();
        _toastHideTimer?.Stop();
        bool replacing = _alert is not null && _toast is { Visibility: Visibility.Visible };
        _alert = notice;
        _alertHeld = false;

        var content = ReminderToastText.From(notice);
        var toast = EnsureToast();
        if (!replacing)
        {
            // Grow the window first (nothing is moving), then show the tab.
            GrowWindowForToast();
            Anim.Stop(Host, OpacityProperty, 0);
            Host.IsHitTestVisible = false;
            toast.Close(animate: false);
            toast.Opacity = 1;
            toast.IsHitTestVisible = true;
            toast.SetContent(content.State, content.Tab, content.Body, "Open", animate: false);
            toast.Visibility = Visibility.Visible;
        }
        else
        {
            toast.SetContent(content.State, content.Tab, content.Body, "Open");   // swaps in place, like Sileo's refresh
        }
        bool blink = _settings.ReminderBlink;
        FileLogger.Info($"reminder pill alert: \"{notice.PillText}\" toast {content.State} \"{content.Tab}\"" +
            $" (window {_windowWidth:0}x{(_windowLarge ? IslandPositioner.ExpandedHeightDip : IslandPositioner.CollapsedHeightDip):0}" +
            $"{(blink ? ", flash" : "")})");
        AutomationProperties.SetName(toast, "TaskIsland reminder: " + notice.PillText);
        _toastOpenTimer!.Stop();
        _toastOpenTimer.Start();   // Sileo opens the body 150 ms after the tab appears
        if (blink) toast.Flash();
        _alertTimer.Stop();
        _alertTimer.Start();
        return true;
    }

    private SileoToast EnsureToast()
    {
        if (_toast is not null) return _toast;
        var toast = new SileoToast
        {
            Align = ToastAlign.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            FillKey = "PillBrush",   // the same black as the notch it replaces
            Visibility = Visibility.Collapsed,
        };
        _toast = toast;
        toast.Clicked += (_, _) => OpenFromReminder();
        toast.ButtonClick += (_, _) => OpenFromReminder();
        toast.MouseEnter += (_, _) =>
        {
            _alertHover = true;
            if (_alert is not null) toast.Open();   // hover opens the body, like Sileo
        };
        toast.MouseLeave += (_, _) =>
        {
            _alertHover = false;
            if (_alert is not null && _alertHeld) EndAlert();
        };
        Root.Children.Add(toast);
        _toastOpenTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(150) };
        _toastOpenTimer.Tick += (_, _) =>
        {
            _toastOpenTimer.Stop();
            if (_alert is not null) toast.Open();
        };
        _toastHideTimer = new DispatcherTimer(DispatcherPriority.Normal);
        _toastHideTimer.Tick += (_, _) =>
        {
            _toastHideTimer.Stop();
            HideToast(settle: true);
        };
        return toast;
    }

    /// <summary>Click on the toast or its Open button: the panel opens on that task's list.</summary>
    private void OpenFromReminder()
    {
        if (_alert is not { } alert || _expanded) return;
        _director.NoteInteraction();
        EndAlert(settle: false);
        _vm.ShowList(alert.ListId);
        Expand();
    }

    /// <summary>Taller window for the body, done before the toast shows. The island above it does not move.</summary>
    private void GrowWindowForToast()
    {
        if (_toastWindow || _hwnd == IntPtr.Zero) return;
        _toastWindow = true;   // from now on the toast owns shrinking the window back
        if (_windowLarge) return;   // still tall from a panel that is folding away; Collapse leaves it alone while a toast shows
        _windowLarge = true;
        _positioner.Apply(_hwnd, expanded: true, _windowWidth);
    }

    /// <summary>
    /// Back to the resting notch. <paramref name="settle"/> false: the panel is about to open, so the toast goes at once
    /// and nothing animates here. Otherwise the toast folds (body first, then it fades) and is removed afterwards.
    /// </summary>
    private void EndAlert(bool settle = true)
    {
        if (_alert is null) return;
        _alert = null;
        _alertHeld = false;
        _alertHover = false;
        _alertTimer.Stop();
        _toastOpenTimer?.Stop();
        FileLogger.Info("reminder pill alert ended");
        if (_toast is not { Visibility: Visibility.Visible } toast) return;
        if (!settle || _expanded)
        {
            HideToast(settle: false);
            return;
        }
        toast.IsHitTestVisible = false;
        toast.Close();
        var fade = _motion.IsReduced ? TimeSpan.Zero : TimeSpan.FromMilliseconds(380);
        Anim.To(toast, OpacityProperty, 0, fade, FluentEase.Accelerate,
            delay: _motion.IsReduced ? TimeSpan.Zero : TimeSpan.FromMilliseconds(260));
        _toastHideTimer!.Interval = _motion.IsReduced ? TimeSpan.FromMilliseconds(30) : TimeSpan.FromMilliseconds(700);
        _toastHideTimer.Start();
    }

    /// <summary>The toast is gone: the island is shown again and the window gets its small size back.</summary>
    private void HideToast(bool settle)
    {
        _toastHideTimer?.Stop();
        if (_toast is { } toast)
        {
            toast.Visibility = Visibility.Collapsed;
            toast.Close(animate: false);
            toast.Opacity = 1;
        }
        Host.IsHitTestVisible = true;
        Anim.To(Host, OpacityProperty, 1, settle && !_motion.IsReduced ? _motion.Fast : TimeSpan.Zero, FluentEase.Decelerate);
        if (_toastWindow)
        {
            _toastWindow = false;
            // settle false: the panel is about to open and wants the tall window anyway, so do not shrink and regrow.
            if (settle && !_expanded)
            {
                _windowLarge = false;
                if (_hwnd != IntPtr.Zero) _positioner.Apply(_hwnd, expanded: false, _windowWidth);
            }
        }
        if (settle && !_expanded) GoResting(animate: true);
    }

    /// <summary>
    /// Window (and host) width for an island this wide on screen: the usual width, or more for a wide reminder pill, with
    /// room for the red glow (or for the accent pulse, which grows the ring by 12 %), at most the monitor width.
    /// </summary>
    private void SetWindowWidth(double islandDip)
    {
        double need = _settings.ReminderBlink ? islandDip + 24 : islandDip * 1.12 + 24;
        double w = Math.Max(IslandPositioner.WidthDip, Math.Ceiling(need));
        if (_positioner.Monitor is { } m && m.Scale > 0)
            w = Math.Max(IslandPositioner.WidthDip, Math.Min(w, Math.Floor(m.Width / m.Scale)));
        if (Math.Abs(w - _windowWidth) < 0.5) return;
        bool growing = w > _windowWidth;
        _windowWidth = w;
        // Growing: window first, so the island never draws past it. Narrowing: island host first.
        if (!growing) Host.SetHostWidth(w);
        if (_hwnd != IntPtr.Zero) _positioner.Apply(_hwnd, _windowLarge, w);
        if (growing) Host.SetHostWidth(w);
        FileLogger.Info($"window width {w:0}");
    }

    /// <summary>Back to the usual width once nothing wide is on screen.</summary>
    private void RestoreWindowWidth()
    {
        if (_alert is null) SetWindowWidth(0);
    }

    /// <summary>Reminder: an accent ring pulses twice around the island (none in reduced motion: the overdue dot shows).</summary>
    public void PulseReminder()
    {
        if (_expanded || !IsVisible || _motion.IsReduced) return;
        FileLogger.Info("reminder pulse on the island");
        Host.Pulse(TimeSpan.FromMilliseconds(600), 2);
    }

    /// <summary>Lets Exit close the window; otherwise Alt+F4 only collapses.</summary>
    public void AllowClose() => _allowClose = true;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        WindowStyler.ApplyIslandStyles(_hwnd);
        _positioner.Apply(_hwnd, expanded: false);
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        GoResting(animate: false);
#if DEBUG
        RegisterDebugHotkeys();
#endif
    }

    // ---- states ----

    private bool Mascot => _settings.MascotEnabled;

    /// <summary>Collapsed resting state: Mini notch, or the classic info pill when the mascot is off.</summary>
    private void GoResting(bool animate)
    {
        DebugLog($"GoResting from {Host.State} hovering={_hovering}");
        AutomationProperties.SetName(Host, "TaskIsland: " + _vm.PillText);
        if (!Mascot)
        {
            EndPeekVisual();
            Host.AnimateTo(IslandState.Info, _settings.NotchWidth, 36, animate ? _motion.MorphCollapse : TimeSpan.Zero,
                FluentEase.Accelerate, Rested);
            return;
        }
        var d = animate ? _motion.MorphCollapse : TimeSpan.Zero;
        _face.ShowMini(_director.IsSleepy, animate, d);
        Host.AnimateTo(IslandState.Mini, 120, 32, d, FluentEase.Accelerate, Rested);
    }

    private void Rested()
    {
        if (!_expanded) RestoreWindowWidth();
        RefreshTuck();
        if (!_hovering && !_expanded) _efficiency.ScheduleOn();
        if (_panel is null)
            Dispatcher.BeginInvoke(PrewarmPanel, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Builds and lays out the panel once while the island is resting, so the first expand animates instead of
    /// freezing ~0.2 s on template creation and first layout. The panel stays Collapsed (no layout, rendering or
    /// hit testing) until it is opened. Deviation from "created on first expand" is logged in DEVLOG.
    /// </summary>
    private void PrewarmPanel()
    {
        if (_panel is not null || _expanded || Host.IsAnimating) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        EnsurePanel();
        _panel!.Opacity = 0;
        _panel.Visibility = Visibility.Visible;
        _panel.Measure(new Size(IslandPositioner.PanelWidthDip, MaxPanelHeight));
        _panel.Visibility = Visibility.Collapsed;
        DebugLog($"prewarm {sw.ElapsedMilliseconds} ms");
    }

    private void GoInfo()
    {
        DebugLog("GoInfo");
        EndPeekVisual();
        Host.AnimateTo(IslandState.Info, _settings.NotchWidth, 36, _motion.MorphExpand, FluentEase.Decelerate);
        AutomationProperties.SetName(Host, "TaskIsland: " + _vm.PillText);
        if (_settings.PeekOnHover) RestartPeekTimer(InfoPeekDelay);
    }

    private void OnReactionStarted(Reaction r, bool onPanel)
    {
        DebugLog($"reaction {r} onPanel={onPanel}");
        if (_alert is not null && !onPanel) return; // the reminder pill has the notch for its 5 seconds
        if (onPanel)
        {
            _panel?.Face.Show(r, animate: true);
            return;
        }
        if (_expanded) return;
        _peekTimer.Stop();
        EndPeekVisual();
        _tuckTimer.Stop();
        if (_tucked) SlideTo(tucked: false);
        bool fromMini = Host.State == IslandState.Mini;
        if (Host.State != IslandState.Reaction)
            Host.AnimateTo(IslandState.Reaction, 232, 56, _motion.MorphExpand, FluentEase.Decelerate);
        _face.Show(r, animate: true, arrive: fromMini ? _motion.MorphExpand : TimeSpan.Zero);
        AutomationProperties.SetName(Host, ReactionRules.Describe(r));
    }

    private void OnReactionEnded(bool onPanel)
    {
        DebugLog($"reaction ended onPanel={onPanel} hovering={_hovering}");
        if (_alert is not null && !onPanel) return;
        if (onPanel)
        {
            _panel?.Face.Show(Reaction.Neutral, animate: true);
            return;
        }
        if (_expanded) return;
        if (_hovering) GoInfo();
        else GoResting(animate: true);
    }

    private void OnSleepyChanged(bool sleepy)
    {
        if (!_expanded && Host.State == IslandState.Mini && !Host.IsAnimating) _face.ShowMini(sleepy, animate: true);
    }

    /// <summary>Companion idle movement: only on a face that is on screen and still (never on the Info pill).</summary>
    private void OnLifeTick(LifeMove move, bool onPanel)
    {
        if (onPanel)
        {
            if (_expanded) _panel?.Face.PlayLife(move);
            return;
        }
        if (_expanded || Host.IsAnimating || Host.State is not (IslandState.Mini or IslandState.Reaction)) return;
        bool played = _face.PlayLife(move);
        DebugLog($"life {move} state={Host.State} played={played}");
    }

    // ---- hover, peek, click ----

    private void OnHostMouseEnter(object sender, MouseEventArgs e)
    {
        DebugLog($"enter state={Host.State}");
        if (_expanded) return;
        _hovering = true;
        _efficiency.Off();
        _director.NoteInteraction();
        _tuckTimer.Stop();
        if (_tucked) SlideTo(tucked: false);
        if (_alert is not null) return; // hover keeps the reminder pill open
        if (!Mascot)
        {
            Host.SetHover(true);
            Nudge(2);
            if (_settings.PeekOnHover) RestartPeekTimer(LegacyPeekDelay);
            return;
        }
        if (Host.State == IslandState.Mini && _director.Current is null) _director.RequestHover();
    }

    private void OnHostMouseLeave(object sender, MouseEventArgs e)
    {
        DebugLog($"leave state={Host.State} pos={e.GetPosition(Host)}");
        if (_expanded) return;
        _hovering = false;
        _peekTimer.Stop();
        if (!_settings.AlwaysShowNotch && _alert is null) _tuckTimer.Start();
        if (_alert is not null)
        {
            if (_alertHeld) EndAlert();
            return;
        }
        if (!Mascot)
        {
            Host.SetHover(false);
            Nudge(0);
            if (_peeking) GoResting(animate: true);
            else Rested();
            return;
        }
        if (Host.State == IslandState.Info) GoResting(animate: true);
    }

    private void OnHostClick(object sender, MouseButtonEventArgs e)
    {
        if (_expanded) return;
        e.Handled = true;
        _director.NoteInteraction();
        if (_alert is { } alert)
        {
            // A click on the reminder opens the panel on that task's list.
            EndAlert(settle: false);
            _vm.ShowList(alert.ListId);
            Expand();
            return;
        }
        // Clicking a face that popped up by itself makes it laugh; a second click (or any hover state) opens the panel.
        if (Mascot && Host.State == IslandState.Reaction && _director.Current is { } cur
            && !_director.CurrentIsHover && cur != Reaction.Laughing)
        {
            _director.Request(Reaction.Laughing);
            return;
        }
        Expand();
    }

    private void RestartPeekTimer(TimeSpan delay)
    {
        _peekTimer.Stop();
        _peekTimer.Interval = delay;
        _peekTimer.Start();
    }

    /// <summary>Peek: the info pill widens and crossfades to the top task's title.</summary>
    private void StartPeek()
    {
        DebugLog($"StartPeek state={Host.State} hovering={_hovering}");
        // Never over a reminder pill: both draw their text in the same place.
        if (_expanded || _alert is not null || !_hovering || !_vm.HasTopTask || Host.State != IslandState.Info) return;
        _peeking = true;
        double w = Math.Min(_settings.NotchWidth + 140, 360);
        Host.AnimateTo(IslandState.Info, w, 36, _motion.Normal, FluentEase.Decelerate);
        InfoPeek.Visibility = Visibility.Visible;
        Anim.To(InfoCount, OpacityProperty, 0, _motion.Fast);
        Anim.To(InfoPeek, OpacityProperty, 1, _motion.Fast, delay: _motion.Fast / 2);
        AutomationProperties.SetName(Host, "TaskIsland: next task " + _vm.TopTaskTitle);
    }

    private void EndPeekVisual()
    {
        _peeking = false;
        Anim.Stop(InfoCount, OpacityProperty, 1);
        Anim.Stop(InfoPeek, OpacityProperty, 0);
        InfoPeek.Visibility = Visibility.Collapsed;
    }

    private void Nudge(double y) => Anim.To(ChevronNudge, TranslateTransform.YProperty, y, _motion.Fast, FluentEase.Decelerate);

    // ---- expand / collapse ----

    public void Expand()
    {
        if (_expanded) return;
        EndAlert(settle: false);
        _expanded = true;
        _peekTimer.Stop();
        _tuckTimer.Stop();
        if (_tucked) SlideTo(tucked: false);
        StartAutoCollapse();
        _efficiency.Off();
        _director.End();
        _director.SetSuppressed(Suppression.PanelOpen, true);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        EnsurePanel();
        DebugLog($"expand: EnsurePanel {sw.ElapsedMilliseconds} ms");

        _windowLarge = true;
        _positioner.Apply(_hwnd, expanded: true, _windowWidth); // grow first, while nothing moves (still wide after a reminder pill)
        WindowStyler.SetNoActivate(_hwnd, false);
        Activate();
        WindowStyler.BringToForeground(_hwnd);

        double h = Host.MeasurePanel(MaxPanelHeight);
        DebugLog($"expand: measured {sw.ElapsedMilliseconds} ms");
        Host.AnimateTo(IslandState.Panel, IslandPositioner.PanelWidthDip, h, _motion.MorphExpand, FluentEase.Decelerate,
            () => { RestoreWindowWidth(); Host.ShowShadow(true, _motion.Fast); });
        _panel!.FocusQuickAdd();
        AutomationProperties.SetName(Host, "TaskIsland: to-do panel");
    }

    public void Collapse()
    {
        if (!_expanded) return;
        _expanded = false;
        _autoCollapseTimer.Stop();
        _vm.CancelTransientUi(); // an open edit form or dialog card is dropped, not saved
        Host.ShowShadow(false, TimeSpan.Zero);
        EndPeekVisual();
        var d = _motion.MorphCollapse;
        void Done()
        {
            if (_expanded) return; // re-expanded during the collapse
            if (!_toastWindow)   // a reminder toast that appeared during the collapse still needs the tall window
            {
                _windowLarge = false;
                _positioner.Apply(_hwnd, expanded: false, _windowWidth); // shrink after, while nothing moves
            }
            WindowStyler.SetNoActivate(_hwnd, true);
            _director.SetSuppressed(Suppression.PanelOpen, false);
            ApplyVisibility(); // hidden again if Hide, Pause or a full-screen app is still in effect
            Rested();
        }
        if (Mascot)
        {
            _face.ShowMini(_director.IsSleepy, animate: false);
            Host.AnimateTo(IslandState.Mini, 120, 32, d, FluentEase.Accelerate, Done);
        }
        else
        {
            Host.AnimateTo(IslandState.Info, _settings.NotchWidth, 36, d, FluentEase.Accelerate, Done);
        }
        AutomationProperties.SetName(Host, "TaskIsland: " + _vm.PillText);
    }

    private void EnsurePanel()
    {
        if (_panel is not null) return;
        _panel = new PanelView(_vm, Mascot);
        _panel.CollapseRequested += (_, _) => Collapse();
        _panel.ExitRequested += (_, _) => ((App)Application.Current).ExitApp();
        _panel.SettingsRequested += (_, _) =>
        {
            // The settings window is a normal window: collapse the island first so it does not sit over it.
            Collapse();
            ((App)Application.Current).OpenSettings();
        };
        _panel.SizeChanged += OnPanelSizeChanged;
        Host.Panel = _panel;
    }

    /// <summary>Rows added/removed or a filter switch: the clip re-animates to the new height (normal token).</summary>
    private void OnPanelSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_expanded || Host.State != IslandState.Panel || !e.HeightChanged) return;
        double h = Math.Min(e.NewSize.Height, MaxPanelHeight);
        if (Math.Abs(h - Host.IslandRect.Height) < 0.5) return;
        bool wasStill = !Host.IsAnimating;
        Host.ShowShadow(false, TimeSpan.Zero);
        Host.AnimateTo(IslandState.Panel, IslandPositioner.PanelWidthDip, h, wasStill ? _motion.Normal : _motion.MorphExpand,
            FluentEase.Decelerate, () => Host.ShowShadow(true, _motion.Fast));
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_expanded && HandlePanelKey(e)) return;
        base.OnPreviewKeyDown(e);
    }

    /// <summary>
    /// Esc closes the innermost thing first: a dialog card, the open edit form, the typed text, and only then the panel.
    /// Ctrl+1…9 jump to a list, Ctrl+0 shows All lists, Ctrl+Tab / Ctrl+Shift+Tab cycle (PLAN.md section 6.4).
    /// </summary>
    private bool HandlePanelKey(KeyEventArgs e)
    {
        var mods = Keyboard.Modifiers;
        if (e.Key == Key.Escape && _settings.CollapseOnEscape)
        {
            e.Handled = true;
            if (_vm.HasOverlay || _vm.Editing is not null) _vm.CancelTransientUi();
            else if (_panel is { QuickAddHasText: true } p && p.IsKeyboardFocusWithin) p.ClearQuickAdd();
            else Collapse();
            return true;
        }
        if (mods != ModifierKeys.Control && !(mods == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Tab)) return false;
        if (_vm.HasOverlay) return false;
        int digit = e.Key is >= Key.D0 and <= Key.D9 ? e.Key - Key.D0 : e.Key is >= Key.NumPad0 and <= Key.NumPad9 ? e.Key - Key.NumPad0 : -1;
        if (digit >= 0)
        {
            _vm.SelectChipAt(digit);
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Tab)
        {
            _vm.SelectNextChip(mods.HasFlag(ModifierKeys.Shift) ? -1 : 1);
            e.Handled = true;
            return true;
        }
        return false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // ✕ and Alt+F4 only collapse; Exit is the only way out (PLAN.md section 7.5).
        if (!_allowClose)
        {
            e.Cancel = true;
            Collapse();
        }
        base.OnClosing(e);
    }

    // ---- view model reactions ----

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IslandViewModel.PillText)) return;
        // Count change: the number slides 6 px up while fading in, once.
        if (Host.State == IslandState.Info && !_motion.IsReduced)
        {
            Anim.Keys(CountSlide, TranslateTransform.YProperty, FluentEase.Decelerate, null, (0, 6), (_motion.Fast.TotalMilliseconds, 0));
            Anim.Keys(CountText, OpacityProperty, FluentEase.Decelerate, null, (0, 0), (_motion.Fast.TotalMilliseconds, 1));
        }
        if (!_expanded) AutomationProperties.SetName(Host, "TaskIsland: " + _vm.PillText);
    }

    /// <summary>Settings that change the island's look live (Phase 5 calls this from the Settings window).</summary>
    public void ApplyLookSettings()
    {
        FileLogger.Info($"look applied: mascot {Mascot}, notch width {_settings.NotchWidth}, opacity {_settings.PanelOpacity:0.##}, flush {_settings.TopMargin == 0}");
        Host.SetMascotLook(Mascot);
        Host.Flush = _settings.TopMargin == 0;
        Host.PanelOpacity = _settings.PanelOpacity;
        _panel?.SetMascot(Mascot);
        _director.SettingsChanged();
        if (!_expanded) GoResting(animate: false);
        if (_expanded) StartAutoCollapse();
    }

#if !DEBUG
    private static bool IsDemo => false;
#endif

    /// <summary>Debug builds only: state changes to %TEMP%\TaskIsland-debug.log (Release has no logging until Phase 4).</summary>
    [System.Diagnostics.Conditional("DEBUG")]
    private static void DebugLog(string message)
    {
        try
        {
            System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TaskIsland-debug.log"),
                $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (System.IO.IOException) { }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
#if DEBUG
        if (msg == Hotkeys.WM_HOTKEY && OnDebugHotkey(wParam.ToInt32())) handled = true;
#endif
        return IntPtr.Zero;
    }

#if DEBUG
    // ---- Debug-only helpers (compiled out of Release) ----

    private const int HotkeyCycleFaces = 0xB001, HotkeyTheme = 0xB002, HotkeyMascot = 0xB003;
    private int _debugFace = -1;
    private bool _demo;
    private bool IsDemo => _demo;

    private void RegisterDebugHotkeys()
    {
        const uint mods = Hotkeys.Control | Hotkeys.Alt | Hotkeys.Shift;
        Hotkeys.Register(_hwnd, HotkeyCycleFaces, mods, 0x46); // F: cycle the 12 faces
        Hotkeys.Register(_hwnd, HotkeyTheme, mods, 0x4C);      // L: light/dark
        Hotkeys.Register(_hwnd, HotkeyMascot, mods, 0x4D);     // M: mascot on/off
        Closed += (_, _) =>
        {
            Hotkeys.Unregister(_hwnd, HotkeyCycleFaces);
            Hotkeys.Unregister(_hwnd, HotkeyTheme);
            Hotkeys.Unregister(_hwnd, HotkeyMascot);
        };
    }

    private bool OnDebugHotkey(int id)
    {
        switch (id)
        {
            case HotkeyCycleFaces:
                _debugFace = (_debugFace + 1) % 12;
                _director.Force((Reaction)_debugFace);
                return true;
            case HotkeyTheme:
                ((App)Application.Current).ToggleTheme();
                return true;
            case HotkeyMascot:
                _settings.MascotEnabled = !_settings.MascotEnabled;
                ApplyLookSettings();
                return true;
        }
        return false;
    }

    /// <summary>
    /// Debug screenshots without screen capture: after <paramref name="delay"/> renders the window onto a plain blue
    /// backdrop, saves a PNG at the monitor's scale and exits. Nothing else on the screen can end up in the image.
    /// </summary>
    public void SnapAndExit(string path, TimeSpan delay)
    {
        var t = new DispatcherTimer { Interval = delay };
        t.Tick += (_, _) =>
        {
            t.Stop();
            FileLogger.Info($"snap: alert border {Host.HasAlertBorder}, opacity {Host.AlertBorderOpacity:0.00}");
            double scale = _positioner.Monitor?.Scale ?? 1;
            var root = (FrameworkElement)Content;
            int w = (int)Math.Round(root.ActualWidth * scale), h = (int)Math.Round(root.ActualHeight * scale);
            var island = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            island.Render(root);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var bg = new LinearGradientBrush(Color.FromRgb(70, 110, 200), Color.FromRgb(30, 55, 140), 60);
                dc.DrawRectangle(bg, null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
                dc.DrawImage(island, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
            }
            var final = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            final.Render(visual);
            var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
            png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(final));
            using (var f = System.IO.File.Create(path)) png.Save(f);
            ((App)Application.Current).ExitApp();
        };
        t.Start();
    }

    /// <summary>Debug screenshots: <c>--demo mini|info|peek|panel|face:Name</c> holds that state.</summary>
    public void RunDemo(string demo)
    {
        _demo = true;
        _director.HoldForever = true;
        if (demo.StartsWith("cycle:", StringComparison.OrdinalIgnoreCase) && int.TryParse(demo[6..], out int cycles))
        {
            // Expand/collapse N times (first after 4 s, then every 1.5 s): first-expand timing and leak checks.
            _demo = false;
            _director.HoldForever = false;
            int done = 0;
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            t.Tick += (_, _) =>
            {
                t.Interval = TimeSpan.FromSeconds(0.75);
                if (_expanded) { Collapse(); if (++done >= cycles) t.Stop(); }
                else Expand();
            };
            t.Start();
            return;
        }
        if (demo.StartsWith("face:", StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse<Reaction>(demo[5..], true, out var r))
        {
            _director.Force(r);
            return;
        }
        switch (demo.ToLowerInvariant())
        {
            case "info":
            case "peek":
                _hovering = true;
                _director.End();
                GoInfo();
                _peekTimer.Stop();
                if (demo.Equals("peek", StringComparison.OrdinalIgnoreCase))
                    Dispatcher.BeginInvoke(StartPeek, DispatcherPriority.Background);
                break;
            case "panel":
                _director.End();
                Expand();
                break;
            case "edit":       // Phase 3 screenshots: first row's edit form
            case "newlist":    // the list editor card
            case "deletelist": // the delete-list card
            case "calendar":   // the edit form with the due-date popup open
                _director.End();
                Expand();
                Dispatcher.BeginInvoke(() =>
                {
                    if (demo == "newlist") _vm.NewListCommand.Execute(null);
                    else if (demo == "deletelist") _vm.OpenDeleteListCommand.Execute(_vm.Chips.Skip(1).FirstOrDefault());
                    else if (_vm.Tasks.FirstOrDefault() is { } row)
                    {
                        row.EditCommand.Execute(null);
                        if (demo == "calendar") _panel?.OpenFirstCalendar();
                    }
                }, DispatcherPriority.ApplicationIdle);
                break;
            case "undo":       // Sileo S3 screenshots: the panel with the Undo / notice toast
            case "notice":
            case "error":
                _director.End();
                Expand();
                Dispatcher.BeginInvoke(() => _vm.DebugShowToast(demo.ToLowerInvariant()), DispatcherPriority.ApplicationIdle);
                break;
            case "sleepy":
                _director.End();
                _face.ShowMini(sleepy: true, animate: false);
                break;
            default:
                _director.End();
                break;
        }
    }
#endif
}
