# TaskIsland 1.0.0-rc.1 (portable test release)

A Dynamic Island-style to-do list for Windows 11 (x64), by **RieSources** (https://rie-sources.com).

This is a **portable test release**: unzip and run, no installer. The installer comes after a few days of real-world
testing (it will be 1.0.0).

## Run it

1. Download `TaskIsland-1.0.0-rc.1-win-x64-portable.zip` and unzip it somewhere permanent.
2. Double-click `TaskIsland.exe` ("Start with Windows" is on by default and points at that folder, so do not move it afterwards). A small pill appears at the top centre of the screen and a tray icon is added.
3. The file is **not code-signed**, so Windows SmartScreen may say "Windows protected your PC": click **More info**, then
   **Run anyway**. (Smart App Control, if turned on, can block unsigned apps; code signing is a paid certificate.)

No administrator rights, no internet connection, no .NET install needed (the runtime is inside the folder).

## What it does

- A pill/notch with a little face; click it and it stretches into a to-do list, then folds back.
- Lists (Personal, Office Work, your own), priorities, due dates with reminders, drag to reorder, undo for deletes.
- Tray icon, global hotkey Ctrl+Alt+T, start with Windows (optional), one instance only.
- Settings: themes, accent colour, panel opacity, companion face on/off, JSON export/import, daily backups.
- Data stays on your PC in `%LocalAppData%\TaskIsland`. The app makes no network connections.

## Known limits (please tell us what you see)

- **Memory is above the target.** About 64 MB private memory (126 MB working set) at idle; once the list panel has been
  opened it stays around 130-140 MB working set. The target was 50 MB / 70 MB. WPF itself needs a lot of that.
- The random face "pops" use a little CPU each time (about 0.13 % of one core on average over 30 minutes).
- Sleep, hibernate, reboot auto-start, multi-monitor and display-scaling behaviour is being verified in real use.
  If something goes wrong, check `%LocalAppData%\TaskIsland\Logs`.
- Not code-signed (see above).

## Remove it

Settings, General, untick **Start with Windows**; Exit from the tray menu; delete the folder. To also delete your tasks
and backups, delete `%LocalAppData%\TaskIsland`.

---

Developed by **RieSources** · https://rie-sources.com
