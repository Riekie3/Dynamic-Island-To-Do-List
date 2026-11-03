using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskIsland.App.ViewModels;

/// <summary>One chip in the list switcher: All (Id 0) or a list, with its outstanding count.</summary>
public sealed partial class ListChipViewModel : ObservableObject
{
    public ListChipViewModel(long id, string name, Color color)
    {
        Id = id;
        Name = name;
        ColorBrush = BrushCache.Frozen(color, 1.0);
        SoftBrush = BrushCache.Frozen(color, 0.20);
    }

    public long Id { get; }
    public string Name { get; }
    public Brush ColorBrush { get; }
    public Brush SoftBrush { get; }
    public bool IsAll => Id == 0;

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Frozen brush helpers for colours that come from data (list colours).</summary>
public static class BrushCache
{
    private static readonly Dictionary<(Color, double), Brush> Cache = new();

    public static Brush Frozen(Color color, double opacity)
    {
        if (Cache.TryGetValue((color, opacity), out var b)) return b;
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(color.A * opacity), color.R, color.G, color.B));
        brush.Freeze();
        Cache[(color, opacity)] = brush;
        return brush;
    }

    public static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);
}
