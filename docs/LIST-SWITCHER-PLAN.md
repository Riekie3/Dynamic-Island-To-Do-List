# List switcher style: chips or drop-down (plan, 2026-11-17)

Owner's request: in Settings, choose how the lists/groups are shown at the top of the panel.

1. **Chips (today's look):** All, Personal, Office Work… side by side in one row; scroll sideways (mouse wheel) when
   they do not fit.
2. **Drop-down:** one button showing the current list; click it to see every list in a menu, pick one to view it.

Nothing else in the panel changes. Both styles show the same lists, counts and colours and switch the same filter.

## What the user sees

### Setting
Settings > Appearance, new row **"Lists at the top"** with two choices: **Chips** (default, as today) and **Drop-down**.
It applies at once, without reopening the panel, and is saved like every other setting.

### Drop-down style

```
┌──────────────────────────────────────────────┐
│ ● Office Work  2                     ▾   [+] │   ← one full-width button + the New list button
└──────────────────────────────────────────────┘
        │ click
        ▼
┌──────────────────────────────────────┐
│   All lists                     4  ✓ │
│ ● Personal                      0    │
│ ● Office Work                   2    │  ← hover shows ⋯ (Rename, Colour, Move up/down, Delete)
│ ● DIM booking calendar          2    │
│ ─────────────────────────────────── │
│ + New list                           │
└──────────────────────────────────────┘
```

- The button shows the current list's colour dot, name and open-task count, and a ▾ arrow. Long names end with "…".
- The menu opens under the button, as wide as the panel row, with a short fade (normal motion token; none with reduced
  motion). It shows up to 8 lists, then scrolls (20 lists is the maximum, plus All lists).
- Click a list (or arrow keys + Enter) to switch; the menu closes and the task list crossfades like today.
- Each list row has the same menu as a chip today, on right-click or on its ⋯ button: **Rename…, Change colour…,
  Move up, Move down, Delete list…** ("Move left/right" become "Move up/down" here). All lists has no menu.
- **New list** sits at the bottom of the menu and also stays as the [+] next to the button.
- Esc or a click outside closes the menu without switching. Opening the menu never collapses the panel (the panel
  treats it like the calendar popup).

### Chips style
Unchanged: drag a chip to reorder, wheel to scroll, right-click menu, [+] at the end.

### Same in both styles
- Ctrl+1…9 jump to a list, Ctrl+0 All lists, Ctrl+Tab / Ctrl+Shift+Tab cycle.
- The quick-add list picker, task counts, the "All lists" scope and the deleted-list rules.
- Screen readers: the drop-down button reads "Lists: Office Work, 2 open"; menu items read like the chips.

## How it is built

1. **Core:** `AppSettings.ListSwitcher` (`Chips` | `Dropdown`, default `Chips`, stored as text like the other enums);
   load/save and unknown-value tests.
2. **Settings window:** a two-choice row in Appearance bound to `SettingsViewModel.ListSwitcher`; `SettingsApplier`
   tells the open panel to swap styles at once.
3. **Panel:** row 2 of `PanelView.xaml` holds both: today's `ChipScroller` and a new `ListDropdown` (button + `Popup`
   with `AllowsTransparency`, like the date popup). Only one is visible. Both bind to the same `Chips` collection and
   `SelectListCommand`, so no view-model logic is duplicated.
4. **Menu actions:** reuse the existing chip handlers (`ChipRename_Click`, `ChipLeft/Right_Click`,
   `ChipDelete_Click`) with the list row as the target; `CanMoveList` greys out Move up/down at the ends.
5. **Popup safety:** `PanelView.IsPopupOpen` also returns true while the list menu is open, so a click in it is not a
   "click outside" that collapses the panel; the menu closes when the panel collapses.
6. **No chip drag in the drop-down** (order changes through Move up/down); the chip drag code stays as it is.
7. **Docs:** README (Settings section, with a screenshot of each style), PLAN section 5 settings table, DEVLOG.

## Tests

- Core unit tests: setting default, round trip, unknown text falls back to Chips.
- UI test `tools/ui-tests/listswitcher.ps1` (temporary data folder): switch the setting and see the right control;
  open the menu, pick a list, the filter changes; Esc closes without switching; the panel stays open while the menu
  is open; Rename and Move up/down from the menu; Ctrl+2 still switches in drop-down mode; switching back to Chips
  keeps the order and the selected list.
- Existing `chipdrag.ps1`, `t1`–`t6` stay green in Chips mode.
- Look: screenshots of both styles, dark and light theme, 70 % panel opacity.

## Phases

- **L1:** setting + Settings row + drop-down button and menu that switch lists (releasable on its own).
- **L2:** the per-list menu (rename, colour, move, delete) and New list inside the drop-down, keyboard support.
- **L3:** UI test, screenshots, docs; release 1.0.7 on the owner's go.

## Open questions for the owner

1. Drop-down button **full width** (as drawn above) or **compact** (just as wide as the name, the rest of the row
   empty)?
2. In the drop-down, reorder lists by **Move up/down** in the menu (planned), or should rows also be draggable?
