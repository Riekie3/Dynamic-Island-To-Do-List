using Microsoft.Win32;

namespace TaskIsland.Windows;

/// <summary>
/// The one visible persistence point (PLAN.md sections 7.6 and 7.11 rule 5): <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>,
/// value <c>TaskIsland</c>. It shows in Task Manager → Startup apps. Nothing else is ever written to the registry.
/// </summary>
public sealed class StartupRegistration(string keyPath = StartupRegistration.RunKeyPath)
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "TaskIsland";

    public string KeyPath { get; } = keyPath;

    /// <summary>The current value, or null when there is none.</summary>
    public string? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        return key?.GetValue(ValueName) as string;
    }

    public void Write(string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        key.SetValue(ValueName, command, RegistryValueKind.String);
    }

    public void Remove()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
