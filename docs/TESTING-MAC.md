# TaskIsland for Mac: test checklist

Plan: [PLAN-MAC.md](PLAN-MAC.md). Tick a row only when it was really tested on a real Mac.
✅ passed · ❌ failed · ⏳ not tested yet

**Test Mac:** ⏳ model, chip and macOS version not recorded yet.

## Phase M0: spike (go/no-go)

### Done automatically on GitHub's Mac (macOS 15.8.2, Apple Silicon, virtual machine, 2026-11-05, run 41983923819)

| Check | Result |
|---|---|
| 438 Core and Data tests on macOS, SQLite included | ✅ all pass |
| `.dmg` builds for Apple Silicon and Intel, ad-hoc signature verifies | ✅ (Intel file built only, never run) |
| App starts from the mounted `.dmg` | ✅ |
| Window level and Spaces settings accepted | ✅ level 25, behaviour 81 read back |
| Window can take keyboard focus and hold text | ✅ key window, focused text box |
| Global hotkey registers | ✅ result 0, but never pressed there |
| Menu bar icon created | ✅ created, not seen |
| Morph smoothness | 🟡 closing is smooth (16.5 ms per frame). **First opening hitches: worst frame 287 ms** |
| Idle CPU, face on / off | 2.0 % / 0.4 % of one core over 20 s (a virtual machine, so only a rough guide) |
| Memory | About 145 MB, in the same range as the Windows app |

Things seen there to deal with later:
- The system reports the app's name as "Avalonia Application" instead of the bundle name.
- Login item status was `notFound` when run from the `.dmg`. It must be retested from Applications (row 11).

Known spike bug, not fixed yet: when the panel is open, the eyes stay in the middle of it, over the log text.

### On your Mac

Setup: download `TaskIsland-Spike-dmg` from the
[GitHub run](https://github.com/Riekie3/Dynamic-Island-To-Do-List/actions/runs/41983923819) (sign in to GitHub; the link lasts
30 days). Use `osx-arm64` for an Apple M chip, `osx-x64` for Intel. Open the `.dmg` and drag the app to Applications.

| # | Do this | Expected | Result |
|---|---|---|---|
| 1 | Open the app from Applications | Blocked. System Settings > Privacy & Security > **Open Anyway**, confirm, password. Then it opens | ⏳ |
| 2 | Look at the top of the screen | Dark pill with two eyes, centred **below** the menu bar and notch, nothing hidden. Around the pill is see-through. No Dock icon | ⏳ |
| 3 | Look at the menu bar | Small pill icon that suits the menu bar colour; its menu opens | ⏳ |
| 4 | Swipe to another desktop (Space) | The pill stays on screen and does not slide | ⏳ |
| 5 | Make an app full screen. Then use the menu: "Toggle show over full-screen apps" | Pill hidden in full screen; after the toggle it shows there | ⏳ |
| 6 | Type in Notes while moving the mouse over the pill | Typing carries on in Notes | ⏳ |
| 7 | Click just beside the pill, on the window under it | The click reaches that window | ⏳ |
| 8 | From Notes, click the pill **once** | Panel opens on the first click and typing goes into its box | ⏳ |
| 9 | Click back in Notes. Reopen and press Esc | Panel closes both ways, and you can type in Notes straight away | 🟡 Esc closes and it reopens (owner, 2026-11-05). Click-away and typing in Notes afterwards not tested |
| 10 | Press Control+Option+T in any app | Panel opens and closes, with **no** permission prompt | 🟡 Opens and closes (owner, 2026-11-05). No prompt not confirmed yet |
| 11 | Panel: **Login item ON**. Check System Settings > General > Login Items. Restart the Mac | Listed there and starts after the restart. Turn it OFF after. If it fails: **LaunchAgent ON**, restart, then OFF | 🟡 Register and unregister both returned OK with the ad-hoc signature (log photo). Restart not tested |
| 12 | With a second screen: menu > "Move to next screen" | Pill moves to the other screen, centred below its menu bar | ⏳ |
| 13 | Open and close the panel a few times, by eye | Smooth apart from perhaps the first opening | ⏳ |
| 14 | Leave it alone for 5 minutes | Log lines say `idle:` with low CPU | ⏳ |

Send back: the result of each row, the Mac model and macOS version, and the log. This opens the log:

```bash
open ~/Library/Logs/TaskIslandSpike.log
```

Afterwards: turn Login item and LaunchAgent OFF, choose Quit in the menu, and drag the app to the Bin.

**Go/no-go:** rows 2, 4, 6, 8, 9 and 10 must pass, or the plan's choice of Avalonia (D1) is revisited.

## Phase M5: reliability

Rows added when M5 starts.
