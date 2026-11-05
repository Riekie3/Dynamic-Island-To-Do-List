# TaskIsland 1.0.0-rc.3 (test release: installer + portable)

A Dynamic Island-style to-do list for Windows 11 (x64), by **RieSources** (https://rie-sources.com).

This is the third test release (the second with an **installer**). Version 1.0.0 follows once it has been used for
a few days.

## What's new since rc.2

- **Long task titles wrap.** A title that is too long for the panel now continues on the next line inside the same row,
  so you can read all of it (before it was cut off with "..."). It is still one task with one tick; a done task gets a
  normal strikethrough across the wrapped lines.
- Everything from rc.2 is unchanged: the wide reminder with the blinking red border (Settings > Behaviour > Reminders),
  the per-user installer and the hover-preview fix.

## Upgrading from rc.2 or rc.1

Exit TaskIsland from the tray menu, then run the new installer over the old one. Tasks and settings stay (same data
folder, `%LocalAppData%\TaskIsland`). The installer refuses to run while TaskIsland is open.

## Install (recommended)

1. **If you run the rc.1 portable copy:** Exit it from the tray menu first (the installer asks you to close it anyway).
2. Run `TaskIsland-Setup-1.0.0-rc.3-x64.exe`. It is **not code-signed**, so SmartScreen may say "Windows protected your
   PC": click **More info**, then **Run anyway**. (Smart App Control, if turned on, can block unsigned apps.)
3. It installs to `%LocalAppData%\Programs\TaskIsland`, adds a Start menu entry (desktop icon optional) and starts
   TaskIsland at the end. Your tasks from rc.1 are found again: the data folder is the same (`%LocalAppData%\TaskIsland`).
4. "Start with Windows" moves over to the installed copy on its first start. You can then delete the old portable
   folder (if you run the portable copy again, it takes "Start with Windows" back).

Upgrading later: run a newer installer over the top; tasks and settings stay.

## Or portable

Unzip `TaskIsland-1.0.0-rc.3-win-x64-portable.zip` somewhere permanent and run `TaskIsland.exe` (same SmartScreen note).
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

- 438 automated tests green. Microsoft Defender (signatures 1.463.130.0) found no threats in the installer, the program
  folder or the zip. The installer was tested on this PC with a separate test build (own folder, data folder and registry
  key): install without administrator mode, upgrade (refused while TaskIsland runs, works over the top once closed, tasks
  kept), uninstall with the "Also delete your tasks and backups?" question (default No) and reinstall. All checks passed;
  the one check against the real installed copy was skipped because TaskIsland is already installed on this PC. The long-title wrapping was checked in a screenshot. The
  10-minute Defender soak was **not** repeated for rc.3.

## Known limits (please tell us what you see)

- **Memory is above the target.** About 64 MB private memory (126 MB working set) at idle; once the list panel has been
  opened it stays around 130-140 MB working set. The target was 50 MB / 70 MB. WPF itself needs a lot of that.
- The face's random animations use a little CPU. Measured right after a first start with default settings: about
  1.5-2 % of one core averaged over 4 minutes (rc.1 the same within noise); an earlier 30-minute measurement averaged
  0.13 %. Settings > Appearance > "Face pops up on its own" turns it down or off.
- Sleep, hibernate, reboot auto-start, multi-monitor and display-scaling behaviour is being verified in real use.
  If something goes wrong, check `%LocalAppData%\TaskIsland\Logs`.
- Not code-signed (see above). On VirusTotal the rc.2 installer got 1 detection of 68 (Deep Instinct, no malware name); see the README. Licence: MIT (see LICENSE).

## Remove it

- **Installed:** Windows Settings > Apps > Installed apps > TaskIsland > Uninstall. It asks whether to delete your tasks
  and backups too (default No).
- **Portable:** Settings > General, untick **Start with Windows**; Exit from the tray menu; delete the folder. To also
  delete your tasks and backups, delete `%LocalAppData%\TaskIsland`.

## SHA-256

- `TaskIsland-Setup-1.0.0-rc.3-x64.exe`: `F6F0FFD19D501F0B90DFE9E1AE592F0F095B311F8F776F06CF93E24938CF5A70`
- `TaskIsland-1.0.0-rc.3-win-x64-portable.zip`: `38DDC70A91BD0D4D633BB720B7EC0C1AD119AE19ABAEAB6CC218AB1C74963B0C`

---

Developed by **RieSources** · https://rie-sources.com
