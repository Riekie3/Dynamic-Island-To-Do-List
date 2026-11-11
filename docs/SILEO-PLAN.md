# Plan: Sileo-style notifications for TaskIsland

Status: **planning only, nothing built yet** (2026-11-11). Source studied: github.com/hiaaryan/sileo at `b70fd82`
(2026-10-14), npm package `sileo` 0.1.6, live demo https://sileo.aaryan.design. Licence: **MIT**.

## 1. What Sileo is

An "opinionated, physics-based toast component for React" (React 18+, depends on the `motion` animation library).

What makes it look the way it does:

| Part | How Sileo does it |
|---|---|
| **Shape** | A small rounded **pill** (the "tab": icon + title, 40 px high) and a wider rounded **body** under it (350 px wide). Both are drawn in one SVG and passed through a **gooey filter** (Gaussian blur, then an alpha threshold `0 0 0 20 -10`), so where the pill meets the body the corner flows in with a smooth concave curve, like a folder tab. |
| **Motion** | A spring: bounce 0.25, 600 ms (`SPRING` in `constants.ts`). The body's height grows from 0, the pill's width follows its text. CSS uses the same curve as a `linear()` easing. |
| **Timing** | Shows as a pill, **auto-expands after 150 ms**, auto-collapses after 4 s, disappears after 6 s. Hover expands it; leaving collapses it. Swipe up or down by 30 px dismisses it. |
| **Header swap** | When a toast changes (for example loading → success), the old title blurs and fades out while the new one comes in, in place. The body collapses first, then the new content is shown. |
| **States** | `success` (green), `error` (red), `warning` (amber), `info` (blue), `action` (indigo), `loading` (grey, spinning icon). The icon sits in a round badge; the title takes the state colour. |
| **Extras** | `sileo.promise(...)` (loading → success/error), an action button in the body, 6 screen positions, custom fill colour and roundness. |

The look on the demo: a dark pill with a green tick and "Changes Saved" in green, and a dark body that melts down out
of it with the description text.

## 2. The catch: `npm install sileo` cannot go into TaskIsland

TaskIsland is a **WPF / .NET 10** desktop app. There is no browser and no React inside it, so the npm package cannot be
used as-is. Three ways to get the look:

| Option | Verdict |
|---|---|
| **A. Rebuild the look natively in WPF** (same shape, spring, timings, colours) | **Recommended.** No new dependencies, keeps memory and CPU within PLAN.md section 6.7, no new processes for security software to question (section 7.11). |
| B. Host the real React Sileo inside a WebView2 | Rejected. Adds a Chromium process and roughly 80-150 MB of memory to an app that is already over its memory budget; transparency and click-through on the island's layered window are unreliable; slower start. |
| C. Use the npm package in one of your **web** projects instead (RieSources site, RieLoyal, Pallet Tracking, ...) | Works as documented (`npm i sileo`, add `<Toaster />`, call `sileo.success({...})`). Tell me which project if that is what you meant; it is a small job and needs a separate plan. |

The rest of this plan is option A.

## 3. How to rebuild it in WPF

**Shape without a gooey filter.** WPF has no SVG filters. A pixel-shader effect could fake blur + threshold, but it
costs GPU time on every frame of a layered window and needs a compiled HLSL shader. Instead, draw the result directly:
one `StreamGeometry` outline = pill + body + a **concave fillet** (quarter-circle curving outward) where the pill's side
meets the body's top edge. That is the shape the gooey filter produces, at no filter cost. The fillet radius follows the
body height while it opens (0 when closed, full radius when open), which gives the same "melting" look.

**Spring.** A small `SpringEase : EasingFunctionBase` (damped spring, bounce 0.25, 600 ms) used through the existing
`Anim` helpers. With Windows animations off (`MotionPolicy.IsReduced`), the spring is replaced by an instant change,
same as every other animation in the app.

**Render-only, as in section 6.2.** The outline is rebuilt from two numbers (pill width, body height) on each frame;
text and buttons are laid out once at their final size and clipped by the same geometry, exactly like `IslandHost` does
for the panel morph. No `Width`/`Height` animation, no re-layout per frame.

**Header swap.** Two stacked header layers; the old one fades out with a short blur (a `BlurEffect` only for the
350 ms of the swap, then removed so it costs nothing at rest).

**Colours.** Sileo's six state colours converted from `oklch` to sRGB hex, added to both themes as
`ToastSuccessBrush`, `ToastErrorBrush`, ... Fill stays the island's own dark surface.

## 4. Where it goes in TaskIsland

| Place | Today | With the Sileo style |
|---|---|---|
| **Reminder alert on the notch** (section 7.9) | The notch stretches and shows a dot + text for 5 s, optional red blinking border. | The notch becomes the **tab** ("Reminder" with a bell badge in the state colour); after 150 ms a body melts down showing the task title, due time and list colour, with **Open** (and later **Snooze**) buttons. Hover keeps it open; it folds back into the notch when it ends. |
| **Undo bar** (bottom of the panel) | A plain bar that slides up. | A success-style toast: "1 task deleted" tab with an **Undo** button in the body; several deletes in a row swap the header text in place. |
| **Notice bar** (errors) | Plain bar. | Error-style toast (red badge), warning style for "can't delete the last list". |
| **Settings > Data** (backup, restore, import, export, reset) | A line of status text. | A small toast in the Settings window, using the **promise** pattern: "Backing up..." (loading) morphs into "Backup saved" (success) or "Backup failed" (error). |

## 5. Phases

Each phase ends with build + all tests green, a DEVLOG entry and a push, like the earlier phases.

1. **S1 – The control.** `SileoShape` geometry builder (pure function, unit-tested: outline at closed / half / open,
   fillets never overlap, left/centre/right tab positions), `SpringEase` (unit-tested: starts at 0, ends at 1,
   overshoot about 3-4 % for bounce 0.25), toast colours in both themes, a DEBUG-only `--demo toast` switch to show it.
   No user-visible change yet.
2. **S2 – Reminder alert.** Rebuild the notch reminder as a Sileo toast. The island window must grow to fit the body
   before the animation starts and shrink after it ends (section 6.2 rule: never resize during an animation). Keep the
   existing settings (alert width, blink → the badge pulses instead of the red border, or both). UI test: fire a
   reminder in a test data folder, check the tab text, the body text, Open opens the panel at that task, and the notch
   is back to normal afterwards.
3. **S3 – Undo and notice bars** in the panel restyled as toasts (success + Undo button, error, warning), header swap
   for repeated deletes. UI test on the existing stacked-undo flow.
4. **S4 – Settings window toasts** with the loading → success / error pattern for backup, restore, import, export,
   delete all backups and reset.
5. **S5 – Polish and release 1.1.0.** Measure: CPU while a toast animates, idle CPU and memory unchanged (section 6.7);
   check light and dark theme, 100/150/200 % scaling, Windows animations off. Screenshots for the README. Defender
   scan, installer test, release.

Rough size: S1 1 session, S2 1-2 sessions (window resizing is the fiddly part), S3 1 session, S4 half a session,
S5 1 session.

## 6. Credit

Only the design is reused (shape, timings, colours); no Sileo code is copied into the C# app. Still, credit it: add a
line to the README ("Notification style inspired by Sileo by Aaryan, MIT licence") and, if any constants are copied
verbatim, include Sileo's MIT notice in a `THIRD-PARTY-NOTICES.md`.

## 7. Decisions needed from you before S1

1. **Target:** TaskIsland (this plan), or one of your web projects (option C)?
2. **Scope:** all four places in section 4, or start with the reminder alert only?
3. **Snooze:** add a "Snooze 10 min" button to the reminder toast? That is a new feature (the reminder must fire again
   later and remember the snooze across a restart), so it would be its own small phase.
4. **Old style:** keep the current plain style as a setting ("Notification style: Classic / Sileo"), or replace it?
