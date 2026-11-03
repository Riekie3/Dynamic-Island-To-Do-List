# TaskIsland — Test Checklist

Legend: ✅ passed · ❌ failed (see DEVLOG) · ⏳ not tested yet · 👤 must be tested by the user
**Who** column: `A` = the AI agent can test it, `U` = needs the user (physical action / reboot / sleep).

Only mark ✅ after actually testing. Record date + build/commit in the Notes column.

## User interface

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| UI1 | Collapsed pill appears top-centre of the selected monitor | A | ✅ | 2026-11-03 Phase 2: primary monitor, centre x 960 of 1920, top = TopMargin (8 DIP at 125 %). Default changed to 0 (flush) later on 2026-11-03: window top y = 0 checked live. |
| UI2 | Click pill → expands smoothly (no flicker / window jump) | A | ⏳ | 2026-11-03 Phase 2: click expands, window left/top unchanged (no jump). First expand still skips ~100 ms of frames (DEVLOG open issue); later expands not frame-checked. |
| UI3 | ✕, Esc, click outside → collapses smoothly | A | ✅ | 2026-11-03 Phase 2: Esc (empty box), ✕ and click outside each collapse; Esc with text clears the text first. Scripted mouse/keyboard test. |
| UI4 | Expanded panel stays inside screen bounds (also with HorizontalOffset at ±400) | A | ⏳ | 2026-11-03 Phase 2: centre clamp is in IslandPositioner; ±400 not tested yet. |
| UI5 | Visual match vs `docs/design-reference.webp` (screenshots in docs/screenshots) | A | ✅ | 2026-11-03 Phase 2: matches design-reference-1.webp; see docs/screenshots/phase2-panel-*.png. |
| UI6 | Transparent area around pill is click-through (desktop icon below is clickable) | A | ✅ | 2026-11-03 Phase 2: WindowFromPoint beside the pill and 50 DIP below it returns the window underneath, not TaskIsland. |
| UI7 | Hover / peek never steals focus from the app you are typing in | A | ✅ | 2026-11-03 Phase 2: Notepad stayed the foreground window during 1.9 s of hover (Curious → Info → peek). |
| UI8 | Not in Alt-Tab, not on the taskbar | A | ✅ | 2026-11-03 Phase 2: ex-style TOOLWINDOW set, APPWINDOW cleared (checked with GetWindowLongPtr). |
| UI9 | "All Done" shown when 0 outstanding; "1 task" singular | A | ⏳ | 2026-11-03 Phase 2: text logic in IslandViewModel ("All done", "1 task"); not seen in the UI yet (sample has 3 open). |
| UI10 | Peek on hover shows top task; toggle off works | A | ⏳ | 2026-11-03 Phase 2: peek shows "Finish project report" with its list dot (phase2-collapsed-states.png). Toggle off not tested (no Settings UI yet). |
| UI11 | Light theme, dark theme, System theme follows Windows | A | ⏳ | 2026-11-03 Phase 2: dark ✅ and light ✅ (phase2-panel-dark/light.png). System theme follow arrives in Phase 5. |
| UI12 | Animations off → instant transitions | A | ⏳ | not tested yet |
| UI13 | Modern look per PLAN §6.0–6.1 in dark + light: progress ring, hover-only row actions (also shown on keyboard focus), sliding filter indicator, focus ring visible | A | ⏳ | 2026-11-03 Phase 2: progress ring, sliding filter indicator, hover-only row actions built; keyboard-focus actions and focus ring not checked yet. |
| UI14 | Reduced motion turns on by itself when Windows "Animation effects" is off, and on Battery saver | A | ⏳ | 2026-11-03 Phase 2: implemented (MotionPolicy); needs Windows "Animation effects" off / Battery saver on to test. |
| UI15 | Pill doesn't move by a single pixel when the window switches between collapsed and expanded size | A | ✅ | 2026-11-03 Phase 2: window left/top identical collapsed (565×80 px) and expanded (565×750 px); collapsed rect identical after collapse. |

## Mascot face (PLAN §6.8)

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| M1 | Idle shows the Mini notch with two dots; nothing moves while idle | A | ✅ | 2026-11-03 Phase 2: Mini notch with two dots; 0.0 ms CPU over 61 s idle. |
| M2 | All 12 expressions match `docs/design/mascot-reference.webp` (side-by-side screenshots saved) | A | ✅ | 2026-11-03 Phase 2: docs/screenshots/phase2-faces-vs-reference.png. |
| M3 | Mini → Reaction: dots turn into the eyes smoothly; returns to Mini after 2.5 s (alerts 5 s) | A | ⏳ | 2026-11-03 Phase 2: dots move into the eye positions; Wink greeting returned to Mini after 2.51 s (log). 5 s alert hold only unit-tested. |
| M4 | Each trigger shows the right face: tick Happy, add Wink, last task Excited, High tick Love, hover Curious, click face Laughing, reminder Surprised, import Thinking, error Sad, typing Focused (panel), 60 min untouched Sleepy | A | ⏳ | 2026-11-03 Phase 2: hover → Curious ✅ (log). Click face → Laughing not tested; task/reminder/import/error triggers come in Phases 3–5. |
| M5 | Blink, smile pop, accessory pop, heart beat, shake etc. play once and stop | A | ⏳ | 2026-11-03 Phase 2: built as finite key frames; idle CPU 0 after the Wink shows its clocks ended. Not checked frame by frame for every face. |
| M6 | Random pops follow `IdleReactions` (Off / Rare / Normal / Lively), never Sad/Surprised, never twice in a row | A | ⏳ | 2026-11-03 Phase 2: rules unit-tested (never Sad/Surprised, never twice, intervals per level); live intervals not observed. |
| M7 | No random pops while panel open, hidden, paused, full-screen, locked, display off, Battery saver, reduced motion | A | ⏳ | 2026-11-03 Phase 2: panel open, hidden, locked, reduced motion, Battery saver wired; full-screen and display-off in Phase 4. Not tested live. |
| M8 | `MascotEnabled` off → classic count pill, no faces | A | ✅ | 2026-11-03 Phase 2: --no-mascot shows the classic black count pill (phase2-collapsed-states.png) and the tick icon in the panel header. |
| M9 | Faces stay sharp at 100 / 150 / 200 % scaling | A+U | ⏳ | 2026-11-03 Phase 2: 125 % only (vector paths). |
| M10 | 30 min idle with `IdleReactions=Normal` → average CPU < 0.05 % | A | ⏳ | not tested yet (with `Companion=Off`; the companion has its own budget, M11) |
| M11 | Companion: dots blink/glance/breathe between reactions, faces keep moving, nothing loops; pauses under reduced motion | A | ✅ | 2026-11-03: screen pixels of the notch show the eyes closing fully on Blink, the eye centre moving 3 px on Glance and ~1 px on Breathe, at the times the Debug log gives. Release CPU 60 s at rest (no mouse near): Off 0 ms, Calm 1.1–1.6 s (1.8–2.6 % of one core), Lively 1.8–2.1 s (3.1–3.5 %). Under Windows "Animation effects" off nothing plays (by design). |

## Automated tests (not part of the manual checklist)

Phase 1 (2026-11-01): `dotnet test TaskIsland.sln -c Release` → **218 passed, 0 failed**.
- `TaskIsland.Core.Tests` (101): sorting, filters, stats and percent rounding, reminder logic, settings defaults/clamping,
  JSON export/import round-trip and every validation failure.
- `TaskIsland.Data.Tests` (117, real temp SQLite files): migrations, task CRUD, soft delete/restore/purge, reorder,
  priority/list moves, lists rules (duplicates, max 20, last list, delete+move), settings, backup/restore/prune/daily
  backup, import Merge/Replace, persistence after reopening, commit-before-crash check.
Phase 2 (2026-11-03): **249 passed** (+31 Core: `ReactionRules`, `DueLabel`, `MascotEnabled`/`IdleReactions` settings).

These cover the logic behind T1–T15, L2–L10, L13, D1–D3 and D5–D10, but those rows stay ⏳ until verified in the UI.

## Task management

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| T1 | Add task via Enter and via → button; default priority Medium | A | ⏳ | |
| T2 | Empty / whitespace title is rejected | A | ⏳ | |
| T3 | Choose High / Medium / Low before adding | A | ⏳ | |
| T4 | Edit title, priority, notes, due date+time, list → Save persists | A | ⏳ | |
| T5 | Edit → Cancel discards changes | A | ⏳ | |
| T6 | Delete → Undo restores; after 5 s it's permanent | A | ⏳ | |
| T7 | Tick → blue tick + strikethrough → slides out of Active after 2 s | A | ⏳ | |
| T8 | Untick within 2 s cancels slide-out; untick in Completed view works | A | ⏳ | |
| T9 | Sorting High → Medium → Low; stable order within group | A | ⏳ | |
| T10 | Drag reorder within a priority group persists after restart | A | ⏳ | |
| T11 | Drag into another priority group is blocked | A | ⏳ | |
| T12 | Filters Active / Completed / All; filter remembered | A | ⏳ | |
| T13 | Counters (pill, header, footer, %) update instantly | A | ⏳ | |
| T14 | Clear completed → confirm → Undo works | A | ⏳ | |
| T15 | Due reminder fires once at due time (balloon + pulse); overdue shown red | A | ⏳ | |

## Lists (groups)

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| L1 | First run shows starter lists *Personal* + *Office Work*, no tasks | A | ⏳ | Phase 1: data layer + skeleton window verified (launched exe: "2 lists (Personal, Office Work), 0 tasks"). Still to check in the real island UI (Phase 2/3). |
| L2 | Create list (name + colour) → appears in switcher, persists after restart | A | ⏳ | |
| L3 | Duplicate name (any case) rejected; 21st list rejected | A | ⏳ | |
| L4 | Rename, recolour, move left/right (chip menu and Settings → Lists) | A | ⏳ | |
| L5 | Switch list → only its tasks shown; selection remembered after restart | A | ⏳ | |
| L6 | *All lists* shows every task with list dot + name, High tasks first | A | ⏳ | |
| L7 | Quick add in a list adds there; in *All lists* uses the chosen/default list | A | ⏳ | |
| L8 | Edit → change List moves the task | A | ⏳ | |
| L9 | Delete list → "move tasks" keeps tasks in target list; "delete tasks" removes them; backup made first | A | ⏳ | |
| L10 | Last remaining list cannot be deleted | A | ⏳ | |
| L11 | Chip counts + pill count correct (PillCountScope All / Selected) | A | ⏳ | |
| L12 | Ctrl+0…9 and Ctrl+Tab switch lists | A | ⏳ | |
| L13 | Export/import keeps lists and which task belongs to which list | A | ⏳ | |

## Windows integration

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| W1 | Runs in background; ✕ / Alt+F4 never exit the app | A | ⏳ | |
| W2 | Tray: every menu item works (Open, Expand/Collapse, Show/Hide, Pause/Resume, Settings, Exit) | A | ⏳ | |
| W3 | Exit leaves no ghost tray icon and no process | A | ⏳ | |
| W4 | Second launch → no 2nd process, first one expands | A | ⏳ | |
| W5 | HKCU Run value created on first run; removed when toggled off | A | ⏳ | |
| W6 | **Reboot → log in → TaskIsland appears collapsed automatically** | U | 👤 | |
| W7 | **Sleep (lid close / Start → Sleep) → wake → notch in place, tray icon present, data intact** | U | 👤 | |
| W8 | **Hibernate → resume → same as W7** | U | 👤 | |
| W9 | Screen off (wait for display timeout) → wake → notch in place | U | 👤 | |
| W10 | Lock (Win+L) → unlock → notch in place | A+U | ⏳ | |
| W11 | **Plug in external monitor / unplug it → notch moves to correct screen** | U | 👤 | |
| W12 | Change resolution → notch re-centres | A+U | ⏳ | |
| W13 | Scaling 100 / 125 / 150 / 200 % → correct size & position | A+U | ⏳ | |
| W14 | Choose a different monitor in Settings → moves immediately | U | 👤 | needs 2 screens |
| W15 | Full-screen video / F11 / PowerPoint slideshow → hides, then returns | A | ⏳ | |
| W16 | Ctrl+Alt+T toggles; rebinding works; conflict warning shown | A | ⏳ | |
| W17 | Restart explorer.exe → tray icon returns | A | ⏳ | |
| W18 | No network connections (`Get-NetTCPConnection -OwningProcess …` empty) | A | ⏳ | |

## Data

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| D1 | Tasks saved to `%LocalAppData%\TaskIsland\Data\taskisland.db` | A | ⏳ | |
| D2 | Tasks preserved after app restart | A | ⏳ | |
| D3 | Tasks preserved after `taskkill /f` (crash simulation) | A | ⏳ | |
| D4 | **Tasks preserved after Windows restart** | U | 👤 | |
| D5 | Export JSON → Import (Merge) skips duplicates | A | ⏳ | |
| D6 | Import (Replace) makes backup first, then replaces | A | ⏳ | |
| D7 | Invalid JSON files rejected with clear message, nothing imported | A | ⏳ | |
| D8 | Backup now → restore → data matches; pre-restore copy created | A | ⏳ | |
| D9 | Daily auto-backup created, max 7 kept | A | ⏳ | |
| D10 | All settings persist after restart | A | ⏳ | |

## Performance

Targets from PLAN §6.7. Record the measured number in Notes, every phase from Phase 2 on.

| # | Test | Target | Result | Notes |
|---|---|---|---|---|
| P1 | Idle CPU (collapsed, 60 s) | 0.0 % | ✅ | 2026-11-03 Phase 2: 0.0 ms CPU time over 61 s (Release, collapsed); 0.0 ms over 30 s after a real open/close. |
| P2 | Private memory idle (collapsed, after 60 s) | ≤ 50 MB | ❌ | 2026-11-03 Phase 2: 52–56 MB before the panel is built; 72–73 MB with it built (current build); 129 MB after the text box had focus once (Windows text input + UI Automation). See DEVLOG. |
| P2b | Working set idle | ≤ 70 MB | ❌ | 2026-11-03 Phase 2: 98–102 / 127 / 138 MB at the same moments. Mostly WPF + graphics driver. See DEVLOG. |
| P3 | Cold start to visible pill | < 1.2 s | ✅ | 2026-11-03 Phase 2: 0.84–0.93 s to visible window (3 runs, Release, not ReadyToRun). |
| P4 | Animations smooth on this laptop | no visible stutter | ⏳ | 2026-11-03 Phase 2: morph smooth after the first expand in Debug log; the first expand skips ~100 ms (open issue). |
| P5 | GPU idle (Task Manager GPU column) | 0 % | ✅ | 2026-11-03 Phase 2: GPU Engine counter 0 % in 6 samples over 54 s idle (counter, not a Task Manager screenshot). |
| P6 | Efficiency mode shown in Task Manager while collapsed; off on hover/expand | leaf icon | ✅ | 2026-11-03 Phase 2: EcoQoS on after 16 s at rest (GetProcessInformation state=1). Off on hover/expand is in code; Task Manager leaf not screenshotted. |
| P7 | 10 min idle → CPU time unchanged, private bytes not growing | no leak | ⏳ | |
| P8 | 50 expand/collapse cycles → memory back within 5 MB | no leak | ⏳ | |
| P9 | 500 tasks in DB → idle targets still met, list scrolls smoothly | P1/P2 hold | ⏳ | |
| P10 | Hidden (full-screen app / paused) → 0 % CPU and GPU | 0 % | ⏳ | |
| P11 | No disk writes while idle (Resource Monitor, 60 s) | none | ⏳ | |

## Security software (PLAN §7.11)

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| S1 | Code search finds no banned APIs (hooks, process access, shells, network, `Assembly.Load(byte[])`) | A | ✅ | 2026-11-03 Phase 2: grep over src/ for hooks, OpenProcess/memory/remote thread, Assembly.Load(, shells, network APIs, Process.Start: no hits. |
| S2 | Defender custom scan of `publish\win-x64` and the installer → no threats (signatures updated first) | A | ⏳ | |
| S3 | 10 min of use with Defender on → nothing in Windows Security → Protection history | A | ⏳ | |
| S4 | Install, run, uninstall → no UAC prompt, no firewall prompt | A | ⏳ | |
| S5 | Startup entry visible and switchable in Task Manager → Startup apps; turned off there → TaskIsland doesn't re-add it | A | ⏳ | |
| S6 | Controlled Folder Access on → app runs normally; export to Documents shows the friendly message; Downloads works | A | ⏳ | |
| S7 | All exe/dll files show Company "RieSources", product, version and description in file Properties | A | ⏳ | |
| S8 | Signed build only: `Get-AuthenticodeSignature` = Valid on exe, dlls, installer, uninstaller | A | ⏳ | n/a if unsigned |
| S9 | VirusTotal scan (only with the user's OK) → 0 detections or false positives reported | A+U | ⏳ | optional |

## Deployment

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| R1 | `dotnet build` + `dotnet test` green | A | ✅ | Phase 1, 2026-11-01: 218 tests. Phase 2, 2026-11-03: Release + Debug 0 warnings / 0 errors, **249 pass** (132 Core + 117 Data). Re-run after every phase. |
| R2 | Self-contained publish runs on its own | A | ⏳ | |
| R3 | Installer installs without admin | A | ⏳ | |
| R4 | Upgrade install keeps tasks | A | ⏳ | |
| R5 | Uninstall removes app, Start menu entry and Run value; asks about data (default keep) | A | ⏳ | |
| R6 | **Install on a 2nd Windows 11 PC (optional)** | U | 👤 | |
