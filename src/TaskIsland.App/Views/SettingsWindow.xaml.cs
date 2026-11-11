using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using TaskIsland.App.Controls;
using TaskIsland.App.Logging;
using TaskIsland.App.Services;
using TaskIsland.App.ViewModels;
using TaskIsland.Core.Toasts;
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
    private ToastPresenter? _toasts;

    public SettingsWindow(SettingsViewModel vm, SettingsApplier apply)
    {
        _vm = vm;
        _apply = apply;
        InitializeComponent();
        DataContext = vm;
        vm.Owner = this;
        HotkeyBox.Validator = vm.TryRebindHotkey;
        SourceInitialized += OnSourceInitialized;
        _toasts = new ToastPresenter(ToastLayer, expandsUp: true, "PanelToastFillBrush", "PanelToastStrokeBrush");
        _toasts.Clicked += (_, _) => _toasts.Hide();
        vm.PropertyChanged += OnViewModelChanged;
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

    /// <summary>
    /// Data actions (SILEO-PLAN.md S4): while one runs a loading toast ("Backing Up") shows; when it ends the same toast
    /// morphs into the result ("Backup Saved" with the message, or "Didn't Work" in red) and hides itself after 6 s.
    /// </summary>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(SettingsViewModel.Busy) or nameof(SettingsViewModel.DataStatus) or nameof(SettingsViewModel.DataStatusIsError)))
            return;
        if (_toasts is not { } toasts) return;
        if (_vm.Busy)
            toasts.Show(ToastState.Loading, _vm.DataOperation, null, null);
        else if (_vm.DataStatus.Length > 0)
            toasts.Show(_vm.DataStatusIsError ? ToastState.Error : ToastState.Success,
                _vm.DataStatusIsError ? "Didn't Work" : _vm.DataDoneTitle, _vm.DataStatus, null, TimeSpan.FromSeconds(6));
        else
            toasts.Hide();
    }

    private void OnThemeChanged(bool light) => Dwm.SetDarkTitleBar(Handle, !light);

    private void OnClosed(object? sender, EventArgs e)
    {
        ((App)Application.Current).ThemeChanged -= OnThemeChanged;
        _vm.PropertyChanged -= OnViewModelChanged;
        _vm.Owner = null;
        _apply.FlushPending();
        FileLogger.Info("settings window closed");
    }

#if DEBUG
    /// <summary>Debug screenshots: shows the data-action toast for <c>--demo settings-ok|settings-error|settings-loading</c>.</summary>
    public void DebugToast(string? demo) => Dispatcher.BeginInvoke(() => _vm.DebugStatus(demo), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

    /// <summary>Debug: jump to a page by name.</summary>
    public void SelectPage(string name)
    {
        var button = name.ToLowerInvariant() switch
        {
            "appearance" => NavAppearance,
            "behaviour" or "reminders" => NavBehaviour,
            "lists" => NavLists,
            "data" => NavData,
            "about" => NavAbout,
            _ => NavGeneral,
        };
        button.IsChecked = true;
        // "reminders" (debug screenshots): the Behaviour page scrolled down to its Reminders section.
        if (name.Equals("reminders", StringComparison.OrdinalIgnoreCase))
            Dispatcher.BeginInvoke(() => RemindersLabel.BringIntoView(new Rect(0, 0, 1, PageScroll.ViewportHeight)),
                System.Windows.Threading.DispatcherPriority.Loaded);
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
