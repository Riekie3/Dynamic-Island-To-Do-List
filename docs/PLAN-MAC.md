# TaskIsland for Mac: implementation plan

> Status: **plan only, nothing built yet** (written 2026-11-05). The Windows app and its release are not changed by this
> document. Rules from [PLAN.md](PLAN.md) section 0 still apply: one phase at a time, tests before commit, tick test items
> only when actually tested, status update and push after each phase.

**Goal:** the same TaskIsland on macOS, delivered as one **`.dmg`** (open it, drag TaskIsland to Applications). On Mac the
`.dmg` takes the place of the Windows installer and portable zip; there is no `.pkg` and no zip. Windows keeps its two
formats.

---

## 1. What carries over and what does not

Measured from the current source (v1.0.0-rc.3):

| Project | Lines | Target | On Mac |
|---|---|---|---|
| `TaskIsland.Core` | 1,541 | `net10.0` | **Reused as is** |
| `TaskIsland.Data` | 846 | `net10.0` | **Reused as is.** The SQLite package ships `osx-arm64` and `osx-x64` native files |
| Tests | 438 tests | `net10.0` | **Reused**, and also run on macOS in CI |
| `TaskIsland.App` | 9,807 | WPF, Windows only | **UI rewritten**; view models and services move to a shared project |
| `TaskIsland.Windows` | 785 | Win32 calls | **Replaced** by a Mac equivalent (section 4) |

Size of the WPF work: 65 triggers (Avalonia has no triggers; they become style selectors), 58 control templates, 210
bindings, 15 custom properties, 9 animation sites, 47 `Dispatcher` uses, 43 window-handle uses, and 17 view model or
service files that reference `System.Windows`.

## 2. Decisions

| # | Decision | Recommendation | Why |
|---|---|---|---|
| D1 | UI technology | **Avalonia 12** (C#, MIT licence; 12.1.5 on NuGet today) | Keeps Core, Data and the 438 tests. Its XAML is close to WPF. It also builds and runs on this Windows PC, so UI work does not need a Mac for every change |
| D2 | Signing | **Ad-hoc signed for test builds** (free). Choose Developer ID before a public Mac 1.0 | Same choice as Windows so far, but the cost to Mac users is higher: see section 6 |
| D3 | Chips | **Apple Silicon first**; Intel `.dmg` as a second, lightly tested file | Cross-building is cheap, but testing Intel properly needs an Intel Mac |
| D4 | Real Mac for testing | **Needed: please confirm you have one** | See below. This is the only blocker |
| D5 | Timing | Work on a `mac` branch; merge after Windows 1.0.0 is tagged | Phase M1 touches shared Windows code and should not put the 1.0.0 test period at risk |

**Rejected: native Swift/SwiftUI.** It would give the best Mac feel and the smallest download, but needs a rewrite of
Core, Data and all tests, leaves two codebases to keep in step, and cannot be built or run on this PC at all. It stays the
fallback if the M0 spike fails.

**D4, the real Mac.** GitHub's Mac machines can build the app, run the tests and make the `.dmg`. They cannot show whether
the notch placement, sleep and wake, multiple screens, Spaces or the first-open Gatekeeper steps really work. Without a
real Mac (yours, borrowed, or a rented cloud Mac for part of it), a release could only be labelled untested.

## 3. Structure

```
src/
  TaskIsland.Core            unchanged
  TaskIsland.Data            unchanged
  TaskIsland.Presentation    NEW net10.0: view models + non-visual services, moved from App, no System.Windows
  TaskIsland.App             WPF, Windows only, keeps Views, Controls and Themes
  TaskIsland.Windows         unchanged, implements the platform interfaces
  TaskIsland.Avalonia        NEW net10.0: Mac UI (views, face, island shape); also runs on Windows for development
  TaskIsland.Mac             NEW net10.0: Objective-C runtime calls, implements the platform interfaces
installer/mac/               NEW Info.plist, icon, build-dmg.sh
.github/workflows/mac.yml    NEW the repo's first CI workflow
```

Platform interfaces (in Presentation): `IIslandWindowHost`, `IScreens`, `IHotkeys`, `IStartupRegistration`, `IPowerEvents`,
`ISingleInstance`, `IShellOpen`, `IUiDispatcher`, `IMotionSettings`.

## 4. Windows feature to Mac equivalent

| Windows today | Mac | Note |
|---|---|---|
| `WindowStyler`: topmost, no activation, hidden from Alt-Tab | `NSWindow` level above normal windows, `collectionBehavior` joins all Spaces, app is `LSUIElement` | Set through the Objective-C runtime on Avalonia's window handle. **Main M0 risk** |
| `FullScreenDetector` | Leave out `fullScreenAuxiliary` so macOS hides it in full-screen Spaces | Simpler than Windows; the matching setting flips the flag |
| `MonitorService`, DPI | Avalonia `Screens` plus `NSScreen` `safeAreaInsets` | Cocoa's Y axis runs bottom-up: convert in one place only |
| `HotkeyService` (Ctrl+Alt+T) | Carbon `RegisterEventHotKey`, default Control+Option+T | Needs **no Accessibility permission**. The "no hooks" rule stays: no event tap |
| `StartupRegistration` (Run key) | `SMAppService.mainApp` (macOS 13+); fallback LaunchAgent in `~/Library/LaunchAgents` | Behaviour with an ad-hoc signature is **unverified**: test in M0, use the fallback if it fails |
| `PowerEventsService` | `NSWorkspace` will-sleep and did-wake, screen-parameter change | Feeds the existing `RecoveryCoordinator` |
| `SingleInstance` (mutex + pipe) | Locked file in the data folder + user-only Unix socket | A second launch still sends `EXPAND`. Local only, no network |
| `TrayIconService` | Avalonia `TrayIcon` (menu bar item) | Black and white template image so it follows the menu bar colour |
| `ProcessPower`, `EfficiencyMode` | Dropped | **App Nap** is the Mac concern: it can delay timers, so reminders need an activity assertion or a catch-up on wake. Tested in M5 |
| `explorer.exe` for data folder and links | `NSWorkspace` open, or the `open` command | Update the "no shell spawn" rule to name this one |
| Reduced motion, dark/light | `accessibilityDisplayShouldReduceMotion`, Avalonia theme variant | Keep `MotionPolicy` behaviour |
| `%LocalAppData%\TaskIsland` | `~/Library/Application Support/TaskIsland` | `AppPaths` should already resolve here on .NET 10. A CI test proves it |
| `TASKISLAND_ROOT` test mode | Same variable, and it turns login-item changes into no-ops | Keeps tests away from real data |

## 5. Mac-specific design points

- **The camera notch.** A pill at the very top centre of a MacBook would sit *behind* the notch and hide the face. So the
  pill goes **directly below the menu bar** on every Mac (notched or not), which also keeps it off menu items. The top
  offset setting stays. Hugging the notch can come later.
- **Fonts and icons.** Segoe UI and Segoe Fluent Icons do not exist on Mac and cannot be redistributed. Text uses the
  system font. The 13 icon glyphs in use become vector paths, so no font file is needed.
- **Keyboard.** Cmd replaces Ctrl (Cmd+Z undo, Cmd+, settings). The Mac delete key deletes a task. Macs often have no easy
  F2, so Return on a focused row also starts editing.
- **No Dock icon.** It lives in the menu bar, like the Windows tray.
- **Moving data from Windows.** Settings > Export on Windows, Import on Mac: the JSON format already exists. Add a test.
- **Privacy promise stays:** no network at run time, no Accessibility permission, data on the device only.

## 6. Packaging and Gatekeeper

Build (on a Mac runner, `installer/mac/build-dmg.sh`):

1. `dotnet publish -r osx-arm64 --self-contained` (and `osx-x64` per D3), no trimming at first, same as Windows.
2. Assemble `TaskIsland.app/Contents/{MacOS,Resources,Info.plist}`. Bundle id `com.riesources.taskisland`, `LSUIElement`
   true, `LSMinimumSystemVersion` 14.0 (.NET 10 lists macOS 14, 15 and 26; confirm Avalonia 12's minimum in M0). Icon built
   with `iconutil` from `tools/IconGen`.
3. Sign every native file, then the bundle: `codesign --force --sign -` (ad-hoc). Apple Silicon will not run unsigned code.
4. `hdiutil create -format UDZO` with the app, an `Applications` shortcut and a simple background.
5. Output `TaskIsland-<version>-macos-arm64.dmg` (and `-macos-x64.dmg`); publish SHA-256 in the release notes.

Set `AVALONIA_TELEMETRY_OPTOUT=1` in every build: Avalonia's build tools send anonymous usage data at compile time unless
told not to. The finished app is not affected, but this project should not send anything at any stage.

**What users see, by D2 choice** (macOS 15 Sequoia removed the Control-click shortcut):

| | Ad-hoc (free) | Developer ID + notarized (Apple Developer Program, about US$99 a year: confirm the current price) |
|---|---|---|
| First open | Blocked. System Settings > Privacy & Security > **Open Anyway**, confirm again, enter password | Opens after one normal question |
| Who it suits | You and testers | The public |
| Extra build work | None | Hardened runtime with `allow-jit` entitlement, `notarytool`, `stapler`, certificate as a CI secret |

This is a bigger hurdle than SmartScreen's "Run anyway" on Windows, and the README must walk through it with screenshots.

## 7. Phases

Each phase ends with build, tests, a `docs/DEVLOG.md` entry, commit and push on the `mac` branch.

### M0: Spike on a real Mac (small). **Go/no-go gate**
Throwaway Avalonia app, built by CI and downloaded as an ad-hoc `.dmg`.
Must show: a transparent pill above normal windows, on all Spaces, below the menu bar, on a notched and an external screen;
clicking it does not steal focus until the panel needs typing; no Dock icon; hotkey works without a permission prompt; login
item registers with an ad-hoc signature; menu bar icon; idle CPU and memory measured.
- **Done when:** results are written in `docs/TESTING-MAC.md`. If the window behaviour cannot be achieved, stop and revisit D1.
- Commit: `Mac M0: spike results`

### M1: Share the logic (medium). Windows only, no Mac code
Add `TaskIsland.Presentation`; move view models and non-visual services there. Replace `Brush` in view models with colour
values, `Dispatcher` with `IUiDispatcher`, and Win32 calls with the interfaces from section 3.
- **Done when:** 438 tests green, Windows UI test scripts pass, Windows behaves the same. New view model tests where a move
  made them possible.
- Commit: `Mac M1: move view models and services to Presentation`

### M2: Avalonia panel (large)
`TaskIsland.Avalonia` with a stub platform, running on Windows. Themes, panel, task rows (with title wrapping), quick add,
inline edit, calendar, lists, drag to reorder, undo, dialogs, settings window. Icons as paths. Add `--snap` screenshots.
- **Done when:** side-by-side screenshots match the WPF panel in dark and light, and every panel function works on Windows.
- Commit: `Mac M2: Avalonia panel and settings`

### M3: Island and face (large)
Port `IslandHost` (shape morph, wide reminder, blinking red border) and `FaceRig` with the 12 moods, the reaction logic and
the reduced-motion behaviour. This is the part the product is known for, so it gets its own phase.
- **Done when:** the notch, expand and fold, hover peek, reminder pill and moods match Windows in recordings, with no render
  work while idle.
- Commit: `Mac M3: island shape and face`

### M4: Mac platform layer (medium)
`TaskIsland.Mac`: everything in section 4. Add the CI workflow: tests on macOS, build, ad-hoc `.dmg` as a download.
- **Done when:** CI is green; on a real Mac the app starts from the `.dmg` and runs the full task flow.
- Commit: `Mac M4: platform layer and CI`

### M5: Reliability (medium). Needs the real Mac
Sleep and wake, lid closed, screens plugged in and out, Spaces, full-screen apps, login start, App Nap against a due
reminder, overnight soak, 500-task list, import from a Windows export.
- **Done when:** every row in `docs/TESTING-MAC.md` is tested or honestly marked open.
- Commit: `Mac M5: reliability fixes`

### M6: DMG release (small)
Finished `.dmg` look, icon, tag-triggered CI that attaches the `.dmg` to the GitHub release as a pre-release. Update the README
(Mac download, first-open steps with screenshots, uninstall: drag to Bin, data folder path), release notes and tracker.
- **Done when:** a fresh Mac user account installs from the GitHub download using only the README.
- Commit: `Mac M6: dmg release`

### M7 (optional): Developer ID and notarization (small, plus yearly fee)
Only if chosen under D2. Moves first open to one normal prompt.

## 8. Risks

| Risk | Effect | Handling |
|---|---|---|
| Avalonia's window cannot do the non-activating island behaviour | Clicks steal focus from the user's app, or the pill hides on other Spaces | M0 gate, before any real work. Native calls first; Swift is the fallback |
| Login item fails with an ad-hoc signature | Start at login does not work | LaunchAgent fallback, tested in M0 |
| Morph and face animation stutter, or idle CPU is too high | The main appeal suffers | Measured in M0 and M3; simplify before porting more |
| App Nap delays reminders | A late reminder | Tested in M5 with an activity assertion or wake catch-up |
| Memory is higher than a native app | Windows is already above target | Measure and report honestly; no number promised before M0 |
| M1 refactor breaks the Windows app | A regression in a tested product | Branch, full test scripts, merge after Windows 1.0.0 |
| GitHub Mac runner terms or labels change | CI breaks | Check in M4. Free use on public repos and the arm64 `macos-15` label come from secondary sources, not yet confirmed on GitHub's own pages |
| Upkeep | Every future UI feature is built twice (WPF and Avalonia) | Accepted for now. Moving Windows to Avalonia as well is a separate, later decision |

## 9. Not in this plan

Mac App Store (needs sandboxing and the paid programme), iCloud sync, widgets, iPhone or iPad, moving the Windows app to
Avalonia.
