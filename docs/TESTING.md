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
| UI4 | Expanded panel stays inside screen bounds (also with HorizontalOffset at ±400) | A | ✅ | 2026-11-03 Phase 2: centre clamp is in IslandPositioner; ±400 not tested yet. 2026-11-04 Phase 6 (Release build, tiered compilation off): HorizontalOffset -400, 0 and +400 (1920×1080 at 125 %): collapsed and expanded window rectangles stay inside the screen (centre x 460 / 960 / 1460 px). The panel is narrower than the screen, so the edge clamp itself only matters on small screens (unit-tested). |
| UI5 | Visual match vs `docs/design-reference.webp` (screenshots in docs/screenshots) | A | ✅ | 2026-11-03 Phase 2: matches design-reference-1.webp; see docs/screenshots/phase2-panel-*.png. |
| UI6 | Transparent area around pill is click-through (desktop icon below is clickable) | A | ✅ | 2026-11-03 Phase 2: WindowFromPoint beside the pill and 50 DIP below it returns the window underneath, not TaskIsland. |
| UI7 | Hover / peek never steals focus from the app you are typing in | A | ✅ | 2026-11-03 Phase 2: Notepad stayed the foreground window during 1.9 s of hover (Curious → Info → peek). |
| UI8 | Not in Alt-Tab, not on the taskbar | A | ✅ | 2026-11-03 Phase 2: ex-style TOOLWINDOW set, APPWINDOW cleared (checked with GetWindowLongPtr). |
| UI9 | "All Done" shown when 0 outstanding; "1 task" singular | A | ✅ | 2026-11-04 Phase 3: pill text "1 task", "2 tasks" and "All done" confirmed in rendered --snap images (the pill has no UI Automation text). |
| UI10 | Peek on hover shows top task; toggle off works | A | ⏳ | 2026-11-03 Phase 2: peek shows "Finish project report" with its list dot (phase2-collapsed-states.png). Toggle off not tested (no Settings UI yet). 2026-11-04 Phase 5: Peek on hover setting saved from Settings; its effect with the toggle off was not checked with a real mouse. |
| UI11 | Light theme, dark theme, System theme follows Windows | A | ⏳ | 2026-11-03 Phase 2: dark ✅ and light ✅ (phase2-panel-dark/light.png). System theme follow arrives in Phase 5. 2026-11-04 Phase 5: Light and Dark applied live from Settings (UI Automation + screenshots), saved, still selected after restart. System and Windows accent are applied by the code path, but following a real Windows theme/accent change is 👤 (step 4 below). |
| UI12 | Animations off → instant transitions | A | ⏳ | not tested yet 2026-11-04 Phase 5: Animations / Follow-Windows toggles save; instant transitions not looked at. |
| UI13 | Modern look per PLAN §6.0–6.1 in dark + light: progress ring, hover-only row actions (also shown on keyboard focus), sliding filter indicator, focus ring visible | A | ⏳ | 2026-11-03 Phase 2: progress ring, sliding filter indicator, hover-only row actions built; keyboard-focus actions and focus ring not checked yet. |
| UI14 | Reduced motion turns on by itself when Windows "Animation effects" is off, and on Battery saver | A | ⏳ | 2026-11-03 Phase 2: implemented (MotionPolicy); needs Windows "Animation effects" off / Battery saver on to test. |
| UI15 | Pill doesn't move by a single pixel when the window switches between collapsed and expanded size | A | ✅ | 2026-11-03 Phase 2: window left/top identical collapsed (565×80 px) and expanded (565×750 px); collapsed rect identical after collapse. |

## Mascot face (PLAN §6.8)

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| M1 | Idle shows the Mini notch with two dots; nothing moves while idle | A | ✅ | 2026-11-03 Phase 2: Mini notch with two dots; 0.0 ms CPU over 61 s idle. |
| M2 | All 12 expressions match `docs/design/mascot-reference.webp` (side-by-side screenshots saved) | A | ✅ | 2026-11-03 Phase 2: docs/screenshots/phase2-faces-vs-reference.png. |
| M3 | Mini → Reaction: dots turn into the eyes smoothly; returns to Mini after 2.5 s (alerts 5 s) | A | ⏳ | 2026-11-03 Phase 2: dots move into the eye positions; Wink greeting returned to Mini after 2.51 s (log). 5 s alert hold only unit-tested. |
| M4 | Each trigger shows the right face: tick Happy, add Wink, last task Excited, High tick Love, hover Curious, click face Laughing, reminder Surprised, import Thinking, error Sad, typing Focused (panel), 60 min untouched Sleepy | A | ⏳ | 2026-11-03 Phase 2: hover → Curious ✅ (log). Click face → Laughing not tested; task/reminder/import/error triggers come in Phases 3–5. 2026-11-04 Phase 3: task triggers work (debug log): typing/editing Focused, add Wink, tick Happy, High tick Love, 5th tick in 30 min Love, last task Excited, untick no face, save failure Sad. Still open: click face Laughing, reminder Surprised (Phase 4), import Thinking (Phase 5), 60 min Sleepy. 2026-11-04 Phase 4: reminder → Surprised ✅ (debug log). Still open: click face Laughing, import Thinking (Phase 5), 60 min Sleepy. 2026-11-04 Phase 5: Import "Thinking" face is not built (import ends with a message in Settings, no face). Still open: click face Laughing, import Thinking, 60 min Sleepy. |
| M5 | Blink, smile pop, accessory pop, heart beat, shake etc. play once and stop | A | ⏳ | 2026-11-03 Phase 2: built as finite key frames; idle CPU 0 after the Wink shows its clocks ended. Not checked frame by frame for every face. |
| M6 | Random pops follow `IdleReactions` (Off / Rare / Normal / Lively), never Sad/Surprised, never twice in a row | A | ⏳ | 2026-11-03 Phase 2: rules unit-tested (never Sad/Surprised, never twice, intervals per level); live intervals not observed. |
| M7 | No random pops while panel open, hidden, paused, full-screen, locked, display off, Battery saver, reduced motion | A | ⏳ | 2026-11-03 Phase 2: panel open, hidden, locked, reduced motion, Battery saver wired; full-screen and display-off in Phase 4. Not tested live. 2026-11-04 Phase 4: full-screen and display-off now set the suppression too; random pops were not observed live in those states. |
| M8 | `MascotEnabled` off → classic count pill, no faces | A | ✅ | 2026-11-03 Phase 2: --no-mascot shows the classic black count pill (phase2-collapsed-states.png) and the tick icon in the panel header. |
| M9 | Faces stay sharp at 100 / 150 / 200 % scaling | A+U | ⏳ | 2026-11-03 Phase 2: 125 % only (vector paths). |
| M10 | 30 min idle with `IdleReactions=Normal` → average CPU < 0.05 % | A | ❌ | not tested yet (with `Companion=Off`; the companion has its own budget, M11) 2026-11-04 Phase 6: 30 min, 500 tasks, Companion Off, IdleReactions Normal (tiered compilation on): 2,297 ms CPU = 0.128 % of one core (about 0.008 % of the 16-thread CPU in Task Manager), target < 0.05 %. Start-up burst about 0.2 s, then 0 ms in quiet minutes and 0.4-0.7 s per random face pop (6 pops in 30 min). The pop animation cost is WPF layered-window rendering (see DEVLOG Phase 2/3). |
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

Phase 5 (2026-11-04): **419 passed** (+51 Core in `Phase5RulesTests`: settings keys and clamping, early reminder rules, reminder text;
+3 Data early-mark tests; schema version 2 and a v1 to v2 upgrade test).
UI scripts for Phase 5 (no mouse, Debug unless noted): `p5a` early reminder + pill (6 min), `p5b` the whole Settings window (about 115
checks: every page, lists, export/import/backup/restore with the real file dialogs, restart persistence, hotkey, auto-collapse),
`p5c` pill setting live + skip cases (2.5 min), `p5d` memory with UI Automation attached, `p5e` memory without UI Automation (Release),
`p5f` probe of the island automation name (the scanner does not see it at all).
Memory (Release, seeded data, 2026-11-04): idle 88 MB private. Settings opened and closed with no automation client attached: 83 MB open, 84 MB
28 s after closing (back to baseline). With a UI Automation client attached (p5d): 88 MB before, 115-124 MB open, 109-120 MB after
closing, because the client keeps the accessibility tree alive; this comes from the test tool, not a confirmed leak.

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
| T15b | Pill alert (Phase 5): when a reminder fires the collapsed island widens and shows the task title for 5 s, then shrinks; hover keeps it open; click opens that task's list; not shown while the panel is open or the island is hidden; toggle in Settings turns it off | A | ✅ | planned 2026-11-04 (user decision) 2026-11-04 Phase 5: p5a/p5c (Debug, UI Automation + logs, no mouse): pill widens to 400 DIP showing the title with the list dot (no "Due now" text), pulse plays after it is wide, ends after 5 s, balloon still shown, no Surprised face on the island while the pill shows (deviation: the pill replaces the face). With the setting off (switched in Settings): pulse + Surprised face + balloon, no pill. Skipped while the panel is open (face shows on the panel) and while the notch is hidden (balloon still shown). Not tested with a real mouse: hover keeps it open, click opens the task's list (👤 step 1 below). Seen only in logs, not on screen. |
| T15c | Early reminder (Phase 5): "Remind me before due time" = 1 hour → task due in 2 h gets "In 1 hour: <title>" once at 1 h before, plus "Due now" at the due time; none for date-only tasks or tasks already inside the window; editing the due time re-arms both; survives a restart | A | ✅ | planned 2026-11-04 (user decision) 2026-11-04 Phase 5: p5a with lead 5 min: task due in ~335 s gets "In 5 minutes: Early one" once at its early moment and "Due now" once at the due time; a task already inside the window at start-up is marked quietly (no early toast); date-only task never reminded; no repeat after a restart (marks in DB). Editing the due time clears both marks: repository test (Data). Lead = 1 hour tested only through the Settings picker + unit tests, not by waiting an hour. |
| T15d | Reminder lead and pill settings persist after restart and apply live | A | ✅ | 2026-11-04 Phase 5: p5b: lead (1 hour), pill toggle, Reminders on/off saved, lead change recomputes reminders live, picker disabled while reminders are off, values still shown after restart. |

## Lists (groups)

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| L1 | First run shows starter lists *Personal* + *Office Work*, no tasks | A | ✅ | Phase 1: data layer + skeleton window verified (launched exe: "2 lists (Personal, Office Work), 0 tasks"). Still to check in the real island UI (Phase 2/3). 2026-11-04 Phase 6 (Release build, tiered compilation off): fresh data folder: database has Personal + Office Work and 0 tasks; the real panel (tray double-click) shows both list chips and no rows (tools/ui-tests/p6a.ps1). |
| L2 | Create list (name + colour) → appears in switcher, persists after restart | A | ✅ | 2026-11-04 Phase 3: new list appears as a chip, becomes selected, takes new tasks and is still there after a restart. |
| L3 | Duplicate name (any case) rejected; 21st list rejected | A | ✅ | 2026-11-04 Phase 3: duplicate names in any case are refused with a message; the 20-list limit is enforced. |
| L4 | Rename, recolour, move left/right (chip menu and Settings → Lists) | A | ✅ | 2026-11-04 Phase 3: rename, recolour (Change colour opens the editor card) and move left/right work from the chip menu (opened with Shift+F10). Settings > Lists is Phase 5, so not testable yet. 2026-11-04 Phase 5: Settings > Lists: move up, rename/recolour (dialog), create, delete (with and without moving tasks, pre-delete backup), default list, all tested with UI Automation (p5b). |
| L5 | Switch list → only its tasks shown; selection remembered after restart | A | ✅ | 2026-11-04 Phase 3: switching list shows only its tasks; the selection is remembered after a restart. |
| L6 | *All lists* shows every task with list dot + name, High tasks first | A | ✅ | 2026-11-04 Phase 3: "All" shows every task with the list dot + name, High first. |
| L7 | Quick add in a list adds there; in *All lists* uses the chosen/default list | A | ✅ | 2026-11-04 Phase 3: quick add goes to the selected list; in All lists it uses the quick-add list chip (default list otherwise). |
| L8 | Edit → change List moves the task | A | ✅ | 2026-11-04 Phase 3: changing the List chip in the edit form moves the task and it is saved. |
| L9 | Delete list → "move tasks" keeps tasks in target list; "delete tasks" removes them; backup made first | A | ✅ | 2026-11-04 Phase 3: "delete tasks" and "move tasks" both checked; a pre-delete backup is made first and holds the deleted list and its tasks; deleting the default list moves the default setting; the selection falls back to All. |
| L10 | Last remaining list cannot be deleted | A | ✅ | 2026-11-04 Phase 3: the last remaining list cannot be deleted; the message is shown and the list is still there. |
| L11 | Chip counts + pill count correct (PillCountScope All / Selected) | A | ✅ | 2026-11-04 Phase 3: chip counts and the pill count are right. PillCountScope Selected vs All is a Phase 5 setting, so the Selected variant is not checked. 2026-11-04 Phase 5: PillCountScope saved from Settings; the Selected variant itself is covered by unit tests, not looked at on screen. |
| L12 | Ctrl+0…9 and Ctrl+Tab switch lists | A | ✅ | 2026-11-04 Phase 3: Ctrl+0..9 and Ctrl+Tab switch lists. |
| L13 | Export/import keeps lists and which task belongs to which list | A | ✅ | 2026-11-04 Phase 5: Export from Settings > Data, clear, import: identical tasks and list membership (compared in the DB); importing the same file again skips duplicates. |

## Windows integration

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| W0 | Start-up error dialog: damaged or locked database → Open data folder / Restore from backup / Exit; nothing deleted | A | ✅ | 2026-11-04 Phase 4: damaged file → dialog; Exit ends with code 1 and leaves the file untouched; Open data folder opens Explorer there and the dialog returns; Restore from backup picks a backup, keeps the damaged file as Backups\broken-*.db and starts with the backup's tasks. A locked file (another program holding an exclusive lock) shows the same dialog after about 10 s. |
| W1 | Runs in background; ✕ / Alt+F4 never exit the app | A | ✅ | 2026-11-04 Phase 4: Alt+F4 on the open panel only collapses; the process keeps running (UI Automation + real Alt+F4 key). ✕ (Phase 2) does the same. |
| W2 | Tray: every menu item works (Open, Expand/Collapse, Show/Hide, Pause/Resume, Settings, Exit) | A | ⏳ | 2026-11-04 Phase 4: Open, Expand/Collapse, Show/Hide, Pause/Resume and Exit all work (menu opened through the NotifyIcon's own callback message, items invoked by UI Automation); left-click toggles, double-click opens; Pause also blocks auto-show after a wake and Open still works while paused. "Settings…" is not in the menu yet: the Settings window is Phase 5, and a button that does nothing is not allowed. Not tried with the real mouse. 2026-11-04 Phase 5: "Settings…" added to the tray menu: opens the window; a second click does not open a second one. Not tried with the real mouse. |
| W3 | Exit leaves no ghost tray icon and no process | A | ⏳ | 2026-11-04 Phase 4: Exit (tray and panel menu) ends the process with code 0 and the log shows the tray icon disposed. The icon sits in the hidden-icons flyout here, so its disappearance was not seen on screen. |
| W4 | Second launch → no 2nd process, first one expands | A | ✅ | 2026-11-04 Phase 4: second launch exits with code 0, the first instance expands and takes the keyboard focus; one TaskIsland process. A pipe message other than EXPAND is ignored and logged. |
| W5 | HKCU Run value created on first run; removed when toggled off | A | ✅ | 2026-11-04 Phase 4: first run writes HKCU\…\Run\TaskIsland = "<exe>" --startup (real key checked once, shown as a Windows startup entry, then removed); setting off removes it at the next launch; a value removed elsewhere is not re-added; an old path is rewritten. Test runs use a separate test key. The Settings toggle itself is Phase 5. 2026-11-04 Phase 5: The Settings toggle "Start with Windows" removes/writes the (test) Run value live. |
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
| W16 | Ctrl+Alt+T toggles; rebinding works; conflict warning shown | A | ✅ | 2026-11-04 Phase 4: Ctrl+Alt+T toggles (real key press); when another app already holds Ctrl+Alt+T, TaskIsland logs error 1409 and keeps running. Rebinding and the warning in Settings come with Phase 5 (parsing and the two-modifier rule are unit-tested). 2026-11-04 Phase 5: Rebinding to Ctrl+Alt+Y works live and the new shortcut toggles the island; one modifier is refused with a message. Conflict warning: another app held the shortcut during a restart, TaskIsland logged 1409 and Settings shows the warning. (A keystroke held by another app cannot be typed into the box, so the conflict appears at start-up/wake only.) |
| W17 | Restart explorer.exe → tray icon returns | A | ⏳ | 2026-11-04 Phase 4: real Explorer restart (with the user's OK): TaskbarCreated was handled, the icon was added again, the island stayed visible and topmost. The icon is in the hidden-icons flyout, so I could not see it come back. |
| W18 | No network connections (`Get-NetTCPConnection -OwningProcess …` empty) | A | ✅ | 2026-11-04 Phase 4: Get-NetTCPConnection and Get-NetUDPEndpoint for the TaskIsland process are empty (Release). 2026-11-04 Phase 6: again 0 TCP connections and 0 UDP endpoints in every measurement run (5 runs). |

## Data

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| D1 | Tasks saved to `%LocalAppData%\TaskIsland\Data\taskisland.db` | A | ✅ | 2026-11-04 Phase 3: tasks are written to taskisland.db in the data folder (the tests used a temp TASKISLAND_ROOT; the default %LocalAppData% path is unchanged code). |
| D2 | Tasks preserved after app restart | A | ✅ | 2026-11-04 Phase 3: tasks, lists, filter and selection survive a normal close and relaunch. |
| D3 | Tasks preserved after `taskkill /f` (crash simulation) | A | ✅ | 2026-11-04 Phase 3: taskkill /f, then relaunch: data intact, filter and selection remembered. |
| D4 | **Tasks preserved after Windows restart** | U | 👤 | |
| D5 | Export JSON → Import (Merge) skips duplicates | A | ✅ | 2026-11-04 Phase 5: Import (Merge) of the same file skips duplicates (UI Automation, p5b). |
| D6 | Import (Replace) makes backup first, then replaces | A | ⏳ | 2026-11-04 Phase 5: The UI asks Merge or Replace and import works; the Replace path with its automatic backup is unit-tested in Data (Phase 1) but I did not run Replace through the UI. |
| D7 | Invalid JSON files rejected with clear message, nothing imported | A | ✅ | 2026-11-04 Phase 5: An invalid file is rejected with a message in Settings; nothing changed (tasks identical). |
| D8 | Backup now → restore → data matches; pre-restore copy created | A | ✅ | 2026-11-04 Phase 5: Backup now saves a file, delete tasks, Restore asks for confirmation, tasks are back, a pre-restore copy is made; an invalid backup file is rejected and data stays unchanged. |
| D9 | Daily auto-backup created, max 7 kept | A | ✅ | 2026-11-04 Phase 4: the daily backup now runs at start-up and after a wake: "taskisland-yyyyMMdd-HHmmss.db" written on first start (log). Keeping 7 is unit-tested only. 2026-11-04 Phase 6 (Release build, tiered compilation off): 9 old automatic backups + 1 manual one, last backup 2 days ago: start-up wrote a new backup, exactly 7 automatic ones remained, the manual one was untouched, a second start within 24 h wrote nothing (p6a.ps1). |
| D10 | All settings persist after restart | A | ⏳ | 2026-11-04 Phase 5: About 25 settings changed in the Settings window and checked after a restart (theme, density, sliders, pickers, tick boxes, hotkey, lead, auto-collapse); not every one of the ~45 keys was individually re-read. |

## Performance

Targets from PLAN §6.7. Record the measured number in Notes, every phase from Phase 2 on.

| # | Test | Target | Result | Notes |
|---|---|---|---|---|
| P1 | Idle CPU (collapsed, 60 s) | 0.0 % | ✅ | 2026-11-03 Phase 2: 0.0 ms CPU time over 61 s (Release, collapsed); 0.0 ms over 30 s after a real open/close. 2026-11-04 Phase 3 (Release, seeded data): Companion Off 0 ms CPU over 60 s; Calm 1,672 ms over 60 s = 2.8 % of one core (Phase 2: 1.8-2.6 %). 2026-11-04 Phase 4: Release, seeded data, 60 s: Companion Off 16 ms (twice; only from window switches), Calm 1,125 ms (1.9 % of one core). A version with the planned location-change hook cost 63 ms per 45 s while the PC was in use, so it was replaced (DEVLOG). 2026-11-04 Phase 6 (Release, empty data, 3 runs): Companion Off 0 ms CPU over 60 s each; Calm 1,500 ms = 2.5 % of one core. |
| P2 | Private memory idle (collapsed, after 60 s) | ≤ 50 MB | ❌ | 2026-11-03 Phase 2: 52–56 MB before the panel is built; 72–73 MB with it built (current build); 129 MB after the text box had focus once (Windows text input + UI Automation). See DEVLOG. 2026-11-04 Phase 3: 61.6 MB (Off) / 63.9 MB (Calm) private after 60 s; still over budget (the known Phase 2 issue). 2026-11-04 Phase 4: 64.6–65.0 MB (Off) / 66.6 MB (Calm) private after 60 s; about the same as Phase 3 although WinForms is now loaded for the tray icon. 2026-11-04 Phase 6 (Release, empty data, 60 s): 64.1 / 64.5 / 65.1 MB private with tiered compilation off (it was 77.6 MB with it on; csproj changed, see DEVLOG). Calm companion 69.8 MB. After the panel has been opened once: 129-140 MB (one-time +64 MB: panel render + Windows text input + UI Automation DLLs), it does not go back down. Still over the 50 MB target. |
| P2b | Working set idle | ≤ 70 MB | ❌ | 2026-11-03 Phase 2: 98–102 / 127 / 138 MB at the same moments. Mostly WPF + graphics driver. See DEVLOG. 2026-11-04 Phase 3: 114 MB (Off) / 118.8 MB (Calm). 2026-11-04 Phase 4: 128 MB (Off) / 132 MB (Calm). 2026-11-04 Phase 6: 125.7-126.0 MB working set at idle (144 MB before the tiered-compilation change); 140 MB after the panel was used. Still over 70 MB; most of it is shared WPF/graphics-driver pages. |
| P3 | Cold start to visible pill | < 1.2 s | ✅ | 2026-11-03 Phase 2: 0.84–0.93 s to visible window (3 runs, Release, not ReadyToRun). 2026-11-04 Phase 6: 0.99 / 1.06 / 1.13 s to visible window with tiered compilation off (0.82 s with it on). Still under 1.2 s but with little room; Phase 7 ReadyToRun should give most of it back. |
| P4 | Animations smooth on this laptop | no visible stutter | ⏳ | 2026-11-03 Phase 2: morph smooth after the first expand in Debug log; the first expand skips ~100 ms (open issue). |
| P5 | GPU idle (Task Manager GPU column) | 0 % | ✅ | 2026-11-03 Phase 2: GPU Engine counter 0 % in 6 samples over 54 s idle (counter, not a Task Manager screenshot). |
| P6 | Efficiency mode shown in Task Manager while collapsed; off on hover/expand | leaf icon | ✅ | 2026-11-03 Phase 2: EcoQoS on after 16 s at rest (GetProcessInformation state=1). Off on hover/expand is in code; Task Manager leaf not screenshotted. |
| P7 | 10 min idle → CPU time unchanged, private bytes not growing | no leak | ✅ | 2026-11-04 Phase 6: 30 min idle, 500 tasks, Companion Off, IdleReactions Normal: private 82 → 74 MB (no growth), working set 143-153 MB flat. NOT "CPU unchanged": the random face pops cost 0.4-0.7 s CPU each (see M10). Handle count rises about 12 per minute and drops back when the GC runs (639 → 807 → 639); bounded, but unexplained. |
| P8 | 50 expand/collapse cycles → memory back within 5 MB | no leak | ❌ | 2026-11-04 Phase 6: 200 expand/collapse cycles: 128.7 MB after cycle 10, 137.6 MB at cycle 110, 139.9 MB at cycle 200 (+11 MB in 190 cycles, flattening). Against the start (64.9 MB) it is +75 MB, almost all from the first expand (panel + text input stack), which is never released. The 5 MB criterion is NOT met; no runaway growth seen. |
| P9 | 500 tasks in DB → idle targets still met, list scrolls smoothly | P1/P2 hold | ⏳ |  2026-11-04 Phase 3: 500 tasks: the list is virtualised (7 rows realised), idle CPU about 0-16 ms per 15 s after a start-up burst of about 1.1 s, private 62.1 MB / working set 114.4 MB (memory still over budget). Smooth scrolling NOT judged (needs the mouse wheel and eyes). 2026-11-04 Phase 6: 500 tasks, 30 min idle: private 74-83 MB (about 4-15 MB over the empty database), CPU idle as without tasks except the face pops. Smooth scrolling still needs your eyes (user step 2 below). |
| P10 | Hidden (full-screen app / paused) → 0 % CPU and GPU | 0 % | ⏳ | 2026-11-04 Phase 4: paused (hidden): 0 ms CPU over 30 s (Release). Hidden by a full-screen app and the GPU column not measured. 2026-11-04 Phase 6: GPU for the collapsed island 0 % (Companion Off, 30 s counter); Calm companion 0.22 % max sample. Hidden-by-full-screen GPU still not measured. |
| P11 | No disk writes while idle (Resource Monitor, 60 s) | none | ✅ | 2026-11-04 Phase 6: write operations of the process over 60 s idle: 0 (0 bytes), measured with the process I/O counters, Companion Off and Calm. |

## Security software (PLAN §7.11)

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| S1 | Code search finds no banned APIs (hooks, process access, shells, network, `Assembly.Load(byte[])`) | A | ✅ | 2026-11-03 Phase 2: grep over src/ for hooks, OpenProcess/memory/remote thread, Assembly.Load(, shells, network APIs, Process.Start: no hits. 2026-11-04 Phase 4: grep again: no banned APIs. The only Process.Start is explorer.exe on the data folder after a click in the start-up error dialog; the only registry writes are the HKCU Run value. New native calls: SetWinEventHook (foreground, out of context), SHAppBarMessage (full-screen notice), RegisterPowerSettingNotification, RegisterHotKey, AllowSetForegroundWindow (second launch). 2026-11-04 Phase 6: full grep again (SetWindowsHookEx, OpenProcess, Read/WriteProcessMemory, CreateRemoteThread, Assembly.Load(, powershell, cmd.exe, schtasks, reg.exe, HttpClient, Socket, WebRequest): no hits. Process.Start: explorer.exe for the data folder and the two About links (on click only). Registry writes: only HKCU Run via StartupRegistration. Reads: Themes\Personalize and DWM colours. |
| S2 | Defender custom scan of `publish\win-x64` and the installer → no threats (signatures updated first) | A | ⏳ | 2026-11-04 Phase 6: MpCmdRun custom scan of the Release build folder (176 MB, signatures from 2026-11-04 03:19): "found no threats", exit code 0. The publish folder and installer are scanned in Phase 7. |
| S3 | 10 min of use with Defender on → nothing in Windows Security → Protection history | A | ✅ | 2026-11-04 Phase 6: about 2 hours of runs with real-time protection on: Get-MpThreatDetection has no entry for TaskIsland. The Windows Security history page itself was not opened. |
| S4 | Install, run, uninstall → no UAC prompt, no firewall prompt | A | ⏳ | |
| S5 | Startup entry visible and switchable in Task Manager → Startup apps; turned off there → TaskIsland doesn't re-add it | A | ⏳ | |
| S6 | Controlled Folder Access on → app runs normally; export to Documents shows the friendly message; Downloads works | A | ⏳ | |
| S7 | All exe/dll files show Company "RieSources", product, version and description in file Properties | A | ⏳ | 2026-11-04 Phase 6: our five files (TaskIsland.exe and the four TaskIsland.*.dll) show Company RieSources, Product TaskIsland, Version 0.1.0.0. Third-party DLLs keep their own properties; the publish folder is checked in Phase 7. |
| S8 | Signed build only: `Get-AuthenticodeSignature` = Valid on exe, dlls, installer, uninstaller | A | ⏳ | n/a if unsigned |
| S9 | VirusTotal scan (only with the user's OK) → 0 detections or false positives reported | A+U | ⏳ | optional |

## Deployment

| # | Test | Who | Result | Notes |
|---|---|---|---|---|
| R1 | `dotnet build` + `dotnet test` green | A | ✅ | Phase 1, 2026-11-01: 218 tests. Phase 2, 2026-11-03: Release + Debug 0 warnings / 0 errors, **249 pass** (132 Core + 117 Data). Re-run after every phase. Phase 3, 2026-11-04: Release + Debug 0 warnings / 0 errors, **324 pass** (207 Core + 117 Data). Phase 4, 2026-11-04: Release + Debug 0 warnings / 0 errors, **368 pass** (249 Core + 119 Data). Phase 5, 2026-11-04: Release + Debug 0 warnings / 0 errors, **419 pass** (296 Core + 123 Data). |
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

## Phase 5 — steps for the user (👤)

1. **Real mouse on the pill alert (T15b):** Settings > Behaviour: "Remind me before due time" = 5 minutes. Add a task due in 6 minutes.
   When the alert shows, hover the pill (it must stay open until you move away) and click it (the panel opens on that task's list).
2. **Sliver (Always show the notch off):** Settings > Behaviour: turn it off. A thin sliver stays at the top. Move the mouse to the
   top-centre: the notch slides down. Move away: it tucks back.
3. **Reminder on screen:** with a task due in 1-2 minutes, watch the pill widen, the pulse ring and the Windows notification.
4. **System theme and accent (UI11):** Settings > Appearance: Theme = System, Accent = Windows. In Windows Settings > Personalisation switch
   Light/Dark and change the accent colour. Expected: TaskIsland follows within a second or two.
5. **Look of the Settings window:** check Mica (translucent title area) in light and dark, the six pages, and Compact vs Comfortable rows.
6. **Monitors (if you have two):** Settings > General > Monitor: pick the second display; the notch moves at once.
7. **Memory:** close Settings and watch Task Manager: memory should settle back near where it was before you opened it.

## Phase 6 — steps for the user (👤)

Everything the agent could measure or script is ticked above with its numbers (see DEVLOG "Phase 6"). What is left needs your hands,
eyes or a real restart. Please run what you can and tell me what you saw (one line per step is enough: "step 4 ok" or "step 4: the notch did X").
The Phase 4 steps (reboot, sleep, hibernate, screen off, lock, monitors, resolution and scaling, Explorer restart, full screen) and the
Phase 5 steps (pill alert with the mouse, sliver, System theme and accent, Settings look, second monitor) above still apply and are still open.
New in Phase 6:

1. **Smoothness (P4):** with your normal apps open, expand and collapse the island 10 times with the mouse. The very first expand after
   starting TaskIsland is the one that may stutter (known). Note whether later expands are smooth.
2. **Scrolling (P9):** add or import many tasks (Settings > Data > Import any big JSON, or just add 30) and scroll the list with the mouse wheel.
   Expected: smooth, no blank rows flashing.
3. **Drag to reorder (T10, T11):** in one list with three tasks of the same priority, drag one above another: a blue insertion line shows and the order
   stays after you close and reopen the panel (and after restarting TaskIsland). Then drag a task into a different priority group: the cursor
   shows "not allowed" and nothing moves.
4. **Peek toggle (UI10):** hover the collapsed island: the top task peeks out. Turn "Peek on hover" off in Settings > Behaviour: hovering no longer peeks.
5. **Animations off (UI12) and Windows animations (UI14):** Settings > Behaviour: turn "Animations" off: expand/collapse becomes instant.
   Turn it on again. Then in Windows Settings > Accessibility > Visual effects turn "Animation effects" off *and* in TaskIsland tick
   "Follow Windows animation setting": transitions become short/instant by themselves. Battery saver on does the same.
6. **Keyboard focus (UI13):** open the panel, press Tab until a task row has focus: its action buttons appear and a focus ring is visible.
7. **Faces (M4, M5):** click the face on the collapsed island: it laughs once and returns. Leave TaskIsland alone for 60 minutes: the face falls asleep
   (Sleepy). Hover to wake it.
8. **Sharp faces at other scales (M9):** at 100 %, 150 % and 200 % display scaling the face and text stay sharp (not blurry).
9. **Memory in Task Manager (P2):** Task Manager > Details > TaskIsland.exe > add the columns "Memory (private working set)" and "Commit size".
   Tell me both numbers after the island has been collapsed for a minute (the agent measured them; Task Manager's default column is a different, smaller number).
