# TaskIsland 1.0.8 (installer + portable)

A Dynamic Island-style to-do list for Windows 11 (x64), by **RieSources** (https://rie-sources.com).

## What's new since 1.0.7

- **Reminders recover correctly after a due date is changed.** If a reminder had already fired, the due date was moved,
  and then moved back, the reminder can fire again. Restoring or importing a backup no longer leaves a stale "already
  shown" mark that silently suppresses reminders: the state saved in the database is now authoritative.
- **Due labels and the overdue dot follow the clock.** "Today 5:00 PM", "Tomorrow" and "Overdue · Yesterday" now update
  by themselves when the due moment passes and at midnight, instead of waiting for the next task change. The pill's red
  overdue dot does the same. Waking the PC or changing the clock refreshes everything immediately. This uses one-shot
  timers; the app still never polls.
- The daily backup check now also runs when the date changes while TaskIsland is open.

## Upgrading

Exit TaskIsland from the tray menu, then run the new installer over the old one. Tasks and settings stay (same data
folder, `%LocalAppData%\TaskIsland`). The installer refuses to run while TaskIsland is open.

## Install (recommended)

1. **If you run an older portable copy:** Exit it from the tray menu first (the installer asks you to close it anyway).
2. Run `TaskIsland-Setup-1.0.8-x64.exe`. It is **not code-signed**, so SmartScreen may say "Windows protected your
   PC": click **More info**, then **Run anyway**. (Smart App Control, if turned on, can block unsigned apps.)
3. It installs to `%LocalAppData%\Programs\TaskIsland`, adds a Start menu entry (desktop icon optional) and starts
   TaskIsland at the end. Your tasks from an older version are found again: the data folder is the same
   (`%LocalAppData%\TaskIsland`).
4. "Start with Windows" moves over to the installed copy on its first start. You can then delete the old portable
   folder (if you run the portable copy again, it takes "Start with Windows" back).

Upgrading later: run a newer installer over the top; tasks and settings stay.

## Or portable

Unzip `TaskIsland-1.0.8-win-x64-portable.zip` somewhere permanent and run `TaskIsland.exe` (same SmartScreen note).
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

- 480 automated tests green (341 Core, 139 Data), including 13 new tests for the reminder-state reconciliation and the
  next due-label refresh moment.
- UI smoke test `tools/ui-tests/t1.ps1` passed (add, edit, tick, undo, delete, filters and counts).
- The installer test passed (install without administrator mode, upgrade over the top with tasks kept, uninstall,
  reinstall). The check against the real installed copy was skipped because TaskIsland is already installed on this PC.
- Built from a clean, committed tree (`1.0.8+43363e9`). Microsoft Defender scanned the portable zip, the publish
  folder and the installer with no threats found. The 10-minute Defender soak was **not** repeated.

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

- `TaskIsland-Setup-1.0.8-x64.exe`: `D0B74EB9BF79033BEC6C6C2640BED02B48345FC8CF44525626C5ABA2F3EA3034`
- `TaskIsland-1.0.8-win-x64-portable.zip`: `6E232E6559D83B987C91E7E1C577A0D886151A09F6566EE75DC3964E6560DDD1`

---

Developed by **RieSources** — https://rie-sources.com
