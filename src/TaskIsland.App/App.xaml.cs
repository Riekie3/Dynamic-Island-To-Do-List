using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using TaskIsland.App.Logging;
using TaskIsland.App.Services;
using TaskIsland.App.ViewModels;
using TaskIsland.App.Views;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;
using TaskIsland.Data;
using AppTheme = TaskIsland.Core.Models.ThemeMode;

namespace TaskIsland.App;

/// <summary>Composition root: database, settings, the island window. Exit (panel menu) is the only way out.</summary>
public partial class App : Application
{
    private ServiceProvider? _services;
    private AppTheme _theme;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // PLAN.md section 6.7 rule 2: cap animation frames at 60 fps so 120/144 Hz screens don't double the cost.
        Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata { DefaultValue = 60 });
        try
        {
            var options = LaunchOptions.Parse(e.Args);
            // TASKISLAND_ROOT lets tests and smoke runs use a throw-away data folder.
            var root = Environment.GetEnvironmentVariable("TASKISLAND_ROOT");
            var paths = string.IsNullOrWhiteSpace(root) ? new AppPaths() : new AppPaths(root);

            FileLogger.Init(paths.LogsDir);
            var db = new Database(paths);
            await db.InitializeAsync();
            var settings = await new SqliteSettingsStore(db).LoadAsync();
#if DEBUG
            options.ApplyDebugOverrides(settings);
#endif
            ApplyTheme(settings.Theme);

            _services = Build(paths, db, settings, options);
            // Load lists and tasks before the window exists, so the first paint already shows the real data.
            await _services.GetRequiredService<IslandViewModel>().InitializeAsync();
            var window = _services.GetRequiredService<IslandWindow>();
            window.Show();
#if DEBUG
            // Before Start(), which arms the first pop timer.
            if (options.PopSeconds is { } pop)
                _services.GetRequiredService<ReactionDirector>().PopIntervalOverride = TimeSpan.FromSeconds(pop);
#endif
            _services.GetRequiredService<ReactionDirector>().Start();
#if DEBUG
            if (options.Demo is { } demo) window.RunDemo(demo);
            if (options.Snap is { } snap) window.SnapAndExit(snap, TimeSpan.FromSeconds(2.2));
#endif
        }
        catch (Exception ex)
        {
            MessageBox.Show($"TaskIsland could not start.\n\n{ex.Message}", "TaskIsland",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    /// <summary>The only path that ends the process (the tray menu joins it in Phase 4).</summary>
    public void ExitApp()
    {
        if (_services?.GetService<IslandWindow>() is { } w)
        {
            w.AllowClose();
            w.Close();
        }
        Shutdown();
    }

    /// <summary>Swaps the theme dictionary (index 0). Every theme brush is a DynamicResource, so it applies live.</summary>
    public void ApplyTheme(AppTheme mode)
    {
        _theme = mode;
        bool light = mode == AppTheme.Light || (mode == AppTheme.System && WindowsUsesLightTheme());
        var dict = new ResourceDictionary { Source = new Uri(light ? "Themes/Light.xaml" : "Themes/Dark.xaml", UriKind.Relative) };
        Resources.MergedDictionaries[0] = dict;
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
        return services.BuildServiceProvider();
    }
}

/// <summary>Command-line options. <c>--startup</c> is set by the Run key (Phase 4); the rest exist in Debug builds only.</summary>
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
    public string? Companion { get; private set; }

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
            else if (a == "--companion") o.Companion = Next();
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
