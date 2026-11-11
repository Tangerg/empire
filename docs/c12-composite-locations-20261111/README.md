# C12 composite objective navigation

Base: `4bd006db`. Real 960×640 chapter-entry play, choosing joint control with
Cain, showed three mechanisms spread across the rivers. The third was outside the
initial viewport. The objective was plain text with no locator; inspecting one
mechanism only disclosed the generic structure type, HP and allegiance, not the
other objective locations.

## Minimal fix

The neutralizeComposite handler now publishes remaining part positions through
the existing objective location protocol. CompositeRules owns the shared part
predicate for both progress and location: missing, dead or disabled parts are
neutralized. Merely capturing a still-live enabled part does not complete it.
The private query never exposes mutable structure objects. Its public coordinate
projection is detached, read-only and deduplicated; progress retains the existing
multiplicity of repeated part references. Objective threshold overrides remain
independent of the composite's own aggregate threshold.

No level definitions, campaign choices, allegiance rules, damage, commands or
victory conditions change. This follows the existing public structure visibility
boundary; it does not implement discovery secrecy for hidden structures.

## Actual GUI verification

At 960×640 the new task locator fits without truncation. Clicking it moves the
camera so all three mechanisms and their gold target-cell outlines are visible.
The formerly offscreen third mechanism can then be selected; its dossier reports
180/180 HP, enemy allegiance and bridge terrain. Progress remains 0/2 and the
ready count remains 10: navigation/inspection did not issue a battle command.
Game closed afterward. This is isolated joint-control chapter entry, not a
continuous campaign or C12 victory; no private saves or screenshot binaries ship.

## Contracts and scope

Core tests cover exact C12 cells, composite movement, immutable prior projections,
state/save invariance, disabled/dead/missing/captured parts, completion clearing,
threshold overrides and duplicate reference semantics. A negative control removes
the location projection and must fail the exact C12 target assertion.
CameraIntegration includes C12 in real keyboard locator, busy/rebind, repeat,
selection/state preservation and hidden-objective/ancestor checks.

Verification passes: build (0 warnings/errors), formatting, source metadata,
76 catalog boundary assertions, 17 core groups including all 16 shipped native
AI/replay/save runs, CameraIntegration 396 assertions and objective/outcome 297.
All 16 action/event/receipt SHA256 traces, final state digests and action counts
match the C11 baseline exactly. The empty-location negative control fails at the
intended C12 assertion; the restored implementation passes the complete core run.
Existing native mouse-exit warnings remain, with no ERROR/FAIL in positive runs.
The full native aggregate and remote CI were not run.
