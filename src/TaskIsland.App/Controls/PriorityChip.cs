using System.Windows;
using System.Windows.Controls;
using TaskIsland.Core.Models;

namespace TaskIsland.App.Controls;

/// <summary>Dot + label chip for a priority. Look is in Themes/Controls.xaml.</summary>
public sealed class PriorityChip : Control
{
    public static readonly DependencyProperty PriorityProperty = DependencyProperty.Register(
        nameof(Priority), typeof(Priority), typeof(PriorityChip), new PropertyMetadata(Priority.Medium));

    public Priority Priority
    {
        get => (Priority)GetValue(PriorityProperty);
        set => SetValue(PriorityProperty, value);
    }
}
