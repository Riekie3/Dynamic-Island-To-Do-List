# TaskIsland — Development Log

Live tracker (copy-ready prompts per phase): https://claude.ai/artifact/RAqH7ZmUthw76PcD6sfUK1 (source: docs/tracker.html)

Newest entry at the bottom. Each phase appends one entry using the template below.

## Phase status

| Phase | Status | Commit |
|---|---|---|
| 0 Planning | ✅ done 2026-11-01 | plan + checklist |
| 1 Skeleton & data layer | ✅ done 2026-11-01 | 8537510 |
| 2 Island UI | ⏳ | |
| 3 To-Do functionality | ⏳ | |
| 4 Windows integration | ⏳ | |
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
