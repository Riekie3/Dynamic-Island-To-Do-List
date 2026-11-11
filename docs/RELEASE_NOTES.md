# TaskIsland 1.0.5 (installer + portable)

A Dynamic Island-style to-do list for Windows 11 (x64), by **RieSources** (https://rie-sources.com).

## What's new since 1.0.4

- **Memory no longer grows when you open the panel.** The see-through island window used to leave about 60 MB of
  graphics memory behind after each open and close (64 MB at start, 120+ MB after a few opens). TaskIsland now draws
  its windows in software: 49-55 MB private memory, flat, and about 130 fewer handles. It looks the same (transparency,
  shadow and panel opacity are unchanged).
- **Long task titles no longer get squeezed when you point at a row.** The due date now sits under the title, and the
  space for the edit and delete buttons is kept free, so the title stays on its lines instead of breaking into a thin
  column.
- Trade-off: opening and closing the panel uses about twice the CPU for that moment (about 0.5 s of one core per open
  and close, measured at 1920x1080). Idle CPU is unchanged.

Everything else is the same as 1.0.4.

## Upgrading

Exit TaskIsland from the tray menu, then run the new installer over the old one. Tasks and settings stay (same data
folder, `%LocalAppData%\TaskIsland`). The installer refuses to run while TaskIsland is open.

## Install (recommended)

1. **If you run an older portable copy:** Exit it from the tray menu first (the installer asks you to close it anyway).
2. Run `TaskIsland-Setup-1.0.5-x64.exe`. It is **not code-signed**, so SmartScreen may say "Windows protected your
   PC": click **More info**, then **Run anyway**. (Smart App Control, if turned on, can block unsigned apps.)
3. It installs to `%LocalAppData%\Programs\TaskIsland`, adds a Start menu entry (desktop icon optional) and starts
   TaskIsland at the end. Your tasks from an older version are found again: the data folder is the same
   (`%LocalAppData%\TaskIsland`).
4. "Start with Windows" moves over to the installed copy on its first start. You can then delete the old portable
   folder (if you run the portable copy again, it takes "Start with Windows" back).

Upgrading later: run a newer installer over the top; tasks and settings stay.

## Or portable

Unzip `TaskIsland-1.0.5-win-x64-portable.zip` somewhere permanent and run `TaskIsland.exe` (same SmartScreen note).
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

- 454 automated tests green (no new ones: the change is in how windows are drawn and in the row layout).
- UI tests `t1` to `t6`, `chipdrag.ps1` and `rowdrag.ps1` passed on the software-rendering build (temporary data
  folders). `p5a.ps1` has one failure ("pulse after the pill is wide") that happens the same way on 1.0.4.
- A/B measurement, 10 open/close cycles, 1920x1080 at 125 %: private memory 64 -> 122 MB (1.0.4) against 49 -> 55 MB
  (1.0.5); CPU per open and close 502 ms against 1,083 ms; handles 663 against 528. A screenshot of the open panel
  differs in 25 of 64,200 sampled pixels (0.04 %).
- The installer test passed 29 checks (install without administrator mode, upgrade over the top with tasks kept,
  uninstall with the "Also delete your tasks and backups?" question, reinstall). The check against the real installed
  copy was skipped because TaskIsland is already installed on this PC.
- The installer test passed (install without administrator mode, upgrade over the top with tasks kept, uninstall,
  reinstall). The check against the real installed copy was skipped because TaskIsland is already installed on this PC.
- Built from a clean, committed tree (`1.0.5+3c28bdc`). Microsoft Defender scanned the portable zip and the installer
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

- `TaskIsland-Setup-1.0.5-x64.exe`: `099D6D60A8C854D3F2DE0A386E3A66971D87D167CA38CDCEB6556B59E023BCA0`
- `TaskIsland-1.0.5-win-x64-portable.zip`: `5C0C8B152B7245A5533898FAC27C4A25C8A7A52CF6D143C30DF3A749C9B281DE`

---

Developed by **RieSources** — https://rie-sources.com
