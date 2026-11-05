# Battle camera activation after committed window layout

## Reproduction

Baseline: `6ef0201040df93a26ac4a37fe68a1c3ae543066d`.

With official Godot 4.6.3 .NET and normal application processing, start a new
campaign in a 640×480 window, advance the seven story beats, choose
`rush-north-hill`, and start the opening battle. The application expands the
window to 960×640. The first battlefield origin is `(-333.7631, -252.5418)` at
zoom 1.25. Return to camp through its confirmation and enter the same battle:
the origin becomes `(-173.7631, -172.5418)` at the same final viewport and zoom.
The first entry is therefore displaced left by 160 and up by 80 pixels.

Both measurements persist after five real process frames. A second probe also
captures them synchronously when the actual StartBattle button callback returns;
window, viewport and MapArea already report 960×640 at that point. All instances
render 19 units with zero rendering issues. Starting at 960×640 or 1280×720 does
not reproduce the first-entry/reentry discrepancy.

These are native headless scene and geometry observations, not screenshot,
desktop-input or GPU visual acceptance.

## Cause and ownership

`GrayFlagGame.PrepareMounted` correctly mounts the candidate view hidden and
processing-disabled so a preparation failure cannot alter the live window.
However, `BattleScreen.BindSession` previously initialized its camera using that
hidden view's old viewport. `BattlefieldCamera.Resize` intentionally chooses the
opening focus only on the first usable layout; later resizes preserve scroll.
`GrayFlagGame.Activate` then exposed the view before committing the battle window
minimum. The camera correctly preserved a scroll position chosen too early.

The fix keeps ownership and formulas intact:

- The application commits the incoming view and window minimum before showing it,
  then enables processing.
- The screen defers only its camera's first layout while hidden. Its existing
  resize path also runs synchronously on visibility changes.
- The camera remains the sole owner of focus, scroll, zoom and clamping. Existing
  cameras still resize while hidden and showing them again does not reset the
  player's chosen framing.

No deferred callback or second readiness state is introduced. Failed hidden
preparation retains the previous window and frees the candidate's owned nodes.
There are no changes to authored maps, artwork, battle rules, saves or replay.

This uses the engine's existing visibility/layout lifecycle, documented in
[CanvasItem](https://docs.godotengine.org/en/4.6/classes/class_canvasitem.html)
and [Window](https://docs.godotengine.org/en/4.6/classes/class_window.html).

## Regression coverage

`CampaignNavigationChecks` checks the actual application activation callback,
settled layout and camp reentry against the authoritative camera model using the
real board projection and authored opening focus. Cases include a small window,
an inherited 1000×500 host minimum, and an already-large 1280×720 window that
needs no expansion. Existing failed-preparation cleanup, host-minimum,
resize/gameplay stability, unmount/remount and free checks remain.

`CameraIntegrationChecks` pans to the far boundary, hides the screen and enlarges
the viewport. The world must clamp to the new edge before Show, preserve zoom and
framing on Show, and remain pannable afterwards.

Verification status will be completed after focused red/green and aggregate runs.
