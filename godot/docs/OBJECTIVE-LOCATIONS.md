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

## Authored score locations

A score objective may declare `targetZones` (zone IDs, not copied coordinates).
The score handler validates references and projects their current canonical cells;
score evaluation still reads only its variable and threshold. Scenario triggers
remain the only owners of granting score and their once-only execution ledger.
This is author-declared task geography, not inference from effect names or UI IDs.

C11 links its three grave-reading zones. The locator marks **all three locations**
until the score stage finishes; it does not claim to track unread graves. The
instruction explicitly says to arrive and complete an action. No separate read
button, new action, trigger scan or second progress counter is introduced.

## Composite neutralization locations

An active `neutralizeComposite` objective locates its current non-neutralized
parts. CompositeRules owns both the count and the part eligibility predicate:
missing, dead or disabled parts are already neutralized; a living enabled part
remains a target after ownership changes. Positions come from current structures,
so composite movement cannot leave stale objective coordinates. Objective-specific
thresholds still govern completion; the locator does not use the composite's
aggregate status as a replacement victory decision.

This follows the existing public-objective/structure visibility boundary, not a
new hidden-structure discovery system. Hidden objective ancestors and leaves do
not publish location intents. No fogged units are revealed.

C13 likewise declares its three archive zones on the score objective. The task
explains that arrival followed by a completed action reads the archive. Its three
archive locations and the existing escort exit remain separate task locators;
archive locations are not mislabeled as only unread records. Scenario still owns
the one-time grants, and score completion removes its locator naturally.


Objective focus frames the surface-triangle bounds of the published target cells.
The camera only zooms out when the set needs more space, retaining legal limits
and the current selection. Minimum zoom or edge scroll constraints may prevent
arbitrarily large sets from fitting; the camera model reports that result rather
than breaking its limits. The formal C13 archive set fits the 960×640 safe area.

C14's score objective links both canonical valve zones. Each zone has two valid
interaction cells; the locator shows all four, not an inferred prop center. The
task explains arrival plus action completion. Valve triggers remain once-only;
locations are not an unclosed-only checklist and disappear at score completion.

Public active failOn warnings can locate a condition's explicitly projected
spatial target. Scenario condition handlers own this optional pure read model;
structure conditions expose the current position of their living named
structure. Missing/dead structures return none, while disabled or captured live
structures remain locatable. Unit selectors, random conditions and composed
conditions do not infer positions. Projection never evaluates the condition.
C15 uses this to identify the protected mother root without an order or fogged
unit disclosure; hidden/inactive objective ancestry still suppresses the row.
