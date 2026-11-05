using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace MacSpike;

internal static class Program
{
    public static bool SelfTest;

    [STAThread]
    public static void Main(string[] args)
    {
        SelfTest = args.Contains("--selftest");
        Log.Write($"start: {RuntimeInformationText()} selftest={SelfTest} path={Environment.ProcessPath}");
        AppBuilder.Configure<SpikeApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    private static string RuntimeInformationText() =>
        $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription} " +
        $"{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture} .NET {Environment.Version}";
}

internal static class Log
{
    private static readonly string FilePath = OperatingSystem.IsMacOS()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Logs", "TaskIslandSpike.log")
        : Path.Combine(Path.GetTempPath(), "TaskIslandSpike.log");

    public static event Action<string>? Added;

    public static void Write(string line)
    {
        var text = $"{DateTime.Now:HH:mm:ss.fff} {line}";
        Console.WriteLine(text);
        try { File.AppendAllText(FilePath, text + Environment.NewLine); } catch { /* the log must never stop the test */ }
        Added?.Invoke(text);
    }

    public static string PathText => FilePath;
}

internal sealed class SpikeApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var island = new IslandWindow();
            desktop.MainWindow = island;
            island.Show();
            AddMenuBarIcon(island, desktop);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void AddMenuBarIcon(IslandWindow island, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var menu = new NativeMenu();
        void Item(string text, Action act)
        {
            var i = new NativeMenuItem(text);
            i.Click += (_, _) => act();
            menu.Add(i);
        }
        Item("Open or close the panel", island.Toggle);
        Item("Move to next screen", island.NextScreen);
        Item("Toggle show over full-screen apps", island.ToggleOverFullScreen);
        Item("Toggle face animation", island.ToggleFace);
        Item("Quit", () => desktop.Shutdown());

        var tray = new TrayIcon { Icon = MakeTrayImage(), ToolTipText = "TaskIsland spike", Menu = menu };
        MacOSProperties.SetIsTemplateIcon(tray, true);
        TrayIcon.SetIcons(this, [tray]);
        Log.Write("menu bar icon added");
    }

    private static WindowIcon MakeTrayImage()
    {
        var bmp = new RenderTargetBitmap(new PixelSize(44, 44));
        using (var dc = bmp.CreateDrawingContext())
        {
            dc.DrawRectangle(Brushes.Black, null, new RoundedRect(new Rect(2, 12, 40, 20), 10));
            dc.DrawEllipse(Brushes.White, null, new Point(16, 22), 3.5, 3.5);
            dc.DrawEllipse(Brushes.White, null, new Point(28, 22), 3.5, 3.5);
        }
        using var ms = new MemoryStream();
        bmp.Save(ms);
        ms.Position = 0;
        return new WindowIcon(ms);
    }
}

internal sealed class IslandWindow : Window
{
    private const double PillW = 220, PillH = 52, PanelW = 420, PanelH = 500, Edge = 16;

    private readonly Border _pill;
    private readonly Canvas _face;
    private readonly Avalonia.Controls.Shapes.Ellipse _eyeL, _eyeR;
    private readonly StackPanel _panel;
    private readonly TextBox _input;
    private readonly TextBlock _logView;
    private readonly DispatcherTimer _blink, _stats;
    private readonly List<double> _frames = [];
    private readonly CubicEaseOut _ease = new();
    private readonly Process _proc = Process.GetCurrentProcess();

    private IntPtr _ns;
    private int _screen;
    private bool _expanded, _overFullScreen, _faceOn = true, _animating;
    private TimeSpan _lastCpu;
    private DateTime _lastStats;
    private double _targetW = PillW + Edge * 2, _targetH = PillH + Edge * 2;

    public IslandWindow()
    {
        Title = "TaskIsland spike";
        Width = PillW + Edge * 2;
        Height = PillH + Edge * 2;
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        WindowDecorations = WindowDecorations.None;
        ExtendClientAreaToDecorationsHint = false;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;

        _eyeL = Eye(); _eyeR = Eye();
        _face = new Canvas
        {
            Width = PillW, Height = PillH, IsHitTestVisible = false, Children = { _eyeL, _eyeR },
            // Stays at the top of the pill while the panel is open.
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        Canvas.SetLeft(_eyeL, PillW / 2 - 28); Canvas.SetTop(_eyeL, 20);
        Canvas.SetLeft(_eyeR, PillW / 2 + 16); Canvas.SetTop(_eyeR, 20);

        _input = new TextBox { Watermark = "Type here: does typing work?", Margin = new Thickness(0, 0, 0, 8) };
        _logView = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightGray };
        _panel = new StackPanel { Margin = new Thickness(16, 56, 16, 12), IsVisible = false };
        _panel.Children.Add(_input);
        _panel.Children.Add(Buttons(("Login item ON", () => Mac.LoginItem(true)), ("Login item OFF", () => Mac.LoginItem(false))));
        _panel.Children.Add(Buttons(("LaunchAgent ON", () => Mac.LaunchAgent(true)), ("LaunchAgent OFF", () => Mac.LaunchAgent(false))));
        _panel.Children.Add(new ScrollViewer { Height = 260, Content = _logView });

        _pill = new Border
        {
            Width = PillW, Height = PillH, CornerRadius = new CornerRadius(26),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 0, 0),
            Background = new SolidColorBrush(Color.Parse("#0B0C14")),
            BorderBrush = new SolidColorBrush(Color.Parse("#4A5BFF")), BorderThickness = new Thickness(1.5),
            ClipToBounds = true,
            Child = new Grid { Children = { _face, _panel } },
        };
        Content = _pill;

        _pill.PointerEntered += (_, _) => Log.Write($"hover: frontmost app is {Mac.FrontmostApp()}");
        _pill.PointerPressed += (_, _) =>
        {
            Log.Write($"click on pill: frontmost before = {Mac.FrontmostApp()}");
            if (!_expanded) Toggle();
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape && _expanded) Toggle(); };
        Deactivated += (_, _) => { if (_expanded && !_animating && !Program.SelfTest) { Log.Write("clicked away: collapsing"); Toggle(); } };

        Log.Added += line => Dispatcher.UIThread.Post(() =>
        {
            var t = line + "\n" + _logView.Text;
            _logView.Text = t.Length > 4000 ? t[..4000] : t;
        });

        _blink = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _blink.Tick += async (_, _) =>
        {
            _eyeL.Height = _eyeR.Height = 2;
            await Task.Delay(120);
            _eyeL.Height = _eyeR.Height = 12;
        };
        _stats = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _stats.Tick += (_, _) => LogStats("idle");

        Opened += OnOpened;
        SizeChanged += (_, _) => Place(); // same frame again is a no-op; corrects any later resize by Avalonia
    }

    private static Avalonia.Controls.Shapes.Ellipse Eye() => new() { Width = 12, Height = 12, Fill = Brushes.AliceBlue };

    private static StackPanel Buttons(params (string Text, Action Act)[] items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        foreach (var (text, act) in items)
        {
            var b = new Button { Content = text, FontSize = 12 };
            b.Click += (_, _) => act();
            row.Children.Add(b);
        }
        return row;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        var h = TryGetPlatformHandle();
        Log.Write($"platform handle: {h?.HandleDescriptor} {h?.Handle}");
        _ns = Mac.WindowOf(h?.Handle ?? IntPtr.Zero, h?.HandleDescriptor);
        Mac.SetAccessoryApp();
        Mac.StyleIsland(_ns, _overFullScreen);
        Mac.LogScreens();
        Place();
        Mac.RegisterHotkey(() => Dispatcher.UIThread.Post(() => { Log.Write("hotkey pressed"); Toggle(); }));
        Log.Write($"login item status: {Mac.LoginItemStatus()}");
        Log.Write($"log file: {Log.PathText}");
        _lastCpu = _proc.TotalProcessorTime;
        _lastStats = DateTime.UtcNow;
        _blink.Start();
        _stats.Start();
        if (Program.SelfTest) _ = RunSelfTestAsync();
    }

    private void Place() => Mac.PlaceBelowMenuBar(_ns, _screen, _targetW, _targetH);

    public void NextScreen()
    {
        _screen = (_screen + 1) % Math.Max(1, Mac.ScreenCount);
        Log.Write($"moving to screen {_screen}");
        Place();
    }

    public void ToggleOverFullScreen()
    {
        _overFullScreen = !_overFullScreen;
        Mac.StyleIsland(_ns, _overFullScreen);
    }

    public void ToggleFace()
    {
        _faceOn = !_faceOn;
        if (_faceOn) _blink.Start(); else _blink.Stop();
        Log.Write($"face animation {(_faceOn ? "on" : "off")}");
    }

    public void Toggle()
    {
        if (_animating) return;
        _expanded = !_expanded;
        if (_expanded)
        {
            SetWindowSize(PanelW + Edge * 2, PanelH + Edge * 2);
            Mac.TakeFocus(_ns);
            _panel.IsVisible = true;
            _input.Focus();
            Log.Write($"expanded: frontmost after = {Mac.FrontmostApp()}, key window = {Mac.IsKey(_ns)}");
        }
        else
        {
            _panel.IsVisible = false;
        }
        Morph(_expanded);
    }

    private void SetWindowSize(double w, double h)
    {
        _targetW = w;
        _targetH = h;
        Place();
        Width = w;
        Height = h;
    }

    /// <summary>Drives the pill's size from real frame callbacks, so the numbers show actual frame times.</summary>
    private void Morph(bool open)
    {
        const double ms = 320;
        _animating = true;
        _frames.Clear();
        double fromW = _pill.Width, fromH = _pill.Height;
        double toW = open ? PanelW : PillW, toH = open ? PanelH : PillH;
        TimeSpan? start = null, last = null;

        void Frame(TimeSpan now)
        {
            start ??= now;
            if (last is not null) _frames.Add((now - last.Value).TotalMilliseconds);
            last = now;
            var p = Math.Min(1, (now - start.Value).TotalMilliseconds / ms);
            var e = _ease.Ease(p);
            _pill.Width = fromW + (toW - fromW) * e;
            _pill.Height = fromH + (toH - fromH) * e;
            Canvas.SetLeft(_eyeL, _pill.Width / 2 - 28);
            Canvas.SetLeft(_eyeR, _pill.Width / 2 + 16);
            _face.Width = _pill.Width;
            if (p < 1)
            {
                RequestAnimationFrame(Frame);
                return;
            }
            _animating = false;
            var worst = _frames.Count > 0 ? _frames.Max() : 0;
            var avg = _frames.Count > 0 ? _frames.Average() : 0;
            Log.Write($"morph {(open ? "open" : "close")}: {_frames.Count} frames, average {avg:0.0} ms, worst {worst:0.0} ms");
            if (!open)
            {
                SetWindowSize(PillW + Edge * 2, PillH + Edge * 2);
                Mac.GiveFocusBack();
                Log.Write($"collapsed: frontmost after = {Mac.FrontmostApp()}");
            }
            _ = CheckFrameLaterAsync(open ? "panel" : "pill");
        }
        RequestAnimationFrame(Frame);
    }

    /// <summary>Checks after the resize has settled. "FRAME FAIL" also fails the CI self-test.</summary>
    private async Task CheckFrameLaterAsync(string label)
    {
        await Task.Delay(300);
        var (ok, text) = Mac.CheckFrame(_ns, _screen, _targetW, _targetH);
        Log.Write($"{(ok ? "frame ok" : "FRAME FAIL")} {label}: {text}");
    }

    private void LogStats(string label)
    {
        _proc.Refresh();
        var now = DateTime.UtcNow;
        var cpu = (_proc.TotalProcessorTime - _lastCpu).TotalMilliseconds / Math.Max(1, (now - _lastStats).TotalMilliseconds) * 100;
        _lastCpu = _proc.TotalProcessorTime;
        _lastStats = now;
        Log.Write($"{label}: cpu {cpu:0.00}% of one core, memory {_proc.WorkingSet64 / 1048576} MB, face {(_faceOn ? "on" : "off")}");
    }

    private async Task RunSelfTestAsync()
    {
        await Task.Delay(2000);
        Toggle();
        await Task.Delay(1500);
        _input.Text = "typed by self test";
        Log.Write($"selftest: text box holds \"{_input.Text}\", focused={_input.IsFocused}");
        Toggle();
        await Task.Delay(1500);
        NextScreen();
        LogStats("after morphs");
        await Task.Delay(20000);
        LogStats("idle 20 s, face on");
        ToggleFace();
        await Task.Delay(20000);
        LogStats("idle 20 s, face off");
        Log.Write("SELFTEST DONE");
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }
}
