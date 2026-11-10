# TaskIsland — Detailed Implementation Plan

> **Read this whole file before writing any code.** It is the single source of truth for
> building TaskIsland. Execute phases in order. Each phase lists its goal, exact steps, files,
> acceptance criteria, verification commands and the commit message to use.
> The original product brief is the user's spec. Where this plan is more specific, follow the plan.

- Repo: https://github.com/Riekie3/Dynamic-Island-To-Do-List (public, branch `main`)
- Local folder: `D:\Rie\Claude\TaskIsland`
- Design reference: [design-reference.webp](design-reference.webp) (open and study it before Phase 2)
- **Updated look (2026-11-03):** [design-reference-1.webp](design-reference-1.webp) shows the current target (modern panel,
  list chips, mascot states). Source: `design/preview-1.html`. Where the two images differ, follow design-reference-1.
- Test checklist: [TESTING.md](TESTING.md) · Dev log: [DEVLOG.md](DEVLOG.md)
- Mac version (planned, not started): [PLAN-MAC.md](PLAN-MAC.md)

---

## 0. How to execute this plan (rules for the AI agent)

1. **One phase per session is fine.** At the start, read `docs/DEVLOG.md` to see what is done.
2. Before coding a phase, run `git status` and `git pull`. Never force-push, never rewrite history.
3. After each phase: `dotnet build` (0 warnings treated as goal, 0 errors required) → `dotnet test`
   → manual run where stated → append a report to `docs/DEVLOG.md` → commit → `git push`.
4. Tick boxes in `docs/TESTING.md` **only** for things actually tested. Mark untested items
   honestly (`⏳ needs user`). Never claim sleep/hibernate/reboot work unless the user confirmed it.
5. Give the user a short status update at each phase boundary (they want progress updates and
   continuous pushes, not one big batch at the end).
6. Every visible button must work. No placeholder handlers, no fake persistence.
7. Keep dependencies to the list in §3. Ask the user before adding any other NuGet package.
8. **Compact after every phase.** When a phase is finished (build and tests green, DEVLOG and TESTING updated,
   committed, pushed), write a "Handoff" block at the end of that DEVLOG entry (what exists, what the next phase must
   know, open issues, exact next command). Then end your final report with this line:
   `Phase N complete. Run /compact now (or start a fresh session) before Phase N+1.`
   Do **not** begin the next phase in the same context. (Slash commands like /compact can only be run by the user;
   the agent cannot trigger it. The Handoff block means nothing is lost when context is compacted or cleared.)
9. Commit messages end with the `Co-Authored-By` attribution line your harness specifies.
10. **Every phase follows §7.11 (don't trigger security software) and §6.7 (resource budget).** If a step seems to need
    something §7.11 bans (a hook, a shell, a network call, admin rights), stop and ask the user instead.

**Suggested effort per phase:** Phase 1 Medium · 2 High · 3 Medium · 4 High · 5 Medium · 6 High · 6b Medium · 7 Medium.

**Suggested model per phase:** Opus for Phase 2 (window interop + animation) and Phase 4
(Windows integration). Sonnet is fine for Phases 1, 3, 5, 6 and 7.

### Paste-ready kickoff prompt

```
Project: TaskIsland in D:\Rie\Claude\TaskIsland (repo Riekie3/Dynamic-Island-To-Do-List).
Read docs/PLAN.md fully, then docs/DEVLOG.md to see progress. Execute the next
incomplete phase exactly as specified in PLAN.md, follow section 0 rules (including the
Handoff block and the "Run /compact now" line), then stop and report. Do not skip verification steps.
```

---

## 1. Environment (verified 2026-11-01)

| Item | Value |
|---|---|
| OS | Windows 11 Pro 26200, x64 |
| .NET SDK | **10.0.403** at `C:\Program Files\dotnet` (`dotnet` is on PATH) |
| Inno Setup | 6, per-user: `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe` (**not** on PATH; call by full path) |
| Git / GitHub | `gh` logged in as **Riekie3**, remote `origin` set, `main` tracks `origin/main` |
| IDE | none (no Visual Studio). Use the `dotnet` CLI only |
| Shells | PowerShell 5.1 and Git Bash both available |

---

## Credit: RieSources

All credit goes to **RieSources** (rie-sources.com), not the GitHub username. Use it in:
`Directory.Build.props` (Company, Authors, Copyright), exe file properties (CompanyName), About page
("Developed by RieSources"), installer (`AppPublisher=RieSources`, `AppPublisherURL=https://rie-sources.com`,
`AppCopyright`), README footer, LICENSE copyright line, release notes, and the installer's finish/welcome text.

## 2. Agreed product decisions (2026-11-01)

Core spec (user brief) **plus** these v1 extras/decisions:

| Topic | Decision |
|---|---|
| Due-date reminders | Tasks may have a due date with optional time. When due, show a Windows notification (tray balloon, which Windows 11 shows as a toast) and pulse the pill blue once. Overdue tasks show a red due label. Each reminder fires once. |
| Drag to reorder | In the **Active** view, drag tasks **within the same priority group** to reorder (stored in `SortOrder`). |
| Pill peek on hover | Hover the collapsed pill for 400 ms: pill widens and shows the top outstanding task's title (High first). Mouse leaves → reverts. Setting toggle. |
| Ticking a task | Tick + strikethrough shows for **2 s**, then the row slides out of the **Active** view. Unticking within 2 s cancels. Still listed in Completed/All. |
| Daily auto-backup | On (Settings toggle). Keep last 7. |
| Language | English only. |
| **Grouped lists** | Tasks belong to a **list** (e.g. *Office Work*, *Personal*, *Project Alpha*). Each list has a name and a colour. A switcher row in the panel shows `All · Office Work · Personal · … · +`. "All" shows every list's tasks with a coloured list dot per row. Full spec in §4.6 and §6.4. |
| Category (spec field) | Implemented **as Lists**. No separate free-text category field, so there aren't two overlapping grouping concepts. |
| First run | Two starter lists, **Personal** (default) and **Office Work**. Both can be renamed or deleted. No sample tasks. |
| Delete | Soft delete + **Undo** bar for 5 s, then permanent. |
| Data location | `%LocalAppData%\TaskIsland\` with `Data\`, `Backups\`, `Logs\`. Settings live in the DB `Settings` table (no separate Settings folder needed). |
| **Mascot face** (added 2026-11-02) | The notch has a face. Idle = a tiny notch with two dots. Events (and, now and then, at random) pop it open into a wider pill showing one of **12 expressions** that animate (blink, smile, sleep, react). Full spec §6.8, drawings `docs/design/`. |
| Network | None. The app makes zero network calls. Update info in About is static text (version + GitHub URL). |

---

## 3. Architecture

### 3.1 Solution layout (create exactly this)

```
TaskIsland/
├── TaskIsland.sln
├── Directory.Build.props            # shared: Version, Nullable, LangVersion, TreatWarningsAsErrors=false
├── global.json                      # pin SDK 10.0.403, rollForward latestFeature
├── src/
│   ├── TaskIsland.Core/             # net10.0 — NO WPF/WinForms references
│   │   ├── Models/TaskItem.cs
│   │   ├── Models/TodoList.cs             # a group/list: Id, Name, ColorHex, SortOrder
│   │   ├── Models/Priority.cs
│   │   ├── Models/TaskFilter.cs           # Active, Completed, All
│   │   ├── Models/AppSettings.cs          # strongly typed settings + defaults (see §5)
│   │   ├── Services/ITaskRepository.cs
│   │   ├── Services/ITodoListRepository.cs
│   │   ├── Services/ISettingsStore.cs
│   │   ├── Services/IClock.cs             # for testable time (SystemClock impl)
│   │   ├── Services/TaskSorter.cs         # ordering rules (§4.3)
│   │   ├── Services/TaskStats.cs          # total/completed/outstanding/percent
│   │   ├── Services/ReminderScheduler.cs  # computes next due reminder (pure logic)
│   │   └── Services/JsonTransfer.cs       # export/import + validation (§4.4)
│   ├── TaskIsland.Data/             # net10.0 — Microsoft.Data.Sqlite
│   │   ├── AppPaths.cs                    # %LocalAppData%\TaskIsland\... (overridable for tests)
│   │   ├── Database.cs                    # connection factory, WAL, migrations via PRAGMA user_version
│   │   ├── Migrations.cs                  # ordered list of SQL scripts
│   │   ├── SqliteTaskRepository.cs
│   │   ├── SqliteTodoListRepository.cs
│   │   ├── SqliteSettingsStore.cs
│   │   └── BackupService.cs               # backup/restore/daily auto-backup/prune
│   ├── TaskIsland.Windows/          # net10.0-windows — P/Invoke + OS services, no UI
│   │   ├── Native/User32.cs, Shell32.cs, Shcore.cs, Kernel32.cs   # DllImport declarations
│   │   ├── WindowStyler.cs                # toolwindow / noactivate / topmost helpers
│   │   ├── MonitorService.cs              # enumerate monitors, DPI, pick target, events
│   │   ├── PowerEventsService.cs          # sleep/resume/lock/unlock/display on-off
│   │   ├── FullScreenDetector.cs          # WinEvent hook + SHQueryUserNotificationState
│   │   ├── HotkeyService.cs               # RegisterHotKey via HwndSource
│   │   ├── StartupRegistration.cs         # HKCU Run key
│   │   └── SingleInstance.cs              # Mutex + named pipe
│   └── TaskIsland.App/              # net10.0-windows, UseWPF + UseWindowsForms (NotifyIcon only)
│       ├── App.xaml / App.xaml.cs         # DI composition root, startup flow, global exception handlers
│       ├── app.manifest                   # PerMonitorV2 DPI, Windows 10/11 supportedOS
│       ├── Logging/FileLogger.cs
│       ├── Themes/Dark.xaml, Light.xaml, Controls.xaml (styles/templates), Animations.xaml
│       ├── Controls/IslandHost.cs         # morph control (§6.2), clip-geometry based
│       ├── Controls/FluentEase.cs         # cubic-bezier(0.2,0,0,1) easing (§6.0)
│       ├── Controls/FaceRig.xaml(.cs)     # mascot face: independent eyes/mouth/accessory parts (§6.8)
│       ├── Mascot/Expression.cs, Expressions.cs  # the 12 expressions as data + frozen geometries
│       ├── Services/ReactionDirector.cs   # which face shows when; random pops; priorities (§6.8)
│       ├── Services/MotionPolicy.cs       # reduced-motion decision + duration tokens (§6.7)
│       ├── Services/EfficiencyMode.cs     # EcoQoS on/off while collapsed (§6.7), P/Invoke in Windows project
│       ├── Controls/PriorityChip.xaml(.cs)
│       ├── Views/IslandWindow.xaml(.cs)   # the notch + panel
│       ├── Views/TaskRow.xaml(.cs)        # one row: display + inline edit
│       ├── Views/ListSwitcher.xaml(.cs)   # horizontal list chips: All · lists · +
│       ├── Views/ListEditPopup.xaml(.cs)  # create/rename list + colour picker
│       ├── Views/SettingsWindow.xaml(.cs)
│       ├── ViewModels/IslandViewModel.cs
│       ├── ViewModels/TaskItemViewModel.cs
│       ├── ViewModels/SettingsViewModel.cs
│       ├── Services/TrayIconService.cs
│       ├── Services/IslandPositioner.cs   # glue: MonitorService → window placement
│       ├── Services/RecoveryCoordinator.cs# glue: power/display events → recover
│       ├── Services/ReminderService.cs    # DispatcherTimer to next due time
│       ├── Services/DialogService.cs      # confirm dialogs, open/save file dialogs
│       └── Assets/TaskIsland.ico, tray-light.ico, tray-dark.ico
├── tests/
│   ├── TaskIsland.Core.Tests/       # xUnit, net10.0
│   └── TaskIsland.Data.Tests/       # xUnit, net10.0, temp-file SQLite DBs
├── tools/IconGen/                   # small console app that renders the .ico files (not shipped)
├── installer/TaskIsland.iss
└── docs/ PLAN.md, TESTING.md, DEVLOG.md, RELEASE_NOTES.md, design-reference.webp
```

Project references: App → Core, Data, Windows. Data → Core. Windows → (none). Tests → their target project.

### 3.2 Allowed NuGet packages

| Package | Used in |
|---|---|
| `Microsoft.Data.Sqlite` (latest stable) | Data, Data.Tests |
| `CommunityToolkit.Mvvm` (latest stable) | App |
| `Microsoft.Extensions.DependencyInjection` (latest stable) | App |
| `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` | tests |

Anything else needs user approval. **Do not** use Entity Framework, Hosting, Serilog, H.NotifyIcon,
or toast libraries. `System.Text.Json` is in the BCL. Use it for JSON.

### 3.3 Conventions

- C# latest, `Nullable` enabled, file-scoped namespaces, `ImplicitUsings` enabled.
- In `TaskIsland.App`, WinForms + WPF implicit usings collide (`Application`, `MessageBox`, `Brush`…).
  Add `<Using Remove="System.Windows.Forms" />` and `<Using Remove="System.Drawing" />` in the csproj
  and fully-qualify `System.Windows.Forms.NotifyIcon` in `TrayIconService` only.
- MVVM with CommunityToolkit `[ObservableProperty]` / `[RelayCommand]`. Code-behind only for
  pure view concerns (animation triggers, drag visuals, window interop).
- All DB access is async (`await using` connections, one connection per operation; WAL handles concurrency).
- All times stored as ISO-8601 strings. Audit timestamps (`CreatedAt`, `UpdatedAt`, `CompletedAt`,
  `DeletedAt`, `ReminderShownAt`) in **UTC** (`"o"` format). `DueDate` is a **local wall-clock**
  value (`yyyy-MM-ddTHH:mm:ss`, no offset) with `DueHasTime` flag. A date-only due counts as due at 09:00 local.
- No `async void` except event handlers. Every handler wraps with try/catch → log.

---

## 4. Data layer specification

### 4.1 SQLite schema (migration 1, `PRAGMA user_version = 1`)

```sql
PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;

CREATE TABLE Lists (
  Id         INTEGER PRIMARY KEY AUTOINCREMENT,
  Name       TEXT    NOT NULL CHECK (length(Name) BETWEEN 1 AND 40),
  ColorHex   TEXT    NOT NULL DEFAULT '#1E90FF',
  SortOrder  INTEGER NOT NULL DEFAULT 0,
  CreatedAt  TEXT    NOT NULL,
  UpdatedAt  TEXT    NOT NULL
);
CREATE UNIQUE INDEX UX_Lists_Name ON Lists (Name COLLATE NOCASE);

CREATE TABLE Tasks (
  Id              INTEGER PRIMARY KEY AUTOINCREMENT,
  ListId          INTEGER NOT NULL REFERENCES Lists(Id) ON DELETE CASCADE,
  Title           TEXT    NOT NULL CHECK (length(Title) BETWEEN 1 AND 500),
  Description     TEXT,
  Priority        INTEGER NOT NULL DEFAULT 1 CHECK (Priority IN (0,1,2)),  -- 0 High, 1 Medium, 2 Low
  IsCompleted     INTEGER NOT NULL DEFAULT 0 CHECK (IsCompleted IN (0,1)),
  CreatedAt       TEXT    NOT NULL,
  UpdatedAt       TEXT    NOT NULL,
  CompletedAt     TEXT,
  DueDate         TEXT,
  DueHasTime      INTEGER NOT NULL DEFAULT 0,
  ReminderShownAt TEXT,
  SortOrder       INTEGER NOT NULL DEFAULT 0,   -- order within (ListId, Priority)
  DeletedAt       TEXT
);
CREATE INDEX IX_Tasks_Active ON Tasks (ListId, DeletedAt, IsCompleted, Priority, SortOrder);

CREATE TABLE Settings (
  SettingKey   TEXT PRIMARY KEY,
  SettingValue TEXT NOT NULL
);
```

Migration runner: read `PRAGMA user_version`. Apply each script with a higher number inside a
transaction, then set `user_version`. Future schema changes = new script, never edit old ones.
This keeps user data on upgrades. Migration 1 also inserts the two starter lists
(*Personal* `#1E90FF` SortOrder 0, *Office Work* `#A970FF` SortOrder 1) and sets setting `DefaultListId` to Personal.

### 4.2 Repository API (`ITaskRepository`)

```csharp
Task<IReadOnlyList<TaskItem>> GetAllAsync(bool includeDeleted = false);   // all lists; filtering is in TaskSorter
Task<TaskItem> AddAsync(long listId, string title, Priority priority, DateTime? due = null,
                        bool dueHasTime = false, string? description = null);
Task MoveToListAsync(long id, long targetListId);      // goes to the end of its priority group in the target list
Task UpdateAsync(TaskItem item);                       // sets UpdatedAt
Task SetCompletedAsync(long id, bool completed);       // sets/clears CompletedAt, UpdatedAt
Task SoftDeleteAsync(long id);  Task RestoreAsync(long id);  Task PurgeDeletedAsync();
Task ReorderAsync(long listId, Priority group, IReadOnlyList<long> orderedIds);   // one transaction
Task<int> ClearCompletedAsync(long? listId = null);    // soft-delete completed tasks (all lists or one), undo-able
Task MarkReminderShownAsync(long id);
Task<TaskItem> InsertAsync(TaskItem item);             // keeps timestamps/completion, assigns Id + SortOrder (used by import)
Task ReplaceAllAsync(TransferData data);               // JSON import "Replace": lists + tasks, one transaction
```

New task `SortOrder` = `MAX(SortOrder)+1` within its (list, priority), so it appears at the bottom of its group.
Changing a task's priority moves it to the bottom of the new group.
`PurgeDeletedAsync` runs at startup and when an Undo bar expires.

### 4.3 Sorting & filtering rules (`TaskSorter`, unit-tested)

Every view is first scoped by the **selected list** (one list, or *All lists*). Deleted rows are never shown.

- **Active** view: `!IsCompleted && DeletedAt == null`, ordered by `Priority` asc (High first),
  then `SortOrder` asc, then `CreatedAt` asc, then `Id` asc. This gives a fully predictable order.
  In *All lists* the order is `Priority` → list `SortOrder` → task `SortOrder` → `CreatedAt` → `Id`,
  so all High tasks come first, still grouped by list.
- **Completed** view: `IsCompleted`, ordered by `CompletedAt` desc.
- **All** view: active block (as above) followed by completed block (as above).
- Stats: `Total` = non-deleted count; `Completed`; `Outstanding = Total - Completed`;
  `Percent = Total == 0 ? 0 : round(Completed*100/Total)`. Footer stats use the selected list scope.
  Per-list outstanding counts feed the switcher chips. The pill count uses `PillCountScope` (§5).

### 4.4 JSON export/import (`JsonTransfer`)

Export file (UTF-8, indented):

```json
{
  "app": "TaskIsland",
  "formatVersion": 1,
  "exportedAt": "2026-11-01T08:00:00Z",
  "lists": [ { "name": "Office Work", "color": "#A970FF", "sortOrder": 1 } ],
  "tasks": [ { "list": "Office Work", "title": "...", "description": null, "priority": "High",
               "isCompleted": false, "createdAt": "...", "updatedAt": "...", "completedAt": null,
               "dueDate": null, "dueHasTime": false, "sortOrder": 0 } ]
}
```

Tasks reference their list **by name** (ids differ between PCs). Export can be *all lists* or *current list only*.

Import validation (reject the whole file with a clear message if any rule fails; never partially import):
`app == "TaskIsland"`, `formatVersion == 1`, `tasks` is an array of ≤ 10 000 items, every title is 1–500 chars after trim,
priority ∈ High/Medium/Low (case-insensitive), dates parse, file ≤ 10 MB, list names 1–40 chars, every task's
`list` exists in `lists` (a missing `list` → goes to the default list), colours are `#RRGGBB`.
Import modes (user picks in a dialog): **Merge** (lists matched by name, case-insensitive, and created if missing.
Append tasks. Skip items whose list + title + createdAt already exist)
or **Replace** (automatic backup first, then confirm, then `ReplaceAllAsync`). Show a result summary
("Imported 12, skipped 3 duplicates").

### 4.5 Backup / restore (`BackupService`)

- Backup = `SqliteConnection.BackupDatabase()` (online, safe while running) to
  `Backups\taskisland-yyyyMMdd-HHmmss.db`. Manual backup lets the user pick a path via Save dialog.
- Daily auto-backup: at startup and on resume, if setting on and `LastBackupAt` > 24 h ago → backup
  then prune auto-backups to newest 7 (manual backups saved elsewhere are never pruned).
- Restore: user picks a `.db` → open it read-only and run `PRAGMA integrity_check` and check
  `user_version` ≤ current and `Tasks` table exists → confirm dialog → create a `pre-restore-*.db`
  backup of the current DB → `SqliteConnection.ClearAllPools()` → copy over → run migrations → reload UI.

### 4.6 Lists (groups) (`ITodoListRepository`)

```csharp
Task<IReadOnlyList<TodoList>> GetAllAsync();                 // ordered by SortOrder
Task<TodoList> CreateAsync(string name, string colorHex);    // appended at the end
Task RenameAsync(long id, string name);  Task SetColorAsync(long id, string colorHex);
Task ReorderAsync(IReadOnlyList<long> orderedIds);
Task DeleteAsync(long id, long? moveTasksToListId);         // null = delete its tasks too (one transaction)
```

Rules (unit-tested):
- Name trimmed, 1–40 chars, unique case-insensitive ("Work" vs "work" → rejected with message).
- Max 20 lists (keeps the switcher usable).
- The **last remaining list cannot be deleted**. Deleting the default list makes the first remaining list the default.
- Deleting a list asks: *"Move its N tasks to [list ▾]"* (default) or *"Delete the list and its N tasks"*.
  Both are wrapped in an automatic backup first, because list deletion is not covered by the 5-second Undo bar.
- Colour palette (8 fixed swatches, readable on dark and light): Blue `#1E90FF`, Purple `#A970FF`, Teal `#22C1C3`,
  Green `#3DDC84`, Amber `#FFB020`, Orange `#FF7A45`, Pink `#FF5CA8`, Slate `#8C97A8`. List colour is only a
  dot/underline accent. Priority colours keep their meaning.

---

## 5. Settings specification (`AppSettings`, stored as key/value strings)

| Key | Default | UI section | Notes |
|---|---|---|---|
| `LaunchAtStartup` | `true` | General | writes/removes HKCU Run value |
| `StartCollapsed` | `true` | General | if false, opens expanded on manual launch (never on `--startup`) |
| `CollapseOnClickOutside` | `true` | General | collapse on window `Deactivated` |
| `CollapseOnEscape` | `true` | General | |
| `AlwaysShowNotch` | `true` | General | if false, pill slides up to a 6 px sliver and reveals when the mouse touches it |
| `Theme` | `Dark` | Appearance | Dark / Light / System (follow Windows app theme via registry `AppsUseLightTheme` + `UserPreferenceChanged`) |
| `NotchWidth` | `200` | Appearance | slider 160–280 |
| `HorizontalOffset` | `0` | Appearance | slider −400…+400 px from centre ("Notch position") |
| `TopMargin` | `0` | Appearance | 0–40 px; 0 = flush notch (flat top, rounded bottom corners), more = floating pill |
| `PanelOpacity` | `0.94` | Appearance | 0.70–1.00 |
| `AnimationsEnabled` | `true` | Appearance | false → durations become 0 (reduced motion also turns on automatically, §6.7) |
| `FollowWindowsAnimations` | `false` | Appearance | true → Windows "Animation effects" off also means reduced motion here. Default off (user's choice, 2026-11-04) |
| `MascotEnabled` | `true` | Appearance | Face notch (§6.8). Off → the classic count pill (§6.3) is the collapsed state. New key, add in Phase 2 |
| `IdleReactions` | `Normal` | Appearance | Random face pop-ups: `Off` / `Rare` (every 10–20 min) / `Normal` (4–8 min) / `Lively` (1–3 min). New key, Phase 2 |
| `Companion` | `Calm` | Appearance | Idle life of the face (§6.8 Companion): `Off` / `Calm` / `Lively`. New key, 2026-11-03 |
| `AccentSource` | `Default` | Appearance | `Default` (TaskIsland blue) / `Windows` (Windows accent colour). New key, add to `AppSettings` + tests in Phase 5 |
| `Density` | `Comfortable` | Appearance | Comfortable (row 52 px) / Compact (row 38 px) |
| `HideInFullScreen` | `true` | Behaviour | |
| `MonitorDeviceName` | `""` | Behaviour | empty = primary. Missing monitor → primary |
| `AutoCollapseSeconds` | `0` | Behaviour | 0 = off. Else collapse after N s with mouse outside and no focus in the text box |
| `RestoreAfterWake` | `true` | Behaviour | |
| `PeekOnHover` | `true` | Behaviour | |
| `RemindersEnabled` | `true` | Behaviour | |
| `ReminderLeadMinutes` | `0` | Behaviour | Early reminder before the due time: `0` = off, else 5 / 15 / 30 / **60** / 120 / 1440 minutes. Only for tasks with a due time (date-only tasks have none). New key, Phase 5 |
| `ReminderPillAlert` | `true` | Behaviour | The island widens and shows the task title when a reminder fires (§7.9). Off = pulse, face and notification only. New key, Phase 5 |
| `ReminderAlertWidthPercent` | `50` | Behaviour | "Reminder width": how wide the reminder pill grows, in % of the screen width: `0` = Compact (the old `min(NotchWidth + 200, 420)`), 30 / 40 / **50** / 60 / 75 / 100. Anything else falls back to 50. Added after rc.1 at the user's request |
| `ReminderBlink` | `true` | Behaviour | "Blink a red border": a red border blinks around the reminder pill (about once a second) instead of the accent pulse; steady in reduced motion. Added after rc.1 |
| `ToggleHotkey` | `Ctrl+Alt+T` | Behaviour | capture box. Shows "in use by another app" if RegisterHotKey fails |
| `DailyBackupEnabled` | `true` | Data | |
| `CompletedFadeOut` | `true` | Behaviour | the 2-second slide-out |
| `DefaultListId` | Personal | Lists | where quick-add goes when *All lists* is selected |
| `PillCountScope` | `AllLists` | Lists | `AllLists` or `SelectedList`: what the pill's "3 tasks" counts |
| `FirstRunCompleted`, `LastBackupAt`, `CurrentFilter`, `SelectedListId` (0 = All) | internal | — | |

Settings apply **live** (no restart) and save immediately.

---

## 6. UI / visual specification

### 6.0 Design direction (updated 2026-11-01: modern and light on resources)

Two goals, equal weight:
1. **Modern look.** A clean, calm Windows 11 style (Fluent-aligned spacing, type and icons) with the Dynamic Island
   personality: a pure black pill that grows into a dark graphite card. Few colours, generous spacing, soft depth,
   motion that is quick and purposeful.
2. **Light on power.** TaskIsland sits on screen all day, so it must cost close to nothing while you are not using it:
   **0 % CPU and 0 % GPU when idle**, small memory, and no work at all in the background except one-shot timers.
   The budget and rules are in §6.7. When a visual idea conflicts with the budget, the budget wins.

Design principles:
- **8 px grid** for spacing (4 px for tight gaps). Radii: pill = height/2, panel 24, inputs and rows 12, chips fully round.
- **Colour is information, not decoration.** Neutral surfaces; one accent colour (tick, add button, focus, links);
  priority colours appear only in the small priority chip and the overdue label.
- **Clean rows.** No separator lines. Row actions (✎ 🗑) appear on hover **and on keyboard focus** (so they stay accessible).
- **Depth from a hairline, not heavy effects.** 1 px border + a faint top highlight; one soft shadow, pre-rendered (§6.2).
- **Motion tokens**, used everywhere (no ad-hoc durations): `fast` 120 ms (hover, press), `normal` 200 ms (rows, chips),
  `morph` 260 ms expand / 180 ms collapse. One shared easing: `FluentEase` (a small `IEasingFunction` implementing
  cubic-bezier(0.2, 0, 0, 1), "emphasised decelerate"); collapse uses its mirror (accelerate). No bouncy overshoot.
- **Respect the user's system:** follow Battery saver and Remote Desktop by switching to **reduced motion** automatically,
  and Windows "Animation effects" off only when `FollowWindowsAnimations` is on (§6.7; default off since 2026-11-04). Visible 2 px accent focus ring for keyboard users. Text contrast ≥ 4.5:1.

### 6.1 Visual tokens

Neutral surface scale (Dark / Light). Pill stays black in both themes, like a real island.

| Token | Dark | Light |
|---|---|---|
| `Pill` | `#000000` (opaque) | `#000000` |
| `PillHover` | `#121418` | `#121418` |
| `Surface` (panel) | `#15171C` at `PanelOpacity` | `#FAFBFC` at `PanelOpacity` |
| `SurfaceRaised` (input, hovered row, popups) | `#FFFFFF` @ 6% | `#000000` @ 4% |
| `SurfacePressed` | `#FFFFFF` @ 10% | `#000000` @ 8% |
| `Stroke` (panel border) | 1 px `#FFFFFF` @ 9% | 1 px `#000000` @ 8% |
| `TopHighlight` (inner 1 px line on the panel's top edge) | `#FFFFFF` @ 6% | none |
| `TextPrimary` | `#F3F4F6` | `#111318` |
| `TextSecondary` | `#9AA1AD` | `#5A606B` |
| `TextTertiary` (hints, placeholders) | `#6B7280` | `#8A9099` |
| `Accent` / `AccentHover` / `AccentPressed` | `#2F8CFF` / `#4C9DFF` / `#1F75E0` | `#0A6CF0` / `#2A80F5` / `#085BCB` |
| `OnAccent` (tick, arrow on accent) | `#FFFFFF` | `#FFFFFF` |
| `Overdue` | `#FF6B6B` | `#D22B2B` |
| High chip | dot `#FF6B6B`, bg `#FF6B6B` @ 14%, label "High" | dot `#D22B2B`, bg `#D22B2B` @ 10% |
| Medium chip | dot `#F5B83D`, bg `#F5B83D` @ 14%, label "Medium" | dot `#A86A00`, bg `#A86A00` @ 10% |
| Low chip | dot `#3DD68C`, bg `#3DD68C` @ 14%, label "Low" | dot `#11803F`, bg `#11803F` @ 10% |
| Shadow | pre-rendered, black @ 35%, blur 28, offset y 8 (§6.2) | black @ 14% |
| Font | `Segoe UI Variable` (fallback `Segoe UI`): Display 20 semibold (title) · Text 14 (rows) · Small 12 (meta, footer). Counters use `Typography.NumeralAlignment="Tabular"` so numbers don't jiggle. | |
| Icons | `Segoe Fluent Icons` (fallback `Segoe MDL2 Assets`), 16 px, monochrome: Accept `E73E`, ChevronDown `E70D`, Edit `E70F`, Delete `E74D`, Settings `E713`, Add `E710`, Forward `E72A`, More `E712`, Cancel `E711`, Clock `E823`, Calendar `E787` | |

Accent: `Accent` above by default. Setting `AccentSource = Windows` uses the Windows accent colour instead (registry
`HKCU\Software\Microsoft\Windows\DWM\AccentColor`, re-read on `UserPreferenceChanged`, never polled).
Priority always shows **text label + colour dot** (never colour alone).

### 6.2 The island morph (core technique, built for low cost)

**Window.** `IslandWindow`: `WindowStyle=None`, `AllowsTransparency=True`, `Background={x:Null}`,
`ShowInTaskbar=False`, `Topmost=True`, `ResizeMode=NoResize`, `ShowActivated=False`.
A layered window is needed for the anti-aliased rounded morph (DWM corners are fixed at 8 px and a window region has
jagged edges). A layered window re-uploads its pixels every frame it animates, so **keep the window small when idle**:
- **Collapsed size: 452 × 64 DIP** (Phase 2 change from 376 × 64: the same width as expanded keeps the window's left edge fixed, so a
  resize can never show the pill shifted for a frame. Room for the widest peek, 360, and for the mascot's Mini 120 × 32 and Reaction
  232 × 56 states with their top-right accessory, §6.8; all collapsed states share this window, no resize between them). **Expanded size: 452 × 600 DIP** (420 panel +
  16 px each side for the shadow, 550 max panel height + shadow below).
- The island is always drawn **top-centred** in the window with **0 top inset** in both sizes, so the pill sits on the
  exact same screen pixels in both. Resize only at two moments, with one `SetWindowPos` while nothing is moving:
  just **before** the expand animation starts (grow to expanded size), and just **after** the collapse animation
  ends (shrink to collapsed size). Never resize during an animation.
- Fallback: if Phase 2 sees a one-frame flicker on the resize that can't be fixed, keep the fixed 452 × 600 window
  and log the decision in DEVLOG (idle cost is still zero; only animation frames get more expensive).
- Fully transparent pixels in a layered window are click-through, so the space around the pill doesn't block the
  desktop. Keep the root `Grid` `Background="{x:Null}"`. Verify by clicking something directly below/next to the pill.

**Morph without re-layout.** Animating `Width`/`Height` would re-measure the whole panel on every frame. Instead:
- `IslandHost` (custom `FrameworkElement`/`ContentControl`) has a `double Morph` DependencyProperty (0 = pill, 1 = panel).
- It owns **one** `RectangleGeometry`. The background `Path` uses it as `Data` (fill `Pill` → `Surface`) and the content
  layer uses the same geometry as its `Clip`. On Morph change only the geometry's `Rect` and `RadiusX/RadiusY` are set:
  width `NotchWidth → 420`, height `36 → panelHeight`, radius `18 → 24`, centred, top edge fixed. That is a
  render-only change: **no measure/arrange per frame**.
- Panel content is laid out **once** at its final size (420 × measured height, max 550) before the expand starts and is
  revealed by the growing clip. Height changes while expanded (rows added/removed, list switch) animate the clip
  (`normal`, 200 ms), not the layout of the rows.
- Background colour lerps `Pill` → `Surface` over Morph 0 → 0.5 (frozen brushes swapped at the ends, a single
  animated `SolidColorBrush` during the transition).
- Content crossfade: pill content opacity = `1 − clamp(Morph / 0.3)`; panel content opacity = `clamp((Morph − 0.35) / 0.65)`
  with a 6 px upward slide (`TranslateTransform`, render-only). Both layouts are never visible at once.
- Expand: Morph 0 → 1, `morph` 260 ms, `FluentEase`. Collapse: 1 → 0, 180 ms, mirrored ease.
- When collapsed, the panel subtree is `Visibility=Collapsed` (not just transparent), so layout, rendering and hit
  testing skip it. The panel visual tree is **built once while the island rests after start-up** (Phase 2 change: building it on
  the first click froze that expand for ~0.2 s) and then kept.

**Shadow (cheap).** No `DropShadowEffect` on animating elements. One shadow `Border` behind the panel has the
`DropShadowEffect` **plus `CacheMode=BitmapCache`**, so it is rasterised once and only composited afterwards.
It is hidden (opacity 0, `Visibility=Collapsed`) during the morph, fades in (`fast`) after the expand completes, and is
hidden instantly before collapsing. The collapsed pill has no shadow (a real island doesn't).

**Pill details.**
- Hover: background `Pill → PillHover`, chevron nudges 2 px down (`fast`).
- **Progress ring:** the tick icon sits in a thin 2 px ring showing the % of tasks done for the pill's count scope. It is a
  static arc redrawn only when the numbers change (no animation loop).
- Peek on hover (§2): after 400 ms hover, the clip width grows to `min(NotchWidth + 140, 360)` (`normal`) and the text
  crossfades to the top task's title (ellipsis) with its list-colour dot. Revert on leave. The 400 ms delay is a
  one-shot `DispatcherTimer` started on `MouseEnter`, stopped on `MouseLeave`.
- Count change: the number slides 6 px up while crossfading (`fast`), once per change.
- Reminder pulse: an accent ring scales 1 → 1.12 and fades out, **2 cycles of 600 ms, then the animation is removed**.
  In reduced motion: no ring, the overdue/accent dot appears instead.
- Reminder pill alert (Phase 5, §7.9): clip width tween to the `ReminderAlertWidthPercent` width in `normal`, text crossfade,
  hold 5 s, tween back. One-shot timer; the only repeating animation is the red border blink, which exists only while the
  alert shows.

**Rules for every animation** (enforced in code review, §6.7): finite duration, `FillBehavior=Stop` with the final value
set as the local value (so no animation clock stays alive), only `Opacity`, `RenderTransform`, `Clip` geometry or a brush
colour are animated (never `Width`, `Height`, `Margin` or layout properties of anything with children), and
`AnimationsEnabled=false` or reduced motion → durations 0 (or `fast` for the morph, see §6.7).

### 6.3 Collapsed pill content

With `MascotEnabled` (default) the collapsed island is the **mini notch** (§6.8). Hovering it for 400 ms grows it into
the **info pill** below (this replaces the old peek); with `MascotEnabled=false` the info pill is the permanent collapsed state.

Info pill: `[✓ in progress ring]  3 tasks  [⌄]` (singular "1 task"), 36 px tall, text `Small`/`Text` semibold white.
When outstanding = 0: `✓ All done` (ring full, accent). Count is the outstanding count. If any task is overdue,
show a 6 px `Overdue` dot on the ring.

### 6.4 Expanded panel layout (top → bottom, 420 wide, 16 px padding)

1. **Header**: app tick icon · "My To-Do List" · outstanding count badge · ⚙ (opens Settings) · ✕ (collapse).
   The `…` button in the reference opens a small menu: Clear completed…, Export…, Import…, Settings.
2. **List switcher** (`ListSwitcher`, one row, 32 px): chips `All · Personal 3 · Office Work 5 · Project Alpha 1 · ＋`.
   Each chip = colour dot + name + outstanding count. Selected chip is filled with the list colour @ 20% and
   underlined in the list colour. Horizontal scroll with mouse wheel when it overflows, with fade edges.
   `＋` opens `ListEditPopup` (name + 8 colour swatches + Create). Right-click a chip: Rename…, Colour ▸,
   Move left, Move right, Delete list… (§4.6). Switching lists crossfades the task list (120 ms) and the
   panel height re-animates. Selection is remembered (`SelectedListId`).
   Keyboard while expanded: `Ctrl+1…9` = jump to list N (`Ctrl+0` = All), `Ctrl+Tab` / `Ctrl+Shift+Tab` = next/previous.
3. **Quick add**: rounded input "Add a new task…" with `+` glyph, priority selector chip (cycles
   High/Medium/Low on click, default **Medium**, also a dropdown on right-click/arrow), blue round → button.
   Enter submits, Esc clears text (if empty → collapse). Empty/whitespace title = no-op with a subtle shake.
   After add: input stays focused for rapid entry. New tasks go to the selected list. When *All lists* is selected,
   a small list chip appears in the input (default `DefaultListId`, click to choose another list).
4. **Task list** (`ItemsControl` with `VirtualizingStackPanel` (`VirtualizationMode=Recycling`) in a `ScrollViewer`,
   so only visible rows have visuals, whatever the task count):
   row (12 radius, 4 px gap, no separators) = 20 px round checkbox (accent fill + white tick when done) · title
   (strikethrough + `TextSecondary` when done) · due label with Clock glyph (`Overdue` colour if overdue) · priority chip
   (dot + label) · ✎ · 🗑 (these two are shown on row hover or keyboard focus only). In *All lists* each row also shows
   a 6 px list-colour dot before the title and the list name in `Small` `TextSecondary` under it. Priority group
   separators are not shown, order alone conveys it. Row hover background `SurfaceRaised`. Drag handle = the whole
   row (start drag after 6 px move), **only in the Active filter of a single list** (not in All lists, where the order is computed).
   Scrollbar: thin overlay style (4 px, widens to 8 px on hover), no track.
5. **Undo bar** (slides up above the footer for 5 s after delete/clear completed, `SurfaceRaised` pill): "Task deleted · Undo".
6. **Footer**: a 2 px progress bar across the full width (accent, % done in scope; changes with a `normal` width tween
   via `ScaleTransform`, not `Width`) · "3 total · 1 done · 33%" in `Small` `TextSecondary` · segmented filter
   **Active | Completed | All** whose selected-pill indicator slides between segments (`normal`, `TranslateTransform`).
   Last filter remembered.
7. **Empty states**: 32 px Fluent glyph (Accept in a soft accent circle) + "All done in <list>" + `TextSecondary`
   "Add something new above." Completed empty → "Nothing completed yet." No images, no emoji.

Pill: count follows `PillCountScope`. Peek-on-hover shows the top task's list-colour dot before its title.

### 6.5 Inline edit (row expands in place, 180 ms)

Fields: Title (TextBox), Priority (3 chips), Notes (multi-line, max 2000), Due (DatePicker + optional
time TextBox `HH:mm`, a "×" to clear), **List** (ComboBox of lists with colour dots. Changing it moves the task
via `MoveToListAsync`. If the current view is a different single list, the row slides out after Save).
Buttons **Cancel** (grey) / **Save** (blue). Enter in title = Save, Esc = Cancel. Only one row edits at a time.
Validation: title 1–500 chars, time must parse.

### 6.6 Settings window

Normal (activatable, has taskbar entry while open) 560 × 520 window, themed, left nav:
General · Appearance · Behaviour · **Lists** · Data · About. Only one instance (re-activate if open).
Lists page: reorderable list of lists (↑/↓ buttons) with name, colour swatch, task counts, Rename / Delete buttons,
"New list", Default list dropdown, Pill counts radio (All lists / Selected list).
Data page buttons: Export JSON… · Import JSON… · Back up now… · Restore from backup… · Open data folder ·
Clear completed tasks… (confirm). About: name, version (from assembly), build date, .NET runtime,
"Developed by RieSources" (rie-sources.com, shown as text, link opens only on click), GitHub link (opens browser only when clicked), data folder path, "No data leaves this PC".
Modern look: same tokens as the island, nav items as rounded pills with an accent indicator bar, cards (`SurfaceRaised`,
radius 12) grouping related settings, toggle switches instead of checkboxes. On Windows 11 the Settings window (a normal,
non-layered window) may use Mica via `DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE, DWMSBT_MAINWINDOW)` with a
transparent client background; on Windows 10 or if the call fails it falls back to the solid `Surface` colour.
The window is created when opened and **released on close** (no cached instance), so it costs nothing while closed.

### 6.7 Resource budget and efficiency rules (must-follow)

Targets (measured in Phase 6 on this laptop, Release build, collapsed, after 60 s idle):

| Metric | Target |
|---|---|
| CPU idle (collapsed, mouse elsewhere) | **0.0 %** averaged over 60 s between mascot pops (no wake-ups except the scheduled one-shot timers); **< 0.05 %** averaged over 30 min with `IdleReactions=Normal`. These hold with `Companion=Off`. With the companion on (2026-11-03, user's request) the measured cost is the budget: Calm ≈ 2 % of one core (≈ 0.13 % of the 16-thread CPU in Task Manager), Lively ≈ 3 %, see DEVLOG |
| GPU idle | **0 %** in Task Manager's GPU column (no frames rendered while nothing changes) |
| Private memory idle | ≤ **50 MB** (record working set too; WPF's own floor is roughly 40–60 MB working set) |
| Working set idle | ≤ **70 MB** |
| CPU while morphing / hovering | short spikes only, back to 0 within 1 s after the animation ends |
| Threads | no worker threads of our own besides the pipe server; no busy loops |
| Cold start to visible pill | < 1.2 s (ReadyToRun build) |
| Disk | no writes while idle (logs only on events; no periodic flush) |

If a target cannot be reached because of WPF's baseline, **report the measured numbers honestly in DEVLOG** and what
was tried. Do not use tricks that only hide memory (no `EmptyWorkingSet`/`SetProcessWorkingSetSize` calls).

Rules:
1. **No polling, no periodic timers.** Allowed timers are one-shot only: next random mascot pop (§6.8, re-armed after
   each pop), the 60-min Sleepy timer (reset on interaction), reaction hold (2.5/5 s), next reminder, peek delay (400 ms), tick
   slide-out (2 s), undo bar (5 s), recovery follow-ups (+2 s/+6 s), next midnight (daily backup check and "due today"
   labels; also re-run on resume and `TimeChanged`). The overdue label refresh is one timer set to the next due
   moment among visible rows, and only while expanded.
2. **Nothing renders while idle.** No infinite animations (`RepeatBehavior=Forever` is banned), no animated GIFs, no
   spinners. Animation clocks end (`FillBehavior=Stop` + local value set). Cap animation frame rate at 60 fps
   (`Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata(60))`) so
   120/144 Hz screens don't double the cost.
3. **Small surfaces.** Collapsed window 452 × 64 DIP (§6.2). Panel subtree `Collapsed` when not shown. Panel and Settings
   UI created lazily. Settings window released on close.
4. **Cheap effects only.** Only the one cached shadow uses an `Effect`. No `BlurEffect`, no `BitmapEffect`, no
   `OpacityMask` on large areas, no real-time acrylic. Freeze all brushes/geometries defined in resources
   (`PresentationOptions:Freeze="True"`).
5. **Layout stays cheap.** Animate render properties only (§6.2 rules). Virtualised task list. No bindings that
   update every second. Converters are static instances.
6. **Hide = stop.** When the island is hidden (full-screen app, paused, display off, locked) set `Visibility=Hidden`, so
   WPF renders nothing; stop the peek timer and any running animation.
7. **Efficiency mode while collapsed.** After 10 s collapsed and not hovered, call
   `SetProcessInformation(ProcessPowerThrottling, EXECUTION_SPEED on)` (Windows 11 shows the green leaf / "Efficiency
   mode" in Task Manager; on Windows 10 it is a silent hint). Turn it **off** immediately on `MouseEnter` of the pill,
   the hotkey, tray click or a pipe `EXPAND`, so expanding stays smooth. Wrap in try/catch; failure is logged once and ignored.
8. **Reduced motion** (automatic): if `AnimationsEnabled=false`, Windows "Animation effects" is off and
   `FollowWindowsAnimations=true` (`SystemParameters.ClientAreaAnimation == false`; the setting defaults to false since
   2026-11-04, so by default this Windows option is ignored), Battery saver is on (`GetSystemPowerStatus().SystemStatusFlag & 1`,
   re-checked on `SystemEvents.PowerModeChanged`), or the session is remote (`SystemParameters.IsRemoteSession`):
   morph uses 120 ms with no content slide, all other animations are instant, no reminder pulse ring, no peek width tween.
9. **Runtime settings** in `TaskIsland.App.csproj`: `<ConcurrentGarbageCollection>false</ConcurrentGarbageCollection>`
   (no background GC thread; the heap is tiny), `<TieredCompilation>false</TieredCompilation>` and `<TieredPGO>false</TieredPGO>`
   (changed in Phase 6, 2026-11-04: -13 MB private / -18 MB working set at idle for about +0.25 s cold start, see DEVLOG), Workstation GC (default). Release
   publish uses ReadyToRun (§Phase 7). SQLite: `PRAGMA cache_size = -1024` (1 MB) per connection, connections
   short-lived as today.
10. **Measure, don't guess.** Phase 2 records a baseline (idle CPU, private bytes, working set) right after the island
    works; Phases 3–5 re-measure at the end of each phase and log the numbers in DEVLOG, so a regression is caught
    the phase it happens.

### 6.8 Mascot face notch (added 2026-11-02)

References: the user's sheet `docs/design/mascot-reference.webp` (12 expressions + mini notch) and the vector rig
**`docs/design/mascot-faces.html`**, which is the **geometry source of truth**: open it in a browser, copy each path
1:1 into WPF `Path.Data`/`Ellipse`/`Rectangle` (the SVG coordinate frame is 240 × 64 DIP, eye centres (86,32) and
(154,32), mouth centre (120,42), accessories top-right x 200–238). Match the look as closely as possible; Phase 2
screenshots must be compared side by side with the reference sheet.

**States** (all four are rectangles the `IslandHost` clip animates between, §6.2 generalised to `AnimateTo(state)`):

| State | Size (DIP) | Shows | When |
|---|---|---|---|
| **Mini** (collapsed, no activity) | 120 × 32 | two small glowing dots (r 3.2) | default resting state |
| **Reaction** (expanded, reaction active) | 232 × 56 | face: eyes + mouth + accessory | an event or a random idle pop; holds 2.5 s (alerts 5 s), then back to Mini |
| **Info** | NotchWidth × 36, peek up to 360 | progress ring, "3 tasks", top task on longer hover (§6.3) | hover ≥ 400 ms |
| **Panel** | 420 × ≤ 550 | the To-Do panel (§6.4); a 28-px version of the face sits left of the title and reacts too | click / hotkey / tray |

**Compact scale (2026-11-03 change):** Mini, Reaction and Info are drawn at **0.8×** the sizes above
(`IslandHost.CompactScale`): Mini 96 × 25.6, Reaction 185.6 × 44.8, Info NotchWidth × 0.8 by 28.8 DIP (at 125 %: 120 × 32,
232 × 56, 200 × 36 px). The user asked for a smaller notch; for reference, the Nusa notch app measured 180 × 37 px at 125 %. Code
still passes the design sizes above and the face/info content keeps its 1:1 reference geometry under the scale. The panel is not
scaled.

**Flush notch (2026-11-03 change):** with `TopMargin` 0 (the new default) the island touches the top of the screen like a
notch: flat top, rounded bottom corners (compact states radius 0.3 × height, panel 24). `IslandHost.Flush` extends the shared
geometry upward by its radius so the top corners and top outline fall outside the window. A `TopMargin` above 0 gives the old
floating pill.

Hover (Phase 2): Mini → Curious for 400 ms → Info pill → peek after another 600 ms. Clicking the hover face or the Info pill opens
the panel; clicking a face that popped up by itself shows Laughing and a second click opens the panel.

Mini → Reaction → Mini uses the morph motion (`morph` tokens, `FluentEase`); the dots **become the eyes** (the two
dot elements move to the eye positions and morph into the expression's eye shapes), so it reads as one creature waking up.

**Look** (from the reference): near-black body with a vertical gradient `#16182A → #05060B`, a faint top gloss (white 5 %),
a 2 px neon outline gradient `#3550FF → #6B4DFF`, and a soft outer glow. Features `#EAF2FF` with a `#7FB6FF` glow;
hearts `#8FD3FF`; accessories white with a `#3D5AFE` outline. **All glows are drawn as the same geometry repeated
underneath with a wider, ~28 % opacity stroke** (exactly as the HTML rig does), never with `BlurEffect`/`DropShadowEffect`.
This mascot look is used for the Mini/Reaction/Info states in both themes; the Panel keeps §6.1 tokens with the neon
outline at 40 % opacity so it feels like the same object.

**Face rig** (`Controls/FaceRig.xaml(.cs)`): a 240 × 64 `Canvas` with independent parts, each in its own `Grid` with
its own `RenderTransform` (scale/translate/rotate) and `Opacity`:
`LeftEye`, `RightEye`, `Mouth`, `Accessory`, plus their glow twins. An expression (`Mascot/Expression.cs`, a record:
name, left-eye geometry, right-eye geometry, mouth geometry, accessory geometry, colours) is **data**, so all 12 share
one rig. Geometries are frozen `StreamGeometry` resources parsed once at startup (cheap, tiny).
Changing expression: outgoing part scales to 0.6 + fades (80 ms), incoming part scales 0.6 → 1 + fades in (120 ms),
each part staggered 30 ms (eyes, then mouth, then accessory). No images, no sprite sheets.

**Why not Lottie or sprites:** Lottie in WPF needs extra NuGet packages built on SkiaSharp (more memory, against §3.2
and §6.7). Sprite sheets cost memory per frame and look blurry at 150–200 % scaling. Vector parts in XAML do the
same job (independent eyes/mouth, smooth transitions) at near-zero cost and stay sharp at any DPI.

**Micro-animations** (all finite, render-properties only, §6.2 rules):

| Animation | How | When |
|---|---|---|
| Blink | both eyes `ScaleY` 1 → 0.1 → 1, 140 ms total | once 600 ms after a Neutral/Happy/Curious reaction appears; again 1.6 s later if still showing |
| Smile pop | mouth `ScaleY` 0.6 → 1.1 → 1 (`normal`) | on Happy, Wink, Love, Excited |
| Laugh bounce | eyes `TranslateY` 0 → −2 → 0, 2 times | Laughing, Excited |
| Accessory pop | accessory scale 0 → 1.15 → 1 + rotate −10° → 0 (`normal`) | when an accessory appears |
| Sparkle twinkle | sparkle scale 1 → 0.7 → 1, 2 times | Excited, Focused |
| Sleepy drift | "zZ" `TranslateY` 0 → −4 + fade, 1 time | Sleepy |
| Heart beat | hearts scale 1 → 1.15 → 1, 2 times | Love |
| Shake | whole face `TranslateX` ±3, 3 times, 240 ms | Sad |
| Look around | eyes `TranslateX` −3 → +3 → 0, 600 ms | Curious |

Reduced motion (§6.7 rule 8): the expression appears instantly with no micro-animations and holds the same time.

**Companion — idle life (2026-11-03 change, user request "like a live companion"):** between reactions the face keeps
moving a little. `Core/Mascot/CompanionRules` picks a movement that suits what is showing; `ReactionDirector` arms one
one-shot timer for the next movement (never a repeating timer or an infinite animation); `FaceRig.PlayLife` plays it as
a short finite animation of the part transforms, and skips it while the face's own reaction micros are still running.

| Setting | Resting dots: next movement after | Face showing: next movement after | Faces hold |
|---|---|---|---|
| `Off` | — | — | 2.5 s (alerts 5 s) |
| `Calm` (default) | 4–9 s | 1.5–3.5 s | 4 s (alerts 5 s) |
| `Lively` | 2.5–6 s | 1–2.5 s | 6 s |

Movements: blink (140 ms), double blink, glance left/right (eyes ±3, 1.1 s), breathe (whole face 1.2 DIP down and back,
1 s), happy squeeze (eyes 0.8 high + mouth, 300 ms), mouth twitch (260 ms), accessory wiggle (500 ms). Open eyes (Neutral,
Curious, Surprised, Thinking, Focused, Love) blink and glance; closed or squeezed eyes (Happy, Laughing, Wink, Excited) squeeze
instead; Sleepy and Sad never squeeze; only faces with a mouth twitch it; only faces with an accessory wiggle it. Awake dots
blink most often; sleeping dots only breathe. Paused like random pops (hidden, locked, full screen, display off, paused,
reduced motion) but it does run while the panel is open (on the 28 px face) and in the quiet minute after `--startup`.

**Expressions and triggers** (`Services/ReactionDirector.cs`; one place that decides which face shows):

| # | Reaction | Use case | Trigger in TaskIsland | Accessory |
|---|---|---|---|---|
| 1 | Neutral | Normal idle | random pop; panel face at rest | — |
| 2 | Happy | Task completed | a task is ticked | — |
| 3 | Laughing | Playful interaction | clicking a face that popped up by itself (Reaction state); random pop | `//` lines |
| 4 | Wink | Friendly notification | a task is added; app start greeting (not on `--startup` during the first 60 s) | — |
| 5 | Surprised | Important alert | a due reminder fires; overdue tasks found at startup (holds 5 s) | `//` lines |
| 6 | Curious | User interaction | mouse hovers the mini notch (before it becomes the Info pill); random pop | `?` |
| 7 | Sleepy | Inactive state | the island is untouched for 60 min → the mini notch's dots turn into closed eyes (static, no loop); also on display-dim / resume-from-sleep greeting | `zZ` |
| 8 | Thinking | Processing | import, export, backup, restore in progress (shown until done) | thought bubbles |
| 9 | Love | Positive feedback | a High-priority task is ticked; 5 tasks ticked within 30 min | — |
| 10 | Excited | Achievement | the last outstanding task in the scope is ticked ("All done") | sparkles |
| 11 | Focused | Working mode | panel face while typing in quick-add or editing a task | sparkle |
| 12 | Sad | Error or unsuccessful | a save fails, import is rejected, hotkey can't be registered, DB problem | sweat drop |

Rules:
- Priority: Sad and Surprised beat everything; then Thinking; then task events; random pops are lowest and are dropped
  (not queued) if anything else is showing. Only one reaction at a time; a new event replaces the current face
  through the normal expression transition.
- **Random pops** (the user's "randomly show in a small notch"): the director picks from Neutral, Happy, Laughing,
  Wink, Curious, Love, Thinking (never Sad/Surprised, never the same twice in a row) at a random moment inside the
  `IdleReactions` window. It is **one one-shot `DispatcherTimer`**, re-armed with a new random interval after each
  pop. No pops while: the panel is open, the island is hidden or paused, full-screen is active, the session is locked,
  the display is off, Battery saver is on, reduced motion is on, or the user is in the first 60 s after `--startup`.
- Events still react while the panel is open, but on the 28-px panel face instead of the notch.
- Accessibility: every reaction also sets the island's `AutomationProperties.Name` (e.g. "TaskIsland: task completed")
  so screen readers get the meaning; the face itself is decorative.

---

## 7. Windows integration specification

### 7.1 Window styles & focus (`WindowStyler`)

On `SourceInitialized`: add `WS_EX_TOOLWINDOW` (hide from Alt-Tab/taskbar) and `WS_EX_NOACTIVATE`.
Remove `WS_EX_APPWINDOW`. Use `GetWindowLongPtr/SetWindowLongPtr(GWL_EXSTYLE)`.
- **Expand**: remove `WS_EX_NOACTIVATE`, then `Activate()` + `SetForegroundWindow` and focus the quick-add box.
  This works because the user's click or hotkey gives us foreground rights.
- **Collapse**: re-add `WS_EX_NOACTIVATE`. Do not steal focus back to anything.
- **Topmost re-assert**: `SetWindowPos(hwnd, HWND_TOPMOST, …, SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE)` after
  resume, display change, full-screen end and Explorer restart.

### 7.2 Positioning, monitors, DPI (`MonitorService`, `IslandPositioner`)

- `app.manifest`: `<dpiAwareness>PerMonitorV2</dpiAwareness>` + `<dpiAware>true/pm</dpiAware>`.
- Enumerate with `EnumDisplayMonitors` + `GetMonitorInfo` (`MONITORINFOEX`, `szDevice`, `rcMonitor`, primary flag).
  DPI via `GetDpiForMonitor(MDT_EFFECTIVE_DPI)`.
- Target monitor = `MonitorDeviceName` match, else primary. Position in **physical pixels**:
  `scale = dpi/96`, window size in DIP = collapsed 452 × 64 or expanded 452 × 600 (§6.2), `wPx = wDip*scale`,
  `centreX = rc.Left + rc.Width/2 + HorizontalOffset*scale`, `x = centreX - wPx/2`, `y = rc.Top + TopMargin*scale`.
  Clamp `centreX` (not x) so the 420-wide panel stays inside `rcMonitor`; both sizes share the same centre and top, so
  the pill does not move when the window switches size. Apply with `SetWindowPos(HWND_TOPMOST, x, y, wPx, hPx, SWP_NOACTIVATE)`.
- Handle `WM_DPICHANGED` in an `HwndSource` hook: let WPF rescale, then re-run positioning (ignore the
  suggested rect, use our own).
- Use `rcMonitor` (top of the actual screen), not `rcWork`. The island sits over the top edge.

### 7.3 Power, session & display recovery (`PowerEventsService`, `RecoveryCoordinator`)

Event sources (all event-driven, **no polling**):
- `WM_POWERBROADCAST` (`PBT_APMSUSPEND`, `PBT_APMRESUMEAUTOMATIC`, `PBT_APMRESUMESUSPEND`) via HwndSource hook.
- `RegisterPowerSettingNotification(GUID_CONSOLE_DISPLAY_STATE {6FE69556-704A-47A0-8F24-C28D936FDA47})`.
  Display on/off/dim. This catches **Modern Standby** laptops where classic resume messages may not arrive.
- `Microsoft.Win32.SystemEvents.SessionSwitch` (SessionLock/SessionUnlock).
- `SystemEvents.DisplaySettingsChanged` + `WM_DISPLAYCHANGE` (resolution, monitor plug/unplug).
- `RegisterWindowMessage("TaskbarCreated")` → Explorer restarted → re-add tray icon + re-assert topmost.

On suspend/lock: collapse panel (if expanded, commit any in-progress edit as Cancel), nothing else.
DB writes are already committed. On resume/unlock/display-on/display-change: **debounce 750 ms**, then `RecoverAsync()`:
1. Re-enumerate monitors → choose target (fallback primary) → reposition.
2. Re-apply ex-styles + topmost. Ensure `Visibility = Visible` if `RestoreAfterWake` and not paused/full-screen.
3. Verify tray icon (`NotifyIcon.Visible = true` again).
4. Re-register hotkey if lost. Recompute reminder timer. Run daily-backup check.
5. Schedule two follow-up repositions at +2 s and +6 s (displays often report late after resume).
   These are one-shot timers, not polling.
Log every event and recovery step.

### 7.4 Full-screen detection (`FullScreenDetector`)

- `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, …, WINEVENT_OUTOFCONTEXT)` and
  `EVENT_OBJECT_LOCATIONCHANGE` filtered to the foreground hwnd + `OBJID_WINDOW` (catches F11 in browsers),
  debounced 300 ms. Keep the delegate in a field (GC!).
- Full-screen if `SHQueryUserNotificationState` ∈ {`QUNS_BUSY`, `QUNS_RUNNING_D3D_FULL_SCREEN`,
  `QUNS_PRESENTATION_MODE`} **or** the foreground window rect covers the island's monitor `rcMonitor`
  and its class is not `Progman`/`WorkerW`/`Shell_TrayWnd` and it is not our own window.
- Only hide when the full-screen window is on the **same monitor** as the island. Restore when it ends.

### 7.5 Tray (`TrayIconService`)

`System.Windows.Forms.NotifyIcon`, icon matches the theme. Left-click = toggle expand. Double-click = expand.
Right-click menu (`ContextMenuStrip`, owner-drawn not required):
Open TaskIsland · Expand / Collapse · Show / Hide · Pause visibility / Resume visibility · Settings… · Exit.
- *Show/Hide*: toggles the notch now. Automatic events (wake, full-screen end) may show it again.
- *Pause visibility*: hides the notch and **suppresses all auto-show** until Resume. The tray tooltip says "Paused".
- Tooltip: "TaskIsland — 3 tasks left".
- Exit is the only path that ends the process: dispose tray icon (no ghost icon), unhook events,
  unregister hotkey, close DB pools, `Application.Shutdown()`.
- ✕ in the panel and Alt+F4 only collapse. Intercept `Closing` → cancel → collapse.

### 7.6 Startup (`StartupRegistration`)

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `TaskIsland` = `"<exe path>" --startup`.
On first run (`FirstRunCompleted` false) enable it (setting default true). The toggle adds/removes it.
On every launch, if enabled, rewrite the value if the exe path changed (e.g. after reinstall to another folder).
`--startup` → always start collapsed, no splash, no window activation. The installer uninstall step removes
the value (see §9).

### 7.7 Single instance (`SingleInstance`)

Named mutex `Local\TaskIsland-7F3A9C2E-SingleInstance` (also used as Inno `AppMutex`). If already held:
connect to named pipe `TaskIsland-7F3A9C2E-<user SID>` (server and client use `PipeOptions.CurrentUserOnly`, §7.11),
send `EXPAND`, exit with code 0. The server accepts only the exact `EXPAND` message (max 16 bytes) and ignores anything else. The first
instance runs a pipe server loop (async, background) and marshals `EXPAND` to the dispatcher.

### 7.8 Hotkeys (`HotkeyService`)

`RegisterHotKey(hwnd, id, MOD_CONTROL|MOD_ALT|MOD_NOREPEAT, VK_T)`. Handle `WM_HOTKEY` → toggle.
Parse/format the setting string ("Ctrl+Alt+T"). Require at least two modifiers when the user rebinds,
to avoid conflicts. If registration fails, log it and show a warning in Settings. Escape/Enter are
in-window keys, not global.

### 7.9 Reminders (`ReminderScheduler` + `ReminderService`)

Pure logic computes the next un-reminded due moment among active tasks. The service sets one
`DispatcherTimer` to fire then (max interval 24 h, recompute after any task change, resume or time change via
`SystemEvents.TimeChanged`). On fire: tray balloon "⏰ Due now: <title>" with the list name as the
balloon title (click → expand with that task's list selected),
pulse the pill, `MarkReminderShownAsync`. Tasks already overdue at startup → one combined balloon
"2 tasks overdue". Editing a task's due date clears `ReminderShownAt`.

**Alert style (decided 2026-11-04, built in Phase 5).** A due reminder always does all of: Windows notification, accent pulse
ring, Surprised face. Phase 5 adds the **widening pill alert** (`ReminderPillAlert`, default on):
- The collapsed island widens (same width tween as the hover peek) and shows
  "⏰ <task title>" with the list-colour dot, holds **5 s**, then shrinks back. Width (changed after rc.1 at the user's
  request): `ReminderAlertWidthPercent` of the screen, default **50 %** (Compact = the old `min(NotchWidth + 200, 420)`),
  kept on the screen around the island's centre (`IslandPlacement.AlertWidthDip`). The window widens for the alert and
  narrows again once the island has rested (or once the panel has opened). With `ReminderBlink` (default on) a **red border
  blinks** around the island (about once a second, well under the 3-flashes-a-second limit) instead of the accent pulse;
  steady red in reduced motion; removed when the alert ends. The height does not change and the panel does
  not open. Mouse hover keeps it open until the mouse leaves. Clicking it opens the panel on that task's list.
- Several tasks at once: "⏰ 3 tasks due". An overdue-at-start notice shows "⏰ 2 tasks overdue".
- Skipped (notification still shows) when the panel is expanded, the island is hidden (Pause, user Hide, full screen), or the
  face notch is off (MascotEnabled=false uses the classic pill, which widens the same way). Reduced motion: no tween, the text
  swaps in place for 5 s. Implemented with one-shot timers only; nothing runs after the alert ends (§6.7).

**Early reminder (`ReminderLeadMinutes`, default off; 60 = "1 hour before").** For tasks with a due *time*, a second, gentler
reminder fires that many minutes before the due moment: notification "⏰ In 1 hour: <title>" (list name as heading), the
pill alert and one pulse; the face stays unchanged. The normal "Due now" reminder still fires at the due time.
- Stored per task as a new column `EarlyReminderShownAt` (migration v+1, nullable), so it fires once and survives a restart.
  Editing the due date or time clears both `ReminderShownAt` and `EarlyReminderShownAt`.
- A task created, edited or found already inside the window (due in 30 min with a 60-min lead) gets no early reminder; it is
  marked quietly and only the "Due now" reminder fires. Date-only tasks get no early reminder.
- `ReminderScheduler` gets `GetNextUpcoming` over both moments (due − lead, due); lead changes in Settings call
  `ReminderService.Recompute()`. Unit tests: lead off, lead 60 with due in 2 h / in 30 min / in the past, date-only, completed,
  deleted, due date edited, lead changed after the early one fired.

### 7.10 Logging & errors

`FileLogger` → `Logs\taskisland-yyyyMMdd.log`, keep 14 days, thread-safe append, levels Info/Warn/Error.
Hook `DispatcherUnhandledException` (log, show a small non-fatal error toast, mark handled where safe),
`AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`.
DB failure on startup (locked/corrupt) → message box with "Open data folder" / "Restore from backup" / "Exit".
Never silently drop data.

### 7.11 Don't trigger security software (added 2026-11-01, must-follow)

Goal: Microsoft Defender, third-party antivirus, EDR tools, Windows Firewall and Controlled Folder Access never flag,
block or prompt about TaskIsland. Antivirus works on **behaviour heuristics** and **file reputation**, so the app must
behave like a plain, well-labelled desktop app and avoid every pattern malware uses.

**Banned in code** (search for these in code review, Phase 6):
1. **No keyboard or mouse hooks.** Never `SetWindowsHookEx` (`WH_KEYBOARD_LL`, `WH_MOUSE_LL` or any other); hooks
   look like keyloggers. The global shortcut uses `RegisterHotKey` only. Hover detection uses WPF mouse events on our
   own window only. `SetWinEventHook` is allowed only `WINEVENT_OUTOFCONTEXT` for foreground/location events (§7.4), no
   in-context DLL injection.
2. **No touching other processes:** no `OpenProcess` with write/VM rights, no `ReadProcessMemory`/`WriteProcessMemory`,
   no `CreateRemoteThread`, no DLL injection, no reading other windows' text. Full-screen detection only reads the
   foreground window's rect and class name.
3. **No spawning shells or scripts:** never start `cmd`, `powershell`, `wscript`, `mshta`, `rundll32`, `reg.exe`,
   `schtasks`. The only `Process.Start` calls are: opening the data folder in Explorer (`explorer.exe "<folder>"`) and
   opening the two About links in the default browser (`UseShellExecute=true`), both **only on a user click**.
4. **No network at all** (§2): no sockets, HTTP, update checks or telemetry, so no firewall prompt ever appears.
   Single instance uses a local **named pipe** created with `PipeOptions.CurrentUserOnly` (only the same user can connect).
5. **Persistence only in the normal, visible way:** one `HKCU\…\Run` value named `TaskIsland` pointing at the
   installed exe (§7.6), which is visible in Task Manager → Startup apps and removable there. No scheduled tasks,
   services, `RunOnce`, `Winlogon`, `Image File Execution Options`, WMI subscriptions, startup-folder copies or
   hidden registry tricks. If the user turns startup off (in TaskIsland or in Task Manager), respect it and never
   re-add it silently: only re-write the value when the setting is on **and** the value exists but points at an old path.
6. **Files only in our own folders:** writes go to `%LocalAppData%\TaskIsland\` and to files the user picks in a
   save dialog. Never write to Program Files, Windows, System32, other apps' folders, or the Startup folder.
   No self-copying, no self-deleting, no changing file attributes to hidden/system.
7. **No admin, no UAC prompt.** Manifest `requestedExecutionLevel level="asInvoker"`. Installer per-user (`lowest`).
8. **No obfuscation or packing:** no UPX/packers, no obfuscators, no encrypted strings or payloads, no single-file
   self-extraction (folder publish, §Phase 7), no code loaded at runtime from bytes, no `Assembly.Load(byte[])`.
9. **No security or system setting changes:** never touch Defender, firewall, UAC, proxy, hosts file, certificates or
   policies. `SetProcessInformation` (Efficiency mode) only affects our own process.
10. **Honest window behaviour:** the island is visible (topmost, but not hidden from the user); the tray icon is always
    present while running, so the user can always see and exit the app.

**Controlled Folder Access** (Defender ransomware protection) blocks unknown apps from writing to Documents, Desktop,
Pictures and similar. TaskIsland's own data is in `%LocalAppData%` (not protected), so it is never blocked. For JSON
export and manual backup, the save dialog starts in **Downloads**. If a write still fails with
`UnauthorizedAccessException`/`IOException`, show: "Windows blocked saving here (Controlled folder access may be on).
Choose another folder, such as Downloads." Never retry in a loop, never suggest turning protection off.

**File identity (helps reputation and heuristics):** every exe/dll we build carries full version info
(`Company RieSources`, `Product TaskIsland`, `FileDescription`, `FileVersion`, `InformationalVersion`, `Copyright`),
an application icon and the manifest (§7.2 + `asInvoker` + Windows 10/11 `supportedOS`). Release builds are
deterministic (`<Deterministic>true</Deterministic>`, `ContinuousIntegrationBuild` on publish).

**Code signing** (decides SmartScreen and Smart App Control): an unsigned installer shows the SmartScreen "Windows protected
your PC" warning until it gains download reputation, and on PCs with **Smart App Control** turned on (Windows 11 clean
installs) unsigned apps can be **blocked** outright. Only a trusted code-signing signature fixes both. This is a user decision
(it costs money): see Phase 7 step 4b. Without signing, everything else in this section still prevents antivirus detections,
but the SmartScreen warning will appear on first download.

---

## 8. Phases

Each phase: **Goal → Steps → Acceptance → Verify → Commit.** After each phase append to DEVLOG.md
using the template there, update TESTING.md, push, and report to the user.

### Phase 1 — Solution skeleton, models, data layer

**Goal:** a buildable solution with a fully tested data layer, no UI beyond a blank window.

Steps:
1. `git pull`. Create `global.json` (sdk 10.0.403, `rollForward: latestFeature`) and `Directory.Build.props`
   (`Version 0.1.0`, `Nullable enable`, `ImplicitUsings enable`, `LangVersion latest`, `Company RieSources`,
   `Product TaskIsland`, `Authors RieSources`, `Copyright © 2026 RieSources`).
2. `dotnet new sln -n TaskIsland`. Create projects per §3.1 (`classlib` for Core/Data/Windows, `wpf` for App,
   `xunit` for tests), add to sln, set TFMs, add references and packages (§3.2).
3. Core: `Priority` enum (High=0, Medium=1, Low=2), `TaskItem` (all columns from §4.1), `TodoList`, `TaskFilter`,
   `AppSettings` with defaults (§5), interfaces, `TaskSorter`, `TaskStats`, `IClock`, `ReminderScheduler`, `JsonTransfer`.
4. Data: `AppPaths` (creates `Data`, `Backups`, `Logs`; constructor overload taking a root for tests), `Database`
   (connection string `Data Source=…;Pooling=True` (no shared cache, SQLite discourages it and WAL does the job), WAL set once at initialise, `foreign_keys=ON` and `busy_timeout=3000` on every connection), `Migrations`,
   `SqliteTaskRepository`, `SqliteTodoListRepository`, `SqliteSettingsStore`, `BackupService`.
5. Tests, at least:
   - Core: sort order across all three priorities and ties. Each filter. Stats incl. 0 tasks and percent rounding.
     JSON round-trip. Each import validation failure. Reminder next-due selection (date-only = 09:00, skips shown/completed/deleted).
   - Data: add/get/update/complete/uncomplete. Soft delete + restore + purge. Reorder persists. Priority change moves to group end.
     ClearCompleted. Settings get/set/defaults. Migration on fresh DB sets `user_version=1` and creates the 2 starter lists.
     Reopen DB keeps data (persistence).
   - Lists: create/rename/recolour/reorder. Duplicate name (case-insensitive) rejected. 21st list rejected. Last list
     can't be deleted. Delete with move keeps tasks (now in target list, at group end). Delete without move removes its tasks.
     MoveToList. Reorder within one list doesn't touch other lists. Sorting/stats scoped to one list and to All lists.
     JSON round-trip keeps lists and task→list mapping. Merge import matches list names case-insensitively.
     Backup creates a valid file. Restore replaces data and makes a pre-restore copy. Prune keeps 7.
6. App project: replace template with minimal `App.xaml.cs` that builds the DI container and opens an empty window
   (proves wiring). Add `app.manifest` (§7.2).
7. Update README "Build" section if commands changed.

Acceptance: `dotnet build TaskIsland.sln -c Release` 0 errors. `dotnet test` all green (≥ 40 tests).
Verify: `dotnet build TaskIsland.sln -c Release && dotnet test TaskIsland.sln`.
Commit: `Initial TaskIsland project structure and SQLite data layer`.

### Phase 2 — Dynamic Island interface (Opus recommended)

**Goal:** a modern pill and panel (§6.0–6.4) that morph smoothly **and cost nothing when idle** (§6.7). Uses static
sample tasks bound through the real ViewModel (data wiring comes in Phase 3). The reference image sets the layout;
§6.1 tokens set the look (the modern tokens win where they differ from the image).

Steps:
1. Themes: `Dark.xaml`/`Light.xaml` with all tokens from §6.1 as frozen `SolidColorBrush`/`Color` resources (same keys).
   `Controls.xaml` styles: round checkbox (accent fill + white tick, `fast` scale-in), icon buttons (hover/press using
   `SurfaceRaised`/`SurfacePressed`), pill input box, priority chip (dot + label), segmented filter with sliding
   indicator, primary/secondary buttons, overlay scrollbar, 2 px accent focus ring.
   Use `DynamicResource` for theme brushes so the theme can swap live. Add `FluentEase` and `MotionPolicy`
   (duration tokens + reduced-motion detection, §6.7 rule 8) and the 60 fps frame-rate cap at startup.
2. `IslandHost` clip-geometry morph (§6.2, no per-frame layout) + `IslandWindow` (§6.2/§7.1 styles applied in
   `SourceInitialized`) with the collapsed/expanded window sizes and the resize-only-when-still rule.
3. Pill layout (§6.3, with progress ring), panel layout (§6.4) incl. the list switcher row and virtualised task list,
   with sample rows covering all 3 priorities, one done, one overdue, two lists. Panel tree created on first expand.
4. Cached shadow (§6.2). `IslandPositioner` minimal version: primary monitor top-centre with DPI scale, both window
   sizes sharing the same centre (full logic in Phase 4).
5. Click pill → expand. ✕ / Esc / click outside (`Deactivated`) → collapse. Focus behaviour per §7.1.
6. Hover effects and peek-on-hover (§6.2). Efficiency mode on/off (§6.7 rule 7). `ConcurrentGarbageCollection=false`,
   `TieredPGO` in the App csproj; `PRAGMA cache_size=-1024` in `Database` (keep all 218 tests green).
6b. **Mascot (§6.8):** `FaceRig` with all 12 expressions + Mini notch, geometry copied 1:1 from
   `docs/design/mascot-faces.html`; Mini ↔ Reaction ↔ Info ↔ Panel state machine in `IslandHost`; all micro-animations;
   `ReactionDirector` with priorities and random pops (one-shot timer) and the Sleepy timer; `MascotEnabled` and
   `IdleReactions` keys added to `AppSettings` with default/clamping unit tests. Phase 2 wires only the triggers that
   exist yet (hover → Curious, click face → Laughing, random pops, start greeting); task/import/error triggers are wired
   in Phases 3–5. Add a hidden debug shortcut **Ctrl+Alt+Shift+F** (only in Debug builds) that cycles through all 12
   expressions so they can be screenshotted.
7. Run the app and compare with `docs/design-reference-1.webp` (overall target; `docs/design-reference.webp` for layout) and `docs/design/mascot-reference.webp` (faces).
   Take screenshots (computer-use) of the Mini notch, every one of the 12 reactions, the Info pill and the expanded
   panel in dark and light, save them to `docs/screenshots/phase2-*.png`, put the 12 faces side by side with the
   reference sheet, and iterate until they match as closely as possible.
8. **Baseline measurement** (record in DEVLOG): idle CPU over 60 s (`(Get-Process TaskIsland).CPU` delta), private
   bytes and working set after 60 s collapsed, GPU column in Task Manager (screenshot), and whether Task Manager shows
   Efficiency mode while collapsed. Check against §6.7 targets.

Acceptance: pill at top centre. Smooth 260/180 ms morph with no window flicker or jump, including the window
size switch. Desktop under/around the pill is clickable. Hovering the collapsed pill (incl. peek) never takes focus.
Typing in Notepad continues uninterrupted while the mouse moves over the pill. Expanded panel accepts typing. Not shown
in Alt-Tab or taskbar. Idle CPU 0.0 % over 60 s; no running animation clocks while idle; reduced motion works when
Windows "Animation effects" is turned off. All 12 faces visibly match the reference sheet; the dots turn into eyes
when the Mini notch opens into a reaction; random pops stop while hidden, full-screen, locked or on Battery saver.
Commit: `Implement Dynamic Island collapsed and expanded interface`.

### Phase 3 — To-Do functionality

**Goal:** everything in §4 and §6.4–6.5 working against the real database.

Steps:
1. `IslandViewModel`: `ObservableCollection<TaskItemViewModel>` + `ICollectionView` with filter & live sort
   (or re-order the collection manually after each change, choose the simpler one that keeps animations).
   Commands: Add, ToggleComplete, BeginEdit, SaveEdit, CancelEdit, Delete, Undo, CyclePriority, SetFilter,
   ClearCompleted, Reorder, SelectList, CreateList, RenameList, RecolorList, MoveList, DeleteList.
   `ObservableCollection<ListChipViewModel>` (id, name, colour, outstanding count) for the switcher.
   Stats properties feed pill + footer + chips.
2. Wire repository. Every action persists immediately (await) and only updates UI on success. On failure:
   log, revert UI, show inline error text.
3. Add animation: new row fades in + slides from −8 px (200 ms). Delete: row collapses height + fades (180 ms) → Undo bar.
4. Complete: checkbox animation + strikethrough (TextDecorations animated via opacity of an overlay line,
   200 ms) → after 2 s (if `CompletedFadeOut` and filter = Active and still completed) slide-out 220 ms and remove from view.
   Untick within 2 s cancels the timer.
5. Priority change: chip colour/label crossfade 150 ms. Row animates to its new position (fade out/in is fine).
6. Inline edit (§6.5) with List ComboBox + DatePicker + time.
7. Drag to reorder within the same priority group in Active view. Lift a copy of the row, dim the source and slide the
   other rows aside to show the gap (no insertion line). Dropping outside the group is not allowed (cursor `No`).
   Persist via `ReorderAsync`.
8. Filters, empty states, counters, panel height re-animation when list changes.
8b. Mascot triggers from §6.8 for task events: tick → Happy, High tick or 5 in 30 min → Love, last task done → Excited,
   add → Wink, typing/editing → Focused (panel face), save failure → Sad. Reminder → Surprised is wired in Phase 4,
   import/export/backup → Thinking and failures → Sad in Phase 5.
9. Lists: switcher row, `ListEditPopup`, chip context menu, delete-list dialog (move vs delete, auto-backup first),
   keyboard shortcuts (§6.4), quick-add list chip in *All lists*, List field in inline edit.
10. Remove sample data. First run creates **no** sample tasks (clean start), only the 2 starter lists. Show the empty state.

Acceptance: every item under "Task management", "Lists" + "Data: Save tasks locally / Preserve after app restart" in
TESTING.md passes manually. Kill the process (`taskkill /f`) mid-use → relaunch → data intact.
All row animations follow §6.2 animation rules (render properties only, finite). Re-measure idle CPU/memory (§6.7) and log in DEVLOG.
Commit: `Implement task CRUD, priorities, sorting, filters and persistence`.

**Phase 3 as built (deviations and additions, 2026-11-04):**
- Date entry uses an own `Controls/MiniCalendar` popup instead of the stock `DatePicker`, and the List/Priority pickers are chips
  instead of `ComboBox`es: the stock controls cannot follow the dark theme. "Change colour…" opens the list editor card rather than a
  Colour ▸ submenu.
- Create/rename list, delete list, Clear completed and the Undo bar are overlay cards inside the panel (not separate windows), so they
  never trigger click-outside collapse. `ListEditPopup` is therefore `IslandViewModel.Lists.cs` plus overlay XAML in `PanelView`.
- Extra keyboard shortcuts: F2 edit, Del delete, Alt+Up / Alt+Down reorder (also the way to test reordering without a mouse).
- Row delete is a render-only fade and shrink (the clip then re-animates), not a real height collapse.
- `FileLogger` was added in this phase (small), not in Phase 4. A list delete first writes a `pre-delete-` backup.
- A moved row is given a new `TaskItemViewModel` (reusing the old one left a stale UI Automation node).
- New files: `Controls/MiniCalendar`, `Themes/FormControls.xaml`, `ViewModels/IslandViewModel.Lists.cs`, Core `DueInput`,
  `ReorderRules`, `Mascot/TickStreak`.

### Phase 4 — Windows integration (Opus recommended)

**Goal:** §7.1–7.10 complete. Reliable background utility.

Steps (one sub-commit each is fine):
1. `SingleInstance` → wire in `App.OnStartup` before anything else.
2. `TrayIconService` with full menu + Exit path + `TaskbarCreated`.
3. `StartupRegistration` + `--startup` argument + first-run enable.
4. `MonitorService` + full `IslandPositioner` + `WM_DPICHANGED`.
5. `PowerEventsService` + `RecoveryCoordinator` (§7.3).
6. `FullScreenDetector`.
7. `HotkeyService` (Ctrl+Alt+T default).
8. `ReminderService` + pill pulse + balloon.
9. `FileLogger` + global exception handlers + DB-failure dialog.

Self-testable by the agent (do these and record results):
- Launch twice → second exits, first expands. `Get-Process TaskIsland` shows 1.
- Startup value exists in `HKCU\…\Run` after first run. Toggling it off removes it.
- Lock/unlock (`rundll32.exe user32.dll,LockWorkStation` then ask the user to unlock) → logs show recovery and the notch is in place.
- Change resolution or scaling in Settings (ask the user to toggle 100%↔125%) → notch re-centres.
- Kill and restart `explorer.exe` → tray icon comes back.
- Open a video full-screen / press F11 in Edge → notch hides, comes back after exit.
- Set a task due in 2 minutes → balloon + pulse.
**Needs the user** (write precise steps in TESTING.md): sleep, hibernate, real reboot + login auto-start,
plug/unplug external monitor, 150%/200% scaling on an external display.
Commit: `Add tray, startup, single instance, sleep/wake recovery, multi-monitor and full-screen handling`.

**Phase 4 as built (deviations and additions, 2026-11-04):**
- **Full screen (§7.4):** the `EVENT_OBJECT_LOCATIONCHANGE` hook was built and worked, but it woke TaskIsland for every window,
  caret and cursor move in the foreground app (63 ms CPU per 45 s while the PC was in use, against the 0.0 % idle target in §6.7).
  It is replaced by the shell's own full-screen notice: a hidden, zero-size helper window registered with `SHAppBarMessage(ABM_NEW)`
  (no `ABM_SETPOS`, so no screen space is reserved) receives `ABN_FULLSCREENAPP`. F11 in a browser is still caught. The foreground
  WinEvent hook stays. Both run only while `HideInFullScreen` is on. Maximised windows never count (with an auto-hiding taskbar they
  cover the monitor too).
- **Tray menu (§7.5):** no "Settings…" item yet: the Settings window arrives in Phase 5 and a button that does nothing is not
  allowed (§0 rule 6). Phase 5 adds the item. The tray icon is drawn in memory (`TrayIconArt`) until Phase 7's IconGen ships the
  .ico files; it follows the taskbar theme and is dimmed while paused. Clicking a reminder works from the notification centre too.
- **Visibility model:** Show/Hide, Pause and full screen are three separate reasons to hide the notch. Opening the panel (tray,
  hotkey, second launch, reminder click) always works, even while hidden or paused; when it collapses, the notch hides again if a
  reason is still set.
- **Single instance (§7.7):** the second launch calls `AllowSetForegroundWindow(ASFW_ANY)` before sending EXPAND, so the first
  instance may take the focus. A test data folder (`TASKISLAND_ROOT`) gets its own mutex/pipe names, and a separate Run key
  (`HKCU\Software\TaskIsland-Test\Run`, or `TASKISLAND_RUNKEY`), so tests never meet the real app or register a dev build.
- **Run key (§7.6, §7.11 rule 5):** with the setting off, launch removes an existing value; with it on, a missing value is written
  only on the first run (never re-added silently) and an old path is rewritten. `StartupManager.ApplyToggle` is ready for Phase 5.
- **Reminders (§7.9):** a due date the user saves in the past is marked quietly (the row is already red) instead of a "due now"
  balloon; reminders more than 60 s late say "overdue". Several due at once → one combined balloon.
- **Errors (§7.10):** the start-up dialog is a Windows task dialog (WinForms `TaskDialog`). "Restore from backup" keeps the
  unreadable file as `Backups\broken-*.db` (`BackupService.RestoreBrokenAsync`). The SQLite command timeout is 5 s (was the 30 s
  default) so a locked file shows the dialog after about 10 s instead of a minute of nothing.
- **Daily backup** (§4.5) now runs at start-up and in the wake recovery.
- `TaskIsland.App.csproj` suppresses the WinForms analyzer warning WFO0003 (it asks WinForms apps to move DPI settings out of the
  manifest; this is a WPF app and §7.2 requires the manifest).

**Phase 5 as built (deviations and additions, 2026-11-04):**
- Settings window is 640×580 with six pages (General, Appearance, Behaviour, Lists, Data, About) and a left pill nav; dialogs are code-built (`DialogWindow`).
- Live apply is centralised in `SettingsApplier`; the monitor list shows "Display N — W×H (Primary)"; the hotkey box needs 2+ modifiers and the in-use warning appears at start-up/wake.
- New settings: `AccentSource` (Default/Windows), `ReminderLeadMinutes` (0, 5, 15, 30, 60, 120, 1440), `ReminderPillAlert`. New column `Tasks.EarlyReminderShownAt` (migration 2).
- Pill alert: the Info state widens to min(NotchWidth+200, 420) DIP, shows the title with the list dot for 5 s; it replaces the Surprised face on the island while shown (the face still shows on the open panel or when the pill setting is off). Early reminders never show the face.
- Early reminder moments already past at start-up, wake or a data change (or older than 60 s on the timer) are marked quietly without a notification.
- About links open with UseShellExecute only on a click; the data folder opens with explorer.exe. Mica uses `Dwm` calls with a solid fallback.
- Memory: closing Settings returns to baseline (83 → 84 MB, no automation client attached). Overall idle memory is still over the §6.7 budget.

### Phase 5 — Settings & data management

**Goal:** §5 and §6.6 complete, all settings live-apply and persist.

Steps:
1. `SettingsViewModel` bound to `AppSettings`. Save on change (debounce sliders 300 ms).
2. `SettingsWindow` with 5 pages (§6.6). Theme switching swaps merged dictionaries at runtime. System theme follows Windows.
3. Monitor dropdown lists monitors as "Display 1 — 1920×1080 (Primary)". Changing it moves the notch immediately.
4. Hotkey capture box (§7.8).
5. Data page: export/import (Merge/Replace dialog, result summary), backup now, restore (with confirmations
   and pre-restore copy), open data folder, clear completed (confirm + Undo bar).
6. `AlwaysShowNotch=false` auto-hide sliver behaviour. `AutoCollapseSeconds`. Density.
7. Lists page (§6.6), default list, pill count scope. Export "all lists / current list" option.
8. About page (§6.6).
9. Modern Settings look (§6.6): pill nav, setting cards, toggle switches, Mica on Windows 11 with solid fallback.
   `AccentSource` setting (add the key to `AppSettings` with clamping/default tests). Window released on close.
10. **Reminder settings (Behaviour page):** Reminders on/off, "Remind me before due time" dropdown (Off / 5 min / 15 min /
    30 min / **1 hour** / 2 hours / 1 day) = `ReminderLeadMinutes`, and "Widen the island when a reminder fires" toggle =
    `ReminderPillAlert`. Implement the pill alert, the early reminder and the `EarlyReminderShownAt` column per §7.9 (the
    pulse, face and notification from Phase 4 stay as they are). Add both keys to `AppSettings` with clamping/default tests.
    Tests: scheduler unit tests (§7.9), a UI script that seeds a task due in 3 min with a 2-min lead (use the test-only
    `TASKISLAND_ROOT` and a short custom lead via the settings table) and checks the log lines, the widened pill width and that
    it shrinks after 5 s, and the quiet-skip cases.

Acceptance: change every setting → effect visible immediately → restart app → setting kept.
Reminders: with lead = 60 a task due in 2 h gets the early notification at 1 h before and "Due now" at the due time (each once, also after a restart); the pill widens with the title for 5 s and shrinks back.
Settings window closed → memory drops back near the idle baseline (§6.7). Re-measure and log in DEVLOG.
Export → clear → import → identical tasks. Backup → delete tasks → restore → tasks back.
Commit: `Add settings window, themes, JSON import/export and backup/restore`.

### Phase 6 — Testing & optimisation

1. Run the full TESTING.md checklist. Fix every critical/major defect found. Re-run affected items.
2. Performance targets: measure every row of the §6.7 table and record the numbers in DEVLOG next to the Phase 2
   baseline. Also: 10 minutes collapsed idle → CPU time delta ≈ 0 and private bytes not growing (leak check); 50
   expand/collapse cycles → memory returns to within 5 MB of the start; idle with 500 tasks in the DB (generate them in
   a temp `TASKISLAND_ROOT`) still meets the targets. Walk §6.7 rules 1–8 as a code-review checklist (search for
   `RepeatBehavior`, `DispatcherTimer`, `Effect`, `Width`/`Height` animations, unfrozen brushes) and fix violations.
3. Confirm no network activity: `Get-NetTCPConnection -OwningProcess (Get-Process TaskIsland).Id` → empty.
4. Code review pass: dispose patterns, event unsubscription, no `async void` leaks, nullable warnings.
   **Security-software pass (§7.11):** grep the whole `src/` for `SetWindowsHookEx`, `OpenProcess`, `ReadProcessMemory`,
   `WriteProcessMemory`, `CreateRemoteThread`, `Assembly.Load(`, `powershell`, `cmd.exe`, `schtasks`, `reg.exe`,
   `HttpClient`, `Socket`, `WebRequest`; every hit must be removed or justified in DEVLOG. List every `Process.Start`
   and every registry write and confirm each matches §7.11. Scan the Release build folder with Defender
   (command in Phase 7 step 4c) and record the result.
5. Write precise user-test steps for the items the agent can't do, and **ask the user to run them**. Record their results.
Commit: `Fix defects and optimise performance` (may be several commits).

### Phase 7 — Production release

1. Icon: `tools/IconGen` renders a 256-px master (dark rounded square, blue circle, white tick) with
   WPF `RenderTargetBitmap` → writes multi-size `.ico` (16, 20, 24, 32, 40, 48, 64, 256) for app + light/dark tray variants.
   Set `<ApplicationIcon>`.
2. Version `1.0.0` in `Directory.Build.props`. `FileDescription`, `InformationalVersion` (add git short hash).
3. Publish (folder, not single-file. Faster startup and native SQLite works without extraction):
   ```
   dotnet publish src/TaskIsland.App -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o publish/win-x64
   ```
   (WPF does not support trimming. Do not enable `PublishTrimmed`.) Smoke-test `publish\win-x64\TaskIsland.exe`.
4. `installer/TaskIsland.iss` (Inno Setup 6):
   - `AppId` fixed GUID (generate once, never change), `AppName TaskIsland`, `AppPublisher=RieSources`, `AppPublisherURL=https://rie-sources.com`, `AppCopyright=Copyright (C) 2026 RieSources`, `AppVersion` from define,
     `PrivilegesRequired=lowest`, `DefaultDirName={localappdata}\Programs\TaskIsland`, `ArchitecturesAllowed=x64compatible`,
     `ArchitecturesInstallIn64BitMode=x64compatible`, `AppMutex=Local\TaskIsland-7F3A9C2E-SingleInstance`,
     `CloseApplications=yes`, `SetupIconFile`, `UninstallDisplayIcon`, `WizardStyle=modern`, `OutputBaseFilename=TaskIsland-Setup-1.0.0-x64`.
   - Files: `publish\win-x64\*` recursesubdirs.
   - Start-menu shortcut. Optional desktop shortcut task (unchecked).
   - `[Registry]` HKCU Run value `TaskIsland` = `"{app}\TaskIsland.exe" --startup`, `Flags: uninsdeletevalue`,
     created only if task "Start with Windows" (checked by default).
     **Changed when built (rc.2, 2026-11-04):** Setup writes **no** Run value and has no "Start with Windows" task. The app
     already owns that entry (first start writes it, the Settings toggle removes it, a moved exe is re-pointed); a Setup
     value would switch it back on at every upgrade after the user turned it off, and an unticked task would be overridden
     by the app's first start anyway. The uninstaller removes the value in `[Code]`, but only when it points into `{app}`,
     so a portable copy's entry stays.
   - `[Run]` "Launch TaskIsland" postinstall checkbox.
   - Uninstall: `[UninstallRun]` none needed (AppMutex prompts to close). In `CurUninstallStepChanged` ask
     "Also delete your tasks and backups?" default **No**. Only on Yes delete `{localappdata}\TaskIsland`.
   - Upgrade = install over the top, same AppId. User data is untouched (it lives outside `{app}`).
   - Security-software friendly (§7.11): use the latest Inno Setup 6 release (`winget upgrade JRSoftware.InnoSetup`
     first), default LZMA2 compression, no third-party Inno plugins/DLLs, no `Exec` of cmd/PowerShell in `[Code]`,
     `VersionInfoCompany=RieSources`, `VersionInfoDescription=TaskIsland Setup`, `VersionInfoVersion` = app version.
   Build: `& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" /DAppVersion=1.0.0 installer\TaskIsland.iss`
   (or `tools\build-installer.ps1`, which reads the version from the published exe).
4b. **Code signing: ask the user** (it costs money, so it is their decision; record the answer in DEVLOG):
   - *Sign* (recommended for no SmartScreen warning and no Smart App Control block): Microsoft **Azure Trusted Signing**
     (about US$10/month, needs an Azure account and identity validation, done by the user) or a purchased OV/EV
     certificate. Then sign every `TaskIsland*.exe`/`*.dll` we build, the installer and the uninstaller (Inno `SignTool=`
     directive) with an RFC 3161 timestamp, and verify with `Get-AuthenticodeSignature` → `Valid`.
   - *Unsigned* (default if the user says no or doesn't answer): release as-is; README explains the SmartScreen
     "More info → Run anyway" step and that Smart App Control may block it.
4c. **Antivirus scan before release** (all must be clean, else do not release; fix the cause, do not just rename files):
   - Update Defender signatures, then scan:
     `& "$env:ProgramFiles\Windows Defender\MpCmdRun.exe" -SignatureUpdate` and
     `& "$env:ProgramFiles\Windows Defender\MpCmdRun.exe" -Scan -ScanType 3 -File "<path>" -DisableRemediation`
     for `publish\win-x64` and the installer `.exe`. Record the output in DEVLOG.
   - Run the installed app for 10 minutes with Defender on: no alerts in Windows Security → Protection history.
   - VirusTotal (70+ engines) is **optional and needs the user's OK first**, because uploading makes the file
     public on VirusTotal. If a vendor flags it (false positive), report it and give the user that vendor's
     false-positive submission link; for Defender: https://www.microsoft.com/wdsi/filesubmission (user submits).
5. Test: install → runs → startup value present → upgrade-install same version keeps tasks → uninstall (No) keeps data,
   removes Run value, removes Start menu entry → reinstall finds tasks. No UAC prompt at any point; no firewall prompt.
6. `docs/RELEASE_NOTES.md`, README (features, screenshots, install, SmartScreen note: installer is unsigned →
   "More info → Run anyway", uninstall, data location, build-from-source).
7. Tag and release. **The user approved this in advance (2026-11-01):** "when the project is finished, push a release
   for Windows 64-bit installer in GitHub". So no extra go-ahead is needed, **provided** all of these are true first:
   Phases 1–6 are done in DEVLOG, `dotnet build` + `dotnet test` are green, the installer passed step 5
   (install, upgrade, uninstall), the antivirus scans in step 4c are clean, and no critical/major defect is open. If any of these fails, do **not** release:
   report what is missing and stop. Phase 6b (the user's own tests: sleep, hibernate, reboot, monitors, scaling) is
   **not** required before the release; the user tests those during live use after release (decided 2026-11-01).
   Say so in the release notes ("Sleep/hibernate/multi-monitor behaviour is being verified in real use"). The release must contain the x64 installer `TaskIsland-Setup-1.0.0-x64.exe`
   (plus the portable zip):
   `git tag v1.0.0 && git push origin v1.0.0` then
   `gh release create v1.0.0 installer/Output/TaskIsland-Setup-1.0.0-x64.exe --title "TaskIsland 1.0.0" --notes-file docs/RELEASE_NOTES.md`.
   Also attach a portable zip of `publish/win-x64`.
Commit: `Prepare Windows production installer and v1.0.0 release`.

---

## 9. Known limitations (tell the user, don't hide)

- Sleep, hibernate, reboot auto-start and physical monitor changes require the user to test (TESTING.md).
- If the installer is unsigned (user's choice, Phase 7 step 4b): SmartScreen warns on first download, and on PCs with
  Smart App Control on, Windows may block it. Signing (paid) removes both. Antivirus detections are prevented by §7.11
  either way, but no developer can promise that every one of the many antivirus products will never false-flag a new
  file; if one does, it is reported to that vendor as a false positive.
- True acrylic blur isn't used on the island. Blur on a layered window blurs the whole transparent rectangle, looks
  wrong and costs GPU all the time. The island uses a near-opaque surface + hairline border instead (Settings may use Mica).
- WPF itself needs roughly 40–60 MB of memory even for an empty window. The targets in §6.7 are set close to that floor;
  the real numbers are reported in DEVLOG.
- Exclusive-mode full-screen games may draw over any topmost window. The island hides in those cases anyway.
- Reminders show while TaskIsland is running (it auto-starts, so effectively always). Reminders missed while the
  PC was off are shown as one "overdue" summary on next start.

## 10. Out of scope for v1 (possible later)

Cloud sync, recurring tasks, sub-tasks, tags across lists, per-list pills (several islands), multi-language UI,
auto-update, Microsoft Store packaging.
