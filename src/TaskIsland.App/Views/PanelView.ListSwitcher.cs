using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TaskIsland.App.ViewModels;
using TaskIsland.Core.Models;

namespace TaskIsland.App.Views;

/// <summary>
/// List switcher style (docs/LIST-SWITCHER-PLAN.md): today's chips, or one drop-down button with a menu of lists. Both use
/// the same <see cref="IslandViewModel.Chips"/> and commands; the per-list menu reuses the chip menu handlers.
/// </summary>
public partial class PanelView
{
    /// <summary>Called once at creation and whenever the setting changes.</summary>
    public void SetListSwitcher(ListSwitcherStyle style)
    {
        bool dropdown = style == ListSwitcherStyle.Dropdown;
        ChipScroller.Visibility = dropdown ? Visibility.Collapsed : Visibility.Visible;
        ListDropdown.Visibility = dropdown ? Visibility.Visible : Visibility.Collapsed;
        if (!dropdown) ListPopup.IsOpen = false;
        SyncDropdown();
    }

    /// <summary>The list menu is open (a window of its own: not a click outside, and Esc closes it first).</summary>
    public bool IsListMenuOpen => ListPopup.IsOpen;

    /// <summary>Esc while the list menu is open: closes only the menu. Returns false when it was not open.</summary>
    public bool CloseListMenu()
    {
        if (!ListPopup.IsOpen) return false;
        ListPopup.IsOpen = false;
        ListDropdownButton.Focus();
        return true;
    }

    private void InitListSwitcher()
    {
        ListMenuItems.ItemsSource = _vm.Chips;
        _vm.Chips.CollectionChanged += (_, _) => SyncDropdown();
        SyncDropdown();
    }

    /// <summary>The button shows the selected list (its dot, name and count bind to that chip).</summary>
    private void SyncDropdown()
    {
        var chip = _vm.Chips.FirstOrDefault(c => c.Id == _vm.SelectedListId) ?? _vm.Chips.FirstOrDefault();
        ListDropdownButton.DataContext = chip;
        AutomationProperties.SetName(ListDropdownButton,
            "Lists: " + (chip is null || chip.IsAll ? "All lists" : chip.Name) + (chip is null ? "" : $", {chip.Count} open"));
    }

    private void ListDropdown_Click(object sender, RoutedEventArgs e) => ListPopup.IsOpen = !ListPopup.IsOpen;

    /// <summary>Opening the menu puts the keyboard on the selected list, so arrows and Enter work at once.</summary>
    private void ListPopup_Opened(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var chip = ListDropdownButton.DataContext as ListChipViewModel;
            var item = (chip is null ? null : ItemButton(chip)) ?? ItemButton(_vm.Chips.FirstOrDefault());
            if (item is null) return;
            item.BringIntoView();
            item.Focus();
            Keyboard.Focus(item);
        }, DispatcherPriority.Input);
    }

    private Button? ItemButton(ListChipViewModel? chip)
    {
        if (chip is null || ListMenuItems.ItemContainerGenerator.ContainerFromItem(chip) is not DependencyObject host) return null;
        return FirstDescendant<Button>(host);
    }

    private static T? FirstDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) return t;
            if (FirstDescendant<T>(child) is { } found) return found;
        }
        return null;
    }

    private void ListPopup_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        CloseListMenu();
    }

    private void ListMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ListChipViewModel chip }) return;
        ListPopup.IsOpen = false;
        _vm.SelectListCommand.Execute(chip);
    }

    /// <summary>The ⋯ on a list row opens the same menu as a right-click on that row.</summary>
    private void ListMenuMore_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // not a click on the row itself (that would switch lists)
        DependencyObject? d = sender as DependencyObject;
        while (d is not null && (d == sender || d is not Button { ContextMenu: not null }))
            d = VisualTreeHelper.GetParent(d);
        if (d is not Button { ContextMenu: { } menu, DataContext: ListChipViewModel chip } row) return;
        if (menu.Items.Count >= 4)
        {
            ((MenuItem)menu.Items[2]).IsEnabled = _vm.CanMoveList(chip, -1);
            ((MenuItem)menu.Items[3]).IsEnabled = _vm.CanMoveList(chip, +1);
        }
        menu.PlacementTarget = row;
        menu.Placement = PlacementMode.Bottom;
        menu.HorizontalOffset = row.ActualWidth - 160;
        menu.IsOpen = true;
    }

    private void ListPopupNew_Click(object sender, RoutedEventArgs e)
    {
        ListPopup.IsOpen = false;
        _vm.NewListCommand.Execute(null);
    }
}
