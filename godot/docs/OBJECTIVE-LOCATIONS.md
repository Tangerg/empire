# Public objective locations

Active, public spatial objectives now offer a `定位` action in the task paper.
Clicking or keyboard-activating it centers the existing battlefield camera and
outlines the exact goal cells. It does not select or move a unit, cancel its
pending command, spend an action, or change battle state.

## Ownership and lifecycle

- `ObjectiveHandler.TargetCells` is optional. Each objective kind owns its spatial
  meaning. Escort and control share their existing canonical zone parser; the HUD
  does not inspect objective kinds or infer targets from labels.
- `ObjectiveProgress` takes a detached, immutable copy only for active objectives.
  `GrayFlagSession` exposes it only through visible rows with active ancestry.
  Hidden objectives and hidden ancestors never expose location intents or cells.
- The screen stores only the focused objective ID. Every refresh re-resolves cells
  from the current public projection; commands, presentation locks, rebind and
  unmount clear the marker. Stale intents cannot cross the session stamp boundary.
- `BoardOverlay` draws the outer edges of the existing authored cell triangles.
  The marker does not expose units or terrain behind fog and adds no second
  geometry, camera, objective state, save field, or gameplay rule.
- The read-only button remains reusable after activation and preserves keyboard
  focus across equivalent HUD projections. Target arrays compare by contents.

## Verified examples and limits

Native 960×640 debug-entry fixtures were inspected with actual mouse input:
C02 locates the six canonical east-bank cells (x18–20, y23–24), not an arbitrary
village or the whole east bank. The selected civilian and pending stationary
command remain intact. C07 independently locates its granary and militia camp.
These fixtures are not claims of continuous campaign completion.

`EscortChecks` verifies canonical and changed zones, inactive/terminal states,
reactivation, immutable snapshots and a custom handler. Its negative control
returns an empty zone and fails the six-cell C02 assertion before restoration.
`CameraIntegrationChecks` verifies real native Enter activation, safe-area focus,
selection/state/revision preservation, repeated activation, equivalent-refresh
focus identity, hidden ancestors/leaves, busy rejection and stale rebind callbacks.

Non-spatial objectives remain text. This does not add minimaps, automatic camera
following, campaign balance changes, new art, or route planning. Existing source
catalog `runtimeReady=false` diagnostics remain explicitly unresolved.
