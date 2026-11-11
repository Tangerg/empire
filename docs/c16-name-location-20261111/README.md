# C16 true-name objective locations

Base: `44d1dbd6`.

## Change

The existing `score-true_names` objective now declares its three canonical
`targetZones`: `truth-a` (6,3), `truth-b` (16,8), `truth-c` (8,13).
The short instruction is “到三处真名区完成行动”. Existing scenario rules require
an owner-1 unit in the zone at `afterAction`, and grant each zone once. This is
not an invented interaction button or a requirement to visit every decorative prop.
No trigger, zone coordinates, score threshold, combat rule or UI kind switch changed.
Existing score projection, objective marker and safe-camera framing are reused.
All three locations remain marked while the objective is active, not only unread
locations; the locator naturally disappears when the score completes.

## Focused checks

The existing `EscortChecks` suite now checks the formal C16 declaration and exact
three coordinates, readonly state/save projection, lawful move-and-wait response,
leaving/revisiting without another grant, and per-action complete event/state
equivalence against a level with only the location metadata removed. The isolated
visit fixture changes only the protagonist's starting location to an empty legal
neighbor outside the complete interaction zone, not triggers or score rules.

`CameraIntegrationChecks` includes formal C16 at 960×640 using native locator
activation, the actual projected surface vertices, safe-margin checks, retained
selection/state, refresh focus and hidden-objective boundaries.

Core build succeeded. Focused check passed. Removing only C16's `targetZones`
produced exit 1 at the exact formal-declaration assertion; the unchanged positive
content was restored and the focused check passed again. See `core-negative.log`
and `core-restored.log`.

```sh
source tools/godot/env.sh
dotnet godot/Empire.Core.Tests/bin/Debug/net8.0/Empire.Core.Tests.dll godot/tests/oracle --only 'escort mission and AI intent'
dotnet godot/Empire.Core.Tests/bin/Debug/net8.0/Empire.Core.Tests.dll godot/tests/oracle --only 'shipped native full-level replay/save'
```

## Recorded fixture audit and replay

The recorded native JSON fixtures contain no C16 battle-state witness. The only
C16 reference in `godot/tests/native` is narrative data, not a recorded battle
hash. No stored hash expectation was edited. A full sixteen-level replay/save
run completed separately; its built-in metadata equivalence comparison
checks complete canonical state and events after each action for authored score
locations. The completed trajectory comparison appears below.

## Real-window before/after

Used a private C16 entry fixture validated by the production campaign loader,
with empty battle history and the normal “承认新人格” choice. This is not a carried
campaign completion or a user save. Initial local fixture setup incorrectly put
a manual schema-4 save in the schema-1 autosave slot; the UI correctly rejected it.
Loading the valid manual slot through F9 and proceeding normally produced a valid
staging autosave. That setup error is not counted as a game defect.

At 960×640 before the change, the objective read “让三个真名得到回应·0/3”, had no
locate affordance and clicking it produced no camera/selection response. The
briefing asked for three names without saying where or how to respond.

After importing the updated translation and rebuilding, normal Continue → staging
→ battle displayed “定位·到三处真名区完成行动·0/3”. Clicking it framed all three gold
cell markers together, outside the HUD obstruction, at the canonical truth areas.
Repeated clicks and Escape did not issue orders: turn 1/30, funds 430, command
points 1/6, 13 ready actors and objective 0/3 remained. The game window was closed
and desktop released after verification. No private save or screenshot is added.

## Completed core replay result

The focused `escort mission and AI intent` check and the separate complete
sixteen-level `shipped native full-level replay/save` check both passed (exit 0).
This is **not** a run of all 17 core groups. Each shipped battle also completed its
exact save/replay round trip. Comparison against the committed C15 core log shows
C01–C15 action/event/receipt traces and final state hashes exactly unchanged.
C16 still executes 378 actions; only its metadata-inclusive trace/final hash
changes. The existing dual-track check proves per-action full events and the
complete canonical state (excluding only the targetZones declaration) are equal.
No expected hashes or gameplay rules were changed. See `all-replay.log` and
`replay-comparison.txt` for the exact old/new C16 trace and state hashes.

## Final native checks and limits

- Translation import and native build passed, zero build warnings/errors.
- Complete CameraIntegrationChecks passed: 447 assertions.
- Complete BattleScreenChecks passed: real-session integration.
- Native whitespace formatting and source metadata checks passed.

No all-native aggregate or remote CI pass is claimed. This was a focused C16
location/interaction improvement, not full manual campaign completion. The broader
AnimationChecks suite was not repeated for this data/text-only slice.
