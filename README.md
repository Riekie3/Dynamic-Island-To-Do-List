# TaskIsland

A Dynamic Island-style floating To-Do List for Windows 11.

A small black pill sits at the top centre of your screen showing how many tasks are left.
Click it and it stretches down into a full To-Do list — add, edit, tick and prioritise tasks,
then it folds back into the pill. Runs quietly from the system tray, starts with Windows and
survives sleep / wake. Organise tasks into lists such as Office Work, Personal or per-project. All data stays on your PC (SQLite in `%LocalAppData%\TaskIsland`).

![TaskIsland design preview](docs/design-reference-1.webp)

## Download (test release 1.0.0-rc.2)

From the [Releases page](https://github.com/Riekie3/Dynamic-Island-To-Do-List/releases):

- **Installer** `TaskIsland-Setup-1.0.0-rc.2-x64.exe` (recommended): installs for your user only, no administrator rights.
  Uninstall from Windows Settings > Apps.
- **Portable** `TaskIsland-1.0.0-rc.2-win-x64-portable.zip`: unzip somewhere permanent and run `TaskIsland.exe`.

No .NET install needed. The files are not code-signed, so Windows SmartScreen may warn: click **More info**, then
**Run anyway**. See [docs/RELEASE_NOTES.md](docs/RELEASE_NOTES.md) for what's new and what is known to be rough.

## Status

Phases 1-7 are done up to the test release 1.0.0-rc.2 (database, island interface, to-do features, Windows integration,
Settings, testing and optimisation, portable build and installer). Still open before 1.0.0: the user's own
sleep/hibernate/multi-monitor tests, the code-signing decision and a licence.
See [docs/PLAN.md](docs/PLAN.md) (build guide), [docs/TESTING.md](docs/TESTING.md) and [docs/DEVLOG.md](docs/DEVLOG.md).

## Tech

C# · .NET 10 · WPF (MVVM) · SQLite · Windows 11 x64

## Build (developers)

Needs the .NET 10 SDK. `nuget.config` points at nuget.org only.

```
dotnet build TaskIsland.sln
dotnet test
powershell -File tools\publish.ps1           # portable folder + zip in the publish folder
powershell -File tools\build-installer.ps1   # installer in installer\Output (needs Inno Setup 6)
```

---

Developed by **RieSources** · rie-sources.com
