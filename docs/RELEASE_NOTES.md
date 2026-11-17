# TaskIsland 1.0.7 (installer + portable)

A Dynamic Island-style to-do list for Windows 11 (x64), by **RieSources** (https://rie-sources.com).

## What's new since 1.0.6

- **Choose how your lists show at the top.** Settings > Appearance > **Lists at the top**:
  - **Chips** (as before, the default): All, Personal, Office Work… side by side; scroll sideways when they do not fit.
  - **Drop-down**: one full-width button shows the current list with its count. Click it to see every list, pick one
    to view it. Arrow keys and Enter work, Esc closes the menu (not the panel).
- In the drop-down each list has a **⋯** button (or right-click it): Rename, Change colour, **Move up**, **Move down**,
  Delete. **New list** is at the bottom of the menu and on the **+** next to the button.
- The setting applies at once. Ctrl+1…9, Ctrl+0 and Ctrl+Tab work the same in both styles, and switching back keeps
  your list order and the selected list.

Also in this version: drag the island along the top to place it (1.0.6), memory stays flat (1.0.5).

## Upgrading

Exit TaskIsland from the tray menu, then run the new installer over the old one. Tasks and settings stay (same data
folder, `%LocalAppData%\TaskIsland`). The installer refuses to run while TaskIsland is open.

## Install (recommended)

1. **If you run an older portable copy:** Exit it from the tray menu first (the installer asks you to close it anyway).
2. Run `TaskIsland-Setup-1.0.7-x64.exe`. It is **not code-signed**, so SmartScreen may say "Windows protected your
   PC": click **More info**, then **Run anyway**. (Smart App Control, if turned on, can block unsigned apps.)
3. It installs to `%LocalAppData%\Programs\TaskIsland`, adds a Start menu entry (desktop icon optional) and starts
   TaskIsland at the end. Your tasks from an older version are found again: the data folder is the same
   (`%LocalAppData%\TaskIsland`).
4. "Start with Windows" moves over to the installed copy on its first start. You can then delete the old portable
   folder (if you run the portable copy again, it takes "Start with Windows" back).

Upgrading later: run a newer installer over the top; tasks and settings stay.

## Or portable

Unzip `TaskIsland-1.0.7-win-x64-portable.zip` somewhere permanent and run `TaskIsland.exe` (same SmartScreen note).
"Start with Windows" is on by default and points at that folder, so do not move it afterwards.

No administrator rights, no internet connection, no .NET install needed (the runtime is included) either way.

## What it does

- A pill/notch with a little face; click it and it stretches into a to-do list, then folds back.
- Lists (Personal, Office Work, your own), priorities, due dates with reminders, drag to reorder, undo for deletes.
- Tray icon, global hotkey Ctrl+Alt+T, start with Windows (optional), one instance only.
- Settings: themes, accent colour, panel opacity, companion face on/off, reminder width and blink, JSON export/import,
  daily backups, backup deletion and a full reset.
- Data stays on your PC in `%LocalAppData%\TaskIsland`. The app makes no network connections.

## Checks done for this release

- 467 automated tests green (5 new for the setting: default Chips, saved and read back, any letter case, unknown values
  fall back to Chips).
- New UI test `tools/ui-tests/listswitcher.ps1`: 26/26 passed (switch to Drop-down and back in Settings while running,
  the menu lists every list, picking a list switches and is saved, Esc closes only the menu, the panel stays open while
  the menu is open, Ctrl+1 and Ctrl+0, Move down from the ⋯ menu with Move up greyed out on the first list, New list
  opens the list editor, order kept after switching back).
- UI tests `t1` to `t6`, `chipdrag.ps1`, `rowdrag.ps1`, `dragplace.ps1` and `p5b.ps1` passed (temporary data folders).
- Screenshots of both styles checked in the dark theme. The light theme was not checked by eye.
- INSTALLER_LINE
- BUILD_LINE

## Known limits (please tell us what you see)

- **Memory:** 49-55 MB private memory, flat after the panel has been opened many times (the target was 50 MB). The
  total working set is about 115-125 MB against a 70 MB target; most of it is shared .NET and WPF code, not TaskIsland's
  own data. A 200-cycle soak, 4K and a second monitor have not been measured yet.
- The face's random animations use a little CPU. Measured right after a first start with default settings: about
  1.5-2 % of one core averaged over 4 minutes; an earlier 30-minute measurement averaged 0.13 %. Settings > Appearance >
  "Face pops up on its own" turns it down or off.
- Sleep, hibernate, reboot auto-start, multi-monitor and display-scaling behaviour is being verified in real use.
  If something goes wrong, check `%LocalAppData%\TaskIsland\Logs`.
- Not code-signed (see above). On VirusTotal the rc.2 installer got 1 detection of 68 (Deep Instinct, no malware name);
  see the README. Licence: MIT (see LICENSE).

## Remove it

- **Installed:** Windows Settings > Apps > Installed apps > TaskIsland > Uninstall. It asks whether to delete your tasks
  and backups too (default No).
- **Portable:** Settings > General, untick **Start with Windows**; Exit from the tray menu; delete the folder. To also
  delete your tasks and backups, delete `%LocalAppData%\TaskIsland`.

## SHA-256

- `TaskIsland-Setup-1.0.6-x64.exe`: `D10CB242EB7A96B9E690BA15F100A1B1391A7A7996DC5B8D24F68BA732228E45`
- `TaskIsland-1.0.6-win-x64-portable.zip`: `12B6338835A3F7C9BF9D411541C97BCF9910E9F27AA0D3C1316C5157D6C80494`

---

Developed by **RieSources** — https://rie-sources.com
