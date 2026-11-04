using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using TaskIsland.App.Logging;
using TaskIsland.App.Services;
using TaskIsland.App.ViewModels;
using TaskIsland.Windows;

namespace TaskIsland.App.Views;

/// <summary>
/// The Settings window (PLAN.md section 6.6): a normal window with a taskbar entry, one instance, created when opened and
/// released when closed so it costs nothing while closed. Settings apply live through <see cref="SettingsApplier"/>.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private readonly SettingsApplier _apply;
    private bool _mica;

    public SettingsWindow(SettingsViewModel vm, SettingsApplier apply)
    {
        _vm = vm;
        _apply = apply;
        InitializeComponent();
        DataContext = vm;
        vm.Owner = this;
        HotkeyBox.Validator = vm.TryRebindHotkey;
        SourceInitialized += OnSourceInitialized;
        ((App)Application.Current).ThemeChanged += OnThemeChanged;
        Closed += OnClosed;
    }

    private IntPtr Handle => new WindowInteropHelper(this).Handle;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var app = (App)Application.Current;
        Dwm.SetDarkTitleBar(Handle, !app.IsLightTheme);
        // Mica needs a transparent client area; if Windows refuses, keep the solid Surface colour.
        if (Dwm.TryEnableMica(Handle) && HwndSource.FromHwnd(Handle) is { CompositionTarget: { } target })
        {
            target.BackgroundColor = Colors.Transparent;
            Background = Brushes.Transparent;
            _mica = true;
        }
        FileLogger.Info($"settings window opened (backdrop: {(_mica ? "Mica" : "solid")})");
    }

    private void OnThemeChanged(bool light) => Dwm.SetDarkTitleBar(Handle, !light);

    private void OnClosed(object? sender, EventArgs e)
    {
        ((App)Application.Current).ThemeChanged -= OnThemeChanged;
        _vm.Owner = null;
        _apply.FlushPending();
        FileLogger.Info("settings window closed");
    }

#if DEBUG
    /// <summary>Debug: jump to a page by name.</summary>
    public void SelectPage(string name)
    {
        var button = name.ToLowerInvariant() switch
        {
            "appearance" => NavAppearance,
            "behaviour" => NavBehaviour,
            "lists" => NavLists,
            "data" => NavData,
            "about" => NavAbout,
            _ => NavGeneral,
        };
        button.IsChecked = true;
    }

    /// <summary>Debug screenshots without screen capture: renders the window content on the Surface colour and exits.</summary>
    public void SnapAndExit(string path, TimeSpan delay)
    {
        var t = new System.Windows.Threading.DispatcherTimer { Interval = delay };
        t.Tick += (_, _) =>
        {
            t.Stop();
            var root = (FrameworkElement)Content;
            double w = root.ActualWidth, h = root.ActualHeight;
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap((int)w, (int)h, 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) dc.DrawRectangle((Brush)FindResource("SurfaceBrush"), null, new Rect(0, 0, w, h));
            bmp.Render(visual);
            bmp.Render(root);
            var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
            png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using (var f = System.IO.File.Create(path)) png.Save(f);
            ((App)Application.Current).ExitApp();
        };
        t.Start();
    }
#endif
}
