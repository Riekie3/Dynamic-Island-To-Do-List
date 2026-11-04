using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskIsland.Core.Models;

namespace TaskIsland.App.ViewModels;

/// <summary>One chip in the list switcher: All (Id 0) or a list, with its outstanding count.</summary>
public sealed partial class ListChipViewModel : ObservableObject
{
    public ListChipViewModel(long id, string name, Color color)
    {
        Id = id;
        _name = name;
        ApplyColor(color);
    }

    public long Id { get; }
    public bool IsAll => Id == 0;

    [ObservableProperty] private string _name;
    [ObservableProperty] private Brush _colorBrush = Brushes.Transparent;
    [ObservableProperty] private Brush _softBrush = Brushes.Transparent;
    [ObservableProperty] private string _colorHex = "";
    [ObservableProperty] private int _count;
    [ObservableProperty] private bool _isSelected;

    public void Update(TodoList list)
    {
        Name = list.Name;
        ApplyColor(BrushCache.Parse(list.ColorHex));
        ColorHex = list.ColorHex;
    }

    private void ApplyColor(Color color)
    {
        ColorBrush = BrushCache.Frozen(color, 1.0);
        SoftBrush = BrushCache.Frozen(color, 0.20);
    }
}

/// <summary>A list offered as a choice (inline edit's List field, delete-list target).</summary>
public sealed partial class ListChoiceViewModel(long id, string name, Brush brush) : ObservableObject
{
    public long Id { get; } = id;
    public string Name { get; } = name;
    public Brush Brush { get; } = brush;

    [ObservableProperty] private bool _isSelected;
}

/// <summary>One of the 8 palette colours in the list editor.</summary>
public sealed partial class SwatchViewModel(string hex) : ObservableObject
{
    public string Hex { get; } = hex;
    public Brush Brush { get; } = BrushCache.Frozen(BrushCache.Parse(hex), 1.0);

    [ObservableProperty] private bool _isSelected;
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
