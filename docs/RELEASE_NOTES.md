# TaskIsland 1.0.1 (installer + portable)

A Dynamic Island-style to-do list for Windows 11 (x64), by **RieSources** (https://rie-sources.com).

## What's new since 1.0.0

- **Drag lists into the order you want.** Press on a list chip (for example **Personal**) and drag it left or right in
  the list row; a thin accent line shows where it will land, and the new order is saved. **All** always stays first.
  A plain click still just switches list, and right-click > **Move left** / **Move right** still works.

Everything else is the same as 1.0.0.

## Upgrading

Exit TaskIsland from the tray menu, then run the new installer over the old one. Tasks and settings stay (same data
folder, `%LocalAppData%\TaskIsland`). The installer refuses to run while TaskIsland is open.

## Install (recommended)

1. **If you run the rc.1 portable copy:** Exit it from the tray menu first (the installer asks you to close it anyway).
2. Run `TaskIsland-Setup-1.0.1-x64.exe`. It is **not code-signed**, so SmartScreen may say "Windows protected your
   PC": click **More info**, then **Run anyway**. (Smart App Control, if turned on, can block unsigned apps.)
3. It installs to `%LocalAppData%\Programs\TaskIsland`, adds a Start menu entry (desktop icon optional) and starts
   TaskIsland at the end. Your tasks from rc.1 are found again: the data folder is the same (`%LocalAppData%\TaskIsland`).
4. "Start with Windows" moves over to the installed copy on its first start. You can then delete the old portable
   folder (if you run the portable copy again, it takes "Start with Windows" back).

Upgrading later: run a newer installer over the top; tasks and settings stay.

## Or portable

Unzip `TaskIsland-1.0.1-win-x64-portable.zip` somewhere permanent and run `TaskIsland.exe` (same SmartScreen note).
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

- 444 automated tests green. A new UI test (`tools/ui-tests/chipdrag.ps1`) drags a list chip with the real mouse in a
  separate test copy: Office Work in front of Personal and back again, the panel stays open, and a plain click does not
  change the order. All checks passed.
- Built from a clean, committed tree (`1.0.1+f631a73`). Microsoft Defender found no threats in the installer or the zip.
- The installer test passed again (install without administrator mode, upgrade over the top with tasks kept, uninstall
  with the "Also delete your tasks and backups?" question, reinstall); the check against the real installed copy was
  skipped because TaskIsland is already installed on this PC. The 10-minute Defender soak was **not** repeated.

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

- `TaskIsland-Setup-1.0.1-x64.exe`: `12577FF0426C6DC57FBA0C1B0103F3A2B470292B7A8453DBD5024BC68717B882`
- `TaskIsland-1.0.1-win-x64-portable.zip`: `6B43D83994C35F1AF76F1A20FF0CADD1111CDE921CC756D4FA9CD1304AB7D3E1`

---

Developed by **RieSources** · https://rie-sources.com
