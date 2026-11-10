# TaskIsland 1.0.3 (installer + portable)

A Dynamic Island-style to-do list for Windows 11 (x64), by **RieSources** (https://rie-sources.com).

## What's new since 1.0.2

- **Backups clean up after themselves.** The newest **3** automatic daily backups are kept (previously 7), and the
  safety copies made before you delete a list (`pre-delete-*.db`) are removed automatically after **24 hours**. The
  check runs at start-up, after sleep and once an hour while TaskIsland stays open, so old copies do not pile up.
- **Delete all backups.** Settings > Data > **Delete all backups…** clears every file in the Backups folder after a
  confirmation. Your tasks and settings are kept.
- **Reset TaskIsland.** Settings > Data > **Reset everything…** returns the app to its first-run state: all lists, tasks,
  settings and backups are deleted, the Personal and Office Work starter lists come back with the default settings, and
  the app keeps running without a restart. Logs are kept. This cannot be undone.

Everything else is the same as 1.0.2.

## Upgrading

Exit TaskIsland from the tray menu, then run the new installer over the old one. Tasks and settings stay (same data
folder, `%LocalAppData%\TaskIsland`). The installer refuses to run while TaskIsland is open.

## Install (recommended)

1. **If you run an older portable copy:** Exit it from the tray menu first (the installer asks you to close it anyway).
2. Run `TaskIsland-Setup-1.0.3-x64.exe`. It is **not code-signed**, so SmartScreen may say "Windows protected your
   PC": click **More info**, then **Run anyway**. (Smart App Control, if turned on, can block unsigned apps.)
3. It installs to `%LocalAppData%\Programs\TaskIsland`, adds a Start menu entry (desktop icon optional) and starts
   TaskIsland at the end. Your tasks from an older version are found again: the data folder is the same
   (`%LocalAppData%\TaskIsland`).
4. "Start with Windows" moves over to the installed copy on its first start. You can then delete the old portable
   folder (if you run the portable copy again, it takes "Start with Windows" back).

Upgrading later: run a newer installer over the top; tasks and settings stay.

## Or portable

Unzip `TaskIsland-1.0.3-win-x64-portable.zip` somewhere permanent and run `TaskIsland.exe` (same SmartScreen note).
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

- 451 automated tests green.
- New `tools/ui-tests/backupui.ps1` (temporary data folder): **12/12 passed** — both buttons visible on the Data page;
  Delete all backups removed every backup file and kept the tasks; Reset cleared the tasks, restored Personal + Office
  Work and the default theme, deleted the backups and left the Settings window responsive.
- Live same-priority reorder check on the installed 1.0.2 copy and the real database: **8/8 passed** — dragged
  `Training ISO wei yee` below `Dr Chen monitor` in Office Work, confirmed the saved order, then dragged it back above
  and confirmed the original order was restored.
- The installer test passed 29 checks (install without administrator mode, upgrade over the top with tasks kept,
  uninstall with the "Also delete your tasks and backups?" question, reinstall). The check against the real installed
  copy was skipped because TaskIsland is already installed on this PC.
- Built from a clean, committed tree (`1.0.3+5bdd484`). Microsoft Defender (signatures 1.463.222.0) scanned the
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

- `TaskIsland-Setup-1.0.3-x64.exe`: `51018B5983B6F38BD32777CC245533548486047761767BA47E5821BADBBEE057`
- `TaskIsland-1.0.3-win-x64-portable.zip`: `6013CAB97F5D5E2AFECE2C1E7BA125A3962156881F62E2C33CD5A955DEC58D6F`

---

Developed by **RieSources** — https://rie-sources.com
