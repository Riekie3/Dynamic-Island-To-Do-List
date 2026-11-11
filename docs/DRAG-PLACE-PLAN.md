# Drag-to-place island (plan, 2026-11-11)

Idea from comparing with the Layzee app (its notch can be dragged to 7 places). For TaskIsland we keep what makes it an
island: it always hangs from the **top edge** and the panel always opens **downwards**. Dragging only changes *where along
the top* and *on which monitor*. Nothing is copied from Layzee; this is our own design.

## What the user sees

1. Press on the collapsed pill and move more than 6 px: the pill lifts (small scale-up and shadow, same feel as the list
   drag in 1.0.2) and follows the pointer along the top of the screen. Up and down movement is ignored.
2. While dragging, faint snap marks show at **left**, **centre** and **right** of the monitor the pointer is on. Within
   24 px of a mark the pill snaps to it; anywhere else it stays where it is dropped.
3. Drag onto another monitor: the pill moves there (and takes that monitor's size and scaling).
4. Release: the pill settles with the normal fast ease. The place is saved and used after a restart, after sleep, and when
   a monitor is plugged in or out.
5. **Esc** while dragging puts it back where it was. A plain click (no movement) still opens the panel, exactly as today.
6. Settings > Appearance > Position: the slider follows the dragged place, its range becomes the real width of the
   monitor, and a new **Reset to centre** button puts it back. The tray menu also gets **Reset island position**
   (rescue if it is ever hard to reach).

Not draggable: the open panel, the reminder pill while it is alerting (a click there must open the task), and the
"sliver" state while hidden for a full-screen app.

## Rules it must keep (PLAN.md)

- Section 6.2 / 6.7: no per-frame layout. While dragging, only the **window position** changes (`SetWindowPos` with
  `SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER`), at most once per mouse move. The lift is a render transform.
- Section 7.2: the panel (420 DIP) must stay on screen when opened, so the drop point is clamped with the existing
  `IslandPlacement.Compute` rule. Snapping to *left* or *right* therefore means "as far as the open panel still fits".
- Section 7.11: no new hooks, no global mouse hook. The drag uses normal WPF mouse capture on our own window.
- Never resize the window during the drag. Crossing to a monitor with a different scale resizes once, at the moment the
  pointer crosses, not every frame.

## How it is built

### Core (pure, unit-tested) — `ScreenRules.cs`
- `IslandPlacement.OffsetFromPointer(monitor, scale, pointerX, panelWidthDip)` → the `HorizontalOffset` (DIP from the
  monitor centre) for a pointer position, clamped like `Compute`.
- `IslandPlacement.Snap(offsetDip, monitorWidthDip, panelWidthDip, snapDip = 24)` → the offset after snapping to left,
  centre or right, plus which mark it snapped to (for the marks and the log).
- `IslandPlacement.MonitorAt(monitors, pointX, pointY)` → the monitor under the pointer (falls back to the nearest).
- `AppSettings`: widen `HorizontalOffset` from ±400 to ±4000 (the real limit is `Compute`'s clamp, which already keeps
  the panel on screen). Old values stay valid. No new setting needed: monitor = existing `MonitorDeviceName`.

### App
- `IslandWindow`: on `Host.PreviewMouseLeftButtonDown` remember the start point; on move past 6 px (system drag
  threshold, `SystemParameters.MinimumHorizontalDragDistance`) call `CaptureMouse`, stop the peek timer and hover
  reactions, set `_placing = true`. Mouse up after a drag marks the click as handled so `OnHostClick` does not expand.
- New `IslandPositioner.PlaceAt(hwnd, monitor, offsetDip)`: same maths as `Apply`, used live during the drag.
- On release: write `HorizontalOffset` and `MonitorDeviceName` once (not on every move), log
  `island moved to <monitor> offset <n> (snapped: centre)`.
- Snap marks: three tiny dots drawn in a separate, click-through overlay window only while dragging, closed on release.
  (Alternative if the overlay costs too much: no marks, just a light haptic-style "tick" scale when it snaps.)
- Face: the mascot looks "Excited" while being carried and "Happy" when dropped (existing expressions, no new art).
- Settings slider range bound to the current monitor's half-width; **Reset to centre** button; tray menu item.

## Tests

- Core unit tests: offset from pointer (100 %, 125 %, 150 % scaling, negative monitor coordinates for a monitor left of
  the primary), clamp at both edges, snap within/outside 24 px, monitor under a point and the gap-between-monitors case,
  old ±400 values still load.
- UI test `tools/ui-tests/dragplace.ps1` (temporary data folder, real mouse via `SendInput` like `chipdrag.ps1`):
  drag left → saved offset negative and window moved; drag back near centre → snaps to 0; plain click still opens the
  panel; Esc during drag restores; restart keeps the place; Reset to centre works; drag while the panel is open does
  nothing.
- Measurements: CPU during a 3-second drag (expect a short burst, then 0), memory unchanged after 20 drags, idle CPU 0.
- Manual (owner): two monitors with different scaling, taskbar on top, sleep/wake after moving it.

## Phases

1. **D1 Core**: the three pure functions, the wider offset range, unit tests. Small.
2. **D2 Drag**: drag along the top on one monitor, click still opens, Esc cancels, place saved, slider follows.
   UI test. This alone is releasable.
3. **D3 Polish**: snap marks, monitor hopping, face reactions, Reset to centre (Settings + tray), measurements.
4. **Release 1.0.6** with README/RELEASE_NOTES/DEVLOG, Defender scan, installer test (owner's go for the GitHub release).

## Open questions for the owner

- Snap marks (small overlay window while dragging) or no marks, just the snap feel?
- Should dropping on another monitor be allowed, or keep the monitor choice in Settings only?
