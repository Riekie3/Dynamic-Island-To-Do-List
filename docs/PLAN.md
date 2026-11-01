# TaskIsland — Implementation Plan

Dynamic Island-style floating To-Do List for Windows 11.
Repo: https://github.com/Riekie3/Dynamic-Island-To-Do-List
Design reference: [design-reference.webp](design-reference.webp)

---

## 1. Environment audit (2026-11-01)

| Item | Status |
|---|---|
| GitHub repo | Exists, **public**, empty → initialised cleanly |
| `gh` auth | Logged in as Riekie3 → pushes work |
| .NET | Runtime only — **.NET 10 SDK must be installed** (`winget install Microsoft.DotNet.SDK.10`) |
| Visual Studio | Not installed — not needed, `dotnet` CLI is enough |
| Installer tool | None — **Inno Setup 6** proposed (`winget install JRSoftware.InnoSetup`) |

## 2. Architecture

```
TaskIsland/
├── src/
│   ├── TaskIsland.Core/      Task model, Priority enum, sorting/filter rules, ITaskRepository,
│   │                         ISettingsStore, JSON import/export (no WPF, fully unit-testable)
│   ├── TaskIsland.Data/      SQLite via Microsoft.Data.Sqlite (no EF — lighter, faster start),
│   │                         schema migrations, backup/restore (SQLite online backup API)
│   ├── TaskIsland.Windows/   P/Invoke + OS services: window styles, monitors/DPI, power &
│   │                         session events, startup registry, global hotkey, full-screen detect
│   └── TaskIsland.App/       WPF + MVVM (CommunityToolkit.Mvvm), IslandWindow, SettingsWindow,
│                             tray icon, styles/animations, DI host (Microsoft.Extensions.Hosting)
├── tests/
│   ├── TaskIsland.Core.Tests/   sorting, filters, counters, JSON validation
│   └── TaskIsland.Data.Tests/   CRUD, persistence, backup/restore against temp DB files
├── installer/TaskIsland.iss    Inno Setup script
├── docs/                       PLAN.md, DEVLOG.md, design reference
└── assets/                     icon (.ico), tray icons
```

Dependencies kept to a minimum: `Microsoft.Data.Sqlite`, `CommunityToolkit.Mvvm`,
`Microsoft.Extensions.DependencyInjection`, xUnit for tests. Tray icon via
`System.Windows.Forms.NotifyIcon` (built into .NET, no extra package).

### Key technical decisions

- **One window, animated content — not window resizing.** The island window is a fixed-size
  transparent, borderless window (panel's max size). Only the inner `Border` animates its
  Width/Height/CornerRadius, so the pill visibly "stretches" into the panel with no flicker.
  Transparent areas are made click-through (`WS_EX_TRANSPARENT` toggled on hit-test /
  window region shrunk to the pill when collapsed) so the invisible area never blocks clicks
  on the desktop below.
- **Focus:** collapsed = `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` (no focus stealing, no
  taskbar/Alt-Tab entry). On expand the NOACTIVATE flag is removed and the window is
  activated so the Quick Add box gets keyboard input; re-applied on collapse.
- **Topmost** re-asserted after resume, display change and when Explorer restarts
  (`TaskbarCreated` message — also re-adds the tray icon).
- **Startup:** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value with `--startup`
  flag (no admin). Uninstaller removes it.
- **Single instance:** named Mutex + named pipe; a second launch tells the running one to
  expand, then exits.
- **Sleep / wake / lock:** `SystemEvents.PowerModeChanged`, `SessionSwitch`,
  `DisplaySettingsChanged` + `WM_DPICHANGED`, `WM_DISPLAYCHANGE` — all event-driven, no polling.
  On resume: re-enumerate monitors → fall back to primary if chosen monitor is gone →
  reposition → re-assert topmost → verify tray icon.
- **DPI:** Per-Monitor V2 manifest; positions computed in physical pixels via
  `MonitorFromPoint`/`GetMonitorInfo`/`GetDpiForMonitor`.
- **Full-screen detection:** `SHQueryUserNotificationState` (covers presentations / D3D
  full-screen) + foreground-window-covers-monitor check, triggered by a
  `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` hook — event-driven.
- **Data safety:** SQLite WAL mode, every change committed immediately, automatic daily
  backup (keep last 7) in `%LocalAppData%\TaskIsland\Backups`. Delete = soft-delete with
  5-second **Undo** toast, then hard delete.
- **Logs:** simple rolling text file in `%LocalAppData%\TaskIsland\Logs` (no logging library).

### Data model

`Tasks`: Id, Title, Description, Priority (0 High / 1 Medium / 2 Low), IsCompleted,
CreatedAt, UpdatedAt, CompletedAt, DueDate, Category, SortOrder, DeletedAt
`Settings`: SettingKey, SettingValue
`SchemaVersion` table for forward migrations (upgrades preserve user data).

Sorting: incomplete first → Priority → SortOrder → CreatedAt. Completed tasks grouped
below (or hidden by filter), newest-completed first.

## 3. Phases & milestones

Each phase ends with: build ✔, tests ✔, commit, push, short report in `docs/DEVLOG.md`.

| # | Phase | Deliverable | What I can verify myself |
|---|---|---|---|
| 1 | Setup | Solution + 4 projects + 2 test projects build; DB schema; README/.gitignore | `dotnet build`, `dotnet test` |
| 2 | Island UI | Collapsed pill + expand/collapse animation matching the reference image | Run app, screenshot via computer-use |
| 3 | To-Do | Add/edit/delete+undo/tick/untick/priority/sort/filters/counters, SQLite persistence | Unit + integration tests, manual UI run |
| 4 | Windows integration | Tray, startup, single instance, sleep/wake, monitors/DPI, full-screen hide, hotkey | Tray, startup entry, single-instance, lock/unlock, DPI. **Sleep/hibernate/monitor unplug need you** (I can't sleep the laptop and keep running) |
| 5 | Settings & data | Settings window, light/dark theme, appearance, JSON import/export, backup/restore | Tests + UI run |
| 6 | Test & optimise | Full checklist pass, memory/CPU check, defect fixes | Checklist in `docs/TESTING.md`, items needing you marked clearly |
| 7 | Release | Self-contained x64 publish, Inno Setup installer, icon, version info, GitHub Release v1.0.0 | Install/uninstall on this PC |

## 4. Honest limitations to note now

- Sleep, hibernate, real reboot-to-login and physical monitor plug/unplug are tested by
  **you** with a checklist; I'll test everything I can trigger programmatically
  (lock/unlock, display-change simulation, process restart, DPI change).
- An unsigned installer will show a SmartScreen "unknown publisher" warning. Removing it
  needs a code-signing certificate (paid) — out of scope unless you have one.
- Exclusive full-screen games that draw over everything can't be "beaten" by a topmost
  window anyway; we hide in those cases, which is the desired behaviour.
