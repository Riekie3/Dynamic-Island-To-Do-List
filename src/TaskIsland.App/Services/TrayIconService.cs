using System.ComponentModel;
using Microsoft.Win32;
using TaskIsland.App.Logging;
using TaskIsland.App.ViewModels;
using TaskIsland.App.Views;
using TaskIsland.Core.Services;

namespace TaskIsland.App.Services;

/// <summary>
/// The tray icon (PLAN.md section 7.5): always present while TaskIsland runs, so the user can always see and exit it
/// (section 7.11 rule 10). Left-click toggles the panel, double-click opens it, right-click shows the menu. Reminders
/// use its balloon (Windows 11 shows it as a notification). WinForms is used here only.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly IslandViewModel _vm;
    private readonly IslandWindow _window;
    private readonly System.Windows.Forms.NotifyIcon _icon;
    private readonly System.Windows.Forms.ContextMenuStrip _menu;
    private readonly System.Windows.Forms.ToolStripMenuItem _open, _expand, _show, _pause, _exit;
    private System.Drawing.Icon? _current;
    private bool _lightTaskbar, _iconPaused, _disposed;
    private Action? _balloonClick;

    public TrayIconService(IslandViewModel vm, IslandWindow window)
    {
        _vm = vm;
        _window = window;
        _menu = new System.Windows.Forms.ContextMenuStrip { ShowImageMargin = false };
        _open = Item("Open TaskIsland", () => _window.OpenFromOutside());
        _open.Font = new System.Drawing.Font(_open.Font, System.Drawing.FontStyle.Bold);
        _expand = Item("Expand", () => { if (_window.IsExpanded) _window.Collapse(); else _window.OpenFromOutside(); });
        _show = Item("Hide", () => _window.SetUserHidden(!_window.IsUserHidden));
        _pause = Item("Pause visibility", () => _window.SetPaused(!_window.IsPaused));
        _exit = Item("Exit", () => ((App)System.Windows.Application.Current).ExitApp());
        _menu.Items.AddRange([_open, _expand, _show, _pause, new System.Windows.Forms.ToolStripSeparator(), _exit]);
        _menu.Opening += (_, _) => UpdateMenu();

        _icon = new System.Windows.Forms.NotifyIcon { ContextMenuStrip = _menu };
        _icon.MouseClick += OnMouseClick;
        _icon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left) Guard("tray double-click", _window.OpenFromOutside);
        };
        // Windows 11 moves the notification to the notification centre after a few seconds ("closed"), where it can still
        // be clicked, so the action stays until the next reminder replaces it.
        _icon.BalloonTipClicked += (_, _) =>
        {
            FileLogger.Info("balloon clicked");
            if (_balloonClick is { } a) Guard("reminder click", a);
        };

        _vm.PropertyChanged += OnVmChanged;
        _window.VisibilityStateChanged += UpdateLook;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <summary>
    /// Puts the icon in the tray. <paramref name="force"/> removes and re-adds it: after an Explorer restart the new shell
    /// has forgotten every icon.
    /// </summary>
    public void EnsureVisible(bool force)
    {
        if (_disposed) return;
        UpdateLook();
        if (force && _icon.Visible) _icon.Visible = false;
        if (_icon.Visible) return;
        _icon.Visible = true;
        FileLogger.Info(force ? "tray icon added again" : "tray icon shown");
    }

    /// <summary>Reminder balloon. Clicking it runs <paramref name="onClick"/> (opens the task's list).</summary>
    public void ShowBalloon(ReminderNotice notice, Action onClick)
    {
        if (_disposed) return;
        _balloonClick = onClick;
        FileLogger.Info($"balloon: {notice.Title} / {notice.Text}");
        _icon.ShowBalloonTip(10_000, notice.Title, notice.Text, System.Windows.Forms.ToolTipIcon.None);
    }

    private System.Windows.Forms.ToolStripMenuItem Item(string text, Action action)
    {
        var item = new System.Windows.Forms.ToolStripMenuItem(text);
        item.Click += (_, _) => Guard(text, action);
        return item;
    }

    private void OnMouseClick(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        if (e.Button != System.Windows.Forms.MouseButtons.Left) return;
        Guard("tray click", _window.ToggleFromTray);
    }

    private void UpdateMenu()
    {
        _expand.Text = _window.IsExpanded ? "Collapse" : "Expand";
        _show.Text = _window.IsUserHidden ? "Show" : "Hide";
        _show.Enabled = !_window.IsPaused;
        _pause.Text = _window.IsPaused ? "Resume visibility" : "Pause visibility";
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IslandViewModel.PillText)) UpdateTooltip();
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle)
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(UpdateLook);
    }

    /// <summary>Icon colour follows the taskbar (Windows "system" theme); dimmed while paused.</summary>
    private void UpdateLook()
    {
        if (_disposed) return;
        bool light = TaskbarIsLight();
        bool paused = _window.IsPaused;
        if (_current is null || light != _lightTaskbar || paused != _iconPaused)
        {
            _lightTaskbar = light;
            _iconPaused = paused;
            var old = _current;
            _current = TrayIconArt.Create(light, paused, System.Windows.Forms.SystemInformation.SmallIconSize.Width);
            _icon.Icon = _current;
            old?.Dispose();
        }
        UpdateTooltip();
    }

    private void UpdateTooltip()
    {
        if (_disposed) return;
        string text = _window.IsPaused ? "TaskIsland — Paused"
            : _vm.AllDone ? "TaskIsland — all done"
            : $"TaskIsland — {_vm.PillText} left";
        _icon.Text = text.Length > 127 ? text[..127] : text;
    }

    private static bool TaskbarIsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int v && v != 0;
        }
        catch
        {
            return false;
        }
    }

    private static void Guard(string what, Action action)
    {
        try { action(); }
        catch (Exception ex) { FileLogger.Error($"tray: {what} failed", ex); }
    }

    /// <summary>Exit path: the icon is removed from the tray at once, so no ghost icon stays behind.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _vm.PropertyChanged -= OnVmChanged;
        _window.VisibilityStateChanged -= UpdateLook;
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _current?.Dispose();
    }
}
