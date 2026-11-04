using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using TaskIsland.App.Logging;
using TaskIsland.App.ViewModels;
using TaskIsland.App.Views;

namespace TaskIsland.App.Services;

/// <summary>
/// Opens the one Settings window (PLAN.md section 6.6). If it is already open it is brought to the front. The window and its
/// view model are created on demand and dropped on close, then a collection hands the memory back (section 6.7).
/// </summary>
public sealed class SettingsLauncher(IServiceProvider services)
{
    private SettingsWindow? _window;

    public bool IsOpen => _window is not null;
    internal SettingsWindow? Window => _window;

    public void Show()
    {
        try
        {
            if (_window is not null)
            {
                if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
                _window.Activate();
                return;
            }
            var vm = ActivatorUtilities.CreateInstance<SettingsViewModel>(services);
            var window = new SettingsWindow(vm, services.GetRequiredService<SettingsApplier>());
            window.Closed += (_, _) =>
            {
                _window = null;
                // Let go of the window, its styles and bindings, then collect once while nothing else is happening.
                Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                });
            };
            _window = window;
            window.Show();
            window.Activate();
        }
        catch (Exception ex)
        {
            FileLogger.Error("could not open the settings window", ex);
            _window = null;
        }
    }

    /// <summary>Closes the window if open (Exit).</summary>
    public void Close() => _window?.Close();
}
