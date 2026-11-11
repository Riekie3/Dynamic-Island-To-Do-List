using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using TaskIsland.App.Logging;
using TaskIsland.App.Services;
using TaskIsland.App.ViewModels;
using TaskIsland.App.Views;
using TaskIsland.Core.Mascot;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;
using TaskIsland.Data;
using TaskIsland.Windows;
using AppTheme = TaskIsland.Core.Models.ThemeMode;

namespace TaskIsland.App;

/// <summary>
/// Composition root and start-up flow (PLAN.md sections 7.5–7.10): single instance first, then logging and the global
/// exception handlers, the database (with a recovery dialog if it cannot be opened), the Run key, the island window, the
/// tray icon, Windows event wiring and reminders. Exit (tray menu or panel menu) is the only way out.
/// </summary>
public partial class App : Application
{
    private SingleInstance? _instance;
    private ServiceProvider? _services;
    private IslandViewModel? _vm;
    private AppTheme _theme;
    private AppSettings? _liveSettings;
    private DispatcherTimer? _preDeleteCleanup;
    private bool _exiting;

    /// <summary>True when TASKISLAND_ROOT redirects the data folder (tests): separate Run key, mutex and pipe.</summary>
    public static bool TestRoot { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Hidden island windows must never end the process; only Exit does (PLAN.md section 7.5).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        // PLAN.md section 6.7 rule 2: cap animation frames at 60 fps so 120/144 Hz screens don't double the cost.
        Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata { DefaultValue = 60 });
        var options = LaunchOptions.Parse(e.Args);
        // TASKISLAND_ROOT lets tests and smoke runs use a throw-away data folder.
        var root = Environment.GetEnvironmentVariable("TASKISLAND_ROOT");
        bool testRoot = !string.IsNullOrWhiteSpace(root);
        TestRoot = testRoot;
        var paths = testRoot ? new AppPaths(root!) : new AppPaths();

        // 1. Single instance before anything else (section 7.7). A test data folder gets its own instance scope.
        _instance = new SingleInstance(testRoot ? Scope(paths.Root) : "");
        if (!_instance.TryAcquire())
        {
            bool sent = _instance.SignalFirstInstance();
            _instance = null;
            FileLogger.Init(paths.LogsDir);
            FileLogger.Info($"second launch (pid {Environment.ProcessId}): " +
                (sent ? "asked the running TaskIsland to expand" : "the running TaskIsland did not answer") + "; exiting");
            Shutdown(0);
            return;
        }

        FileLogger.Init(paths.LogsDir);
        FileLogger.Info($"start {typeof(App).Assembly.GetName().Version} pid {Environment.ProcessId} args [{string.Join(' ', e.Args)}]");
        HookGlobalExceptionHandlers();
        System.Windows.Forms.Application.EnableVisualStyles(); // the task dialog needs the modern common controls

        try
        {
            var db = new Database(paths);
            var clock = new SystemClock();
            var settings = await OpenDatabaseAsync(db, paths, clock);
            if (settings is null)
            {
                FileLogger.Info("exit chosen in the database dialog");
                ExitApp(1);
                return;
            }
            var store = new SqliteSettingsStore(db);
#if DEBUG
            options.ApplyDebugOverrides(settings);
#endif
            _liveSettings = settings;
            Appearance.ApplyDensity(Resources, settings.Density);
            ApplyTheme(settings.Theme);
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnWindowsPreferenceChanged;
            StartupManager.ApplyOnLaunch(settings, testRoot);

            _services = Build(paths, db, settings, options);
            _vm = _services.GetRequiredService<IslandViewModel>();
            // Load lists and tasks before the window exists, so the first paint already shows the real data.
            await _vm.InitializeAsync();
            var window = _services.GetRequiredService<IslandWindow>();
            window.Show(); // collapsed and never activated (ShowActivated=False), so --startup takes no focus
            var director = _services.GetRequiredService<ReactionDirector>();
#if DEBUG
            // Before Start(), which arms the first pop timer.
            if (options.PopSeconds is { } pop) director.PopIntervalOverride = TimeSpan.FromSeconds(pop);
#endif
            director.Start();

            var tray = _services.GetRequiredService<TrayIconService>();
            tray.EnsureVisible(force: false);
            _services.GetRequiredService<RecoveryCoordinator>().Attach();

            var reminders = _services.GetRequiredService<ReminderService>();
            var vm = _vm;
            reminders.ListName = vm.ListName;
            reminders.Notify += notice =>
            {
                tray.ShowBalloon(notice, () =>
                {
                    vm.ShowList(notice.ListId);
                    window.OpenFromOutside();
                });
                // The widening pill (setting on) takes the notch for 5 s and pulses once it is wide; otherwise the pulse ring
                // and, for "due now", the Surprised face. The early reminder keeps the face unchanged (PLAN.md section 7.9).
                if (!window.ShowReminderAlert(notice))
                {
                    window.PulseReminder();
                    if (!notice.Early) director.Request(Reaction.Surprised);
                }
            };
            vm.DataChanged += reminders.OnDataChanged;
            reminders.Start(vm.AllTasks);

            _instance.StartServer(
                () => Dispatcher.BeginInvoke(() =>
                {
                    FileLogger.Info("second launch: expanding");
                    window.OpenFromOutside();
                }),
                FileLogger.Warn);

            if (!settings.FirstRunCompleted)
            {
                settings.FirstRunCompleted = true;
                await store.SetAsync(AppSettings.Keys.FirstRunCompleted, "true");
                FileLogger.Info("first run completed");
            }
            var backup = _services.GetRequiredService<BackupService>();
            _ = RunDailyBackupAsync(backup, store, settings);
            _preDeleteCleanup = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromHours(1) };
            _preDeleteCleanup.Tick += (_, _) => PruneExpiredPreDeleteBackups(backup);
            _preDeleteCleanup.Start();

            if (!options.Startup && !settings.StartCollapsed) window.OpenFromOutside();
#if DEBUG
            if (string.Equals(options.Demo, "toast", StringComparison.OrdinalIgnoreCase))
            {
                // SILEO-PLAN.md phase S1: the toast gallery in its own window (it snaps itself when --snap is given).
                new Views.ToastDemoWindow(options.Snap, TimeSpan.FromMilliseconds(options.SnapDelayMs)).Show();
            }
            else
            {
                if (options.Demo is { } demo) window.RunDemo(demo);
                if (options.Snap is { } snap) window.SnapAndExit(snap, TimeSpan.FromMilliseconds(options.SnapDelayMs));
            }
            if (options.SettingsPage is { } page)
            {
                var launcher = _services.GetRequiredService<SettingsLauncher>();
                launcher.Show();
                launcher.Window?.SelectPage(page);
                launcher.Window?.DebugToast(options.Demo);
                if (options.SnapSettings is { } snapSettings) launcher.Window?.SnapAndExit(snapSettings, TimeSpan.FromSeconds(1.5));
            }
#endif
            FileLogger.Info("started" + (options.Startup ? " (--startup)" : ""));
        }
        catch (Exception ex)
        {
            FileLogger.Crash("start-up failed", ex);
            MessageBox.Show($"TaskIsland could not start.\n\n{ex.Message}", "TaskIsland",
                MessageBoxButton.OK, MessageBoxImage.Error);
            ExitApp(1);
        }
    }

    /// <summary>
    /// Opens the database. If it is locked or damaged, asks: open the data folder, restore a backup (the broken file is kept
    /// in Backups), or exit. Never deletes anything. Returns null when the user chose Exit.
    /// </summary>
    private static async Task<AppSettings?> OpenDatabaseAsync(Database db, AppPaths paths, IClock clock)
    {
        while (true)
        {
            try
            {
                await db.InitializeAsync();
                return await new SqliteSettingsStore(db).LoadAsync();
            }
            catch (Exception ex)
            {
                FileLogger.Error("the database could not be opened at start-up", ex);
                var choice = StartupFailureDialog.Ask($"{ex.GetType().Name}: {ex.Message}\n\nData folder: {paths.Root}");
                FileLogger.Info($"database dialog: {choice}");
                switch (choice)
                {
                    case StartupFailureChoice.OpenDataFolder:
                        OpenFolder(paths.Root);
                        break;
                    case StartupFailureChoice.RestoreFromBackup:
                        await RestoreFromBackupAsync(db, paths, clock);
                        break;
                    default:
                        return null;
                }
            }
        }
    }

    private static async Task RestoreFromBackupAsync(Database db, AppPaths paths, IClock clock)
    {
        paths.EnsureCreated();
        var dialog = new OpenFileDialog
        {
            Title = "Choose a TaskIsland backup",
            Filter = "TaskIsland backup (*.db)|*.db",
            InitialDirectory = paths.BackupsDir,
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var kept = await new BackupService(db, clock).RestoreBrokenAsync(dialog.FileName);
            FileLogger.Info($"restored {dialog.FileName}; the unreadable file was kept as {kept ?? "(none)"}");
            StartupFailureDialog.Info("Backup restored",
                kept is null ? "TaskIsland will now start with the backup."
                    : "TaskIsland will now start with the backup. The file that could not be opened was kept as:\n" + kept,
                error: false);
        }
        catch (Exception ex)
        {
            FileLogger.Error("restore at start-up failed", ex);
            StartupFailureDialog.Info("Couldn't restore that backup", ex.Message, error: true);
        }
    }

    /// <summary>Explorer on our own data folder, only after a user click (PLAN.md section 7.11 rule 3).</summary>
    internal static void OpenFolder(string folder)
    {
        try
        {
            System.IO.Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + folder + "\"")
            {
                UseShellExecute = false,
            });
        }
        catch (Exception ex)
        {
            FileLogger.Error("could not open the data folder", ex);
        }
    }

    private static async Task RunDailyBackupAsync(BackupService backup, ISettingsStore store, AppSettings settings)
    {
        try
        {
            if (await backup.RunDailyBackupIfDueAsync(store) is { } path)
            {
                settings.LastBackupAt = DateTime.UtcNow;
                FileLogger.Info($"daily backup written: {System.IO.Path.GetFileName(path)}");
            }
        }
        catch (Exception ex)
        {
            FileLogger.Error("daily backup failed", ex);
        }
    }

    /// <summary>Hourly while the app runs; start-up and wake already prune through the daily-backup check.</summary>
    private static void PruneExpiredPreDeleteBackups(BackupService backup)
    {
        try
        {
            int removed = backup.PruneExpiredPreDeleteBackups();
            if (removed > 0) FileLogger.Info($"removed {removed} pre-delete backup(s) older than 24 hours");
        }
        catch (Exception ex)
        {
            FileLogger.Error("could not remove old pre-delete backups", ex);
        }
    }

    /// <summary>PLAN.md section 7.10: log everything; keep running after a UI exception where it is safe to.</summary>
    private void HookGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            FileLogger.Crash("unhandled exception on the UI thread", e.Exception);
            if (e.Exception is OutOfMemoryException or InvalidProgramException) return;
            e.Handled = true;
            try { _vm?.ReportUnexpectedError(); }
            catch (Exception ex) { FileLogger.Error("could not show the error notice", ex); }
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            FileLogger.Crash($"unhandled exception (terminating: {e.IsTerminating})", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            FileLogger.Crash("unobserved task exception", e.Exception);
            e.SetObserved();
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _preDeleteCleanup?.Stop();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnWindowsPreferenceChanged;
        try { _services?.Dispose(); }
        catch (Exception ex) { FileLogger.Error("dispose at exit failed", ex); }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        _instance?.Dispose();
        FileLogger.Info($"exited (code {e.ApplicationExitCode})");
        base.OnExit(e);
    }

    /// <summary>
    /// The only path that ends the process (PLAN.md section 7.5): remove the tray icon at once (no ghost icon), unhook
    /// Windows events and the hotkey, close the window, release the database pools, shut down.
    /// </summary>
    public void ExitApp(int exitCode = 0)
    {
        if (_exiting) return;
        _exiting = true;
        FileLogger.Info("exit requested");
        try { _services?.GetService<SettingsLauncher>()?.Close(); }
        catch (Exception ex) { FileLogger.Error("settings close failed", ex); }
        try { _services?.GetService<TrayIconService>()?.Dispose(); }
        catch (Exception ex) { FileLogger.Error("tray dispose failed", ex); }
        try { _services?.GetService<RecoveryCoordinator>()?.Dispose(); }
        catch (Exception ex) { FileLogger.Error("unhooking failed", ex); }
        if (_services?.GetService<IslandWindow>() is { } w)
        {
            w.AllowClose();
            w.Close();
        }
        Shutdown(exitCode);
    }

    /// <summary>A stable short name for a test data folder (FNV-1a), so its instance never meets the real app.</summary>
    private static string Scope(string root)
    {
        uint h = 2166136261;
        foreach (char c in root.ToUpperInvariant()) { h ^= c; h *= 16777619; }
        return h.ToString("x8");
    }

    /// <summary>Opens (or brings forward) the Settings window.</summary>
    public void OpenSettings() => _services?.GetService<SettingsLauncher>()?.Show();

    /// <summary>Swaps the theme dictionary (index 0). Every theme brush is a DynamicResource, so it applies live.</summary>
    public void ApplyTheme(AppTheme mode)
    {
        _theme = mode;
        bool light = mode == AppTheme.Light || (mode == AppTheme.System && WindowsUsesLightTheme());
        var dict = new ResourceDictionary { Source = new Uri(light ? "Themes/Light.xaml" : "Themes/Dark.xaml", UriKind.Relative) };
        Resources.MergedDictionaries[0] = dict;
        IsLightTheme = light;
        FileLogger.Info($"theme applied: {(light ? "Light" : "Dark")} (setting {mode}), accent {_liveSettings?.AccentSource ?? AccentSource.Default}");
        Appearance.ApplyAccent(Resources, _liveSettings?.AccentSource ?? AccentSource.Default);
        ThemeChanged?.Invoke(light);
    }

    /// <summary>The light theme is in use (also resolves "System").</summary>
    public bool IsLightTheme { get; private set; }

    /// <summary>Raised after the theme dictionary was swapped; the argument is true for the light theme.</summary>
    public event Action<bool>? ThemeChanged;

    /// <summary>Accent source or density changed in Settings.</summary>
    public void ApplyAppearance()
    {
        if (_liveSettings is not { } s) return;
        Appearance.ApplyDensity(Resources, s.Density);
        Appearance.ApplyAccent(Resources, s.AccentSource);
        FileLogger.Info($"appearance applied: density {s.Density}, accent {s.AccentSource}");
    }

    /// <summary>Windows changed its theme or accent: follow when the setting says "System" or "Windows accent".</summary>
    private void OnWindowsPreferenceChanged(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (Microsoft.Win32.UserPreferenceCategory.General or Microsoft.Win32.UserPreferenceCategory.VisualStyle)) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (_liveSettings is null || _exiting) return;
            if (_liveSettings.Theme == AppTheme.System || _liveSettings.AccentSource == AccentSource.Windows)
            {
                FileLogger.Info("Windows theme or accent changed: re-applying");
                ApplyTheme(_liveSettings.Theme);
            }
        });
    }

    public void ToggleTheme()
    {
        bool lightNow = _theme == AppTheme.Light || (_theme == AppTheme.System && WindowsUsesLightTheme());
        ApplyTheme(lightNow ? AppTheme.Dark : AppTheme.Light);
    }

    /// <summary>Windows "app mode" (read once here; live following arrives in Phase 5).</summary>
    private static bool WindowsUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v != 0;
        }
        catch
        {
            return false;
        }
    }

    public static ServiceProvider Build(AppPaths paths, Database db, AppSettings settings, LaunchOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddSingleton(db);
        services.AddSingleton(settings);
        services.AddSingleton(options);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITaskRepository, SqliteTaskRepository>();
        services.AddSingleton<ITodoListRepository, SqliteTodoListRepository>();
        services.AddSingleton<ISettingsStore, SqliteSettingsStore>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<DataMaintenanceService>();
        services.AddSingleton<ImportService>();
        services.AddSingleton(_ => new MotionPolicy(settings.AnimationsEnabled, options.ForceReducedMotion)
        {
            FollowWindowsAnimations = settings.FollowWindowsAnimations && !options.FullMotion,
        });
        services.AddSingleton<EfficiencyMode>();
        services.AddSingleton<IslandPositioner>();
        services.AddSingleton(sp => new ReactionDirector(settings, sp.GetRequiredService<MotionPolicy>(), options.Startup));
        services.AddSingleton<IslandViewModel>();
        services.AddSingleton<IslandWindow>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<ReminderService>();
        services.AddSingleton<RecoveryCoordinator>();
        services.AddSingleton<SettingsApplier>();
        services.AddSingleton<SettingsLauncher>();
        return services.BuildServiceProvider();
    }
}

/// <summary>Command-line options. <c>--startup</c> is set by the Run key; the rest exist in Debug builds only.</summary>
public sealed class LaunchOptions
{
    public bool Startup { get; private set; }
    public bool ForceReducedMotion { get; private set; }
    /// <summary>Debug: ignore Windows "Animation effects" off (to test animations on a PC that has them off).</summary>
    public bool FullMotion { get; private set; }
    public string? Demo { get; private set; }
    public string? Theme { get; private set; }
    public bool NoMascot { get; private set; }
    public int? PopSeconds { get; private set; }
    public string? Filter { get; private set; }
    public string? Snap { get; private set; }
    /// <summary>Debug: milliseconds before --snap saves the PNG (default 2200).</summary>
    public int SnapDelayMs { get; private set; } = 2200;
    public string? Companion { get; private set; }
    /// <summary>Debug: open the Settings window at start (optionally on a page: general, appearance, behaviour, lists, data, about).</summary>
    public string? SettingsPage { get; private set; }
    public string? SnapSettings { get; private set; }

    public static LaunchOptions Parse(string[] args)
    {
        var o = new LaunchOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
#if DEBUG
            string? Next() => i + 1 < args.Length ? args[++i] : null;
#endif
            if (a.Equals("--startup", StringComparison.OrdinalIgnoreCase)) o.Startup = true;
#if DEBUG
            else if (a == "--reduced-motion") o.ForceReducedMotion = true;
            else if (a == "--full-motion") o.FullMotion = true;
            else if (a == "--demo") o.Demo = Next();
            else if (a == "--theme") o.Theme = Next();
            else if (a == "--no-mascot") o.NoMascot = true;
            else if (a == "--filter") o.Filter = Next();
            else if (a == "--snap") o.Snap = Next();
            else if (a == "--snap-delay" && int.TryParse(Next(), out var sd)) o.SnapDelayMs = sd;
            else if (a == "--companion") o.Companion = Next();
            else if (a == "--settings") o.SettingsPage = Next();
            else if (a == "--snap-settings") o.SnapSettings = Next();
            else if (a == "--pop-seconds" && int.TryParse(Next(), out var s)) o.PopSeconds = s;
#endif
        }
        return o;
    }

#if DEBUG
    public void ApplyDebugOverrides(AppSettings s)
    {
        if (Enum.TryParse<AppTheme>(Theme, true, out var t)) s.Theme = t;
        if (NoMascot) s.MascotEnabled = false;
        if (Enum.TryParse<TaskFilter>(Filter, true, out var f)) s.CurrentFilter = f;
        if (Enum.TryParse<CompanionLevel>(Companion, true, out var c)) s.Companion = c;
    }
#endif
}
