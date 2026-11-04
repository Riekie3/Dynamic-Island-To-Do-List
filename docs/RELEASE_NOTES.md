# TaskIsland 1.0.0-rc.2 (test release: installer + portable)

A Dynamic Island-style to-do list for Windows 11 (x64), by **RieSources** (https://rie-sources.com).

This is the second test release and the first one with an **installer**. Version 1.0.0 follows once it has been used for
a few days.

## What's new since rc.1

- **Bigger, louder reminders.** When a reminder fires, the island now grows to **half the screen width** and a **red
  border blinks** around it for 5 seconds (about once a second; steady red if Windows animations are off).
  Settings > Behaviour > Reminders: **Reminder width** (Compact, 30 %, 40 %, 50 %, 60 %, 75 % or Full width) and
  **Blink a red border** (off brings back the blue pulse).
- **Installer** `TaskIsland-Setup-1.0.0-rc.2-x64.exe`: installs for your user only, no administrator rights, no UAC prompt.
- Fix: the hover preview of the next task no longer starts on top of a reminder.
- The README on GitHub is now a full install and how-to-use guide with screenshots.

## Install (recommended)

1. **If you run the rc.1 portable copy:** Exit it from the tray menu first (the installer asks you to close it anyway).
2. Run `TaskIsland-Setup-1.0.0-rc.2-x64.exe`. It is **not code-signed**, so SmartScreen may say "Windows protected your
   PC": click **More info**, then **Run anyway**. (Smart App Control, if turned on, can block unsigned apps.)
3. It installs to `%LocalAppData%\Programs\TaskIsland`, adds a Start menu entry (desktop icon optional) and starts
   TaskIsland at the end. Your tasks from rc.1 are found again: the data folder is the same (`%LocalAppData%\TaskIsland`).
4. "Start with Windows" moves over to the installed copy on its first start. You can then delete the old portable
   folder (if you run the portable copy again, it takes "Start with Windows" back).

Upgrading later: run a newer installer over the top; tasks and settings stay.

## Or portable

Unzip `TaskIsland-1.0.0-rc.2-win-x64-portable.zip` somewhere permanent and run `TaskIsland.exe` (same SmartScreen note).
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

- 438 automated tests green. Defender (signatures 1.463.121.0) found no threats in the installer, the program folder or
  the zip; 10 minutes of running with Defender on raised nothing (first rc.2 build; the final build ran 3 minutes clean).
- Installer tested on this PC with a separate test build (own folder, data folder and registry key): per-user install with
  no administrator mode, Start menu entry, Apps list entry (publisher RieSources); upgrade is refused while TaskIsland
  runs and works over the top once it is closed, tasks kept; uninstall removes the program, the Start menu entry and its
  "Start with Windows" entry and asks "Also delete your tasks and backups?" (default **No**: kept; **Yes**: deleted);
  reinstall finds the tasks. The release installer itself refuses to run while TaskIsland is running.

## Known limits (please tell us what you see)

- **Memory is above the target.** About 64 MB private memory (126 MB working set) at idle; once the list panel has been
  opened it stays around 130-140 MB working set. The target was 50 MB / 70 MB. WPF itself needs a lot of that.
- The face's random animations use a little CPU. Measured right after a first start with default settings: about
  1.5-2 % of one core averaged over 4 minutes (rc.1 the same within noise); an earlier 30-minute measurement averaged
  0.13 %. Settings > Appearance > "Face pops up on its own" turns it down or off.
- Sleep, hibernate, reboot auto-start, multi-monitor and display-scaling behaviour is being verified in real use.
  If something goes wrong, check `%LocalAppData%\TaskIsland\Logs`.
- Not code-signed (see above). Licence: MIT (see LICENSE).

## Remove it

- **Installed:** Windows Settings > Apps > Installed apps > TaskIsland > Uninstall. It asks whether to delete your tasks
  and backups too (default No).
- **Portable:** Settings > General, untick **Start with Windows**; Exit from the tray menu; delete the folder. To also
  delete your tasks and backups, delete `%LocalAppData%\TaskIsland`.

## SHA-256

- `TaskIsland-Setup-1.0.0-rc.2-x64.exe`: `E3CE55F9137A4A019E42BEE2241E837DC53D5CF6BFB6808CD734E30929B771C1`
- `TaskIsland-1.0.0-rc.2-win-x64-portable.zip`: `8ACE638D1D6BE2010B817149A21C9D1C7E183893E9DE883800D1C96A108FB5EA`

---

Developed by **RieSources** · https://rie-sources.com
