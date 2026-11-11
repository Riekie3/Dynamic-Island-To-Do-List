# TaskIsland 1.0.6 (installer + portable)

A Dynamic Island-style to-do list for Windows 11 (x64), by **RieSources** (https://rie-sources.com).

## What's new since 1.0.5

- **Drag the island to place it.** Press on the closed pill and move it sideways: it follows the mouse along the top of
  the screen and stays where you drop it (it stops where the open list still fits, so it cannot get lost off the
  edge). The place is remembered after a restart. Settings > Appearance > Position shows the same value and still
  works too; its range is now wider (-2000 to +2000).
- **The face reacts.** While you carry it the face looks excited, and when you drop it it is happy. The pill also dips
  when picked up and bounces when dropped (also with the face turned off; skipped when animations are reduced).
- A plain click still opens the list, exactly as before.

Also in this version (new in 1.0.5): memory stays flat at about 50-55 MB however often the panel opens, and long task
titles no longer get squeezed when you point at a row.

## Upgrading

Exit TaskIsland from the tray menu, then run the new installer over the old one. Tasks and settings stay (same data
folder, `%LocalAppData%\TaskIsland`). The installer refuses to run while TaskIsland is open.

## Install (recommended)

1. **If you run an older portable copy:** Exit it from the tray menu first (the installer asks you to close it anyway).
2. Run `TaskIsland-Setup-1.0.6-x64.exe`. It is **not code-signed**, so SmartScreen may say "Windows protected your
   PC": click **More info**, then **Run anyway**. (Smart App Control, if turned on, can block unsigned apps.)
3. It installs to `%LocalAppData%\Programs\TaskIsland`, adds a Start menu entry (desktop icon optional) and starts
   TaskIsland at the end. Your tasks from an older version are found again: the data folder is the same
   (`%LocalAppData%\TaskIsland`).
4. "Start with Windows" moves over to the installed copy on its first start. You can then delete the old portable
   folder (if you run the portable copy again, it takes "Start with Windows" back).

Upgrading later: run a newer installer over the top; tasks and settings stay.

## Or portable

Unzip `TaskIsland-1.0.6-win-x64-portable.zip` somewhere permanent and run `TaskIsland.exe` (same SmartScreen note).
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

- 462 automated tests green (8 new for the drag: where the pill lands at 100-200 % scaling, on a monitor left of the
  main one, at the screen edges, and that the saved place puts it back in the same spot).
- New real-mouse UI test `tools/ui-tests/dragplace.ps1`: 12/12 passed (drag left saves the place and moves the window,
  the drag does not open the list, the face shows Excited then Happy, a click still opens the list, a fast drag far
  right stops at the edge, the place is kept after a restart).
- UI tests `t1` to `t6`, `chipdrag.ps1`, `rowdrag.ps1` and `p5b.ps1` passed (temporary data folders). `p5a.ps1` has one
  failure ("pulse after the pill is wide") that happens the same way on 1.0.4.
- The installer test passed (install without administrator mode, upgrade over the top with tasks kept, uninstall,
  reinstall). The check against the real installed copy was skipped because TaskIsland is already installed on this PC.
- Built from a clean, committed tree (`1.0.6+23ed963`). Microsoft Defender scanned the portable zip and the installer
  with no threats found. The 10-minute Defender soak was **not** repeated.

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
