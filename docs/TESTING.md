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
| UI9 | "All Done" shown when 0 outstanding; "1 task" singular | A | ✅ | 2026-11-04 Phase 3: pill text "1 task", "2 tasks" and "All done" confirmed in rendered --snap images (the pill has no UI Automation text). |
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
| M4 | Each trigger shows the right face: tick Happy, add Wink, last task Excited, High tick Love, hover Curious, click face Laughing, reminder Surprised, import Thinking, error Sad, typing Focused (panel), 60 min untouched Sleepy | A | ⏳ | 2026-11-03 Phase 2: hover → Curious ✅ (log). Click face → Laughing not tested; task/reminder/import/error triggers come in Phases 3–5. 2026-11-04 Phase 3: task triggers work (debug log): typing/editing Focused, add Wink, tick Happy, High tick Love, 5th tick in 30 min Love, last task Excited, untick no face, save failure Sad. Still open: click face Laughing, reminder Surprised (Phase 4), import Thinking (Phase 5), 60 min Sleepy. 2026-11-04 Phase 4: reminder → Surprised ✅ (debug log). Still open: click face Laughing, import Thinking (Phase 5), 60 min Sleepy. |
| M5 | Blink, smile pop, accessory pop, heart beat, shake etc. play once and stop | A | ⏳ | 2026-11-03 Phase 2: built as finite key frames; idle CPU 0 after the Wink shows its clocks ended. Not checked frame by frame for every face. |
| M6 | Random pops follow `IdleReactions` (Off / Rare / Normal / Lively), never Sad/Surprised, never twice in a row | A | ⏳ | 2026-11-03 Phase 2: rules unit-tested (never Sad/Surprised, never twice, intervals per level); live intervals not observed. |
| M7 | No random pops while panel open, hidden, paused, full-screen, locked, display off, Battery saver, reduced motion | A | ⏳ | 2026-11-03 Phase 2: panel open, hidden, locked, reduced motion, Battery saver wired; full-screen and display-off in Phase 4. Not tested live. 2026-11-04 Phase 4: full-screen and display-off now set the suppression too; random pops were not observed live in those states. |
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

Phase 3 (2026-11-04): **324 passed** (+39 Core: `DueInput`, `ReorderRules`, `TickStreak`).

Phase 4 (2026-11-04): **368 passed** (+42 Core in `Phase4RulesTests`: hotkey text, Run-key policy, monitor choice and placement,
full-screen rule, reminder text and timer; +2 Data: start-up restore of a damaged database).
UI scripts for Phase 4 (no mouse): `p4a` single instance / Run key / hotkey / pipe / Exit, `p4b` reminders, recovery from posted
power and display messages, TaskbarCreated, tray menu and clicks, `p4c` database error dialog (damaged and locked file, open
folder, restore), `p4d` full screen (covers the screen briefly), `p4e` real Explorer restart, `p4f` Alt+F4, network, paused CPU,
hotkey conflict. `p4d` and `p4e` interrupt the user: run them only with the user's OK.

The UI checks marked pass in Phase 3 were run with UI Automation scripts (no mouse): see `tools/ui-tests/` (Debug build, a temp `TASKISLAND_ROOT`, `python -I` to read the DB).

These cover the logic behind T1–T15, L2–L10, L13, D1–D3 and D5–D10, but those rows stay ⏳ until verified in the UI.

## Task management

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| T1 | Add task via Enter and via → button; default priority Medium | A | ✅ | 2026-11-04 Phase 3: by UI Automation, no mouse: the → button and the real Enter key both add; default priority Medium; row saved to the DB. |
| T2 | Empty / whitespace title is rejected | A | ✅ | 2026-11-04 Phase 3: empty and whitespace titles are not added (no row, nothing in the DB). |
| T3 | Choose High / Medium / Low before adding | A | ✅ | 2026-11-04 Phase 3: priority chip cycles Medium → High → Low; the chosen priority is saved. It stays selected after an add (by design). |
| T4 | Edit title, priority, notes, due date+time, list → Save persists | A | ✅ | 2026-11-04 Phase 3: title, priority, notes, due date + time and list all saved and read back from the DB. Date by the own MiniCalendar (see PLAN as-built notes); a time without a date and a bad time are refused with a message. |
| T5 | Edit → Cancel discards changes | A | ✅ | 2026-11-04 Phase 3: Cancel (and Esc) discards the draft; DB unchanged. |
| T6 | Delete → Undo restores; after 5 s it's permanent | A | ✅ | 2026-11-04 Phase 3: Undo bar restores the task; after 5 s the soft-deleted row is purged from the DB. |
| T7 | Tick → blue tick + strikethrough → slides out of Active after 2 s | A | ✅ | 2026-11-04 Phase 3: tick keeps the row for 2 s (strike-line overlay), then it slides out of Active; completed state saved. The animation frames were not watched (render-only code, ran without errors). |
| T8 | Untick within 2 s cancels slide-out; untick in Completed view works | A | ✅ | 2026-11-04 Phase 3: untick within 2 s keeps the row in Active; untick in the Completed view works. |
| T9 | Sorting High → Medium → Low; stable order within group | A | ✅ | 2026-11-04 Phase 3: High, Medium, Low, stable within a group; checked in the UI row order and in the DB. |
| T10 | Drag reorder within a priority group persists after restart | A | ⏳ | 2026-11-04 Phase 3: keyboard reorder (Alt+Up/Down) works: persisted and survives a restart. The real mouse drag with the insertion line is NOT tested (needs the mouse; ask the user). |
| T11 | Drag into another priority group is blocked | A | ⏳ | 2026-11-04 Phase 3: group rule works by keyboard (Alt+Down stops at the group edge; reorder is off in All lists). The "No" cursor on a mouse drag into another group is NOT tested (needs the mouse). |
| T12 | Filters Active / Completed / All; filter remembered | A | ✅ | 2026-11-04 Phase 3: Active / Completed / All; the filter is remembered after a restart and after taskkill /f. |
| T13 | Counters (pill, header, footer, %) update instantly | A | ✅ | 2026-11-04 Phase 3: footer, list chip, header badge and pill ("1 task", "2 tasks", "All done", read from rendered --snap images) update at once on add / tick / delete / undo. |
| T14 | Clear completed → confirm → Undo works | A | ✅ | 2026-11-04 Phase 3: Clear completed shows a confirm card (with the count), Cancel keeps everything, Confirm removes and the Undo bar restores them. |
| T15 | Due reminder fires once at due time (balloon + pulse); overdue shown red | A | ✅ | 2026-11-04 Phase 4: reminders run in the app (Debug, temp data): a task due in 45 s fired once on time ("⏰ Due now: Soon task", list name as heading), the island pulse played and the Surprised face showed (logs); two tasks already overdue at start gave one combined "⏰ 2 tasks overdue"; each is marked once in the DB; a task due tomorrow was left alone. Clicking the balloon opened the panel on that task's list. I did not watch the toast or the pulse on screen (checked from the logs). Overdue red label: Phase 3. |
| T15b | Pill alert (Phase 5): when a reminder fires the collapsed island widens and shows the task title for 5 s, then shrinks; hover keeps it open; click opens that task's list; not shown while the panel is open or the island is hidden; toggle in Settings turns it off | A | ⏳ | planned 2026-11-04 (user decision) |
| T15c | Early reminder (Phase 5): "Remind me before due time" = 1 hour → task due in 2 h gets "In 1 hour: <title>" once at 1 h before, plus "Due now" at the due time; none for date-only tasks or tasks already inside the window; editing the due time re-arms both; survives a restart | A | ⏳ | planned 2026-11-04 (user decision) |
| T15d | Reminder lead and pill settings persist after restart and apply live | A | ⏳ | |

## Lists (groups)

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| L1 | First run shows starter lists *Personal* + *Office Work*, no tasks | A | ⏳ | Phase 1: data layer + skeleton window verified (launched exe: "2 lists (Personal, Office Work), 0 tasks"). Still to check in the real island UI (Phase 2/3). |
| L2 | Create list (name + colour) → appears in switcher, persists after restart | A | ✅ | 2026-11-04 Phase 3: new list appears as a chip, becomes selected, takes new tasks and is still there after a restart. |
| L3 | Duplicate name (any case) rejected; 21st list rejected | A | ✅ | 2026-11-04 Phase 3: duplicate names in any case are refused with a message; the 20-list limit is enforced. |
| L4 | Rename, recolour, move left/right (chip menu and Settings → Lists) | A | ⏳ | 2026-11-04 Phase 3: rename, recolour (Change colour opens the editor card) and move left/right work from the chip menu (opened with Shift+F10). Settings > Lists is Phase 5, so not testable yet. |
| L5 | Switch list → only its tasks shown; selection remembered after restart | A | ✅ | 2026-11-04 Phase 3: switching list shows only its tasks; the selection is remembered after a restart. |
| L6 | *All lists* shows every task with list dot + name, High tasks first | A | ✅ | 2026-11-04 Phase 3: "All" shows every task with the list dot + name, High first. |
| L7 | Quick add in a list adds there; in *All lists* uses the chosen/default list | A | ✅ | 2026-11-04 Phase 3: quick add goes to the selected list; in All lists it uses the quick-add list chip (default list otherwise). |
| L8 | Edit → change List moves the task | A | ✅ | 2026-11-04 Phase 3: changing the List chip in the edit form moves the task and it is saved. |
| L9 | Delete list → "move tasks" keeps tasks in target list; "delete tasks" removes them; backup made first | A | ✅ | 2026-11-04 Phase 3: "delete tasks" and "move tasks" both checked; a pre-delete backup is made first and holds the deleted list and its tasks; deleting the default list moves the default setting; the selection falls back to All. |
| L10 | Last remaining list cannot be deleted | A | ✅ | 2026-11-04 Phase 3: the last remaining list cannot be deleted; the message is shown and the list is still there. |
| L11 | Chip counts + pill count correct (PillCountScope All / Selected) | A | ⏳ | 2026-11-04 Phase 3: chip counts and the pill count are right. PillCountScope Selected vs All is a Phase 5 setting, so the Selected variant is not checked. |
| L12 | Ctrl+0…9 and Ctrl+Tab switch lists | A | ✅ | 2026-11-04 Phase 3: Ctrl+0..9 and Ctrl+Tab switch lists. |
| L13 | Export/import keeps lists and which task belongs to which list | A | ⏳ | |

## Windows integration

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| W0 | Start-up error dialog: damaged or locked database → Open data folder / Restore from backup / Exit; nothing deleted | A | ✅ | 2026-11-04 Phase 4: damaged file → dialog; Exit ends with code 1 and leaves the file untouched; Open data folder opens Explorer there and the dialog returns; Restore from backup picks a backup, keeps the damaged file as Backups\broken-*.db and starts with the backup's tasks. A locked file (another program holding an exclusive lock) shows the same dialog after about 10 s. |
| W1 | Runs in background; ✕ / Alt+F4 never exit the app | A | ✅ | 2026-11-04 Phase 4: Alt+F4 on the open panel only collapses; the process keeps running (UI Automation + real Alt+F4 key). ✕ (Phase 2) does the same. |
| W2 | Tray: every menu item works (Open, Expand/Collapse, Show/Hide, Pause/Resume, Settings, Exit) | A | ⏳ | 2026-11-04 Phase 4: Open, Expand/Collapse, Show/Hide, Pause/Resume and Exit all work (menu opened through the NotifyIcon's own callback message, items invoked by UI Automation); left-click toggles, double-click opens; Pause also blocks auto-show after a wake and Open still works while paused. "Settings…" is not in the menu yet: the Settings window is Phase 5, and a button that does nothing is not allowed. Not tried with the real mouse. |
| W3 | Exit leaves no ghost tray icon and no process | A | ⏳ | 2026-11-04 Phase 4: Exit (tray and panel menu) ends the process with code 0 and the log shows the tray icon disposed. The icon sits in the hidden-icons flyout here, so its disappearance was not seen on screen. |
| W4 | Second launch → no 2nd process, first one expands | A | ✅ | 2026-11-04 Phase 4: second launch exits with code 0, the first instance expands and takes the keyboard focus; one TaskIsland process. A pipe message other than EXPAND is ignored and logged. |
| W5 | HKCU Run value created on first run; removed when toggled off | A | ✅ | 2026-11-04 Phase 4: first run writes HKCU\…\Run\TaskIsland = "<exe>" --startup (real key checked once, shown as a Windows startup entry, then removed); setting off removes it at the next launch; a value removed elsewhere is not re-added; an old path is rewritten. Test runs use a separate test key. The Settings toggle itself is Phase 5. |
| W6 | **Reboot → log in → TaskIsland appears collapsed automatically** | U | 👤 | |
| W7 | **Sleep (lid close / Start → Sleep) → wake → notch in place, tray icon present, data intact** | U | 👤 | 2026-11-04 Phase 4: not tested by the user yet. Agent check: a posted PBT_APMRESUMEAUTOMATIC + WM_DISPLAYCHANGE give one debounced recovery (reposition, styles, topmost, tray, hotkey, reminders, daily backup) and follow-ups at +2 s and +6 s; PBT_APMSUSPEND collapses the open panel. |
| W8 | **Hibernate → resume → same as W7** | U | 👤 | |
| W9 | Screen off (wait for display timeout) → wake → notch in place | U | 👤 | |
| W10 | Lock (Win+L) → unlock → notch in place | A+U | ⏳ | 2026-11-04 Phase 4: lock/unlock code is in place (SessionSwitch → collapse, then recovery); a real lock was not run (the user preferred not to). The same recovery path passed with posted resume/display messages (see W7). |
| W11 | **Plug in external monitor / unplug it → notch moves to correct screen** | U | 👤 | |
| W12 | Change resolution → notch re-centres | A+U | ⏳ | 2026-11-04 Phase 4: not tested (needs the user to change the resolution). Placement maths is unit-tested; WM_DISPLAYCHANGE and DisplaySettingsChanged trigger the recovery. |
| W13 | Scaling 100 / 125 / 150 / 200 % → correct size & position | A+U | ⏳ | 2026-11-04 Phase 4: not tested at other scales (this PC runs 125 %). Placement per monitor DPI is unit-tested; WM_DPICHANGED repositions. |
| W14 | Choose a different monitor in Settings → moves immediately | U | 👤 | needs 2 screens |
| W15 | Full-screen video / F11 / PowerPoint slideshow → hides, then returns | A | ⏳ | 2026-11-04 Phase 4: with test windows on the main screen: a borderless window covering the monitor hides the notch and it comes back when it closes; a maximised window keeps it; an F11-style switch of the same window (no foreground change) hides and restores it; HideInFullScreen off keeps it. A real video, Edge F11 and a PowerPoint slideshow were not tried. |
| W16 | Ctrl+Alt+T toggles; rebinding works; conflict warning shown | A | ⏳ | 2026-11-04 Phase 4: Ctrl+Alt+T toggles (real key press); when another app already holds Ctrl+Alt+T, TaskIsland logs error 1409 and keeps running. Rebinding and the warning in Settings come with Phase 5 (parsing and the two-modifier rule are unit-tested). |
| W17 | Restart explorer.exe → tray icon returns | A | ⏳ | 2026-11-04 Phase 4: real Explorer restart (with the user's OK): TaskbarCreated was handled, the icon was added again, the island stayed visible and topmost. The icon is in the hidden-icons flyout, so I could not see it come back. |
| W18 | No network connections (`Get-NetTCPConnection -OwningProcess …` empty) | A | ✅ | 2026-11-04 Phase 4: Get-NetTCPConnection and Get-NetUDPEndpoint for the TaskIsland process are empty (Release). |

## Data

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| D1 | Tasks saved to `%LocalAppData%\TaskIsland\Data\taskisland.db` | A | ✅ | 2026-11-04 Phase 3: tasks are written to taskisland.db in the data folder (the tests used a temp TASKISLAND_ROOT; the default %LocalAppData% path is unchanged code). |
| D2 | Tasks preserved after app restart | A | ✅ | 2026-11-04 Phase 3: tasks, lists, filter and selection survive a normal close and relaunch. |
| D3 | Tasks preserved after `taskkill /f` (crash simulation) | A | ✅ | 2026-11-04 Phase 3: taskkill /f, then relaunch: data intact, filter and selection remembered. |
| D4 | **Tasks preserved after Windows restart** | U | 👤 | |
| D5 | Export JSON → Import (Merge) skips duplicates | A | ⏳ | |
| D6 | Import (Replace) makes backup first, then replaces | A | ⏳ | |
| D7 | Invalid JSON files rejected with clear message, nothing imported | A | ⏳ | |
| D8 | Backup now → restore → data matches; pre-restore copy created | A | ⏳ | |
| D9 | Daily auto-backup created, max 7 kept | A | ⏳ | 2026-11-04 Phase 4: the daily backup now runs at start-up and after a wake: "taskisland-yyyyMMdd-HHmmss.db" written on first start (log). Keeping 7 is unit-tested only. |
| D10 | All settings persist after restart | A | ⏳ | |

## Performance

Targets from PLAN §6.7. Record the measured number in Notes, every phase from Phase 2 on.

| # | Test | Target | Result | Notes |
|---|---|---|---|---|
| P1 | Idle CPU (collapsed, 60 s) | 0.0 % | ✅ | 2026-11-03 Phase 2: 0.0 ms CPU time over 61 s (Release, collapsed); 0.0 ms over 30 s after a real open/close. 2026-11-04 Phase 3 (Release, seeded data): Companion Off 0 ms CPU over 60 s; Calm 1,672 ms over 60 s = 2.8 % of one core (Phase 2: 1.8-2.6 %). 2026-11-04 Phase 4: Release, seeded data, 60 s: Companion Off 16 ms (twice; only from window switches), Calm 1,125 ms (1.9 % of one core). A version with the planned location-change hook cost 63 ms per 45 s while the PC was in use, so it was replaced (DEVLOG). |
| P2 | Private memory idle (collapsed, after 60 s) | ≤ 50 MB | ❌ | 2026-11-03 Phase 2: 52–56 MB before the panel is built; 72–73 MB with it built (current build); 129 MB after the text box had focus once (Windows text input + UI Automation). See DEVLOG. 2026-11-04 Phase 3: 61.6 MB (Off) / 63.9 MB (Calm) private after 60 s; still over budget (the known Phase 2 issue). 2026-11-04 Phase 4: 64.6–65.0 MB (Off) / 66.6 MB (Calm) private after 60 s; about the same as Phase 3 although WinForms is now loaded for the tray icon. |
| P2b | Working set idle | ≤ 70 MB | ❌ | 2026-11-03 Phase 2: 98–102 / 127 / 138 MB at the same moments. Mostly WPF + graphics driver. See DEVLOG. 2026-11-04 Phase 3: 114 MB (Off) / 118.8 MB (Calm). 2026-11-04 Phase 4: 128 MB (Off) / 132 MB (Calm). |
| P3 | Cold start to visible pill | < 1.2 s | ✅ | 2026-11-03 Phase 2: 0.84–0.93 s to visible window (3 runs, Release, not ReadyToRun). |
| P4 | Animations smooth on this laptop | no visible stutter | ⏳ | 2026-11-03 Phase 2: morph smooth after the first expand in Debug log; the first expand skips ~100 ms (open issue). |
| P5 | GPU idle (Task Manager GPU column) | 0 % | ✅ | 2026-11-03 Phase 2: GPU Engine counter 0 % in 6 samples over 54 s idle (counter, not a Task Manager screenshot). |
| P6 | Efficiency mode shown in Task Manager while collapsed; off on hover/expand | leaf icon | ✅ | 2026-11-03 Phase 2: EcoQoS on after 16 s at rest (GetProcessInformation state=1). Off on hover/expand is in code; Task Manager leaf not screenshotted. |
| P7 | 10 min idle → CPU time unchanged, private bytes not growing | no leak | ⏳ | |
| P8 | 50 expand/collapse cycles → memory back within 5 MB | no leak | ⏳ | |
| P9 | 500 tasks in DB → idle targets still met, list scrolls smoothly | P1/P2 hold | ⏳ |  2026-11-04 Phase 3: 500 tasks: the list is virtualised (7 rows realised), idle CPU about 0-16 ms per 15 s after a start-up burst of about 1.1 s, private 62.1 MB / working set 114.4 MB (memory still over budget). Smooth scrolling NOT judged (needs the mouse wheel and eyes). |
| P10 | Hidden (full-screen app / paused) → 0 % CPU and GPU | 0 % | ⏳ | 2026-11-04 Phase 4: paused (hidden): 0 ms CPU over 30 s (Release). Hidden by a full-screen app and the GPU column not measured. |
| P11 | No disk writes while idle (Resource Monitor, 60 s) | none | ⏳ | |

## Security software (PLAN §7.11)

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| S1 | Code search finds no banned APIs (hooks, process access, shells, network, `Assembly.Load(byte[])`) | A | ✅ | 2026-11-03 Phase 2: grep over src/ for hooks, OpenProcess/memory/remote thread, Assembly.Load(, shells, network APIs, Process.Start: no hits. 2026-11-04 Phase 4: grep again: no banned APIs. The only Process.Start is explorer.exe on the data folder after a click in the start-up error dialog; the only registry writes are the HKCU Run value. New native calls: SetWinEventHook (foreground, out of context), SHAppBarMessage (full-screen notice), RegisterPowerSettingNotification, RegisterHotKey, AllowSetForegroundWindow (second launch). |
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
| R1 | `dotnet build` + `dotnet test` green | A | ✅ | Phase 1, 2026-11-01: 218 tests. Phase 2, 2026-11-03: Release + Debug 0 warnings / 0 errors, **249 pass** (132 Core + 117 Data). Re-run after every phase. Phase 3, 2026-11-04: Release + Debug 0 warnings / 0 errors, **324 pass** (207 Core + 117 Data). Phase 4, 2026-11-04: Release + Debug 0 warnings / 0 errors, **368 pass** (249 Core + 119 Data). |
| R2 | Self-contained publish runs on its own | A | ⏳ | |
| R3 | Installer installs without admin | A | ⏳ | |
| R4 | Upgrade install keeps tasks | A | ⏳ | |
| R5 | Uninstall removes app, Start menu entry and Run value; asks about data (default keep) | A | ⏳ | |
| R6 | **Install on a 2nd Windows 11 PC (optional)** | U | 👤 | |

## Phase 4 — steps for the user (👤)

Use the installed or Release build with your real data. After each step, the log is in
`%LocalAppData%\TaskIsland\Logs\taskisland-<date>.log` (look for "recovery start" / "recovery done").

1. **W6 reboot:** make sure TaskIsland is running once (it registers itself to start with Windows). Restart Windows and log in.
   Expected: the collapsed notch appears at the top centre by itself, no window takes focus, the tray icon is there.
2. **W7 sleep:** with the panel closed, choose Start → Power → Sleep (or close the lid). Wake the PC and log in.
   Expected: within a few seconds the notch is in its place, the tray icon is there, your tasks are unchanged.
3. **W8 hibernate:** same as step 2 with Hibernate (if your PC offers it).
4. **W9 screen off:** leave the PC until the screen turns off, then move the mouse. Expected: the notch is in place.
5. **W10 lock:** press Win+L, then unlock. Expected: the notch is in place; an open panel was closed when locking.
6. **W11 external monitor:** plug a monitor in, then unplug it. Expected: the notch stays on (or returns to) the main screen,
   centred, within about 6 seconds.
7. **W12 / W13 resolution and scaling:** Settings → System → Display: change the resolution, then the scale (100 %, 125 %,
   150 %, and 200 % if offered). Expected: after each change the notch is centred at the top and the right size.
8. **W17 tray after Explorer restart:** open the hidden-icons arrow (^) next to the clock and check the TaskIsland icon is
   there; restart Explorer from Task Manager (Windows Explorer → Restart) and check again.
9. **W3 / W2 tray menu with the mouse:** right-click the tray icon and try Hide, Show, Pause visibility, Resume visibility,
   Expand, then Exit. Expected: each does what it says; after Exit the icon disappears from the tray at once.
10. **W15 real full screen:** play a YouTube video full screen, press F11 in Edge, and start a PowerPoint slideshow.
    Expected: the notch hides each time and comes back when you leave full screen.
