using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TaskIsland.App.Controls;
using TaskIsland.App.Services;
using TaskIsland.App.ViewModels;
using TaskIsland.Core.Mascot;
using TaskIsland.Core.Models;
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
    private readonly DispatcherTimer _peekTimer;
    private PanelView? _panel;
    private IntPtr _hwnd;
    private bool _hovering, _expanded, _peeking, _allowClose;

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
        Host.SetMascotLook(settings.MascotEnabled);
        Host.MouseEnter += OnHostMouseEnter;
        Host.MouseLeave += OnHostMouseLeave;
        Host.MouseLeftButtonUp += OnHostClick;

        _peekTimer = new DispatcherTimer(DispatcherPriority.Normal);
        _peekTimer.Tick += (_, _) => { _peekTimer.Stop(); StartPeek(); };

        director.ReactionStarted += OnReactionStarted;
        director.ReactionEnded += OnReactionEnded;
        director.SleepyChanged += OnSleepyChanged;
        vm.PropertyChanged += OnVmChanged;

        SourceInitialized += OnSourceInitialized;
        Deactivated += (_, _) => { if (_expanded && _settings.CollapseOnClickOutside && !IsDemo) Collapse(); };
        IsVisibleChanged += (_, _) => director.SetSuppressed(Suppression.Hidden, !IsVisible);
    }

    public bool IsExpanded => _expanded;

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
        if (onPanel)
        {
            _panel?.Face.Show(r, animate: true);
            return;
        }
        if (_expanded) return;
        _peekTimer.Stop();
        EndPeekVisual();
        bool fromMini = Host.State == IslandState.Mini;
        if (Host.State != IslandState.Reaction)
            Host.AnimateTo(IslandState.Reaction, 232, 56, _motion.MorphExpand, FluentEase.Decelerate);
        _face.Show(r, animate: true, arrive: fromMini ? _motion.MorphExpand : TimeSpan.Zero);
        AutomationProperties.SetName(Host, ReactionRules.Describe(r));
    }

    private void OnReactionEnded(bool onPanel)
    {
        DebugLog($"reaction ended onPanel={onPanel} hovering={_hovering}");
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

    // ---- hover, peek, click ----

    private void OnHostMouseEnter(object sender, MouseEventArgs e)
    {
        DebugLog($"enter state={Host.State}");
        if (_expanded) return;
        _hovering = true;
        _efficiency.Off();
        _director.NoteInteraction();
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
        if (_expanded || !_hovering || !_vm.HasTopTask || Host.State != IslandState.Info) return;
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
        _expanded = true;
        _peekTimer.Stop();
        _efficiency.Off();
        _director.End();
        _director.SetSuppressed(Suppression.PanelOpen, true);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        EnsurePanel();
        DebugLog($"expand: EnsurePanel {sw.ElapsedMilliseconds} ms");

        _positioner.Apply(_hwnd, expanded: true); // grow first, while nothing moves
        WindowStyler.SetNoActivate(_hwnd, false);
        Activate();
        WindowStyler.BringToForeground(_hwnd);

        double h = Host.MeasurePanel(MaxPanelHeight);
        DebugLog($"expand: measured {sw.ElapsedMilliseconds} ms");
        Host.AnimateTo(IslandState.Panel, IslandPositioner.PanelWidthDip, h, _motion.MorphExpand, FluentEase.Decelerate,
            () => Host.ShowShadow(true, _motion.Fast));
        _panel!.FocusQuickAdd();
        AutomationProperties.SetName(Host, "TaskIsland: to-do panel");
    }

    public void Collapse()
    {
        if (!_expanded) return;
        _expanded = false;
        Host.ShowShadow(false, TimeSpan.Zero);
        EndPeekVisual();
        var d = _motion.MorphCollapse;
        void Done()
        {
            if (_expanded) return; // re-expanded during the collapse
            _positioner.Apply(_hwnd, expanded: false); // shrink after, while nothing moves
            WindowStyler.SetNoActivate(_hwnd, true);
            _director.SetSuppressed(Suppression.PanelOpen, false);
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
        if (e.Key == Key.Escape && _expanded && _settings.CollapseOnEscape)
        {
            e.Handled = true;
            if (_panel is { QuickAddHasText: true } p && p.IsKeyboardFocusWithin) p.ClearQuickAdd();
            else Collapse();
            return;
        }
        base.OnPreviewKeyDown(e);
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
        Host.SetMascotLook(Mascot);
        Host.PanelOpacity = _settings.PanelOpacity;
        _panel?.SetMascot(Mascot);
        _director.SettingsChanged();
        if (!_expanded) GoResting(animate: false);
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
