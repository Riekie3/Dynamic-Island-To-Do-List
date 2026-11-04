using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using TaskIsland.App.Logging;
using TaskIsland.Core.Models;

namespace TaskIsland.App.Services;

/// <summary>
/// Accent colour and row density (PLAN.md section 5). Both are plain resources set on the application, so every
/// DynamicResource user updates at once; nothing is polled. The Windows accent is read from the registry only when the
/// setting is chosen and when Windows tells us its colours changed.
/// </summary>
public static class Appearance
{
    private static readonly string[] AccentKeys = ["AccentBrush", "AccentHoverBrush", "AccentPressedBrush", "AccentSoftBrush", "OnAccentBrush"];

    /// <summary>The user's Windows accent colour, or null when it cannot be read.</summary>
    public static Color? WindowsAccent()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int abgr)
                return Color.FromRgb((byte)(abgr & 0xFF), (byte)((abgr >> 8) & 0xFF), (byte)((abgr >> 16) & 0xFF));
            if (key?.GetValue("ColorizationColor") is int argb)
                return Color.FromRgb((byte)((argb >> 16) & 0xFF), (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));
        }
        catch (Exception ex)
        {
            FileLogger.Warn($"could not read the Windows accent colour: {ex.Message}");
        }
        return null;
    }

    /// <summary>Sets (Windows) or removes (Default) the accent overrides on the application resources.</summary>
    public static void ApplyAccent(ResourceDictionary appResources, AccentSource source)
    {
        foreach (var k in AccentKeys) appResources.Remove(k);
        if (source != AccentSource.Windows || WindowsAccent() is not { } c) return;
        appResources["AccentBrush"] = Frozen(c);
        appResources["AccentHoverBrush"] = Frozen(Shift(c, +0.12));
        appResources["AccentPressedBrush"] = Frozen(Shift(c, -0.12));
        appResources["AccentSoftBrush"] = Frozen(Color.FromArgb(0x33, c.R, c.G, c.B));
        appResources["OnAccentBrush"] = Frozen(Luminance(c) > 0.6 ? Color.FromRgb(0x10, 0x12, 0x16) : Colors.White);
    }

    /// <summary>Comfortable rows are 52 px, Compact rows 38 px.</summary>
    public static void ApplyDensity(ResourceDictionary appResources, DensityMode density) =>
        appResources["RowMinHeight"] = density == DensityMode.Compact ? 38.0 : 52.0;

    private static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;

    private static Color Shift(Color c, double amount)
    {
        byte Ch(byte v) => (byte)Math.Clamp(amount > 0 ? v + (255 - v) * amount : v * (1 + amount), 0, 255);
        return Color.FromRgb(Ch(c.R), Ch(c.G), Ch(c.B));
    }

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
