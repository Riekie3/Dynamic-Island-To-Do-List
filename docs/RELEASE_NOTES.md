# TaskIsland 1.1.0 (installer + portable)

A Dynamic Island-style to-do list for Windows 11 (x64), by **RieSources** (https://rie-sources.com).

## What's new since 1.0.8

- **Drag the island anywhere on the screen.** Press the collapsed pill and move it in any direction. Drop it near the
  top, bottom, left or right edge and it sticks there; drop it in the middle and it stays a free, round floating pill.
- **The shape follows the edge.** A top-edge pill is the familiar notch (flat top, rounded bottom); a bottom-edge pill
  is the same upside down; a side-edge pill is flat on that side. The auto-hide sliver tucks towards the attached edge.
- **The panel follows the pill.** Near the bottom the panel opens upwards from the pill. A side-attached pill keeps its
  side flush and the 420-wide panel slides inwards, so it stays on screen. A free pill opens downwards unless there is
  more room above. If the full-height panel does not fit either way (a free or side pill near the middle), it opens to
  the room it has and the task list scrolls, so the panel never runs off the screen.
- **The place is remembered exactly.** Both the horizontal and vertical place are saved and restored after a restart,
  sleep or monitor change. The attached edge is derived from them, so a 125 % screen puts the pill back on the same
  pixel. Settings > Appearance has "Horizontal position" (−4000…+4000) and "Vertical position" (0…8000) sliders.
- A wide reminder alert grows inwards from a side edge instead of being cut off.

## Upgrading

Exit TaskIsland from the tray menu, then run the new installer over the old one. Tasks and settings stay (same data
folder, `%LocalAppData%\TaskIsland`). The installer refuses to run while TaskIsland is open.

## Install (recommended)

1. **If you run an older portable copy:** Exit it from the tray menu first (the installer asks you to close it anyway).
2. Run `TaskIsland-Setup-1.1.0-x64.exe`. It is **not code-signed**, so SmartScreen may say "Windows protected your
   PC": click **More info**, then **Run anyway**. (Smart App Control, if turned on, can block unsigned apps.)
3. It installs to `%LocalAppData%\Programs\TaskIsland`, adds a Start menu entry (desktop icon optional) and starts
   TaskIsland at the end. Your tasks from an older version are found again: the data folder is the same
   (`%LocalAppData%\TaskIsland`).
4. "Start with Windows" moves over to the installed copy on its first start. You can then delete the old portable
   folder (if you run the portable copy again, it takes "Start with Windows" back).

Upgrading later: run a newer installer over the top; tasks and settings stay.

## Or portable

Unzip `TaskIsland-1.1.0-win-x64-portable.zip` somewhere permanent and run `TaskIsland.exe` (same SmartScreen note).
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

- 505 automated tests green (366 Core, 139 Data), including the new free-placement, snap, panel-height and window tests.
- Real-mouse UI test `tools/ui-tests/dragplace.ps1` 37/37 (free middle, all four edges, bottom open-up, side panel
  on-screen, free-middle panel capped to the room below, restart). `p4b.ps1` 39/39 and `p5a.ps1` 23/23, both updated
  to expect the default widening reminder pill; `t1.ps1`, `chipdrag.ps1`, `rowdrag.ps1` and `p5b.ps1` passed.
- The four edge shapes and the open layouts were rendered with the app's `--snap` renderer; the mid-height panel
  overflow found that way is fixed, and the panels end at the screen edge or are capped to the room below with the
  task list scrolling.
- The installer test passed (install without administrator mode, upgrade over the top with tasks kept, uninstall,
  reinstall). The check against the real installed copy was skipped because TaskIsland is already installed on this PC.
- Built from a clean, committed tree (`1.1.0+17d4462`). Microsoft Defender (signatures 1.463.471.0) scanned the
  portable zip, the publish folder and the installer with no threats found. The 10-minute Defender soak was **not**
  repeated.

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

- `TaskIsland-Setup-1.1.0-x64.exe`: `AF5AAEA87E544025369155589D5033493442CF6BC2E88C957173AD7D769D5E13`
- `TaskIsland-1.1.0-win-x64-portable.zip`: `4BB24D1BC1D22ECBFE76E3AB2D393F9CDC77CD316CBCAD6C412E22D28E0E5B86`

---

Developed by **RieSources** — https://rie-sources.com
