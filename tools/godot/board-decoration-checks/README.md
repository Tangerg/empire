# Source board decoration parity

Run `bash tools/godot/board-decoration-checks/run.sh` from the repository. This executes the original TypeScript presentation exporters as development-only oracles, builds C#, and checks native Godot resources. The shipped game never invokes TypeScript.

Source owners:
- `packages/game-ui/src/art/board-decorations.ts`: GreyFlag ground ellipses, selected/cursor rings and cubic movement path policy.
- `packages/game-ui/src/ui/board.ts`: range ordering, attack/move exclusion, incoming countdowns, path outlines/terminal arrow, outside-cell containment.
- `packages/game-ui/src/art/board-surface.ts`: range/path below structures, markers and units; cursor above foreground/effects.
- `packages/story-candidate-01/src/presentation/candidate-01-board-style.ts`: per-element drop shadows, post-filter marker opacity and smoothing.
- `candidate-01-map-scene.ts` and `candidate-01-world-material.ts`: ordered route/edge cells, individual-cell filtering, atlas bleed and world-phase material masks.

Native owners:
- `BoardOverlay` owns one read-only session projection and feeds two draw planes. `SourceBoardDecorations` owns nine shared static drawings and at most one replaceable path texture. Detach clears draw commands before disposing textures; reattach creates a fresh resource owner.
- `TerrainView` preserves source row-major route-then-edge order. `CandidateRouteComposition` composes each material mask separately before filtering. Equal atlas index/material phase uses the existing board shadow cache; standalone nodes own their texture explicitly.
- `DynamicStateView` applies the exported .78 opacity only to authored marker art; source fallback drawings keep their own semantics.

The checks cover actual source markup, exact cubic path strings, source range order, outside cells, layer depths, mounted rebind/teardown, route bleed/filter/cache ownership and all 2,298 source connector cells across 16 fields. The latter test compares the real native terrain tree, not an alternate test projection.

The deterministic compressed source connector fixture is `godot/tests/oracle/board-connectors.json.gz` (50,689 bytes); the runner regenerates it from original source and compares decompressed bytes before native checks.

Negative controls: after sourcing `tools/godot/env.sh`, run `python3 tools/godot/board-decoration-checks/mutants.py`. Mutants compile in an isolated temporary project. Compilation failures do not count as caught behavior regressions.

These are source-contract/resource checks, not browser screenshot parity or live visual acceptance. Font rendering and GPU antialiasing remain native backend differences. Nine source fields still lack recovered authored scenery; this batch does not invent any.

## Verified working-tree checkpoint

On 2026-10-20 the native decoration suite passed 470 assertions, the actual terrain/marker suite passed 9,227 assertions, and all eight compiled behavioral mutants were rejected. The source connector export covered 2,298 ordered cells across all 16 fields. Six animation process launches and three lifecycle launches (73 assertions each) exited cleanly with no error or leak diagnostics. Screen bindings and the existing 1,537 source-lighting assertions also exited cleanly, but a concurrent scenery build changed the shared assembly before the final lighting launch. These results are therefore working-tree evidence, not a substitute for the integration owner's final immutable combined-tree gate.

A Node-free native verifier can run `Scenes/BoardDecorationChecks.tscn` without arguments and `Scenes/ConnectorDecorationChecks.tscn -- --connectors=/absolute/path/to/godot/tests/oracle/board-connectors.json.gz`. The connector reader accepts either plain JSON or the deterministic gzip fixture.
