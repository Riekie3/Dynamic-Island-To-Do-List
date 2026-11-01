# TaskIsland — Development Log

Live tracker (copy-ready prompts per phase): https://claude.ai/artifact/RAqH7ZmUthw76PcD6sfUK1 (source: docs/tracker.html)

Newest entry at the bottom. Each phase appends one entry using the template below.

## Phase status

| Phase | Status | Commit |
|---|---|---|
| 0 Planning | ✅ done 2026-11-01 | plan + checklist |
| 1 Skeleton & data layer | ⏳ | |
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
```

---

### Phase 0 — Planning (2026-11-01, Opus 5.5)
**Implemented:** Repo initialised (was empty). Detailed plan `docs/PLAN.md`, test checklist `docs/TESTING.md`,
this log, README, .gitignore, design reference image. Installed .NET SDK 10.0.403 and Inno Setup 6 (per-user) via winget.
**Decisions:** due-date reminders, drag reorder, peek on hover, 2 s slide-out of ticked tasks, daily backup toggle, English only,
grouped lists (Office Work / Personal / projects, with an "All lists" view). The spec's Category field is implemented as Lists.
**Tests performed:** none (no code yet).
**Pushed:** yes
