# Keep camera gestures from restoring tactical hover — 2026-11-03

## Confirmed failure

The native screen remembered pointer coordinates so stationary-pointer wheel
scrolling and zoom could inspect the subject under the new camera transform.
Those automatic repicks did not distinguish a stationary pointer from an active
captured gesture. `BattlefieldGestures` deliberately suppresses hover updates
throughout a held gesture and clears the cursor when dragging starts, but the
screen immediately recreated that cursor after applying the pan transform.

A real C01 session reproduced the mismatch before repair: hold a mouse button,
move two pixels (below the six-pixel drag threshold), then Ctrl-wheel zoom. The
camera changed the HUD projection despite the held-gesture hover policy. The
original-code regression failed at the held sub-threshold reprojection check.

The same ownership error also affected same-revision screen refresh: applying a
snapshot repicked the remembered pointer without consulting active capture.

## Repair and interaction scope

`BattleScreen` now checks the existing gesture owner's `ActivePointerId` before
both automatic repicks, in `ApplyCamera` and `SynchronizeSnapshot`. There is no
second gesture flag, timer, hit-test owner, or change to canonical battle state.

- A held press does not acquire new terrain or target hover from pan, zoom,
  resize, or snapshot refresh.
- Starting a drag still clears the existing cursor/forecast. An armed attack
  remains armed; it is not canceled merely because the player pans the map.
- A completed drag still cannot become a click, including capture release over
  the real overlapping HUD tray or outside the viewport.
- Ordinary pointer movement after release resumes inspection. Unheld stationary
  pointer pan/zoom still repicks the new rendered subject as before.
- Explicit keyboard focus remains owned by the tactical controller and is not
  blocked by a global hover guard.

The original click-versus-drag threshold, touch routing, source capture/release
policy, camera geometry, and all authored town scenes/artwork are unchanged.
This does not redefine sub-threshold releases across floating HUD boundaries.

## Verification

The existing `CameraIntegrationChecks` scene includes the complete held-pointer
flow for left and middle buttons: real actor pixel, sub-threshold wheel zoom,
drag, same-revision refresh, continued pan, resize, release, and fresh inspection.
It also uses the existing detached adjacent-enemy fixture to verify that armed
attack forecasts remain cleared during drag and refresh, and that HUD/outside
release cannot issue combat. This fixture is not a played save or altered level.

Final-code verification:

- Build: zero warnings and errors.
- Full native camera integration: 309 assertions passed, including stationary
  pointer re-picking, the held-gesture scenarios, forecast handoff, cancellation,
  busy presentation, and rebind/unmount coverage.
- Full native BattleScreen real-session integration passed.
- Tactical HUD: 379 assertions passed.
- All three native processes exited zero with no error, leaked-resource,
  segmentation-fault, or unhandled-exception diagnostics. Existing source-readiness
  and mouse-notification warnings remain visible.
- Changed-source whitespace, `git diff --check`, and source metadata passed
  (154 unique script UIDs; 14 positive/negative metadata cases).
- Independent read-only review found no remaining blockers.

Tests use fresh `XDG_DATA_HOME` directories and a 600-second process budget;
player saves are not read or written. The original-code failure is negative
regression evidence, not a passing run. The pure TypeScript camera oracle was
not run because this worktree has no installed `vite-node` runner; its helpers
and source contracts were not changed.

Current GUI/window-manager validation remains unavailable in this environment.
These checks exercise native scene layout, Godot events, real screen adapters,
and real sessions headlessly, not a desktop hardware interaction. The existing
repository-wide formatting failures in 184 unrelated files remain outside this
slice; targeted checks must not be described as a full aggregate pass.
