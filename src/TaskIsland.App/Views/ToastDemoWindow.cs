#if DEBUG
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TaskIsland.App.Controls;
using TaskIsland.Core.Toasts;

namespace TaskIsland.App.Views;

/// <summary>
/// Debug only (<c>--demo toast</c>, SILEO-PLAN.md phase S1): a gallery of <see cref="SileoToast"/> states plus one live
/// toast that loops through Sileo's sequence (pill, melt open, collapse, swap to loading, swap to success).
/// With <c>--snap file.png</c> it renders itself to a PNG after <paramref name="snapDelay"/> and exits.
/// </summary>
public sealed class ToastDemoWindow : Window
{
    private readonly SileoToast _live = new() { Align = ToastAlign.Center };
    private readonly DispatcherTimer _loop = new();
    private int _step;

    public ToastDemoWindow(string? snapPath, TimeSpan snapDelay)
    {
        Title = "TaskIsland toast demo";
        Width = 1260;
        Height = 760;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new LinearGradientBrush(Color.FromRgb(70, 110, 200), Color.FromRgb(30, 55, 140), 60);

        var gallery = new WrapPanel { Margin = new Thickness(24, 24, 24, 0) };
        gallery.Children.Add(Cell("Closed (tab only)", Make(ToastState.Success, "Changes Saved", null, null, ToastAlign.Left, open: false)));
        gallery.Children.Add(Cell("Open, tab left", Make(ToastState.Success, "Changes Saved",
            "Your backup was written to the Backups folder.", null, ToastAlign.Left, open: true)));
        gallery.Children.Add(Cell("Open, tab centred, button", Make(ToastState.Error, "Backup Failed",
            "Windows blocked writing the file. Choose another folder.", "Try again", ToastAlign.Center, open: true)));
        gallery.Children.Add(Cell("Open, tab right", Make(ToastState.Warning, "Last List",
            "The last remaining list can't be deleted.", null, ToastAlign.Right, open: true)));
        gallery.Children.Add(Cell("Info", Make(ToastState.Info, "Reminder",
            "Training ISO wei yee is due at 2:00 PM.", "Open", ToastAlign.Left, open: true)));
        gallery.Children.Add(Cell("Action", Make(ToastState.Action, "1 Task Deleted", null, "Undo", ToastAlign.Left, open: true)));
        gallery.Children.Add(Cell("Loading (never opens)", Make(ToastState.Loading, "Backing Up", "ignored", null, ToastAlign.Left, open: true)));

        var liveCell = Cell("Live: loops through the Sileo sequence", _live);
        liveCell.Margin = new Thickness(24, 8, 0, 0);
        var root = new StackPanel { Children = { gallery, liveCell } };
        Content = root;

        _live.SetContent(ToastState.Success, "Changes Saved", "Saved to the database. Your lists are up to date.", null, animate: false);
        _loop.Tick += (_, _) => Next();
        Loaded += (_, _) =>
        {
            _loop.Interval = TimeSpan.FromMilliseconds(150);   // Sileo opens 150 ms after showing
            _loop.Start();
        };

        if (snapPath is not null)
        {
            var t = new DispatcherTimer { Interval = snapDelay };
            t.Tick += (_, _) =>
            {
                t.Stop();
                Snap(root, snapPath);
                ((App)Application.Current).ExitApp();
            };
            t.Start();
        }
    }

    /// <summary>One loop: open, close, loading, success (open), close, back to the start.</summary>
    private void Next()
    {
        switch (_step++ % 6)
        {
            case 0:
                _live.Open();
                _loop.Interval = TimeSpan.FromSeconds(2.4);
                break;
            case 1:
                _live.Close();
                _loop.Interval = TimeSpan.FromSeconds(0.9);
                break;
            case 2:
                _live.SetContent(ToastState.Loading, "Backing Up", null, null);
                _loop.Interval = TimeSpan.FromSeconds(1.6);
                break;
            case 3:
                _live.SetContent(ToastState.Success, "Backup Saved", "taskisland-backup-20261111.db is in your Downloads folder.", "Show file");
                _loop.Interval = TimeSpan.FromMilliseconds(150);
                break;
            case 4:
                _live.Open();
                _loop.Interval = TimeSpan.FromSeconds(2.4);
                break;
            default:
                _live.Close();
                _live.SetContent(ToastState.Success, "Changes Saved", "Saved to the database. Your lists are up to date.", null);
                _loop.Interval = TimeSpan.FromSeconds(1.2);
                break;
        }
    }

    private static SileoToast Make(ToastState state, string title, string? description, string? button, ToastAlign align, bool open)
    {
        var toast = new SileoToast { Align = align };
        toast.SetContent(state, title, description, button, animate: false);
        if (open) toast.Open(animate: false);
        return toast;
    }

    private static FrameworkElement Cell(string label, SileoToast toast) => new StackPanel
    {
        Margin = new Thickness(0, 0, 24, 20),
        Width = SileoToast.ToastWidth,
        Children =
        {
            new TextBlock { Text = label, Foreground = Brushes.White, Opacity = 0.8, FontSize = 12, Margin = new Thickness(0, 0, 0, 8) },
            new Border { Height = 140, Child = toast },
        },
    };

    private static void Snap(FrameworkElement root, string path)
    {
        var source = PresentationSource.FromVisual(root);
        double scale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1;
        double dw = root.ActualWidth, dh = root.ActualHeight;
        int w = (int)Math.Round(dw * scale), h = (int)Math.Round(dh * scale);
        var content = new RenderTargetBitmap(w, h, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        content.Render(root);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(70, 110, 200), Color.FromRgb(30, 55, 140), 60), null,
                new Rect(0, 0, dw, dh));
            dc.DrawImage(content, new Rect(0, 0, dw, dh));
        }
        var bitmap = new RenderTargetBitmap(w, h, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using var f = System.IO.File.Create(path);
        png.Save(f);
    }
}
#endif
