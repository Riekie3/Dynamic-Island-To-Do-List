# TaskIsland 1.0.4 (installer + portable)

A Dynamic Island-style to-do list for Windows 11 (x64), by **RieSources** (https://rie-sources.com).

## What's new since 1.0.3

- **A locked old backup can no longer stop the daily backup.** The clean-up of old backups now skips a file it cannot
  delete (for example one an antivirus scan or another program is holding) and tries again next time, instead of
  skipping that day's backup.
- **Reset everything really wipes the old data.** After a reset the database file is compacted and its write-ahead file
  emptied, so the deleted lists and tasks are no longer sitting in unused space on disk.
- **Reset no longer reuses task numbers.** New tasks after a reset always get numbers higher than any task before, so a
  reminder the app remembers can never be mistaken for a new task with the same due time.

Everything else is the same as 1.0.3.

## Upgrading

Exit TaskIsland from the tray menu, then run the new installer over the old one. Tasks and settings stay (same data
folder, `%LocalAppData%\TaskIsland`). The installer refuses to run while TaskIsland is open.

## Install (recommended)

1. **If you run an older portable copy:** Exit it from the tray menu first (the installer asks you to close it anyway).
2. Run `TaskIsland-Setup-1.0.4-x64.exe`. It is **not code-signed**, so SmartScreen may say "Windows protected your
   PC": click **More info**, then **Run anyway**. (Smart App Control, if turned on, can block unsigned apps.)
3. It installs to `%LocalAppData%\Programs\TaskIsland`, adds a Start menu entry (desktop icon optional) and starts
   TaskIsland at the end. Your tasks from an older version are found again: the data folder is the same
   (`%LocalAppData%\TaskIsland`).
4. "Start with Windows" moves over to the installed copy on its first start. You can then delete the old portable
   folder (if you run the portable copy again, it takes "Start with Windows" back).

Upgrading later: run a newer installer over the top; tasks and settings stay.

## Or portable

Unzip `TaskIsland-1.0.4-win-x64-portable.zip` somewhere permanent and run `TaskIsland.exe` (same SmartScreen note).
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

- 454 automated tests green (3 new). Each new test was also run against the 1.0.3 code and fails there: a locked old
  backup blocking the daily backup, task ids reused after a reset, and old task text still readable in the database
  file after a reset.
- `tools/ui-tests/backupui.ps1` (temporary data folder): 12/12 passed with the compacting reset. `chipdrag.ps1` and
  `rowdrag.ps1` were not re-run: no drag code changed.
- The installer test passed 29 checks (install without administrator mode, upgrade over the top with tasks kept,
  uninstall with the "Also delete your tasks and backups?" question, reinstall). The check against the real installed
  copy was skipped because TaskIsland is already installed on this PC.
- Built from a clean, committed tree (`1.0.4+dde6e2b`). Microsoft Defender scanned the portable zip and the installer
  with no threats found. The 10-minute Defender soak was **not** repeated.

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

- `TaskIsland-Setup-1.0.4-x64.exe`: `3676DBE11AC98D287D4A3E3B1F362E473BEEC4088E4F298CAEFCAA81329EE745`
- `TaskIsland-1.0.4-win-x64-portable.zip`: `261B4AD69E6B88412E97504F1E2B131B512BFB659C3F8A4982A493F3269CC974`

---

Developed by **RieSources** — https://rie-sources.com
