using System.Globalization;

namespace TaskIsland.Core.Services;

/// <summary>Modifier flags with the same values as Win32 <c>MOD_*</c>.</summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}

/// <summary>
/// A global shortcut such as "Ctrl+Alt+T" (PLAN.md section 7.8). <see cref="VirtualKey"/> is the Win32 virtual-key code.
/// Supported keys: A–Z, 0–9, F1–F24, Space. Parsing ignores case and spaces; formatting is canonical
/// ("Ctrl+Alt+Shift+Win+Key").
/// </summary>
public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, uint VirtualKey)
{
    public static readonly HotkeyGesture Default = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x54); // Ctrl+Alt+T

    public int ModifierCount => System.Numerics.BitOperations.PopCount((uint)Modifiers);

    /// <summary>When the user rebinds, at least two modifiers are required to avoid clashing with app shortcuts.</summary>
    public bool IsAllowedForRebind => ModifierCount >= 2 && VirtualKey != 0;

    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        var mods = HotkeyModifiers.None;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var m = parts[i].ToLowerInvariant() switch
            {
                "ctrl" or "control" => HotkeyModifiers.Control,
                "alt" => HotkeyModifiers.Alt,
                "shift" => HotkeyModifiers.Shift,
                "win" or "windows" => HotkeyModifiers.Win,
                _ => HotkeyModifiers.None,
            };
            if (m == HotkeyModifiers.None || mods.HasFlag(m)) return false;
            mods |= m;
        }
        if (!TryParseKey(parts[^1], out uint vk)) return false;
        gesture = new HotkeyGesture(mods, vk);
        return true;
    }

    /// <summary>Parses the setting, falling back to Ctrl+Alt+T when it is missing or invalid.</summary>
    public static HotkeyGesture ParseOrDefault(string? text) => TryParse(text, out var g) ? g : Default;

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(KeyName(VirtualKey));
        return string.Join("+", parts);
    }

    private static bool TryParseKey(string s, out uint vk)
    {
        vk = 0;
        if (s.Length == 1)
        {
            char c = char.ToUpperInvariant(s[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9') { vk = c; return true; }
            return false;
        }
        if (s.Equals("Space", StringComparison.OrdinalIgnoreCase)) { vk = 0x20; return true; }
        if ((s[0] == 'F' || s[0] == 'f') && int.TryParse(s.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int n)
            && n is >= 1 and <= 24)
        {
            vk = (uint)(0x70 + n - 1);
            return true;
        }
        return false;
    }

    private static string KeyName(uint vk) => vk switch
    {
        >= 'A' and <= 'Z' or >= '0' and <= '9' => ((char)vk).ToString(),
        0x20 => "Space",
        >= 0x70 and <= 0x87 => "F" + (vk - 0x70 + 1).ToString(CultureInfo.InvariantCulture),
        _ => "0x" + vk.ToString("X2", CultureInfo.InvariantCulture),
    };
}
