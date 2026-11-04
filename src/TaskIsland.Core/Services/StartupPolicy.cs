namespace TaskIsland.Core.Services;

public enum StartupAction { None, Write, Remove }

/// <summary>
/// What to do with the <c>HKCU\…\Run</c> value (PLAN.md sections 7.6 and 7.11 rule 5). The setting is the source of
/// truth, but a value the user removed elsewhere (Task Manager, another tool) is never re-added silently: only the first
/// run writes a missing value; later launches only fix a value that points at an old path.
/// </summary>
public static class StartupPolicy
{
    /// <param name="settingOn">The LaunchAtStartup setting.</param>
    /// <param name="firstRun">FirstRunCompleted is still false.</param>
    /// <param name="existingValue">The current Run value, or null when there is none.</param>
    /// <param name="expectedValue">The value we would write: <c>"&lt;exe&gt;" --startup</c>.</param>
    public static StartupAction OnLaunch(bool settingOn, bool firstRun, string? existingValue, string expectedValue)
    {
        if (!settingOn) return existingValue is null ? StartupAction.None : StartupAction.Remove;
        if (existingValue is null) return firstRun ? StartupAction.Write : StartupAction.None;
        return Same(existingValue, expectedValue) ? StartupAction.None : StartupAction.Write;
    }

    /// <summary>The user flipped the toggle: on writes (or fixes) the value, off removes it.</summary>
    public static StartupAction OnToggle(bool settingOn, string? existingValue, string expectedValue) =>
        settingOn
            ? existingValue is not null && Same(existingValue, expectedValue) ? StartupAction.None : StartupAction.Write
            : existingValue is null ? StartupAction.None : StartupAction.Remove;

    public static string CommandFor(string exePath) => $"\"{exePath}\" --startup";

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
