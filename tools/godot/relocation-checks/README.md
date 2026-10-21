# Native relocation and placement ownership

```sh
bash tools/godot/relocation-checks/run.sh --verify-guard
```

This external-consumer project references the compiled Core assembly. The runner
copies only Core and check sources into an isolated temporary project, uses the
configured official SDK, and reads checked-in fixtures. No game save is accessed
or production source mutated. Every build/process is bounded; build errors,
timeouts and unrelated failures cannot count as killed mutants.

## One owner, distinct policies

`BattleRelocation` owns live-unit position, capture invalidation and removal.
`MoveUnit` validates origin/destination storage and occupancy before changing
anything, clears origin tile capture, and updates position/unit capture.
`RemoveUnit` clears origin tile capture without changing the departing object's
historical fields. `RemoveForEmbark` returns an independent snapshot with unit
capture reset while retaining the original object's fields.

Deployment swaps and placement of returning/disembarking snapshots are separate
meaningful operations, not movement flags. They reset unit capture but preserve
map capture, including capture at a snapshot's historical location. Readiness,
facing, patrol advancement, parting shots and events remain explicit caller
policy; no policy switches are added to the aggregate.

Tests cover public movement/departure operations, atomic rejection of occupied,
out-of-bounds and missing capture storage, same-cell/missing-unit no-ops, and
real canonical voluntary/forced movement, teleport, deployment, embark,
disembark, return and lethal departure. They observe event ordering and
transaction rollback, not just final coordinates.

Four compiling negative controls retain origin capture, admit invalid
destinations, retain boarding capture, and consume readiness during teleport.
Each must fail its intended semantic assertion.

Run the affected tactical, deployment, combat, damage, transport and scenario
source-oracle suites as integration coverage. Full shipped-level replay remains
a separate gate. Seeds, campaign level projections and composite structure
coordinates are not live-unit placement writers.
