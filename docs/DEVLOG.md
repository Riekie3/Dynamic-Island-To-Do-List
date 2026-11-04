# TaskIsland — Development Log

Live tracker (copy-ready prompts per phase): https://claude.ai/artifact/RAqH7ZmUthw76PcD6sfUK1 (source: docs/tracker.html)

Newest entry at the bottom. Each phase appends one entry using the template below.

## Phase status

| Phase | Status | Commit |
|---|---|---|
| 0 Planning | ✅ done 2026-11-01 | plan + checklist |
| 1 Skeleton & data layer | ✅ done 2026-11-01 | 8537510 |
| 2 Island UI | ✅ done 2026-11-03 (open: memory budget, first-expand hitch) | see Phase 2 entry |
| 3 To-Do functionality | ✅ done 2026-11-04 (open: mouse-only tests, memory budget) | see Phase 3 entry |
| 4 Windows integration | ✅ done 2026-11-04 (open: user tests sleep/reboot/monitors/scaling, memory budget) | see Phase 4 entry |
| 5 Settings & data | ⏳ | |
| 6 Testing & optimisation | ⏳ | |
| 7 Release | ⏳ | |

## Entry template

```
### Phase N — <name> (YYYY-MM-DD, <model used>)
**Implemented:** …
**Files created/modified:** …
**Tests performed:** … (unit test count, manual checks, TESTING.md IDs ticked)
**Remaining defects / not tested:** …
**Commit:** <hash> <message> · **Pushed:** yes/no
**Handoff (for the next phase, survives /compact):** what exists now · what Phase N+1 must know · open issues · exact next step
```

---

### Phase 0 — Planning (2026-11-01, Opus 5.5)
**Implemented:** Repo initialised (was empty). Detailed plan `docs/PLAN.md`, test checklist `docs/TESTING.md`,
this log, README, .gitignore, design reference image. Installed .NET SDK 10.0.403 and Inno Setup 6 (per-user) via winget.
**Decisions:** due-date reminders, drag reorder, peek on hover, 2 s slide-out of ticked tasks, daily backup toggle, English only,
grouped lists (Office Work / Personal / projects, with an "All lists" view). The spec's Category field is implemented as Lists.
**Tests performed:** none (no code yet).
**Pushed:** yes

### Phase 1 — Setup and database (2026-11-01, Sonnet 5.5)
**Implemented:** Solution `TaskIsland.sln` with `TaskIsland.Core`, `.Data`, `.Windows` (empty placeholder, net10.0-windows), `.App`
(WPF, assembly name `TaskIsland`) and two xUnit projects. `global.json` (SDK 10.0.403), `Directory.Build.props` (RieSources
credit, version 0.1.0), `nuget.config` (nuget.org only; this PC had no NuGet source configured).
- Core: models (`TaskItem`, `TodoList`, `Priority`, `TaskFilter`, `AppSettings` with clamping + defaults, transfer records),
  `TaskSorter`, `TaskStats`, `ReminderScheduler`, `IsoDate`, `IClock`/`SystemClock`, `JsonTransfer` (export + strict validation),
  `ImportService` (Merge / Replace), repository + settings interfaces.
- Data: `AppPaths`, `Database` (WAL, foreign keys + busy timeout per connection, migrations via `PRAGMA user_version`),
  `Migrations` (v1 schema, 2 starter lists, `DefaultListId`), `SqliteTaskRepository`, `SqliteTodoListRepository`,
  `SqliteSettingsStore`, `BackupService` (backup, validate, restore with pre-restore copy, prune 7, daily backup).
- App: DI container (`App.Build`), `MainViewModel` (CommunityToolkit), blank window showing the database status,
  `app.manifest` (PerMonitorV2, Windows 10/11). `TASKISLAND_ROOT` env var redirects data to a throw-away folder.
**Deviations from PLAN.md (PLAN.md updated to match):**
1. No `Cache=Shared` in the connection string (SQLite discourages shared cache; WAL + pooling is enough).
2. `ITaskRepository.ReplaceAllAsync(TransferData)` instead of `(IEnumerable<TaskItem>)` because Replace must swap lists too;
   added `InsertAsync(TaskItem)` (import keeps timestamps); `ClearCompletedAsync(long? listId = null)` for per-list clearing.
3. Added `ImportService` + `TransferData` records in Core so Merge/Replace logic is testable (plan only listed `JsonTransfer`).
**Files:** see commit 8537510 (55 files).
**Tests performed:** `dotnet build TaskIsland.sln -c Release` → 0 warnings, 0 errors. `dotnet test` → **218 passed, 0 failed**
(101 Core, 117 Data; Data tests use real temp SQLite files, run serially). The tests found and fixed one real bug
(UTC date parsing used an invalid `DateTimeStyles` combination). Launched `TaskIsland.exe` with a temp `TASKISLAND_ROOT`:
window responded, created `Data/Backups/Logs` + `taskisland.db`, and showed "2 lists (Personal, Office Work), 0 tasks"
(checked through UI Automation), then was closed and the folder deleted. TESTING.md: R1 ✅ (everything else stays ⏳ until
verified in the UI).
**Remaining defects / not tested:** No UI beyond the skeleton window. Adding a task to a missing list surfaces a raw
`SqliteException` (Phase 3 should check the list exists first and show a friendly message). `Windows` project is empty.
Nothing was tested on Windows 10 (this PC is Windows 11).
**Commit:** 8537510 Initial TaskIsland project structure and SQLite data layer · **Pushed:** yes
**Handoff (for the next phase, survives /compact):**
- *What exists:* a green solution (218 tests). All task/list/settings/backup/import logic is done and tested in Core + Data; Phase 2 and 3
  should reuse it, not re-implement it. App has only `MainWindow` + `MainViewModel` placeholders (delete/replace in Phase 2) and
  `App.Build(AppPaths)` as the DI composition root (register new services there).
- *Phase 2 must know:* read `docs/design-reference.webp` and PLAN §6. WPF `App` namespace is `TaskIsland.App`, assembly `TaskIsland`.
  `App.xaml` has no `StartupUri` (the window is created in `OnStartup`). Tray/`NotifyIcon` is not set up yet, so when the island
  window is added keep the process alive without a taskbar window. Use `<Using Remove="System.Windows.Forms"/>`-style fixes
  only if WinForms is enabled later (Phase 4). Heredocs with `$`/backticks in the Bash tool broke once, so use the Write tool for files.
- *Environment:* `dotnet` on PATH (SDK 10.0.403). NuGet packages restore via the repo's `nuget.config`. Run the app against a throw-away
  data folder with `$env:TASKISLAND_ROOT = "$env:TEMP\TaskIslandDev"` before launching.
- *Open issues:* see "Remaining defects" above.
- *Exact next step:* start Phase 2 (Opus, High effort): `dotnet build` first to confirm green, then Themes + `IslandHost` morph control + `IslandWindow` per PLAN §6.2.

### Plan update — modern UI and low resource use (2026-11-01, Opus 5.5)
The user asked for a more modern design that uses as little CPU, GPU and RAM as possible. Docs only, no code changed.
- PLAN §6.0 (new): design direction and principles (8 px grid, one accent, hairline depth, motion tokens, `FluentEase`).
- PLAN §6.1: new neutral token set (black pill, graphite panel), priority chips as dot + label, tabular numerals,
  optional Windows accent (`AccentSource`, new setting in §5, added in Phase 5).
- PLAN §6.2: morph now animates **one clip geometry** instead of Width/Height (no layout per frame); window is
  small when collapsed (376×64 DIP) and grows to 452×600 only while expanded; shadow is a cached bitmap shown only
  when still; progress ring on the pill; strict animation rules (finite, render properties only).
- PLAN §6.4: virtualised task list, hover/focus-only row actions, footer progress bar, sliding filter indicator.
- PLAN §6.6: modern Settings (pill nav, cards, toggles, Mica on Windows 11), released on close.
- PLAN §6.7 (new): resource budget (0 % idle CPU/GPU, ≤ 50 MB private, ≤ 70 MB working set, < 1.2 s start) and 10 rules
  (no polling, no infinite animations, 60 fps cap, Efficiency mode while collapsed, automatic reduced motion, GC settings).
- §7.2 positioning, Phases 2/3/5/6 steps and acceptance, §9 limitations updated. TESTING: UI13–UI15 and P1–P11.
- *Handoff for Phase 2:* read PLAN §6.0–6.7 before coding; Phase 2 must record a performance baseline (step 8).

### Plan update — release and security software (2026-11-01, Opus 5.5)
- User pre-approved publishing the GitHub release v1.0.0 with the x64 installer when Phase 7's checks pass.
  Phase 6b (user's sleep/monitor tests) is not a release gate; the user tests in live use after release.
- New PLAN §7.11 "Don't trigger security software": banned APIs/behaviours (no hooks, process access, shells, network,
  packing, admin), visible persistence only, Controlled Folder Access handling, full file version info, code-signing
  decision. §0 rule 10, Phase 6 security grep + Defender scan, Phase 7 steps 4b (ask user about signing) and 4c
  (Defender scan, optional VirusTotal with user OK). TESTING S1–S9. The existing `app.manifest` is already `asInvoker`.

### Plan update — mascot face notch (2026-11-02, Opus 5.5)
- The user supplied a sheet of 12 face expressions + a mini notch (`docs/design/mascot-reference.webp`).
- Drew the faces as a vector rig, `docs/design/mascot-faces.html` (geometry source of truth, checked against the reference in a browser).
- New PLAN §6.8: four island states (Mini 120×32 → Reaction 232×56 → Info pill → Panel), face rig with independent
  eyes/mouth/accessory parts, 9 micro-animations, the 12 expressions mapped to app triggers, random pops on a one-shot
  timer (`IdleReactions` Off/Rare/Normal/Lively), `MascotEnabled` toggle (off = classic count pill). Vector XAML instead of
  Lottie/sprites (no extra NuGet, sharp at any DPI, near-zero cost). §2, §3.1, §5, §6.2, §6.3, §6.7 and Phases 2/3 updated.
  TESTING M1–M10.
- *Handoff for Phase 2:* step 6b builds the mascot; compare all 12 faces side by side with the reference sheet.

### Plan update — updated design preview (2026-11-03, Opus 5.5)
- Added `docs/design-reference-1.webp` (source `docs/design/preview-1.html`): the current visual target (modern panel, list
  chips, mascot states). README now shows it. PLAN header and Phase 2 step 7 point to it.

**▶ Current next step (supersedes the Phase 1 handoff's last line):** start Phase 2 (Opus, High effort). Read PLAN §0,
§2, §5, **§6.0–6.8**, §7.1–7.2 and **§7.11** first; visual targets are `docs/design-reference-1.webp` and
`docs/design/mascot-faces.html`. Run `dotnet build` + `dotnet test` (expect 218 green) before changing anything.
Phase 2 is large (island UI + mascot); if the context gets long, commit what works, write a Handoff block naming the
remaining Phase 2 steps, and continue in a fresh session.

### Phase 2 — Dynamic Island interface + mascot (2026-11-03, Opus 5.5)
**Implemented:**
- *Themes:* `Themes/Dark.xaml`, `Light.xaml` (all §6.1 tokens, frozen, same keys) and `Controls.xaml` (icon buttons, round checkbox,
  quick-add box, priority chip, list chip, segmented filter, overlay scrollbar, menus, tooltip, 2 px accent focus ring, mascot brushes).
  `App.ApplyTheme` swaps the theme dictionary live (Phase 5 wires it to Settings).
- *Motion:* `Controls/FluentEase` (cubic-bezier 0.2,0,0,1 and its mirror), `Controls/Anim` (finite, FillBehavior=Stop, final value left as
  the local value, key-frame holds instead of BeginTime), `Services/MotionPolicy` (fast/normal/morph tokens; reduced motion from Windows
  animation effects, Battery saver, remote session, all event driven). 60 fps cap in `App.OnStartup`.
- *Island:* `Controls/IslandHost`: one shared `RectangleGeometry` drives the body paths and the content clip; `AnimateTo(state, w, h)`
  morphs between Mini 120×32, Reaction 232×56, Info NotchWidth×36 (peek up to 360) and Panel 420×≤550 with no per-frame layout;
  content crossfade per §6.2; cached `DropShadowEffect` + `BitmapCache` shadow shown only while still. `Views/IslandWindow`: styles
  per §7.1, state machine, hover/peek, click, Esc/✕/click-outside/Alt+F4 collapse, window grown before expand and shrunk after collapse.
  `Services/IslandPositioner` (primary monitor, DPI, HorizontalOffset clamp), `Services/EfficiencyMode` (EcoQoS after 10 s at rest, off on
  hover/expand).
- *Panel:* `Views/PanelView` (header with 28 px face, list chips, quick add with priority chip and → button, virtualised list, empty
  state, progress bar via ScaleX, sliding filter indicator) and `Views/TaskRow` (round checkbox, list dot + name in All lists, due label,
  priority chip, edit/delete on hover or keyboard focus). `ViewModels/IslandViewModel` runs on in-memory `SampleData` through the real
  Core rules (`TaskSorter`, `TaskStats`): select list, filter, tick, quick add, cycle priority and delete work in memory; nothing is saved.
- *Mascot (§6.8):* `Mascot/Expression(s)` (12 expressions + Mini dots, geometry copied 1:1 from `docs/design/mascot-faces.html`, frozen),
  `Controls/FaceRig` (4 independent parts × 2 crossfade slots, glow = wider translucent copy, all 9 micro-animations, dots move into eye
  positions), `Services/ReactionDirector` (priorities via Core `ReactionRules`, one-shot hold / random-pop / 60-min Sleepy / startup-quiet
  timers, pops suppressed while the panel is open, hidden, locked, reduced motion or Battery saver). Wired now: start greeting (Wink, not
  on `--startup`), hover → Curious → Info, click on a popped face → Laughing, random pops, Sleepy.
- *Core:* `AppSettings.MascotEnabled` / `IdleReactions` (+ tests), `Mascot/ReactionRules` (+ tests), `Services/DueLabel` (+ tests).
  *Data:* `PRAGMA cache_size = -1024` per connection. *App csproj:* `ConcurrentGarbageCollection=false`, `TieredPGO`, `Deterministic`.
- *Windows project:* `Native/User32|Kernel32|Shcore`, `WindowStyler`, `MonitorService.GetPrimary`, `ProcessPower` (EcoQoS, Battery saver),
  `Hotkeys` (RegisterHotKey only).
- *Debug-only tools (compiled out of Release):* hotkeys Ctrl+Alt+Shift+F (cycle faces), +L (light/dark), +M (mascot on/off); arguments
  `--demo mini|sleepy|info|peek|panel|face:Name|cycle:N`, `--snap <png>` (renders the window onto a plain backdrop and exits, so
  screenshots never contain other windows), `--theme`, `--no-mascot`, `--filter`, `--reduced-motion`, `--pop-seconds`;
  state log in `%TEMP%\TaskIsland-debug.log`.

**Deviations from PLAN.md (PLAN updated):**
1. Collapsed window is **452 × 64** DIP, not 376 × 64. With the same width in both sizes the window's left/top never change on resize,
   so a stale frame can't show the pill shifted by 38 px. Cost: a few thousand more transparent pixels; idle CPU/GPU stay 0.
2. The panel is built and laid out once **while the island rests after start-up** (ApplicationIdle), not on the first click. Building it
   on the click froze the first expand for ~230 ms (Release) and the morph was skipped. The panel stays `Collapsed` until opened.
   Cost: the memory it needs is taken before the first open instead of at it (see measurements).
3. `FaceRig` is code-only (`Controls/FaceRig.cs`, no XAML) because every visual is generated from expression data.
4. Hover flow with the mascot: Mini → Curious (400 ms) → Info pill → peek after another 600 ms. Clicking the hover face or the Info pill
   opens the panel; clicking a face that popped up by itself shows Laughing, and a second click opens the panel. The "double-click the
   mini notch → Laughing" trigger was dropped because a single click already opens the panel.
5. `AnimationsEnabled=false` makes every duration 0 including the morph (§5 and UI12 say instant); automatic reduced motion keeps the
   120 ms morph (§6.7 rule 8).
6. The ⋯ menu has only "Exit TaskIsland" (the only way out until the tray arrives in Phase 4). Clear completed / Export / Import /
   Settings are added in Phases 3–5 when they work. Edit (✎) and "+ new list" show disabled until Phase 3.

**Tests performed:**
- `dotnet build TaskIsland.sln -c Release` and `-c Debug`: 0 warnings, 0 errors. `dotnet test`: **249 passed, 0 failed** (132 Core incl.
  31 new: ReactionRules, DueLabel, the two new settings; 117 Data).
- Live checks on this PC (Windows 11, 1920×1080 at 125 %), with the user's go-ahead for the mouse tests:
  window top-centre (centre x 960, top 10 px = TopMargin 8 × 1.25); ex-styles TOOLWINDOW + NOACTIVATE + TOPMOST, no APPWINDOW;
  `WindowFromPoint` beside the pill returns the window underneath (click-through); hovering 1.9 s keeps Notepad in the foreground;
  a click expands (window 565×750 px, same left/top) and the panel takes focus and accepts typing; Esc with text clears it; Esc on an
  empty box, ✕ and a click outside each collapse; the collapsed rect returns to exactly the start rect. The Debug log confirms
  Mini → Curious → Info → peek while hovering, and the Wink greeting returning to Mini after 2.51 s.
- Screenshots (rendered by the app on a plain backdrop, 125 %): `docs/screenshots/phase2-faces.png`,
  `phase2-faces-vs-reference.png` (the 12 faces next to the reference sheet), `phase2-collapsed-states.png` (Mini, sleepy Mini, Info,
  peek, classic pill), `phase2-panel-dark.png`, `phase2-panel-light.png`, `phase2-panel-active.png`. All 12 faces match the reference
  shapes; the panel matches `design-reference-1.webp`.
- Security grep (§7.11) over `src/`: no hits for hooks, process access, shells, network, `Assembly.Load(` or `Process.Start`.

**Baseline measurement (§6.7, Release build, collapsed, not ReadyToRun):**

| Metric | Measured | Target |
|---|---|---|
| CPU idle, 61 s collapsed | 0.0 ms CPU time = **0.0 %** | 0.0 % ✅ |
| CPU idle after one real open/close, 30 s | 0.0 ms | 0.0 % ✅ |
| GPU idle (GPU Engine counter, 6 samples over 54 s) | **0 %** | 0 % ✅ |
| Efficiency mode after 16 s at rest | EcoQoS on (`GetProcessInformation` state=1) | on ✅ |
| Cold start to visible window (3 runs) | **0.84–0.93 s** | < 1.2 s ✅ |
| Threads | 11 (WPF render, GC/finalizer, SystemEvents, runtime); none of ours | ✅ |
| Private memory before the panel was built | 52–56 MB | ≤ 50 MB ❌ |
| Private memory with the panel built (current build, at rest) | 72–73 MB | ≤ 50 MB ❌ |
| Private memory after the quick-add box had keyboard focus once | 129 MB | ≤ 50 MB ❌ |
| Working set at the same three moments | 98–102 / 127 / 138 MB | ≤ 70 MB ❌ |

What was tried: software rendering (`RenderMode.SoftwareOnly`) and a self-contained ReadyToRun publish both measured *higher*
(69 / 68 MB private) and were reverted. Findings: the NVIDIA driver (`nvgpucomp64.dll`, `nvd3dumx.dll`) loads into every WPF process;
building the panel adds ~20 MB; the first keyboard focus in a text box loads Windows text input and UI Automation
(`textinputframework.dll`, `msctfui.dll`, `UIAutomationCore.dll`, `CoreMessaging.dll`, …) for ~56 MB. Turning off the input method on the
box would save memory but break IME typing (Chinese and others), so it was not done. No working-set trimming tricks were used (§6.7).

**Remaining defects / not tested:**
- **Memory is over the §6.7 budget** (table above). Needs a decision in Phase 6: investigate further (text/font cost of the panel, UIA
  peers) and/or ask the user to accept a realistic WPF target.
- **The first expand still drops ~100 ms of frames** (Release burst capture: the panel appears ~60 % open), even with the prewarm. Later
  expands were not frame-checked. Phase 3/6: start the morph after the first rendered frame, or render the panel once while hidden.
- One hover run showed the Info pill back at Mini before the pointer left; two re-runs with logging did not reproduce it.
- Not tested yet: HorizontalOffset ±400 clamp (UI4), "All done"/"1 task" text in the UI (UI9), PeekOnHover off (UI10), System theme
  (UI11), AnimationsEnabled off and automatic reduced motion (UI12/UI14, need Windows settings changed), row actions on keyboard focus
  and the focus ring (UI13), the 5 s alert hold live (M3), micro-animations frame by frame (M5), random-pop intervals live (M6), pops
  paused while locked / full-screen / display off (M7; full-screen and display-off detection arrive in Phase 4), 100/150/200 % scaling
  (M9), 30-min CPU (M10).
- Sample data only: nothing persists until Phase 3.

**Commit:** "Implement Dynamic Island collapsed and expanded interface" (hash in `git log`) · **Pushed:** yes

**Handoff (for Phase 3, survives /compact):**
- *What exists:* the island UI and mascot run on `IslandViewModel` + `SampleData` (in memory). Core rules are reused (`TaskSorter`,
  `TaskStats`, `DueLabel`). The DI root is `App.Build(paths, db, settings, options)`; settings are loaded before the container is built and
  registered as a singleton `AppSettings` (save changes through `ISettingsStore`).
- *Phase 3 must know:* replace `SampleData` with the repositories inside `IslandViewModel` (keep its public properties; the views bind
  to them). Rows are recreated on every `Refresh()`. Still to build: the 2 s slide-out, Undo bar, inline edit (`TaskRow` ✎ is disabled),
  list popup (`+` chip disabled) and the ⋯ menu items (Phases 3/5). Mascot triggers for task events go through
  `ReactionDirector.Request(...)`, which shows them on the panel face while the panel is open. Every animation must use `Anim` and the
  `MotionPolicy` tokens. The panel height re-animates by itself on `SizeChanged`.
- *Testing tools:* Debug build + `--snap` for screenshots (no screen capture, nothing private in them); `--demo cycle:N` for
  expand/collapse loops without the mouse; `%TEMP%\TaskIsland-debug.log`. Real mouse tests move the user's pointer: ask first.
- *Open issues:* the memory budget and the first-expand hitch (above).
- *Exact next step:* start Phase 3 (Sonnet or Opus, Medium effort): `dotnet build` + `dotnet test` (expect 249 green), then PLAN §8 Phase 3 step 1.

### Change — smaller compact island (2026-11-03, Opus 5.5)
**Asked:** the notch should be a bit smaller; the user runs Nusa (another notch app) and asked to compare its position and size.
**Nusa, measured live** (its running window, not the installer; `NusaSetup.exe` was not run): one 800 × 238 px window at the top
centre of the screen (y = 0) whose window region follows the visible shape. At rest the drawn notch is **180 × 37 px** at 125 %
(≈ 144 × 30 DIP), flush with the top edge, flat top, rounded bottom corners. TaskIsland was Mini 120 × 32, Info 200 × 36 and
Reaction 232 × 56 DIP, 8 DIP below the top edge.
**Done:** `IslandHost.CompactScale = 0.8` draws Mini, Reaction and Info at 0.8× (Mini 96 × 25.6, Info 160 × 28.8 by default,
Reaction 185.6 × 44.8 DIP; at 125 % that is 120 × 32, 200 × 36, 232 × 56 px). The face and info content keep their design-size
layout under a frozen `LayoutTransform`, so the 1:1 reference geometry is unchanged; outline/glow strokes and the gloss scale with
it. The panel is not scaled. Window size, position and `TopMargin` are unchanged. PLAN §6.8 updated.
**Tests:** Release + Debug build 0 warnings / 0 errors; `dotnet test` 249 passed. `--snap` renders of every compact state and all
12 faces checked (nothing clipped; replaced `docs/screenshots/phase2-collapsed-states.png` and `phase2-faces.png`). Release start
checked live: window 565 × 80 px at (678, 10), unchanged. Hover/click not re-run with the mouse (no input code changed; the hit
area is the painted shape, which scales with it).
**Open:** position is still a floating pill 8 DIP below the top edge, unlike Nusa's flush notch; the user decides whether to change it.

### Change — flush notch (2026-11-03, Opus 5.5)
**Asked:** make the island flush with the top edge, like Nusa.
**Done:** `TopMargin` default 8 → **0**. `IslandHost.Flush` (on when `TopMargin` is 0) draws a notch: flat top, rounded bottom
corners (compact states radius 0.3 × height, panel 24, the panel shadow matches). The shared geometry is extended upward by its
radius, so the top corners and the top outline fall outside the window; nothing else in the morph changed. `IslandRect` now returns
the visible rectangle. A `TopMargin` above 0 still gives the floating pill. PLAN §6.8 and the settings table updated.
**Tests:** Release + Debug 0 warnings / 0 errors; 249 passed (default test updated to 0). `--snap` renders of all compact states,
12 faces and the panel (dark, light, Active) checked and replace the `docs/screenshots/phase2-*` images except the reference
comparison. Release start: window at y = 0 (565 × 80 px at x 678). Hover/click not re-run with the mouse.
**Fix:** the faces picture pushed in 7b45ce3 showed the sleeping notch in place of the Sleepy face (the snapshot script wrote
`sleepy.png` and `Sleepy.png`, the same file on Windows); renamed in the script and re-rendered.

### Change — live companion (2026-11-03, Opus 5.5)
**Asked:** "why is the face not animated … like a live companion"; then "Yes, add it".
**Why it looked dead:** the plan only allowed one or two short micro-animations per face and kept the resting dots still
(0.0 % idle rule). On this PC there was a second reason: **Windows "Animation effects" is off**
(`SystemParameters.ClientAreaAnimation = false`), and `MotionPolicy` treats that as reduced motion, so no face micro-animation
and no random pop has ever played here, only the instant shape changes.
**Done:**
- *Core:* `CompanionLevel { Off, Calm, Lively }` setting `Companion` (default Calm); `Mascot/CompanionRules` (delay windows,
  movements per face, weighted pick without repeats); `ReactionRules.HoldTime(reaction, companion)` (Calm 4 s, Lively 6 s).
  +36 tests (`CompanionRulesTests`, settings parse/round trip).
- *App:* `FaceRig.PlayLife` (blink, double blink, glance, breathe, squeeze, mouth twitch, accessory wiggle; finite key
  frames; skipped while the reaction's own micros run); `ReactionDirector` one-shot `_life` timer + `LifeTick` event, paused
  like random pops except for the open panel and the start-up quiet minute; `IslandWindow.OnLifeTick` plays it only on a
  still Mini/Reaction face or the panel face. `MotionPolicy.FollowWindowsAnimations` (true; Debug `--full-motion` turns it
  off for testing). Debug `--companion Off|Calm|Lively`.
- *Fixes found on the way:* ticking a task crashed the app when animations run (`TaskRow` animated a frozen template
  `ScaleTransform`; now it gets its own) — never seen before because animations were off here. Debug `--pop-seconds` was
  applied after the first pop timer had started (now before `Start()`).
**Tests:** Release + Debug 0 warnings / 0 errors; **285 passed** (168 Core, 117 Data). Debug log + pixel watch of the notch:
moves play at the logged times. Full-motion `--demo cycle:6` (expand/collapse) ran without a crash.
**Cost (Release, 60 s at rest, no mouse near, i7-10875H 16 threads, full motion forced):**

| Companion | CPU time / 60 s | % of one core | ≈ Task Manager |
|---|---|---|---|
| Off | 0 ms (2 runs) | 0.0 % | 0 % |
| Calm | 1.09 / 1.56 s | 1.8–2.6 % | ≈ 0.13 % |
| Lively | 2.08 / 1.84 s | 3.1–3.5 % | ≈ 0.2 % |

Far above the "< 0.1 %" I estimated before building it. Experiments (all reverted): with the timer running but nothing drawn
0.1 %; a 30 or 20 fps cap (per animation or global) did not lower it; software rendering about the same. So the cost is
WPF's layered-window rendering while any animation runs (about 3 ms CPU per frame for this 565 × 80 px window), not the frame
count. A cheaper path would need a different window technique (region-shaped opaque window or DirectComposition), not tried.
**Open:** the user decides whether TaskIsland should ignore Windows "Animation effects" off (otherwise the companion and all
face animations stay off on this PC), and whether the measured cost is acceptable (Calm default) or Off should be the default.

### Change — animate even with Windows "Animation effects" off (2026-11-04, Opus 5.5)
**Asked:** "Yes, ignore it" (TaskIsland should animate although Windows "Animation effects" is off on this PC).
**Done:** new setting `FollowWindowsAnimations` (default **false**) → `MotionPolicy.FollowWindowsAnimations`. By default the
Windows option no longer turns on reduced motion; Battery saver, Remote Desktop and `AnimationsEnabled=false` still do.
PLAN §5 table, §6.0 and §6.7 rule 8 updated. Settings UI for it comes with Phase 5.
**Tests:** Release + Debug 0 warnings / 0 errors; 285 passed (default + round trip cover the new key). Release build with
default settings on this PC (Animation effects off): notch pixels show blinks at 3.8 s and 20.3 s and glances left/right, so
the companion and face animations now play without any Debug override.
**Note:** all full-motion paths had never run on this PC before; one crash (`TaskRow` tick) was found and fixed on 2026-11-03,
and a 6-cycle expand/collapse ran clean. Other panel animations (filter slide, row actions, progress) were not clicked through.

### Phase 3 — To-Do functionality (2026-11-04, Sonnet 5.5)
**Implemented:** everything in PLAN §8 Phase 3 against the real database; no sample data any more.
- Add (button and Enter), toggle with a 2 s linger then slide-out, delete with a 5 s Undo bar, Clear completed with a confirm card,
  inline edit (title, priority, notes, due date + time, list) with Save/Cancel, priority cycling, sort High → Medium → Low,
  filters Active / Completed / All (remembered), counters, empty states, reorder within a priority group (drag and Alt+Up/Down).
- Lists: switcher chips with counts, create / rename / recolour / move / delete (move or delete tasks, auto backup `pre-delete-`
  first, last list protected), quick-add list chip in All lists, Ctrl+0…9 / Ctrl+Tab, chip context menu (also Shift+F10).
- Every action is awaited against the repositories; a failure is logged, shows an inline notice and the Sad face, and reloads the UI
  from the database. Mascot triggers for tasks: tick Happy, High tick / 5 in 30 min Love, last task Excited, add Wink,
  typing/editing Focused, failure Sad.
- Deviations from the PLAN are listed in PLAN.md under "Phase 3 as built".

**Files created:** Core `Services/DueInput.cs`, `Services/ReorderRules.cs`, `Mascot/TickStreak.cs`; tests `Phase3RulesTests.cs` (39);
App `Logging/FileLogger.cs`, `ViewModels/IslandViewModel.Lists.cs`, `Controls/MiniCalendar.xaml(.cs)`, `Themes/FormControls.xaml`;
`tools/ui-tests/` (the UI Automation scripts used below). **Modified:** `IslandViewModel`, `TaskItemViewModel`, `ListChipViewModel`,
`TaskRow`, `PanelView`, `IslandWindow.xaml.cs`, `ReactionDirector` (`Keep`), `App.xaml(.cs)`. **Deleted:** `SampleData.cs`.

**Tests performed:** Release + Debug build 0 warnings / 0 errors; **324 tests pass** (207 Core, 117 Data). The §7.11 grep found no
banned APIs. UI Automation scripts (Debug build, fresh temp data folder, no mouse): T1–T9, T12–T14, L1–L10, L12, D1–D3 pass; T10 / T11
by keyboard only; mascot triggers via the debug log; a failure path (list deleted behind the app's back) shows the notice, saves nothing,
refreshes the chips and logs it. Screenshots: `docs/screenshots/phase3-*.png` (panel dark/light, edit, new list, delete list).
Performance (Release, collapsed, seeded data, 60 s): Companion Off 0 ms CPU; Calm 1,672 ms (2.8 % of one core); with 500 tasks only 7 rows
are realised (virtualised) and idle CPU stays near zero after the start-up burst. Private memory 62–64 MB, working set 114–119 MB.

**Bugs found and fixed on the way:** a moved row left a stale UI Automation node (moved rows now get a new view model); a selection
trigger on `Tag` inside a ControlTemplate did not fire (now a Style-level DataTrigger); the edit form was too tall (labels now sit beside
the fields, and the row scrolls into view); a stray `AutomationProperties.Name` hid the notice text.

**Remaining defects / not tested:**
- **Not tested, needs the real mouse (ask the user first):** mouse drag-reorder with the insertion line (T10), the "No" cursor (T11),
  hover row actions, right-click menus, clicking days in the calendar popup, scroll smoothness with 500 tasks (P9), the frame-by-frame
  look of the row animations, and any Release run of the functional flows (the demo options are Debug-only).
- Memory is still over the §6.7 budget (Phase 2 issue, unchanged); the companion costs about 2.8 % of a core in Calm.
- Not built here by design: reminders (T15, Phase 4), export/import (L13, Phase 5), Settings → Lists (Phase 5), Surprised / Thinking
  faces, Sleepy and click-face Laughing (M4).

**Commit:** "Implement task CRUD, priorities, sorting, filters and persistence" (hash in `git log`) · **Pushed:** yes

**Handoff (for Phase 4, survives /compact):**
- *What exists:* the island works end to end on SQLite. `IslandViewModel` (+ `.Lists.cs`) holds the data: operations go through
  `RunAsync` (a `SemaphoreSlim` gate, so they run one at a time), then the in-memory copy is reloaded and the `Tasks`
  collection is reconciled in place (removed rows animate out, new rows animate in, a moved row gets a NEW `TaskItemViewModel`).
  `TaskItemViewModel` talks back through `ITaskRowHost`. A "lingering" set keeps a just-ticked row for 2 s.
  `FileLogger` (`Logs\taskisland-yyyyMMdd.log`, 14-day prune) already exists, so Phase 4 step 9 only needs the global exception
  handlers and the DB-failure dialog. `ReactionDirector.Keep(reaction)` extends a hold without re-animating.
- *Phase 4 must know:* reminders should read due tasks through the same repository and request `Reaction.Surprised` through
  `ReactionDirector.Request`; the ⋯ menu already has "Exit". `CancelTransientUi()` runs on collapse. The stock WPF DatePicker and
  ComboBox are not used (dark theme): use `MiniCalendar` and the chips. Dialog cards are overlays inside the panel, so no extra
  window triggers click-outside collapse.
- *Testing tools:* `tools/ui-tests/uia.ps1` (helpers) + `t1`–`t6.ps1`; Debug build with `TASKISLAND_ROOT=<temp>` and
  `--demo panel --full-motion --pop-seconds 3600`; read the DB with `python -I`. A RadioButton needs SetFocus + SendKeys Space.
  Real mouse tests move the user's pointer: ask first.
- *Open issues:* the mouse-only checks above, the memory budget, the first-expand hitch, the companion CPU cost.
- *Exact next step:* start Phase 4 (Opus recommended): `dotnet build` + `dotnet test` (expect 324 green), then PLAN §8 Phase 4 step 1.

### Phase 4 — Windows integration (2026-11-04, Opus 5.5)
**Implemented:** PLAN §7.1–7.10 (deviations listed in PLAN.md under "Phase 4 as built").
- **Single instance:** named mutex + `CurrentUserOnly` named pipe; the second launch passes the foreground right, sends EXPAND and
  exits with 0; the pipe ignores anything else (max 16 bytes).
- **Tray:** NotifyIcon with Open · Expand/Collapse · Show/Hide · Pause/Resume visibility · Exit; left-click toggles, double-click
  opens; tooltip "TaskIsland — 3 tasks left" / "Paused"; icon drawn in memory, follows the taskbar theme, dimmed while paused;
  re-added after an Explorer restart. Exit is the only way out and disposes the icon, hooks, hotkey and DB pools.
- **Start with Windows:** HKCU Run value on first run, `--startup` starts collapsed without focus, rules from §7.11 rule 5.
- **Monitors / DPI:** monitors enumerated on every placement; `MonitorDeviceName` else primary; `WM_DPICHANGED` repositions.
- **Power / session / display recovery:** suspend and lock collapse; resume, unlock, display on and display changes trigger one
  debounced recovery (750 ms) plus follow-ups at +2 s / +6 s; display off and lock pause the mascot.
- **Full screen:** foreground WinEvent + the shell's ABN_FULLSCREENAPP notice (replaces the planned location hook, see below).
- **Hotkey:** Ctrl+Alt+T through RegisterHotKey; failure (e.g. 1409, in use) is logged; re-registered in recovery if lost.
- **Reminders:** one-shot timer to the next due moment (max 24 h); balloon with the list name, island pulse ring, Surprised face;
  overdue tasks at start → one combined balloon; clicking it opens the task's list.
- **Errors:** global handlers (UI thread → logged + inline notice + Sad face, kept running; others logged with stack);
  start-up DB failure → task dialog Open data folder / Restore from backup / Exit.

**Files created:** Core `HotkeyGesture`, `StartupPolicy`, `ScreenRules` (placement + full-screen rule), `ReminderText`; Windows
`SingleInstance`, `StartupRegistration`, `PowerEventsService`, `FullScreenDetector`, `HotkeyService`, `Native/Shell32`; App
`TrayIconService`, `TrayIconArt`, `RecoveryCoordinator`, `ReminderService`, `StartupManager`, `StartupFailureDialog`; tests
`Phase4RulesTests` (42); `tools/ui-tests/p4a`–`p4f.ps1`, `seed_reminders.py`, `make_backup.py`.
**Modified:** `App.xaml.cs` (start-up flow, exit path, handlers), `IslandWindow` (visibility reasons, toggles, reposition, pulse),
`IslandHost` (pulse ring), `IslandPositioner` (monitor choice), `IslandViewModel` (DataChanged, ShowList, error notice),
`BackupService.RestoreBrokenAsync`, `Database` (5 s command timeout), `MonitorService`, `User32`, `EfficiencyMode` and
`FileLogger` (Crash), App csproj (WinForms for the tray only).

**Tests performed:** Release + Debug 0 warnings / 0 errors; **368 tests pass** (249 Core, 119 Data). §7.11 grep: no banned APIs.
UI scripts, no mouse (Debug unless noted, temp data folders): p4a 24/24 (single instance, Run key incl. the real key once, hotkey,
pipe, Exit), p4b 37/37 (reminders, recovery from posted messages, TaskbarCreated, every tray menu item), p4c 16/16 (damaged and
locked database dialog, Explorer, restore), p4d 9/9 (full screen with test windows, with the user's OK), p4e 5/5 (real Explorer
restart, with the user's OK), p4f 11/11 (Release: Alt+F4, no network, 0 ms CPU paused, hotkey conflict). Phase 3 scripts t1–t6
re-run green.
TESTING ticked: W0 (new), W1, W4, W5, W18, T15, P1, S1, R1. Partly done, still ⏳: W2, W3, W7 (agent part), W10, W12, W13, W15–W17,
M4, M7, P10, D9.

**Performance (Release, collapsed, seeded data, 60 s, no mouse near):** Companion Off 16 ms CPU (two runs; window switches
only), Calm 1,125 ms (1.9 % of one core). Private 64.6–66.6 MB, working set 128–132 MB (Phase 3: 62–64 / 114–119). Paused: 0 ms
in 30 s. Two other runs were thrown away because the panel was opened during them (log), so they measured use, not idle.

**Problems found and fixed on the way:**
- The planned `EVENT_OBJECT_LOCATIONCHANGE` hook cost 63 ms CPU per 45 s while the PC was in use (0 ms with it off): replaced by
  the shell notice `ABN_FULLSCREENAPP` on a hidden helper window; the F11 case still passes.
- A locked database took about 63 s to reach the dialog (two SQLite commands × 30 s default timeout): command timeout now 5 s,
  about 10 s.
- The reminder click action was dropped when Windows moved the balloon to the notification centre: kept until the next one.
- Test-script fixes only (the app was right): t2 expected the "09:30" message for a time without a date ("Pick a date…" is correct),
  t6 broke on PowerShell 5.1 quoting, `Start-TI` failed with no arguments, task-dialog buttons need Space, not Enter.

**Remaining defects / not tested:**
- **Needs the user** (steps at the end of TESTING.md): reboot auto-start (W6), sleep (W7), hibernate (W8), screen off (W9), Win+L
  (W10, declined for now), monitor plug/unplug (W11), resolution and 100/150/200 % scaling (W12, W13), tray icon after Explorer
  restart seen in the flyout (W17), tray menu with the real mouse (W2/W3), real video / Edge F11 / PowerPoint (W15).
- Not seen on screen by me: the reminder toast and pulse ring (verified by logs), the tray icon art (it sits in the hidden-icons
  flyout), the tray menu's look at 125 %.
- Posted tray clicks carry no foreground right, so in tests "Open" from the tray could not take the keyboard focus; a real click
  does (the second-launch path was checked with real focus).
- Memory still over the §6.7 budget (Phase 2 issue); the companion costs about 2 % of a core in Calm.
- Phase 5 items by design: Settings… in the tray menu, the hotkey capture box and its conflict warning, the startup toggle UI
  (`StartupManager.ApplyToggle` ready), the HideInFullScreen toggle (`RecoveryCoordinator.ApplyFullScreenSetting` ready).

**Commit:** "Add tray, startup, single instance, sleep/wake recovery, multi-monitor and full-screen handling" (hash in `git log`) · **Pushed:** yes

**Handoff (for Phase 5, survives /compact):**
- *What exists:* `App.OnStartup` order is single instance → log + handlers → DB (with the error dialog) → Run key → services →
  window → tray → `RecoveryCoordinator.Attach()` → reminders → pipe server → first-run flag → daily backup. `RecoveryCoordinator`
  owns the HwndSource hook (hotkey + power messages), SystemEvents, the full-screen detector and the hidden notice window.
  `IslandWindow` has `OpenFromOutside/Toggle/ToggleFromTray/SetUserHidden/SetPaused/SetFullScreen/AutoShow/Reposition/
  ReapplyWindowStyles/PulseReminder`. `ReminderService.Recompute()` re-arms reminders. All Windows events are logged.
- *Phase 5 must know:* live-apply hooks already exist: `StartupManager.ApplyToggle(on, testRoot)`,
  `RecoveryCoordinator.RegisterHotkey()` (reads `settings.ToggleHotkey`; check `Hotkey.IsRegistered` / `LastError` 1409 for the
  "in use" warning; require `HotkeyGesture.IsAllowedForRebind`), `RecoveryCoordinator.ApplyFullScreenSetting()`,
  `ReminderService.Recompute()` (RemindersEnabled), `IslandWindow.Reposition()` (MonitorDeviceName, offset, margin). Add
  "Settings…" to the tray menu (`TrayIconService` constructor) once the window exists. The tray menu is WinForms; WPF windows
  opened from it need `Activate()`. Test runs must keep `TASKISLAND_ROOT` set (separate mutex, pipe and Run key).
- *Testing tools:* `tools/ui-tests/p4*.ps1` (see TESTING.md "Automated tests"). Tray menu without the mouse: post `0x800` with
  lParam `0x205` (right-click) / `0x202` / `0x203` / `0x405` (balloon click) to the process's `WindowsForms*` windows, then invoke
  the MenuItem with UI Automation. p4d/p4e interrupt the user: ask first. Scripts written by the Write tool are read as ANSI by
  PowerShell 5.1: avoid non-ASCII literals or save with a BOM.
- *Open issues:* the user tests listed above, memory budget, the first-expand hitch, the companion CPU cost.
- *Exact next step:* start Phase 5: `dotnet build` + `dotnet test` (expect 368 green), then PLAN §8 Phase 5 step 1.
