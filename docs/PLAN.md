# TaskIsland — Detailed Implementation Plan

> **Read this whole file before writing any code.** It is the single source of truth for
> building TaskIsland. Execute phases in order. Each phase lists its goal, exact steps, files,
> acceptance criteria, verification commands and the commit message to use.
> The original product brief is the user's spec. Where this plan is more specific, follow the plan.

- Repo: https://github.com/Riekie3/Dynamic-Island-To-Do-List (public, branch `main`)
- Local folder: `D:\Rie\Claude\TaskIsland`
- Design reference: [design-reference.webp](design-reference.webp) (open and study it before Phase 2)
- Test checklist: [TESTING.md](TESTING.md) · Dev log: [DEVLOG.md](DEVLOG.md)

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
8. Commit messages end with the `Co-Authored-By` attribution line your harness specifies.

**Suggested model per phase:** Opus for Phase 2 (window interop + animation) and Phase 4
(Windows integration). Sonnet is fine for Phases 1, 3, 5, 6 and 7.

### Paste-ready kickoff prompt

```
Project: TaskIsland in D:\Rie\Claude\TaskIsland (repo Riekie3/Dynamic-Island-To-Do-List).
Read docs/PLAN.md fully, then docs/DEVLOG.md to see progress. Execute the next
incomplete phase exactly as specified in PLAN.md, follow section 0 rules, then stop
and report. Do not skip verification steps.
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
│       ├── Controls/IslandHost.cs         # morph control (§6.2)
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
Task<int> ClearCompletedAsync();                       // soft-delete all completed (undo-able)
Task MarkReminderShownAsync(long id);
Task ReplaceAllAsync(IEnumerable<TaskItem> items);     // used by JSON import "Replace", one transaction
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
| `TopMargin` | `8` | Appearance | 0–40 px |
| `PanelOpacity` | `0.94` | Appearance | 0.70–1.00 |
| `AnimationsEnabled` | `true` | Appearance | false → durations become 0 |
| `Density` | `Comfortable` | Appearance | Comfortable (row 52 px) / Compact (row 38 px) |
| `HideInFullScreen` | `true` | Behaviour | |
| `MonitorDeviceName` | `""` | Behaviour | empty = primary. Missing monitor → primary |
| `AutoCollapseSeconds` | `0` | Behaviour | 0 = off. Else collapse after N s with mouse outside and no focus in the text box |
| `RestoreAfterWake` | `true` | Behaviour | |
| `PeekOnHover` | `true` | Behaviour | |
| `RemindersEnabled` | `true` | Behaviour | |
| `ToggleHotkey` | `Ctrl+Alt+T` | Behaviour | capture box. Shows "in use by another app" if RegisterHotKey fails |
| `DailyBackupEnabled` | `true` | Data | |
| `CompletedFadeOut` | `true` | Behaviour | the 2-second slide-out |
| `DefaultListId` | Personal | Lists | where quick-add goes when *All lists* is selected |
| `PillCountScope` | `AllLists` | Lists | `AllLists` or `SelectedList`: what the pill's "3 tasks" counts |
| `FirstRunCompleted`, `LastBackupAt`, `CurrentFilter`, `SelectedListId` (0 = All) | internal | — | |

Settings apply **live** (no restart) and save immediately.

---

## 6. UI / visual specification

### 6.1 Visual tokens (from the reference image)

| Token | Dark | Light |
|---|---|---|
| Pill background | `#0B0D12` (opaque) | `#0B0D12` (pill stays black in both themes, like a real island) |
| Panel background | `#141A24` at `PanelOpacity` + top-to-bottom gradient to `#10151E` | `#F7F9FC` at `PanelOpacity` |
| Panel border | 1 px `#FFFFFF` @ 8% | 1 px `#000000` @ 8% |
| Primary text | `#F2F5FA` | `#121722` |
| Secondary text | `#8C97A8` | `#5B6575` |
| Accent (tick, add, links) | `#1E90FF` → hover `#3AA0FF` | same |
| Input background | `#FFFFFF` @ 6% | `#000000` @ 5% |
| High chip | bg `#3A1218`, fg `#FF5A6A`, icon ↑ | bg `#FFE3E6`, fg `#C8102E` |
| Medium chip | bg `#3A2A08`, fg `#FFB020`, icon ↑ | bg `#FFF1D6`, fg `#9A6200` |
| Low chip | bg `#0F2E1C`, fg `#3DDC84`, icon ✓-circle | bg `#DDF7E8`, fg `#11803F` |
| Shadow | `DropShadowEffect` blur 24, depth 4, opacity 0.45 | opacity 0.18 |
| Font | `Segoe UI Variable Text` (fallback `Segoe UI`). Title 18 semibold, rows 14.5, footer 13 | |
| Icons | `Segoe Fluent Icons` glyphs (fallback `Segoe MDL2 Assets`): Accept `E73E`, ChevronDown `E70D`, Edit `E70F`, Delete `E74D`, Settings `E713`, Add `E710`, Forward `E72A`, More `E712`, Cancel `E711`, BarChart `E9D2`, Clock `E823` | |

Priority always shows **text label + colour + icon** (accessibility).

### 6.2 The island morph (core technique)

- `IslandWindow`: `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent`,
  `ShowInTaskbar=False`, `Topmost=True`, `ResizeMode=NoResize`, `ShowActivated=False`.
  Fixed size = max expanded size + shadow margin: **470 × 620 DIP**. Never resize the window during animation.
- In a layered (AllowsTransparency) window, fully transparent pixels are **automatically click-through**.
  So the empty area under a collapsed pill does not block the desktop. Keep the root `Grid`
  `Background="{x:Null}"`, not `Transparent`. (`Transparent` brushes are hit-testable inside WPF but still
  pass through at the OS level when alpha is 0. Use `{x:Null}` anyway to be safe.) Verify by clicking a desktop
  icon/window directly below the pill area during Phase 2.
- `IslandHost` (custom `ContentControl`) has a `double Morph` DependencyProperty (0 = pill, 1 = panel).
  On change it lerps: `Width` (NotchWidth → 420), `Height` (46 → measured panel height, max 550),
  `CornerRadius` (23 → 26). CornerRadius isn't animatable, so it is computed in the callback.
  Top edge stays fixed. The pill grows **downward and outward**, centred.
- Expand: `DoubleAnimation` Morph 0→1, **240 ms**, `CubicEase EaseOut` (try a subtle `BackEase Amplitude 0.25`
  and keep it if it looks good). Collapse: 1→0, **190 ms**, `CubicEase EaseIn`.
- Content crossfade: pill content opacity = `1 - clamp(Morph/0.35)`. Panel content opacity =
  `clamp((Morph-0.4)/0.6)` with a 6 px upward slide-in (TranslateTransform). This prevents both
  layouts showing at once.
- Panel height: measure panel content `DesiredSize` at width 420 before animating. Re-animate height
  (150 ms) when the list length changes while expanded. Inner list scrolls once 550 is reached.
- Hover on pill: background lightens to `#161A22`, chevron nudges 2 px down (120 ms).
- Peek on hover (§2): after 400 ms hover, width animates to `min(NotchWidth+140, 360)` and text
  crossfades to the top task title (ellipsis). Revert on leave.
- Reminder pulse: accent-coloured outer glow ring scales 1→1.15 and fades out, 2 cycles, 600 ms each.
- `AnimationsEnabled=false` → all durations 0.

### 6.3 Collapsed pill content

`[✓ blue circle]  3 tasks  [⌄]` (singular "1 task"). When outstanding = 0: `✓ All Done`.
Count is the outstanding count. If any task is overdue, show a small red dot on the tick icon.

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
4. **Task list** (`ItemsControl` in `ScrollViewer`, virtualization not needed below 1000 items):
   row = checkbox · title (strikethrough + secondary colour when done) · due label (if any, red if overdue)
   · priority chip · ✎ · 🗑. In *All lists* each row also shows a 6 px list-colour dot before the title and the list
   name in small secondary text under it. Priority group separators are not shown, order alone conveys it.
   Row hover background `#FFFFFF` @ 4%. Drag handle = the whole row (start drag after 6 px move), **only in the
   Active filter of a single list** (not in All lists, where the order is computed).
5. **Undo bar** (appears above footer for 5 s after delete/clear completed): "Task deleted · Undo".
6. **Footer**: bar-chart glyph · "3 total • 1 completed • 33%" (for the selected list scope) · segmented filter
   **Active | Completed | All** (replaces "View All" from the mockup). Last filter remembered.
7. **Empty states**: Active empty → "🎉 All done in <list>. Add something new above." Completed empty → "Nothing completed yet."

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
developer "Riekie3", GitHub link (opens browser only when clicked), data folder path, "No data leaves this PC".

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
  `scale = dpi/96`, `wPx = 470*scale`, `x = rc.Left + (rc.Width - wPx)/2 + HorizontalOffset*scale`,
  `y = rc.Top + TopMargin*scale - shadowMarginPx`. Clamp x so the 420-wide panel stays inside `rcMonitor`.
  Apply with `SetWindowPos(HWND_TOPMOST, x, y, wPx, hPx, SWP_NOACTIVATE)`.
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
connect to named pipe `TaskIsland-7F3A9C2E-<user SID>`, send `EXPAND`, exit with code 0. The first
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

### 7.10 Logging & errors

`FileLogger` → `Logs\taskisland-yyyyMMdd.log`, keep 14 days, thread-safe append, levels Info/Warn/Error.
Hook `DispatcherUnhandledException` (log, show a small non-fatal error toast, mark handled where safe),
`AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`.
DB failure on startup (locked/corrupt) → message box with "Open data folder" / "Restore from backup" / "Exit".
Never silently drop data.

---

## 8. Phases

Each phase: **Goal → Steps → Acceptance → Verify → Commit.** After each phase append to DEVLOG.md
using the template there, update TESTING.md, push, and report to the user.

### Phase 1 — Solution skeleton, models, data layer

**Goal:** a buildable solution with a fully tested data layer, no UI beyond a blank window.

Steps:
1. `git pull`. Create `global.json` (sdk 10.0.403, `rollForward: latestFeature`) and `Directory.Build.props`
   (`Version 0.1.0`, `Nullable enable`, `ImplicitUsings enable`, `LangVersion latest`, `Company Riekie3`,
   `Product TaskIsland`, `Copyright © 2026 Riekie3`).
2. `dotnet new sln -n TaskIsland`. Create projects per §3.1 (`classlib` for Core/Data/Windows, `wpf` for App,
   `xunit` for tests), add to sln, set TFMs, add references and packages (§3.2).
3. Core: `Priority` enum (High=0, Medium=1, Low=2), `TaskItem` (all columns from §4.1), `TodoList`, `TaskFilter`,
   `AppSettings` with defaults (§5), interfaces, `TaskSorter`, `TaskStats`, `IClock`, `ReminderScheduler`, `JsonTransfer`.
4. Data: `AppPaths` (creates `Data`, `Backups`, `Logs`; constructor overload taking a root for tests), `Database`
   (connection string `Data Source=…;Cache=Shared;Pooling=True`, WAL on open, `busy_timeout=3000`), `Migrations`,
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

**Goal:** the pill and panel look like the reference and morph smoothly. Uses static sample tasks bound
through the real ViewModel (data wiring comes in Phase 3).

Steps:
1. Themes: `Dark.xaml`/`Light.xaml` with all tokens from §6.1 as `SolidColorBrush`/`Color` resources (same keys).
   `Controls.xaml` styles: round checkbox (blue fill + white tick, 140 ms scale-in), icon buttons (hover/press),
   input box, priority chip, segmented filter, primary/secondary buttons, scrollbar (thin, auto-hide look).
   Use `DynamicResource` everywhere so the theme can swap live.
2. `IslandHost` morph control (§6.2) + `IslandWindow` (§6.2/§7.1 styles applied in `SourceInitialized`).
3. Pill layout (§6.3), panel layout (§6.4) incl. the list switcher row, with sample rows covering all 3 priorities,
   one done, one overdue, two lists.
4. `IslandPositioner` minimal version: primary monitor top-centre with DPI scale (full logic in Phase 4).
5. Click pill → expand. ✕ / Esc / click outside (`Deactivated`) → collapse. Focus behaviour per §7.1.
6. Hover effects and peek-on-hover (§6.2).
7. Run the app and compare with `docs/design-reference.webp`. Take screenshots (computer-use) collapsed,
   hover-peek and expanded, save them to `docs/screenshots/phase2-*.png` and iterate until close.

Acceptance: pill at top centre. Smooth 240/190 ms morph with no window flicker or jump. Desktop under the
transparent area is clickable. Hovering the collapsed pill (incl. peek) never takes focus. Typing in Notepad
continues uninterrupted while the mouse moves over the pill. Expanded panel accepts typing. Not shown in Alt-Tab or taskbar.
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
7. Drag to reorder within the same priority group in Active view. Show an insertion line. Dropping
   outside the group is not allowed (cursor `No`). Persist via `ReorderAsync`.
8. Filters, empty states, counters, panel height re-animation when list changes.
9. Lists: switcher row, `ListEditPopup`, chip context menu, delete-list dialog (move vs delete, auto-backup first),
   keyboard shortcuts (§6.4), quick-add list chip in *All lists*, List field in inline edit.
10. Remove sample data. First run creates **no** sample tasks (clean start), only the 2 starter lists. Show the empty state.

Acceptance: every item under "Task management", "Lists" + "Data: Save tasks locally / Preserve after app restart" in
TESTING.md passes manually. Kill the process (`taskkill /f`) mid-use → relaunch → data intact.
Commit: `Implement task CRUD, priorities, sorting, filters and persistence`.

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

Acceptance: change every setting → effect visible immediately → restart app → setting kept.
Export → clear → import → identical tasks. Backup → delete tasks → restore → tasks back.
Commit: `Add settings window, themes, JSON import/export and backup/restore`.

### Phase 6 — Testing & optimisation

1. Run the full TESTING.md checklist. Fix every critical/major defect found. Re-run affected items.
2. Performance targets (measure, record in DEVLOG): idle CPU ≈ 0% (check over 60 s in Task Manager / `Get-Process`
   CPU delta), working set < 80 MB idle collapsed, cold start to visible pill < 1.5 s, animations without
   dropped frames on this laptop. If AllowsTransparency rendering is heavy, reduce shadow blur radius and avoid
   `BitmapEffect`s other than one `DropShadowEffect` on the outer border.
3. Confirm no network activity: `Get-NetTCPConnection -OwningProcess (Get-Process TaskIsland).Id` → empty.
4. Code review pass: dispose patterns, event unsubscription, no `async void` leaks, nullable warnings.
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
   - `AppId` fixed GUID (generate once, never change), `AppName TaskIsland`, `AppVersion` from define,
     `PrivilegesRequired=lowest`, `DefaultDirName={localappdata}\Programs\TaskIsland`, `ArchitecturesAllowed=x64compatible`,
     `ArchitecturesInstallIn64BitMode=x64compatible`, `AppMutex=Local\TaskIsland-7F3A9C2E-SingleInstance`,
     `CloseApplications=yes`, `SetupIconFile`, `UninstallDisplayIcon`, `WizardStyle=modern`, `OutputBaseFilename=TaskIsland-Setup-1.0.0-x64`.
   - Files: `publish\win-x64\*` recursesubdirs.
   - Start-menu shortcut. Optional desktop shortcut task (unchecked).
   - `[Registry]` HKCU Run value `TaskIsland` = `"{app}\TaskIsland.exe" --startup`, `Flags: uninsdeletevalue`,
     created only if task "Start with Windows" (checked by default).
   - `[Run]` "Launch TaskIsland" postinstall checkbox.
   - Uninstall: `[UninstallRun]` none needed (AppMutex prompts to close). In `CurUninstallStepChanged` ask
     "Also delete your tasks and backups?" default **No**. Only on Yes delete `{localappdata}\TaskIsland`.
   - Upgrade = install over the top, same AppId. User data is untouched (it lives outside `{app}`).
   Build: `& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" /DAppVersion=1.0.0 installer\TaskIsland.iss`.
5. Test: install → runs → startup value present → upgrade-install same version keeps tasks → uninstall (No) keeps data,
   removes Run value, removes Start menu entry → reinstall finds tasks.
6. `docs/RELEASE_NOTES.md`, README (features, screenshots, install, SmartScreen note: installer is unsigned →
   "More info → Run anyway", uninstall, data location, build-from-source).
7. Tag and release (ask the user for a final go-ahead before publishing the release, because it's public):
   `git tag v1.0.0 && git push origin v1.0.0` then
   `gh release create v1.0.0 installer/Output/TaskIsland-Setup-1.0.0-x64.exe --title "TaskIsland 1.0.0" --notes-file docs/RELEASE_NOTES.md`.
   Also attach a portable zip of `publish/win-x64`.
Commit: `Prepare Windows production installer and v1.0.0 release`.

---

## 9. Known limitations (tell the user, don't hide)

- Sleep, hibernate, reboot auto-start and physical monitor changes require the user to test (TESTING.md).
- Installer is unsigned → SmartScreen warning. A code-signing certificate would remove it (paid, out of scope).
- True acrylic blur isn't used. Blur on a layered window blurs the whole transparent rectangle, which looks wrong.
  The glass look is imitated with a semi-opaque gradient + hairline border.
- Exclusive-mode full-screen games may draw over any topmost window. The island hides in those cases anyway.
- Reminders show while TaskIsland is running (it auto-starts, so effectively always). Reminders missed while the
  PC was off are shown as one "overdue" summary on next start.

## 10. Out of scope for v1 (possible later)

Cloud sync, recurring tasks, sub-tasks, tags across lists, per-list pills (several islands), multi-language UI,
auto-update, Microsoft Store packaging.
