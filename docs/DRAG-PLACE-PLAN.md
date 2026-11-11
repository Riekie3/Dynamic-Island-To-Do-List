# Drag-to-place island (2026-11-11)

Idea from comparing with the Layzee app. The owner asked for **only the drag itself** plus a face reaction; the earlier
extras (snap marks, moving to another monitor by dragging, Reset buttons, extra measurements) were dropped.

## What the user sees

- Press on the collapsed pill and move it sideways: once it has moved 6 px it follows the pointer **along the top edge**.
  Up and down movement is ignored; it stays an island and the panel still opens downwards.
- While it is carried the face looks **Excited**; when it is dropped it shows **Happy**. The pill also dips a little when
  picked up and bounces when dropped (with or without the face; skipped under reduced motion).
- It stops where the open panel (420 DIP) still fits on the screen, so it can never be lost off the edge.
- The place is saved at once and used after a restart, sleep or a monitor change. The Settings slider (Appearance >
  Position, now −2000…+2000) shows the same value and can still be used instead.
- A plain click still opens the panel. Clicking the face right after a drop (while Happy shows) makes it laugh, like any
  face that is showing a reaction; the next click opens the panel.
- Not draggable: the open panel, a reminder pill that is alerting, the hidden sliver, or while hidden for full screen.
- If the mouse is taken away mid-drag (Alt+Tab, lock screen), the pill goes back to where it was.

## How it is built

- `IslandPlacement.OffsetForCentre` (Core, pure): pointer position → `HorizontalOffset` in DIPs, clamped like
  `IslandPlacement.Compute`, so the saved value is exactly where the pill is drawn. 8 unit tests in `DragPlaceTests`.
- `IslandWindow` (drag section): press captures the mouse; past 6 px the drag starts; each move only calls `Reposition`
  (window position, same size: no layout, no resize, PLAN section 6.2); release writes one setting
  (`HorizontalOffset`) through `ISettingsStore`. The dip and bounce animate the existing slide transform only.
- `AppSettings.HorizontalOffset` range widened from ±400 to ±2000 (old values still load).
- UI test: `tools/ui-tests/dragplace.ps1` (real mouse, temporary data folder, 12 checks).
