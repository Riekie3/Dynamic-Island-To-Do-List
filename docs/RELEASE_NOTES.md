# TaskIsland 1.0.2 (installer + portable)

A Dynamic Island-style to-do list for Windows 11 (x64), by **RieSources** (https://rie-sources.com).

## What's new since 1.0.1

- **Drag and drop now lifts the item out.** Drag a list chip or a task row and a copy lifts out of the panel and follows
  the pointer; the item left behind dims and the other chips/rows slide aside to show the gap where it will land.
  Dropping settles the item into that gap. The old insertion lines (and the thin line for list chips) are gone.
- **Task rows use the same lifted preview.** Rows still reorder only inside the same priority group, only when reorder
  is available, and completed or editing rows still cannot be dragged. Keyboard reorder (Alt+Up/Down) and the list
  chip's **Move left** / **Move right** menu are unchanged.

Everything else is the same as 1.0.1.

## Upgrading

Exit TaskIsland from the tray menu, then run the new installer over the old one. Tasks and settings stay (same data
folder, `%LocalAppData%\TaskIsland`). The installer refuses to run while TaskIsland is open.

## Install (recommended)

1. **If you run an older portable copy:** Exit it from the tray menu first (the installer asks you to close it anyway).
2. Run `TaskIsland-Setup-1.0.2-x64.exe`. It is **not code-signed**, so SmartScreen may say "Windows protected your
   PC": click **More info**, then **Run anyway**. (Smart App Control, if turned on, can block unsigned apps.)
3. It installs to `%LocalAppData%\Programs\TaskIsland`, adds a Start menu entry (desktop icon optional) and starts
   TaskIsland at the end. Your tasks from an older version are found again: the data folder is the same
   (`%LocalAppData%\TaskIsland`).
4. "Start with Windows" moves over to the installed copy on its first start. You can then delete the old portable
   folder (if you run the portable copy again, it takes "Start with Windows" back).

Upgrading later: run a newer installer over the top; tasks and settings stay.

## Or portable

Unzip `TaskIsland-1.0.2-win-x64-portable.zip` somewhere permanent and run `TaskIsland.exe` (same SmartScreen note).
"Start with Windows" is on by default and points at that folder, so do not move it afterwards.

No administrator rights, no internet connection, no .NET install needed (the runtime is included) either way.

## What it does

- A pill/notch with a little face; click it and it stretches into a to-do list, then folds back.
- Lists (Personal, Office Work, your own), priorities, due dates with reminders, drag to reorder, undo for deletes.
- Tray icon, global hotkey Ctrl+Alt+T, start with Windows (optional), one instance only.
- Settings: themes, accent colour, panel opacity, companion face on/off, reminder width and blink, JSON export/import,
  daily backups.
- Data stays on your PC in `%LocalAppData%\TaskIsland`. The app makes no network connections.

## Checks done for this release

- 444 automated tests green.
- `tools/ui-tests/chipdrag.ps1` (real mouse): all 5 checks passed — Office Work dragged in front of Personal and back
  again, the panel stays open, and a plain click does not change the order.
- New `tools/ui-tests/rowdrag.ps1` (real mouse): all 6 checks passed, twice in a row — a Medium task dragged below
  another Medium task was saved in the database and still shown in that order after a restart; a drop onto a High task
  was refused; a plain click did not reorder.
- Drag screenshots taken with `--full-motion` were checked: the chip/row copy follows the pointer across the
  list and quick-add strip, the source is dimmed and the gap opens. The row ghost has an opaque backing, so the rows
  underneath do not show through.
- The installer test passed 29 checks (install without administrator mode, upgrade over the top with tasks kept,
  uninstall with the "Also delete your tasks and backups?" question, reinstall). The check against the real installed
  copy was skipped because TaskIsland is already installed on this PC.
- Built from a clean, committed tree (`1.0.2+ec6d26f`). Microsoft Defender (signatures 1.463.218.0) scanned the
  published folder, the portable zip and the installer with no threats found. The 10-minute Defender soak was **not**
  repeated.

## Known limits (please tell us what you see)

- **Memory is above the target.** About 64 MB private memory (126 MB working set) at idle; once the list panel has been
  opened it stays around 130-140 MB working set. The target was 50 MB / 70 MB. WPF itself needs a lot of that.
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

- `TaskIsland-Setup-1.0.2-x64.exe`: `5C52494E06FF778383C6E1A7FFBC31028FAF3AEAA13F1B48D27849FDD6557016`
- `TaskIsland-1.0.2-win-x64-portable.zip`: `94C8C92024BC9510F6E5E0EC2AA9726C116B393842B823B7D14DC68C96FF6E97`

---

Developed by **RieSources** — https://rie-sources.com
