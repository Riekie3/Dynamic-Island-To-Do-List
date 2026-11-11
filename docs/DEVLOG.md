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
| 5 Settings & data | ✅ done 2026-11-04 (open: user tests of mouse hover/sliver/System theme, memory budget) | see Phase 5 entry |
| 6 Testing & optimisation | ✅ done 2026-11-04 (open: memory over budget, M10 CPU, user tests) | see Phase 6 entry |
| 7 Release | 🟡 rc.1 (portable) and rc.2 (installer + portable) released 2026-11-04; 1.0.0 waits for the user's testing, the signing answer and a licence | see Phase 7 entries |

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
- *Added to Phase 5 on 2026-11-04 (user decision after Phase 4):* keep pulse + Surprised face + notification for due reminders, and add
  (1) the **widening pill alert** with the task title (`ReminderPillAlert`) and (2) an **early reminder** setting (`ReminderLeadMinutes`,
  e.g. 1 hour before, new column `EarlyReminderShownAt`). Spec: PLAN §7.9 "Alert style" and "Early reminder", Phase 5 step 10;
  tests T15b-T15d. `ReminderService` / `ReminderScheduler` / `IslandHost` (peek width tween) are the places to extend.


### Phase 5 — Settings & data (2026-11-04, Sonnet 5.5)
**Implemented:** PLAN §8 Phase 5 steps 1-10 (deviations below and in PLAN.md "Phase 5 as built").
- Settings window (6 pages: General, Appearance, Behaviour, Lists, Data, About), opened from the tray ("Settings…") and the panel's ⋯ menu;
  one instance, released (GC) on close. Every control applies live through `SettingsApplier` (sliders save 300 ms debounced).
- Themes: Dark / Light / System (follows Windows live), accent source Default / Windows, density Comfortable / Compact, Mica with solid fallback.
- Data page: export JSON, import (Merge / Replace), backup now, restore (pre-restore copy), open data folder, clear completed with Undo bar.
- Lists page: create, rename, recolour, reorder, delete (move or delete tasks, backup first), default list, pill count scope.
- Hotkey capture box (needs 2+ modifiers) with in-use warning; monitor dropdown; AlwaysShowNotch=false sliver; AutoCollapseSeconds.
- Step 10: reminders on/off, "Remind me before due time" (Off/5/15/30 min/1 h/2 h/1 day), early reminder with the new `EarlyReminderShownAt`
  column (migration 2), and the widening pill alert (title + list dot, 5 s, hover holds it, click opens that list).
**Files created/modified:** Core: `AppSettings`, `TaskItem`, `ReminderScheduler`, `ReminderText`, interfaces. Data: `Migrations`, `Sql`,
`SqliteTaskRepository`. App: new `SettingsApplier`, `SettingsLauncher`, `Appearance`, `Windows/Dwm`, `Views/SettingsWindow`, `Views/DialogWindow`,
`Controls/SettingsControls`, `Themes/SettingsControls.xaml`, `ViewModels/SettingsViewModel(.Lists/.Data)`; changed `App.xaml.cs`, `IslandWindow`,
`IslandViewModel(.Lists)`, `ReminderService`, `TrayIconService`, `RecoveryCoordinator`, `PanelView`, `TaskRow`. Tests: `Phase5RulesTests` etc.
UI scripts `tools/ui-tests/p5a-p5f.ps1`, `seed_p5.py`.
**Tests performed:** `dotnet build` Release + Debug 0 warnings / 0 errors; `dotnet test` **419 pass** (296 Core + 123 Data). UI Automation scripts:
p5b 115 checks (all Settings pages, lists, export/import/backup/restore through the real file dialogs, hotkey, auto-collapse, sliver,
restart persistence) all pass in the last run except the memory line, which is now informational (see below); p5a (early reminder + pill) 23 checks pass;
p5c 22 checks pass. TESTING.md: T15b, T15c, T15d, L4, L11, L13, W5, W16, D5, D7, D8, R1 ✅; UI10, UI11, UI12, M4, W2, D6, D10 stay ⏳ with notes.
- **Memory (acceptance):** Release, seeded data. Idle 88 MB private. Settings opened and closed with no automation client attached:
  83 MB open, 84 MB after closing (28 s) = back at baseline. With a UI Automation client attached it grows to 109-120 MB after closing;
  that is the test tool keeping the accessibility tree alive (p5d vs p5e), not confirmed as an app leak. The Phase 2 memory budget (§6.7) is still exceeded: idle is 88 MB private on this seeded Release run.
**Deviations:**
- The pill alert replaces the Surprised face on the island while it shows (the face still shows on the open panel and when the pill setting is off).
  Early reminders never show the face.
- Settings window is 640×580 (plan 560×520) for the nav and cards. Info grid is 420 DIP wide for the alert.
- A hotkey conflict can only be seen at start-up / after wake (a held shortcut swallows the key before the box sees it).
- The island's UI Automation name ("TaskIsland reminder: …") is set in code but my scanner never sees it, so it is not tested.
- Import does not show the Thinking face (not built).
**Remaining defects / not tested:**
- 👤 see TESTING.md "Phase 5 — steps for the user": real mouse hover/click on the pill alert, sliver reveal with the mouse, System theme and
  Windows accent following live, Mica look, second monitor dropdown.
- Replace import through the UI (D6), lead = 1 hour waited out in real time, per-key re-read of every setting after restart (D10).
- Phase 4 user tests (sleep, reboot, monitors, scaling) still open. Memory budget still over.
- Test-script fixes only: WinForms swallows the first button-up after a double-click (p5c sends it twice); file-dialog name box is a Pane.
**Commit:** "Add settings window, themes, JSON import/export and backup/restore" plus earlier parts 9093187 and db5f90a; final hash in `git log` · **Pushed:** yes (see below)

**Handoff (for Phase 6, survives /compact):**
- *What exists:* full app per PLAN through Phase 5. Settings live-apply is centralised in `SettingsApplier` (keyed by setting key); add a key there
  when adding a setting. `App.ApplyTheme` + `Appearance.ApplyAccent/ApplyDensity` drive look. `SettingsLauncher.Close()` drops the window and GCs.
  `ReminderService` handles early + due reminders (marks `EarlyReminderShownAt` / `ReminderShownAt`). `IslandWindow` has the alert API
  (`ShowReminderAlert`, `EndAlert`) and sliver/auto-collapse timers.
- *Phase 6 must know:* test scripts are `tools/ui-tests/*.ps1` (Debug build, temp `TASKISLAND_ROOT`; p5e is the Release memory control). Debug options:
  `--settings [page]`, `--snap-settings <png>`, `--full-motion`, `--pop-seconds`. File dialogs need SetFocus + SendKeys with AutomationId 1001/1148.
  Kill TaskIsland.exe before building. No hooks, no shell spawn except explorer.exe for the data folder and the two About links on click.
- *Open issues:* memory over budget (idle 88 MB private seeded Release), first-expand hitch, companion CPU, the 👤 lists above.
- *Exact next step:* start Phase 6: `dotnet build` + `dotnet test` (expect 419 green), then PLAN §8 Phase 6 step 1.

### Fix after Phase 5 — panel opacity slider had no visible effect (2026-11-04)
- Cause: the cached shadow layer under the panel (`IslandHost._shadow`) is a solid SurfaceBrush border, so it stayed 100 % opaque below the panel body and hid the opacity setting.
- Fix: the shadow is now hosted in a Grid whose Clip cuts out the panel rectangle (CombinedGeometry Exclude), so only the soft shadow outside the panel remains.
- Verified: `tools/ui-tests/op.ps1` (Release, screenshots at 100 % and 70 %): at 70 % the text behind the panel shows through, at 100 % it is solid. 419 unit tests green.


### Phase 6 — Testing & optimisation (2026-11-04, Sonnet 5.5)
**Implemented:** PLAN §8 Phase 6 steps 1-5. One code/config change: `TieredCompilation=false` and `TieredPGO=false` in `TaskIsland.App.csproj`
(PLAN §6.7 rule 9 updated). No other behaviour changed.
**Files created/modified:** `TaskIsland.App.csproj`, `docs/PLAN.md` (rule 9), `docs/TESTING.md` (results + "Phase 6 — steps for the user"), new test tools
`tools/ui-tests/perf.ps1` (measurements), `p6a.ps1` / `p6b.ps1` (first run, ±400 offset, daily backup), `exp1.ps1` / `exp2.ps1` (memory experiments),
`memtry.ps1` (runtime-setting experiments), `setkey.py`. `perf-results/` holds the raw output of the runs below.

**Measurements (§6.7 table, Release, this laptop 1920×1080 @125 %, empty data unless noted; "now" = tiered compilation off):**

| Metric | Phase 2 baseline | Phase 6 before change | Phase 6 now | Target |
|---|---|---|---|---|
| CPU idle, 60 s, Companion Off | 0.0 ms | 0 ms | **0 ms** (3 runs) | 0.0 % ✅ |
| CPU idle, 60 s, Calm companion | - | (Phase 4: 1.9 %) | 1,500 ms = **2.5 % of one core** | measured cost is the budget |
| CPU 30 min, 500 tasks, Off, IdleReactions Normal | - | **2,297 ms = 0.128 % of one core** (= 0.008 % of the 16-thread CPU) | not re-run | < 0.05 % ❌ |
| GPU idle (GPU Engine counter, 30 s) | 0 % | 0 % | **0 %** (Calm: 0.22 % max sample) | 0 % ✅ |
| Private memory idle (60 s) | 52-56 (no panel) / 72-73 | 77.6 MB | **64.1 / 64.5 / 65.1 MB** | ≤ 50 MB ❌ |
| Working set idle | 98-102 | 143.9 MB | **125.7-126.0 MB** | ≤ 70 MB ❌ |
| Private after the panel was opened once | 129 MB | 143-152 MB | **129-140 MB** | - |
| Cold start to visible window | 0.84-0.93 s | 0.82 s | **0.99 / 1.06 / 1.13 s** | < 1.2 s ✅ (little room) |
| Threads | 11 | 15 | 15 idle (13-18 after use) | none of ours besides the pipe server ✅ |
| Disk writes while idle (process I/O counters, 60 s) | - | 0 | **0 ops / 0 bytes** | none ✅ |
| Network (TCP + UDP endpoints of the process) | - | 0 | **0 + 0** in every run | empty ✅ |
| Efficiency mode | on after 16 s | - | not re-checked (code unchanged) | ✅ |

- **Leak checks.** 30 min idle with 500 tasks (Companion Off, tiered compilation on): private memory 82 → 74 MB (not growing), working set 143-153 MB flat.
  200 expand/collapse cycles (tiered compilation off): 128.7 MB after cycle 10, 137.6 MB at cycle 110, 139.9 MB at cycle 200 (+11 MB over 190 cycles,
  flattening; with tiered compilation on: 143 → 152 MB over 50 cycles). Against the *start* (64.9 MB) the cycles end +75 MB, because the first expand adds
  ~64 MB once (panel rendering, `textinputframework.dll`, `UIAutomationCore.dll`, `CoreUIComponents.dll`, `CoreMessaging.dll`, ... measured with `exp1.ps1`) and
  that memory is not given back (after one cycle it can drop to ~94 MB, after three it stays at ~132 MB). **P8 therefore fails its 5 MB criterion**; I saw no
  runaway growth. Handle count in the 30 min run rises ~12 per minute and falls back to 639 when the GC runs (639 → 807 → 639): bounded, cause not found.
- **500 tasks:** idle numbers within about 4-15 MB of the empty database; the list is virtualised (7 rows). Smooth scrolling is not judged (user step).
- **CPU of the random face pops (M10).** The 30 min run shows a ~0.2 s start-up burst, 0 ms in quiet minutes, and 0.4-0.7 s per pop (6 pops).
  Average 0.128 % of one core vs the 0.05 % target. The cost is WPF layered-window rendering while the face animates (~3 ms/frame), as measured in Phase 2/3.
  Halving it would need a 30 fps cap for the face, which I did not do without your say (it would change how the morph looks).
- **What was tried for memory (honest list):** `DOTNET_TieredPGO=0` -1 MB; `GCgen0size`, `GCConserveMemory`, `GCgen0MaxBudget`, `TC_BackgroundWorkerTimeoutMs` 0-2 MB (noise);
  `TC_QuickJit=0` and `ReadyToRun=0` no gain; **`TieredCompilation=0` -13 MB private / -18 MB working set** → adopted. A forced compacting GC after 10 s at rest
  freed only ~4 MB, so the rest is not managed garbage; reverted (extra code for nothing). Not tried: dropping the auto-focus of the quick-add box (the
  text-input DLLs load on first focus; it would cost the "start typing at once" behaviour), a 30 fps cap. No working-set trimming was used (§6.7).
- **Code review (§6.7 rules 1-8 as a grep checklist):** no `RepeatBehavior`/`Forever`/GIF/spinner anywhere; all `DispatcherTimer`s are one-shot (every Tick stops
  itself or is re-armed by the event that needs it; the companion `_life` timer is the documented Calm/Lively cost); only the Morph property is animated
  (render-time, no Width/Height animations); the only `Effect` is the one cached `DropShadowEffect`; no Blur/OpacityMask; all theme brushes carry `po:Freeze`,
  brushes made in code are frozen (the one unfrozen gradient is in the debug `--snap` path); the 60 fps frame cap is set in `OnStartup`. `async void`: only
  `OnStartup` (inside try/catch with the exception handlers hooked). Static event subscriptions (`SystemEvents`, `SystemParameters`) are unhooked in
  `Dispose`/`OnExit` except `MotionPolicy` and `ReactionDirector`, which live as long as the process. Build: 0 warnings, so no nullable warnings.
- **Security-software pass (§7.11):** grep of all of `src/` for `SetWindowsHookEx`, `OpenProcess`, `ReadProcessMemory`, `WriteProcessMemory`, `CreateRemoteThread`,
  `Assembly.Load(`, `powershell`, `cmd.exe`, `schtasks`, `reg.exe`, `HttpClient`, `Socket`, `WebRequest`: **no hits**. `Process.Start`: `explorer.exe` for the data folder
  (`App.xaml.cs`, UseShellExecute false) and the two About links (`SettingsViewModel.Data.cs`, only on click). Registry writes: only the HKCU Run key
  (`StartupRegistration`); reads: `Themes\Personalize` and the DWM colours. Defender custom scan of the Release folder (176 MB, signatures of 2026-11-04 03:19,
  `-DisableRemediation`): "found no threats". No Defender detection for TaskIsland during about 2 hours of runs.
- **TESTING.md ticked this phase:** L1, UI4, D9, P7, P11, S3 ✅; P2, P2b, P8, M10 ❌ with numbers; notes added to P1, P3, P9, P10, S1, S2, S7, W18.
  Scripted checks: `p6a.ps1` 19/19 (L1 5, UI4 9, D9 5), run with the final build; regression run of `p5b.ps1` (Settings window, Debug build with tiered compilation off): 114 PASS, 0 FAIL (the memory line is informational). `dotnet test` 419 green.
- **Not done / honest limits:** the first-expand hitch (~100 ms of dropped frames) was not looked at again (needs frame capture; user step 1 asks for your eyes);
  UI2, UI10-UI14, M3-M9, T10, T11, W2, W3, W10, W12, W13, W15, W17, D6, D10 stay ⏳ (mouse, Windows settings, scaling or real sleep needed); P4 needs your eyes;
  P10 hidden-by-full-screen GPU not measured; installer, publish, signing and S2/S4-S9 are Phase 7.
**Remaining defects:** memory over the budget (64 MB fresh, 130-140 MB once the panel was opened; target 50 MB); random pops give 0.128 % CPU (target 0.05 %);
first-expand hitch; slow handle creep that the GC clears; the 👤 lists in TESTING.md (Phase 4, 5 and now 6 steps).
**Commit:** "Fix defects and optimise performance" (tiered compilation off + Phase 6 results; hash in `git log`) · **Pushed:** see below

**Handoff (for Phase 7, survives /compact):**
- *What exists:* the full app through Phase 6, 419 unit tests green, Release build 0 warnings. Runtime config now has `TieredCompilation=false`.
- *Phase 7 must know:* cold start is 0.99-1.13 s with tiered compilation off (limit 1.2 s): measure it again after the ReadyToRun publish; if it is still near
  1.2 s consider turning tiering back on for the published build only. Trim the `runtimes/` folder (SQLite native files for other platforms) in the publish.
  Checks that need an installer: S2 (scan publish + installer), S4-S9, R2-R6. The credit text for LICENSE, installer publisher and welcome text is in PLAN
  "Credit: RieSources" (About page, exe properties and README already have it). Test scripts: `tools/ui-tests/perf.ps1 -Mode idle|cycles|big` (Release build, own
  temp data folder; a second TaskIsland with the same data folder just asks the first to expand, so never run two against the same root).
- *Open issues:* see "Remaining defects"; the user's results for the Phase 4/5/6 steps in TESTING.md are not in yet.
- *Exact next step:* get the user's results for the 👤 steps (or their go-ahead), then start Phase 7: `dotnet build` + `dotnet test` (expect 419 green), then PLAN §8 Phase 7 step 1.

### Phase 7 (portable part) — Production release, portable first (2026-11-04, Sonnet 5.5)
**Decision (user, 2026-11-04):** "go to phase 7 but no need to build installer yet, release a portable first. After I test for a few days, only then we build the installer."
So this entry covers PLAN §8 Phase 7 steps 1, 2, 3, 4c (scan of the portable files), 6 and the portable half of step 7. **Not done on purpose:** step 4 (installer
`installer/TaskIsland.iss`), 4b (code-signing question), 5 (install/upgrade/uninstall test), the installer scan and the `v1.0.0` release with the installer.
- **Icon:** `tools/IconGen` (small WPF console project, not in the solution) renders the 256 px master (dark rounded square, blue circle, white tick) and writes
  `src/TaskIsland.App/Assets/TaskIsland.ico` with 8 PNG-compressed sizes (16, 20, 24, 32, 40, 48, 64, 256). `<ApplicationIcon>` set. The tray icon stays drawn in
  memory by `TrayIconArt` (it follows the taskbar theme and the paused state, so shipped files would not add anything).
- **Version:** `1.0.0-rc.1` in `Directory.Build.props` (a release candidate while the user tests; the About page shows it; the installer build will make it `1.0.0`).
  `AssemblyTitle`/`Description` set so the exe's file description is "TaskIsland". Git short hash is part of the informational version.
- **Publish:** `tools/publish.ps1` = `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:DebugType=none` into `publish\win-x64`,
  copies LICENSE and `docs/PORTABLE.txt` (as README.txt) in, and zips it as `publish\TaskIsland-1.0.0-rc.1-win-x64-portable.zip` (66.6 MB). Folder: 264 files, 160 MB,
  no `runtimes\` folder (a RID-specific publish only copies the win-x64 native SQLite), no language sub-folders (`SatelliteResourceLanguages=en`), no .pdb.
  `PublishTrimmed` is not used (WPF). `msquic.dll` and `createdump.exe` come from the .NET runtime pack and were left in.
- **Measured on the published exe (Companion Off, empty data, 3 runs, `perf-results/publish_idle_off.txt`):** cold start **0.78 / 0.84 / 0.86 s** (the plain Release build
  with tiering off was 0.99-1.13 s: ReadyToRun more than pays for the tiering-off cost, so tiering stays off); private **62.4-63.2 MB**, working set 123-124 MB; CPU 0 ms
  over 60 s; disk writes 0; GPU 0; TCP 0, UDP 0. Memory is still above the 50 / 70 MB targets (same open defect as Phase 6; ReadyToRun saved ~2 MB private).
- **Checks:** `dotnet build` Release 0 warnings, `dotnet test` **419 green** (296 + 123). Zip extracted into a fresh folder and started: island, tray icon, hotkey, DB and
  backup created (log). Defender: signatures updated first (1.463.119.0), custom scan with `-DisableRemediation` of `publish\win-x64` and of the zip, both "found no threats";
  no TaskIsland entry in the Defender threat history. File properties of all five TaskIsland files: Company RieSources, Product TaskIsland (TESTING S7 ✅, S2 portable half).
- **Docs:** `docs/RELEASE_NOTES.md` (what it is, how to run, SmartScreen "More info, Run anyway", honest known limits), README (download section, status, publish command),
  `docs/PORTABLE.txt`, `LICENSE` (only "Copyright (c) 2026 RieSources, all rights reserved"; **the user has not chosen a licence yet**), `THIRD-PARTY-NOTICES.txt`.
  Credit RieSources: About page, exe properties, README footer, LICENSE, release notes, PORTABLE.txt. The installer's publisher/welcome text comes with the installer.
- **Released (portable, pre-release):** tag `v1.0.0-rc.1` on commit 0953be9, GitHub pre-release
  https://github.com/Riekie3/Dynamic-Island-To-Do-List/releases/tag/v1.0.0-rc.1 with `TaskIsland-1.0.0-rc.1-win-x64-portable.zip` (66.6 MB, SHA-256
  5A6B00BC67FF804C73DE89B770057ABD4C185A26C2CCE32BC5BBEFA0BE55B7C1, re-downloaded from GitHub and the hash matched). Marked pre-release so the later `v1.0.0` installer release is the "latest".
- **Mistakes this phase (so they are not repeated):** a background chain started with `&` inside a subshell kept running after the tool call "ended", so the second
  chain collided with it on the same data folder (the same trap as in Phase 6); both were killed by process path and the three runs repeated once, cleanly.
- **Open for the installer phase:** PLAN steps 4 (Inno Setup script; check `winget`/Inno Setup 6 is installed), 4b (ask the user: sign or not), 5 (install, upgrade,
  uninstall tests), the installer scan (S2 rest), S4-S6, S8, S9, R3-R5, then `v1.0.0` with `TaskIsland-Setup-1.0.0-x64.exe` plus the portable zip; the `Version` becomes `1.0.0`.
  Also remember to ask which licence the user wants. The user's 👤 steps (Phases 4, 5, 6 in TESTING.md) are the "few days of testing": record their findings first.
**Handoff (for the installer phase, survives /compact):**
- *What exists:* everything through Phase 6, plus the portable build: `tools/publish.ps1`, IconGen, icon, version 1.0.0-rc.1, release notes, README, LICENSE.
- *Exact next step:* get the user's test findings (defects first). Fix them, bump to `1.0.0-rc.2` if needed and re-run `tools/publish.ps1`. When the user says the testing is done:
  install Inno Setup 6, write `installer/TaskIsland.iss` per PLAN step 4, ask the signing question (4b), scan, run the step 5 tests, set Version `1.0.0`, then tag and release.
- *Perf scripts:* `tools/ui-tests/perf.ps1` takes `TI_EXE` (env var) to measure any exe; never run two chains against the same `%TEMP%\TaskIslandP6<mode>` folder.

### Change — wide reminder pill with a blinking red border (2026-11-04, Opus 5.5)

User request (while testing rc.1): "for the reminder, can you make it blinking red light colour on the border and the width expand
for 50% of the screen or maybe we can set it".

- **Width:** new setting `ReminderAlertWidthPercent` (Settings > Behaviour > Reminders > "Reminder width": Compact, 30, 40, **50**,
  60, 75 % or Full width; default 50). `IslandPlacement.AlertWidthDip` (Core, unit-tested) turns it into DIPs for the island's
  monitor and keeps the pill on the screen around the island's centre (offset included, 8 DIP gap to each edge). The island host and
  window were a fixed 452 DIPs, so `IslandHost` got a live width (`SetHostWidth`) and `IslandPositioner.Apply` a width argument.
  The window widens only for the alert (growing: window first, then host; narrowing: host first) and goes back to 452 once the
  island has rested, or after the expand animation when the alert is clicked. The alert text gets 15 px instead of 13 px when the
  pill is 600 design units or wider.
- **Red border:** new setting `ReminderBlink` (default on, "Blink a red border"). `IslandHost.StartAlertBorder` adds a red line
  (#FF3B30, 2.5 px) with a soft red glow (8 px, 40 %) on the island's own geometry, so it follows the morph. Opacity 1 -> 0.12 and
  back, 450 ms each way (about 1.1 blinks a second, under the 3 a second flashing limit), sine ease. Steady in reduced motion. With
  the blink on, the accent pulse is skipped (it would compete and needs 12 % extra window width). The ring is removed when the alert
  ends, so no animation runs afterwards.
- **Debug:** `--snap-delay <ms>` for `--snap`; the snapshot logs the border state. `tools/ui-tests/alert_snap.ps1` seeds a due task
  and snapshots the pill. `p5a.ps1`/`p5c.ps1` no longer expect the fixed width 400.

**Measured (Debug, 1920x1080 at 125 %):** pill 960 px = 50 % of the screen, window 990 px, back to 565 px 0.17 s after the alert
ended; panel opened during the alert from the 990 px window and narrowed after the expand. Border opacity 0.99 at one moment and
0.17 half a blink later. Settings: rows present, 50 % shown, disabled with "Widen the island" off, 75 % saved.
Tests: 438 green (315 + 123, 19 new in `ReminderAlertTests`).

**Mistakes:** the first snapshots of the blink always landed on its dim moment (the snapshot fires 2.2 s after start, which is
about 0.4 s into a blink cycle), so the border looked missing; a steady (reduced motion) snapshot and logged opacities showed it
works. `-Extra '--snap-delay',1750` through `powershell -File` arrived as one string, so the delay first had no effect.

**Not done:** not in the released rc.1 portable zip; needs a new build (rc.2) for the user to see it in the copy they run.

### Phase 7b — Installer and release 1.0.0-rc.2 (2026-11-04, Opus 5.5)

User: "yes, release rc.2 and also proceed with 7b phase for this release". So rc.2 ships the installer next to the portable
zip; 1.0.0 itself still waits for the user's days of testing, the code-signing answer (4b) and a licence.

**Implemented:**
- Version `1.0.0-rc.2` (commit 1d71a31); `tools/publish.ps1` → `TaskIsland-1.0.0-rc.2-win-x64-portable.zip` (66.6 MB).
- `installer/TaskIsland.iss` (Inno Setup 6.7.3, already installed per-user in Phase 0; winget: no newer version) per PLAN
  step 4: AppId `{39F89B18-992E-4E46-8881-1E8CCC6F1D16}` (fixed, never change), publisher RieSources, per-user
  (`PrivilegesRequired=lowest`, `{localappdata}\Programs\TaskIsland`), x64 only, Windows 10 1809+, `AppMutex` = the app's
  real lock, `CloseApplications`, modern wizard, LZMA2, version info, Start menu entry, optional desktop icon, "Launch
  TaskIsland" at the end. No plugins, no `Exec` of cmd/PowerShell. Uninstall asks "Also delete your tasks and backups?"
  (default No; silent uninstall keeps them). Output `TaskIsland-Setup-1.0.0-rc.2-x64.exe` (50.8 MB).
- **Deviation:** no Run value from Setup and no "Start with Windows" task (PLAN step 4 updated with the reason): the app owns
  the entry; the uninstaller removes it only when it points into the install folder.
- Test switches: AppId, title, app lock, Run key, data folder and output name are ISCC defines that default to the release
  values. `tools/build-installer.ps1` builds; `tools/ui-tests/installer-test.ps1` builds a TEST installer with its own values.
- Code signing (4b): not asked before this release; rc.2 is **unsigned** (the plan's default). The question goes to the user
  before 1.0.0.

**Tests performed:**
- `tools/ui-tests/installer-test.ps1`: **31 checks, all pass** (install without admin mode, files, Start menu, Apps entry,
  app runs and registers Start with Windows; upgrade refused while running, then over the top with tasks kept; uninstall
  keeps data by default and removes program, Start menu, Apps entry and its Run value; reinstall finds the tasks; uninstall
  with Yes deletes the data and leaves a foreign Run value alone; the RELEASE installer refuses while the real app lock is
  held — held by the test itself because the user had exited rc.1 at 18:03:48 — and installs nothing; the user's real Run
  value unchanged). Nothing left behind (no test folder, Apps entry or registry key).
- Defender: signatures 1.463.119.0; installer, `publish\win-x64` and zip: no threats. 10-minute run of the rc.2 build in a
  test data folder: see the result line below.
- TESTING.md: S2, S4, R3, R4, R5 ✅. S5 (Task Manager startup toggle), S6 (Controlled Folder Access), S8 (signed only),
  S9 (VirusTotal, needs the user's OK) and R6 (second PC) stay open.

**Mistakes:** Python's deprecation warning on stderr stopped the PowerShell test (`ErrorActionPreference Stop`): run with
`-W ignore`. The uninstaller removes its folder a moment after its Apps entry, so the clean-up check ran too early. The
"delete data?" message box buttons expose no UI Automation pattern, and the uninstaller's second phase is named `_unins.tmp`
(ProcessName includes `.tmp`): the test now finds the dialog by that process and the folder name in its text and sends
BM_CLICK to that one button. A message box left open by an aborted run kept its log locked until answered.

**Rebuild after the README request (same day):** the user asked to "update all the documentation and how to use/install
and also screenshot". README rewritten as a full guide with nine screenshots (`tools/ui-tests/readme_shots.ps1`: the app
renders its own window, no screen capture). The first peek screenshot showed two texts on top of each other: the sample
data's overdue reminder fired while the demo forced the hover peek. Real hovering was already blocked during a reminder,
but `StartPeek` itself had no guard; it now returns while a reminder pill shows. Rebuilt as 1.0.0-rc.2+9bea5b3: 438 tests
green, Defender 1.463.121.0 clean on installer, folder and zip, installer test 31/31 again.
CPU check while preparing the release: right after a first start with default settings, rc.1 and rc.2 side by side used
1.64 % and 2.05 % of one core over 4 minutes (random face pops, so noise); not caused by the reminder change, which runs
nothing without a reminder. It is the open M10 item; the release notes now say so instead of only the 30-minute 0.13 %.

**Release:** see the line below.

**Handoff (for 1.0.0, survives /compact):**
- *What exists:* rc.2 installer + portable on GitHub (pre-release). `installer/TaskIsland.iss`, `tools/build-installer.ps1`,
  `tools/ui-tests/installer-test.ps1` (re-run it for every installer change; about 2 minutes).
- *Decided 2026-11-04 (PLAN 4b):* code signing: **no, stay unsigned** (user: "No, stay unsigned"). 1.0.0 ships unsigned with the
  SmartScreen "More info > Run anyway" note; S8 is n/a.
- *Decided 2026-11-04:* licence **MIT**, (c) 2026 RieSources (user: "MIT (open source)"). LICENSE, README and release notes updated after rc.2 was published (rc.2's own files still carry the old all-rights-reserved text).
- *Waiting on the user:* testing findings, optional VirusTotal OK.
- *Exact next step:* fix findings; then Version `1.0.0`, `tools/publish.ps1`, `tools/build-installer.ps1`, scans, installer
  test, tag `v1.0.0`, `gh release create v1.0.0` with both files (not a pre-release).

### Change — lifted drag preview for list chips and task rows (2026-11-10, DeepSeek V4.1 Flash)

User request: "improve the list/group drag and drop to have like animation like the button is pulled out. instead of play drag
and drop to the front and back".

**Implemented:**
- New `Controls/DragLift.cs`: snapshots the chip/row with `RenderTargetBitmap` before dimming it, then floats the copy in the
  panel's adorner layer 3 px above the pointer. It follows the whole drag through `DragDrop.GiveFeedback` plus the screen cursor
  position (`Mouse.GetPosition` is unreliable inside the OLE drag loop). The copy starts at 0.65 opacity, lifts to 1.05 for chips
  / 1.03 for rows with a short overshoot, and the source dims to 0.2. An opaque surface backing stops rows underneath showing
  through transparent task rows; a 1 px outline replaces the too-expensive `DropShadowEffect`. Finite `Anim.Keys` calls,
  render-only opacity/scale, and reduced motion sets the final values instantly.
- New `Views/PanelView.Drag.cs`: both chips and rows keep a dim placeholder, slide the items between the source and the projected
  slot aside by one chip width / row height, and move the gap to the projected slot. Positions use base layout coordinates
  (excluding the in-flight translate) so an animation cannot feed back into the next update. Drop completion stays on screen until
  `MoveListToAsync` / `MoveAsync` finishes; Esc, cancel and drop outside restore immediately. Row rules are unchanged:
  `CanReorder`, same `(ListId, Priority)`, single list in the Active filter, completed/editing rows not draggable.
- Removed the old `ChipDropLine` and the `DropMarker` / `DropAbove` / `DropBelow` insertion lines. `TaskRow` now exposes
  `ShiftForDrag` / `ResetDragShift` / `DragShiftY` and starts `PanelView.BeginTaskDrag`; the panel registers `GiveFeedbackEvent`
  in its constructor. A row drag starts on the title area; the checkbox still toggles instead of dragging.
- Updated `README.md` (drag now lifts and makes room) and `docs/TESTING.md` T10/T11 plus the Phase 6 mouse step.

**Files created/modified:** `Controls/DragLift.cs` (new), `Views/PanelView.Drag.cs` (new), `Views/PanelView.xaml`,
`Views/PanelView.xaml.cs`, `Views/TaskRow.xaml`, `Views/TaskRow.xaml.cs`, `ViewModels/TaskItemViewModel.cs`,
`tools/ui-tests/rowdrag.ps1` (new), `README.md`, `docs/TESTING.md`, this log.

**Tests performed:** `dotnet build TaskIsland.sln -c Debug` 0 warnings / 0 errors. `dotnet test TaskIsland.sln -c Debug`:
**444 passed, 0 failed** (315 Core + 129 Data).
- `tools/ui-tests/chipdrag.ps1` (real mouse): **5/5 pass** — start order, Office Work dragged in front of Personal, panel still
  open, dragged back behind Personal, plain click does not reorder.
- New `tools/ui-tests/rowdrag.ps1` (real mouse): **6/6 pass, twice in a row** — seeded `Draft roadmap` as a second Medium task
  in Office Work, dragged `Prepare slides for Monday` below it; DB order `Draft roadmap+Prepare slides for Monday`, panel still
  open, a Medium drop on a High row leaves the order unchanged, a plain click does not reorder, and the panel order is still
  correct after a restart. The test clears the seeded overdue due date before launch: the startup reminder pill resizes the panel
  and made the first version fail intermittently when the drag began during the alert.
- Visual check with screenshots (temp `rowpreview` / `dragpreview` folders): the chip and row copies follow the pointer across the
  chip row and quick-add strip, the source is dimmed, the gap opens, and the task-row ghost has a solid surface with no text
  show-through. Screenshots inspected after the opaque backing and 3 px offset change.
- Only capture/visual checks, not unit tests, cover the animation math; the reorder rules themselves are unit-tested.

**Remaining defects / not tested:** drag remains mouse-only (Alt+Up/Down still works). The "not allowed" cursor during a
blocked group drop was not observed directly; the test asserts the order stayed unchanged. Reduced motion was not re-captured,
but the code path sets the lift/gap values without animation.

**Release:** [v1.0.2](https://github.com/Riekie3/Dynamic-Island-To-Do-List/releases/tag/v1.0.2) published 2026-11-10,
built from `ec6d26f` (`1.0.2+ec6d26f`). Portable zip 66.7 MB, installer 50.8 MB; both re-downloaded from GitHub and their
SHA-256 hashes matched. Hashes and checks in `docs/RELEASE_NOTES.md`.

**Commit:** `499ab13` (drag preview, tests and docs), `ec6d26f` (Version 1.0.2), `7067add` (release notes, README, hashes).

**Handoff (for the next drag change, survives /compact):** `DragLift.Start` is the only lift implementation; it needs an adorner
layer and returns null otherwise. `GiveFeedback` is what keeps the copy under the pointer between drop targets; `DragOver` only
fires over `AllowDrop` elements. Chip preview widths come from `ChipSlots()` (`element.ActualWidth`, with the chip margin already
in the slot); row preview heights are captured when the drag starts. When a drag ends with `state.Dropped == false`, the
`finally` in `Chip_PreviewMouseMove` / `BeginTaskDrag` cleans up; when it is true, `Finish*DropAsync` owns cleanup after the move.
Re-run `chipdrag.ps1` + `rowdrag.ps1` after any change to drag thresholds, templates or margins. Any UI test that seeds an
overdue task must clear it before a mouse drag: the startup reminder pill changes the panel width and invalidates the captured
row coordinates.

### Maintenance — backup retention, delete-backups and full reset (2026-11-10, DeepSeek V4.1 Flash)

User request: "pre-delete backup from the list you deleted also should be automatically remove after 24h", "change newest 7
automatic daily backups to 3 only", "add a button to delete all data folder for backup", and a reset button at the bottom of
Settings > Data that returns the app to a first-run state. The user also asked for a live same-priority reorder test.

**Implemented:**
- `BackupService`: `KeepAutoBackups` 7 → 3; new `PreDeletePrefix` and `PreDeleteRetention` (24 h); new
  `PruneExpiredPreDeleteBackups()` (age from the file's last-write time) and `DeleteAllBackups()` (every file in the
  Backups folder only). The daily check now prunes expired pre-delete copies and extra automatic backups even when daily
  backups are switched off or not due yet. `App` repeats the pre-delete prune hourly while it stays open.
- New `DataMaintenanceService.ResetToFirstRunAsync()`: clears Tasks/Lists/Settings, resets the id counters, re-inserts the
  migration-1 starter lists and DefaultListId, then deletes every backup. The schema is kept, so the running app reloads
  without a restart.
- Settings > Data: **Delete all backups…** and **Reset everything…**, both with a destructive confirmation. Reset also
  cancels debounced settings saves and the task Undo bar, applies the default settings and reloads lists/tasks.
- UI copy now says the newest 3 are kept and pre-delete copies are removed after 24 h. Docs: README, PLAN §4.5, TESTING
  D9/D11/D12.

**Files created/modified:** `src/TaskIsland.Data/BackupService.cs`, `src/TaskIsland.Data/DataMaintenanceService.cs` (new),
`src/TaskIsland.App/App.xaml.cs`, `src/TaskIsland.App/Services/SettingsApplier.cs`,
`src/TaskIsland.App/ViewModels/SettingsViewModel.cs`, `SettingsViewModel.Data.cs`, `SettingsViewModel.Lists.cs`,
`IslandViewModel.cs`, `IslandViewModel.Lists.cs`, `Views/SettingsWindow.xaml`,
`tests/TaskIsland.Data.Tests/BackupServiceTests.cs`, `DataMaintenanceServiceTests.cs` (new), `tools/ui-tests/backupui.ps1`
(new), `README.md`, `docs/PLAN.md`, `docs/TESTING.md`, this log.

**Tests performed:** `dotnet build TaskIsland.sln -c Release` 0 warnings / 0 errors; `dotnet test -c Release --no-build`
**451 passed, 0 failed** (315 Core + 136 Data).
- `tools/ui-tests/backupui.ps1` (new, temp data folder), **12/12 pass**: both buttons visible on the Data page; Delete all
  backups removed all 4 fake backup files and kept the seeded tasks; Reset removed the tasks, restored Personal + Office
  Work and the default Dark theme, deleted the backup file, and left the Settings window responsive. Re-ran on the final
  Debug build after the version bump: 12/12 again.
- Live installed 1.0.2 (user's real database), **8/8 pass**: panel opened, Office Work showed both High tasks; dragging
  `Training ISO wei yee` below `Dr Chen monitor` saved that order, dragging it back above restored the original order; the
  panel stayed open through both drags and was closed again afterwards. Database order was checked after each drop.
- `chipdrag.ps1` / `rowdrag.ps1` were not re-run because drag code did not change.
- Release build `1.0.3+5bdd484` published by `tools\publish.ps1` (portable zip 66.7 MB) and `tools\build-installer.ps1`
  (installer 50.8 MB). `installer-test.ps1`: **29 checks pass**, real-installer check skipped because TaskIsland is
  already installed. Defender signatures 1.463.222.0: custom scans of the installer, `publish\win-x64` and the zip found
  no threats; no TaskIsland entry in threat history.

**Remaining defects / not tested:** existing extra automatic backups shrink to 3 at the next startup/recovery (not
immediately when this build is installed). Reset keeps `Logs` on purpose and lets the default `LaunchAtStartup=true`
re-add the Run entry. The hourly prune was verified by unit test, not by leaving the app open for a day.

**Release:** [v1.0.3](https://github.com/Riekie3/Dynamic-Island-To-Do-List/releases/tag/v1.0.3) published 2026-11-10
as the latest release, built from `5bdd484` (`1.0.3+5bdd484`). Portable zip 66.7 MB, installer 50.8 MB; both were
re-downloaded from GitHub and their SHA-256 hashes matched. Hashes and checks in `docs/RELEASE_NOTES.md`.

**Commit:** `739f4da` (retention, reset, tests and docs), `5bdd484` (Version 1.0.3), `1886cdb` (release notes, README,
hashes).

### Release 1.0.4 — review fixes (2026-11-10, Opus 5.5)

Review of 1.0.2/1.0.3 found three small issues, all fixed in `3760ff9`: a locked old backup no longer stops the daily
backup (pruning skips files it cannot delete); Reset compacts the database (`VACUUM` + `wal_checkpoint(TRUNCATE)`) so
deleted rows are gone from disk; Reset no longer restarts the Tasks id counter (the reminder service remembers ids).
3 new tests, each confirmed to fail on the 1.0.3 code. 454 tests green, `backupui.ps1` 12/12, installer test 29 pass
(real-install check skipped), Defender clean. Built `1.0.4+dde6e2b`; release v1.0.4, hashes in RELEASE_NOTES.

### Sileo-style notifications: tried and cancelled (2026-11-11, Opus 5.5 / Sonnet 5.5)

The owner asked to adapt the Sileo toast look (github.com/hiaaryan/sileo, MIT) for TaskIsland. Phases S1-S4 were built
and pushed (commits `3a54290`, `803745b`, `883fbb2`: reminder as a toast card, Undo/notice toasts, Settings data toasts),
then the owner cancelled the plan before any release and asked for it to be reverted. The three commits were undone with
`git revert` (history kept; nothing was ever in a release). The code is still reachable in git history at `883fbb2` if it
is ever wanted again. Unrelated fix kept: `p5b.ps1`'s About check no longer hard-codes version 0.1.0.

### Release 1.0.5 — software rendering and row layout (2026-11-11, Sonnet 5.5)

From the owner's performance audit (`TaskIsland_Performance_Audit.md`, measured on 1.0.3), re-measured here on 1.0.4.
The see-through (layered) island window keeps about 60 MB of D3D surfaces alive after each morph on the GPU path
(forced GC, 30 fps, BitmapCache, no shadow: none of them freed it). `RenderOptions.ProcessRenderMode = SoftwareOnly`,
set in `App.OnStartup` before any window exists, removes it. Same look (25 of 64,200 sampled pixels differ in a
screenshot of the open panel), transparency unchanged.

| 10 open/close cycles, 1080p @125 % | 1.0.4 (GPU) | 1.0.5 |
|---|---|---|
| Private memory start / after | 64 / 122 MB | 49 / 55 MB (flat from cycle 3) |
| CPU per open+close | 502 ms | 1,083 ms |
| Handles | 663 | 528 |

Switching only the island window (`HwndSource.CompositionTarget.RenderMode` in `OnSourceInitialized`) was tried and
rejected: 68 MB, because the first frame is already drawn on the GPU. Process-wide is better. Not measured yet: cold
start, 4K / second monitor, scroll CPU, 200-cycle soak. PLAN section 6.7's total-working-set target (70 MB) is mostly
shared framework pages; private bytes (50 MB) is the number that matters.

Also fixed: pointing at a task row showed the edit/delete buttons and squeezed the title into a thin column. The due
date moved under the title and the buttons use `Visibility.Hidden` (space reserved). `p5a.ps1` "pulse after the pill is
wide" fails the same way on 1.0.4 (not caused by this release).
