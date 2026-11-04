using TaskIsland.App.Logging;
using TaskIsland.Core.Models;
using TaskIsland.Core.Services;
using TaskIsland.Windows;

namespace TaskIsland.App.Services;

/// <summary>
/// Launch-at-startup (PLAN.md section 7.6): applies <see cref="StartupPolicy"/> to the Run value. A test data folder
/// (TASKISLAND_ROOT) uses a separate test key unless TASKISLAND_RUNKEY names one, so test runs never register a
/// development build to start with Windows.
/// </summary>
public static class StartupManager
{
    public const string TestKeyPath = @"Software\TaskIsland-Test\Run";

    public static StartupRegistration Registration(bool testRoot)
    {
        var overrideKey = Environment.GetEnvironmentVariable("TASKISLAND_RUNKEY");
        if (!string.IsNullOrWhiteSpace(overrideKey)) return new StartupRegistration(overrideKey.Trim());
        return new StartupRegistration(testRoot ? TestKeyPath : StartupRegistration.RunKeyPath);
    }

    /// <summary>At every launch: first run writes the value; later launches only fix an old path or remove it when off.</summary>
    public static void ApplyOnLaunch(AppSettings settings, bool testRoot)
    {
        try
        {
            var reg = Registration(testRoot);
            string expected = StartupPolicy.CommandFor(ExePath());
            var existing = reg.Read();
            var action = StartupPolicy.OnLaunch(settings.LaunchAtStartup, !settings.FirstRunCompleted, existing, expected);
            Apply(reg, action, expected, "launch");
            if (action == StartupAction.None)
                FileLogger.Info($"startup entry unchanged ({(existing is null ? "none" : "present")}, setting {(settings.LaunchAtStartup ? "on" : "off")}) in HKCU\\{reg.KeyPath}");
        }
        catch (Exception ex)
        {
            FileLogger.Error("could not check the startup entry", ex);
        }
    }

    /// <summary>The Settings toggle (Phase 5): on writes or fixes the value, off removes it.</summary>
    public static void ApplyToggle(bool on, bool testRoot)
    {
        var reg = Registration(testRoot);
        string expected = StartupPolicy.CommandFor(ExePath());
        Apply(reg, StartupPolicy.OnToggle(on, reg.Read(), expected), expected, "toggle");
    }

    private static void Apply(StartupRegistration reg, StartupAction action, string expected, string why)
    {
        switch (action)
        {
            case StartupAction.Write:
                reg.Write(expected);
                FileLogger.Info($"startup entry written ({why}): HKCU\\{reg.KeyPath}\\{StartupRegistration.ValueName} = {expected}");
                break;
            case StartupAction.Remove:
                reg.Remove();
                FileLogger.Info($"startup entry removed ({why}) from HKCU\\{reg.KeyPath}");
                break;
        }
    }

    private static string ExePath() =>
        Environment.ProcessPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "TaskIsland.exe");
}
