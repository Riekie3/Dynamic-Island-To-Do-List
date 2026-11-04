using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using TaskIsland.App.ViewModels;
using TaskIsland.Core.Models;
using TaskIsland.Windows;

namespace TaskIsland.App.Views;

public enum DialogButtonKind { Normal, Primary, Danger }

public sealed record DialogButton(string Text, DialogButtonKind Kind = DialogButtonKind.Normal, bool IsCancel = false);

/// <summary>
/// The small themed dialogs of the Settings window (confirm, choose, list editor, delete list). Built in code so they cost
/// nothing until shown; they use the same styles as the island cards. Modal over the Settings window.
/// </summary>
public sealed class DialogWindow : Window
{
    private readonly Func<int, bool>? _canClose;

    public int ResultIndex { get; private set; } = -1;

    public DialogWindow(Window? owner, string title, string? message, UIElement? body, DialogButton[] buttons,
        Func<int, bool>? canClose = null)
    {
        _canClose = canClose;
        Title = title;
        Owner = owner;
        WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.Height;
        Width = 440;
        UseLayoutRounding = true;
        FontFamily = (FontFamily)Application.Current.FindResource("TextFont");
        SetResourceReference(BackgroundProperty, "SurfaceBrush");

        var root = new StackPanel { Margin = new Thickness(22, 20, 22, 18) };
        root.Children.Add(new TextBlock
        {
            Text = title, FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap,
            FontFamily = (FontFamily)Application.Current.FindResource("DisplayFont"),
        }.Themed(TextBlock.ForegroundProperty, "TextPrimaryBrush"));
        if (!string.IsNullOrEmpty(message))
        {
            root.Children.Add(new TextBlock { Text = message, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) }
                .Themed(TextBlock.ForegroundProperty, "TextSecondaryBrush"));
        }
        if (body is not null)
        {
            if (body is FrameworkElement fe) fe.Margin = new Thickness(0, 14, 0, 0);
            root.Children.Add(body);
        }

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            var b = buttons[i];
            var btn = new Button
            {
                Content = b.Text,
                Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0),
                IsCancel = b.IsCancel,
                IsDefault = b.Kind == DialogButtonKind.Primary,
                Style = (Style)Application.Current.FindResource(b.Kind switch
                {
                    DialogButtonKind.Primary => "PrimaryTextButton",
                    DialogButtonKind.Danger => "DangerTextButton",
                    _ => "TextButton",
                }),
            };
            btn.Click += (_, _) =>
            {
                if (_canClose is not null && !b.IsCancel && !_canClose(index)) return;
                ResultIndex = index;
                DialogResult = true;
            };
            row.Children.Add(btn);
        }
        root.Children.Add(row);
        Content = root;

        SourceInitialized += (_, _) =>
            Dwm.SetDarkTitleBar(new WindowInteropHelper(this).Handle, !((App)Application.Current).IsLightTheme);
    }

    // ---- ready-made dialogs ----

    /// <summary>OK / Cancel style question. True when the confirm button was pressed.</summary>
    public static bool Confirm(Window? owner, string title, string message, string confirmText, bool destructive = false)
    {
        var d = new DialogWindow(owner, title, message, null,
        [
            new DialogButton("Cancel", IsCancel: true),
            new DialogButton(confirmText, destructive ? DialogButtonKind.Danger : DialogButtonKind.Primary),
        ]);
        d.ShowDialog();
        return d.ResultIndex == 1;
    }

    /// <summary>Information with one OK button.</summary>
    public static void Info(Window? owner, string title, string message) =>
        new DialogWindow(owner, title, message, null, [new DialogButton("OK", DialogButtonKind.Primary, IsCancel: true)]).ShowDialog();

    /// <summary>Several choices plus Cancel (always the last button). Returns the chosen index, or -1 for Cancel.</summary>
    public static int Choose(Window? owner, string title, string message, params DialogButton[] choices)
    {
        var all = choices.Append(new DialogButton("Cancel", IsCancel: true)).ToArray();
        var d = new DialogWindow(owner, title, message, null, all);
        d.ShowDialog();
        return d.ResultIndex == all.Length - 1 ? -1 : d.ResultIndex;
    }

    /// <summary>Name and colour of a list. <paramref name="validate"/> returns an error message or null.</summary>
    public static (string Name, string Color)? EditList(Window? owner, string title, string confirmText, string name, string color,
        Func<string, string?> validate)
    {
        var box = new TextBox { Text = name, MaxLength = TodoList.MaxNameLength, Style = (Style)Application.Current.FindResource("FieldBox"), Tag = "List name" };
        System.Windows.Automation.AutomationProperties.SetName(box, "List name");
        var error = new TextBlock { FontSize = 12, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap }
            .Themed(TextBlock.ForegroundProperty, "OverdueBrush");
        string chosen = color;
        var swatches = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var buttons = new List<Button>();
        void Mark()
        {
            foreach (var b in buttons) b.Tag = string.Equals((string)b.CommandParameter, chosen, StringComparison.OrdinalIgnoreCase);
        }
        foreach (var hex in TodoList.Palette)
        {
            var b = new Button
            {
                Style = (Style)Application.Current.FindResource("SwatchButton"),
                Background = BrushCache.Frozen(BrushCache.Parse(hex), 1),
                CommandParameter = hex,
                ToolTip = hex,
            };
            b.Click += (_, _) => { chosen = hex; Mark(); };
            buttons.Add(b);
            swatches.Children.Add(b);
        }
        Mark();
        var body = new StackPanel();
        body.Children.Add(box);
        body.Children.Add(swatches);
        body.Children.Add(error);

        var d = new DialogWindow(owner, title, null, body,
            [new DialogButton("Cancel", IsCancel: true), new DialogButton(confirmText, DialogButtonKind.Primary)],
            canClose: _ =>
            {
                var msg = validate(box.Text.Trim());
                error.Text = msg ?? "";
                error.Visibility = msg is null ? Visibility.Collapsed : Visibility.Visible;
                return msg is null;
            });
        d.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        d.ShowDialog();
        return d.ResultIndex == 1 ? (box.Text.Trim(), chosen) : null;
    }

    /// <summary>Delete a list: move its tasks to another list, or delete them with it. Null target = delete the tasks too.</summary>
    public static (bool Confirmed, long? MoveTo) DeleteList(Window? owner, string listName, int taskCount,
        IReadOnlyList<(long Id, string Name)> others)
    {
        string tasks = taskCount == 1 ? "1 task" : $"{taskCount} tasks";
        var body = new StackPanel();
        long? target = null;
        if (taskCount == 0)
        {
            var d0 = new DialogWindow(owner, $"Delete \"{listName}\"?", "The list is empty. A backup of your data is made first.", null,
            [
                new DialogButton("Cancel", IsCancel: true), new DialogButton("Delete list", DialogButtonKind.Danger),
            ]);
            d0.ShowDialog();
            return (d0.ResultIndex == 1, null);
        }

        var move = new RadioButton { GroupName = "del", IsChecked = true, Content = $"Move its {tasks} to another list", Margin = new Thickness(0, 0, 0, 6) }
            .Themed(Control.ForegroundProperty, "TextPrimaryBrush");
        var combo = new ComboBox { Style = (Style)Application.Current.FindResource("SettingsCombo"), ItemsSource = others.Select(o => o.Name).ToList(),
            SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(22, 0, 0, 12) };
        var delete = new RadioButton { GroupName = "del", Content = $"Delete the list and its {tasks}" }
            .Themed(Control.ForegroundProperty, "OverdueBrush");
        move.Checked += (_, _) => combo.IsEnabled = true;
        delete.Checked += (_, _) => combo.IsEnabled = false;
        body.Children.Add(move);
        body.Children.Add(combo);
        body.Children.Add(delete);

        var d = new DialogWindow(owner, $"Delete \"{listName}\"?", "A backup of your data is made first, because this can't be undone with the Undo bar.",
            body, [new DialogButton("Cancel", IsCancel: true), new DialogButton("Delete list", DialogButtonKind.Danger)]);
        d.ShowDialog();
        if (d.ResultIndex != 1) return (false, null);
        if (move.IsChecked == true) target = others[Math.Max(0, combo.SelectedIndex)].Id;
        return (true, target);
    }
}

internal static class DialogExtensions
{
    /// <summary>Binds a brush property to a theme resource so it follows the theme live.</summary>
    public static T Themed<T>(this T element, DependencyProperty property, string resourceKey) where T : FrameworkElement
    {
        element.SetResourceReference(property, resourceKey);
        return element;
    }
}
