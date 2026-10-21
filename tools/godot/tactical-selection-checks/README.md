# Tactical selection controller checks

Run `bash tools/godot/tactical-selection-checks/run.sh` for the live original
TypeScript selection/keyboard oracle and native controller contracts. The frozen
native gate runs the same program against `godot/tests/oracle/tactical-selection.json.gz`.
Run `python3 tools/godot/tactical-selection-checks/mutants.py` after sourcing
`tools/godot/env.sh` for isolated behavioral negative controls.

The native cell-eligibility contract adds immutable `InteractiveCells` from the
revision-bound adapter. Regression checks reject outside/out-of-bounds clicks,
leave rather than inspect excluded hover cells, skip excluded keyboard candidates
and actor-cycle entries, and clear a cursor excluded by a settled revision.
The synthetic hidden-unit fixture explicitly projects its original 20-by-2 board;
ordinary source traces retain their 6-by-2 board. Isolated mutation checks remove
each click/hover/direction guard and must fail behaviorally.
