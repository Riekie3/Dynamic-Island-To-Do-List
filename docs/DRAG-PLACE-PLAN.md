# Drag-to-place island (2026-11-11, free 2D placement 2026-11-25)

Idea from comparing with the Layzee app. The owner asked for **only the drag itself** plus a face reaction; the earlier
extras (snap marks, moving to another monitor by dragging, Reset buttons, extra measurements) were dropped. The first
version moved only along the top edge; the owner then asked for free placement anywhere on the screen.

## What the user sees

- Press the collapsed pill and move it: once it has moved 6 px it follows the pointer on **both axes**, anywhere on the
  monitor. It is kept fully on the screen while carried.
- Drop it within 24 DIP of an edge and it sticks to that edge:
  - top: the notch shape (flat top, rounded bottom corners);
  - bottom: the same upside down, with the panel opening **upwards**;
  - left/right: a flat side, with the panel sliding inwards so the 420 DIP panel stays on screen;
  - away from every edge: a free, round floating pill.
- A free pill opens its panel downwards unless there is more room above (then upwards). A side-attached pill keeps its
  side flush; only the panel is inset by the normal 16 DIP host margin. If the full 550 DIP panel does not fit in
  either direction (a free or side pill near the middle), it opens towards the larger side and is shortened to the room
  it has; the task list scrolls, so the panel never runs off the screen.
- While it is carried the face looks **Excited**; when it is dropped it shows **Happy**. The pill also dips a little when
  picked up and bounces when dropped (with or without the face; skipped under reduced motion).
- The place is saved at once and used after a restart, sleep or a monitor change. Two values are saved:
  `HorizontalOffset` (pill centre from the monitor centre) and `TopMargin` (pill top from the monitor top), both in
  DIPs. The attached edge is derived again from those values (1 DIP tolerance), so a 125 % screen puts the pill back on
  the same pixel. Settings > Appearance has matching sliders ("Horizontal position" −4000…+4000, "Vertical position"
  0…8000) and can still be used instead.
- A plain click still opens the panel. Clicking the face right after a drop (while Happy shows) makes it laugh, like any
  face that is showing a reaction; the next click opens the panel.
- Not draggable: the open panel, a reminder pill that is alerting, the hidden sliver, or while hidden for full screen.
- If the mouse is taken away mid-drag (Alt+Tab, lock screen), the pill goes back to where it was.
- The auto-hide sliver tucks towards the attached edge (top up, bottom down, left/right sideways); a floating pill tucks
  up. A wide reminder alert grows inwards from a side edge instead of symmetrically around the pill.

## How it is built

- `IslandPlacement` (Core, pure arithmetic, unit-tested):
  - `PillAt`: saved offsets → resting pill, clamped fully on the monitor, flush within 1 DIP of an edge;
  - `Snap`: drag position → nearest edge within 24 DIP (top/bottom/left/right, else `None`);
  - `OffsetsFor`: pill → both saved offsets;
  - `OpensUp`: panel height (max 550 DIP) vs the room above/below the pill;
  - `PanelHeightLimit`: the tallest panel that still fits between the pill and the screen edge it opens towards
    (a full 550 DIP panel from a mid-height free/side pill would otherwise run off the screen);
  - `WindowFor`: collapsed 452 × 64 around the pill; expanded 452 × 600, slid inwards so the 420-wide panel fits,
    flush to a side edge, bottom-anchored when it opens upwards. The expanded window may overhang the monitor by
    16 DIP so the panel reaches the edge;
  - `AlertWidthDip`: the reminder pill widens around the island's centre, or inwards from a side edge.
- `IslandPositioner`: `RestingPill`, `Compute`, `ShouldOpenUp`, `PanelHeightLimit`; logs the monitor, window and resting
  pill (with the edge) whenever the placement changes, which the UI test reads.
- `IslandHost`: `Edge` draws the attached side flat, `BottomAnchored` draws a bottom-edge pill / upward panel against
  the host bottom, `CompactCentre` keeps the pill on its screen spot while the expanded window slides inwards; shadow,
  pulse and content shift are edge-aware. `MeasurePanel` applies the height limit as the panel's `MaxHeight`, so the
  scrolling list shrinks with it.
- `IslandWindow` (drag section): press captures the mouse; past 6 px the drag starts; each move only calls `Reposition`
  (window position and shape, same size: no layout, no resize, PLAN section 6.2); release snaps, writes both settings
  through `ISettingsStore`, and logs the edge. The dip and bounce animate the existing slide transform only.
  Expand/collapse pick the opening direction from `ShouldOpenUp`.
- `AppSettings.HorizontalOffset` range widened to ±4000 (it was ±2000 after the first drag version) and `TopMargin` to
  0–8000 (it was 0–40); old values still load and are clamped by the parser.
- UI test: `tools/ui-tests/dragplace.ps1` (real mouse, temporary data folder, 32 checks): top drag, click vs drag, free
  middle, all four edges, bottom open-up, side panel on-screen, restart persistence. It raises its own window above a
  running installed copy and needs an idle pointer, so run it when nobody is using the mouse.
