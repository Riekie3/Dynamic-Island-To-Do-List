using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TaskIsland.App.ViewModels;
using TaskIsland.Core.Services;
using TaskIsland.Data;

namespace TaskIsland.App;

/// <summary>Composition root. Phase 1 only proves the wiring: DI container, database, one blank window.</summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            // TASKISLAND_ROOT lets tests and smoke runs use a throw-away data folder.
            var root = Environment.GetEnvironmentVariable("TASKISLAND_ROOT");
            var paths = string.IsNullOrWhiteSpace(root) ? new AppPaths() : new AppPaths(root);

            _services = Build(paths);
            await _services.GetRequiredService<Database>().InitializeAsync();

            var window = new MainWindow { DataContext = _services.GetRequiredService<MainViewModel>() };
            window.Show();
            await _services.GetRequiredService<MainViewModel>().RefreshAsync();
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

    public static ServiceProvider Build(AppPaths paths)
    {
        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<Database>();
        services.AddSingleton<ITaskRepository, SqliteTaskRepository>();
        services.AddSingleton<ITodoListRepository, SqliteTodoListRepository>();
        services.AddSingleton<ISettingsStore, SqliteSettingsStore>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<ImportService>();
        services.AddSingleton<MainViewModel>();
        return services.BuildServiceProvider();
    }
}
