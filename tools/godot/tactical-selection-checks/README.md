# Tactical selection controller checks

Run `bash tools/godot/tactical-selection-checks/run.sh` for the live original
TypeScript selection/keyboard oracle and native controller contracts. The frozen
native gate runs the same program against `godot/tests/oracle/tactical-selection.json.gz`.
Run `python3 tools/godot/tactical-selection-checks/mutants.py` after sourcing
`tools/godot/env.sh` for isolated behavioral negative controls.

The native cell-eligibility contract adds immutable `InteractiveCells` from the
revision-bound adapter. Regression checks reject outside/out-of-bounds clicks,
leave rather than inspect excluded hover cells, skip excluded keyboard candidates
and actor-cycle entries, and clear a cursor excluded by a settled revision. A genuinely new revision
invalidates both the old rendered hit and its inspection; same-revision refusal
preserves the current hit. Ability target metadata admission rejects unknown enum
values, and default ability kinds and singleton command coordinates are asserted.
The synthetic hidden-unit fixture explicitly projects its original 20-by-2 board;
ordinary source traces retain their 6-by-2 board. Isolated mutation checks remove
each click/hover/direction guard and must fail behaviorally.

The explicit native pointer contract uses `TacticalHit` (Ground, visible Unit id,
or Object), independently of canonical target subjects. New direct typed cases
cover concealed units under Ground/Object, same-cell hover changes, unit and
structure target modes, true cell-target abilities, keyboard Enter, and deployment
swaps. The historical differential input is annotated by `Fixture.SourceHit` only
because it records semantic coordinate clicks without renderer coverage. That
helper is not used by the new typed regressions and is not mouse acceptance.

Without the historical TypeScript dependencies, run the checked-in oracle:

```sh
source tools/godot/env.sh
gzip -dc godot/tests/oracle/tactical-selection.json.gz > /tmp/empire-selection-oracle.json
dotnet run --project tools/godot/tactical-selection-checks -- /tmp/empire-selection-oracle.json
python3 tools/godot/tactical-selection-checks/mutants.py
```

The mutation runner defaults to this unchanged frozen oracle, or accepts an
explicit decompressed oracle path. Singleton HUD command execution and explicit
keyboard navigation retain their existing canonical semantics; neither is a
substitute for native pointer/coverage tests.

Keyboard direction, cycle and roster events explicitly declare `CursorFocused`
when establishing canonical focus. The continuous historical keyboard input trace
now has explicit native cursor expectations: cycle focuses the chosen actor, and
later navigation starts there rather than at the obsolete pointer. Native camera
checks cover focus ownership across reveal and Enter without moving the pointer.

Destination clicks now always preview rather than committing a sole self-targeted
order. The source action comparison inserts and verifies explicit confirmation,
then compares the same canonical action. Invalid target clicks retain the complete
preview; the historical cancel-on-miss trace is resumed only after a separately
asserted refusal and explicit Back operations. New regression assertions cover
repeated self-clicks, explicit wait, unreachable destination feedback, and cycle
followed by Enter/directional input. The frozen oracle is not rewritten.
