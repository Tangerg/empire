# AI planning dependency contract

`./tools/godot/ai-planning-checks/run.sh` regenerates observations using the original
TypeScript `createBattleEngine`, `AiTurnContext`, and planners, then checks native
choices, threat maps, appraisals, menus, actors, and grid distance. `--frozen` reads
`godot/tests/oracle/ai-planning.json.gz` instead and needs no Node runtime.
`--verify-guard` additionally compiles nine deliberately broken AI consumers and
requires their specified behavioral failures. The options may be combined.

The 23 vectors cover an additional registered ability/evaluator, a second
weapon-using ability selecting its second profile, unusable ability and evaluator
replacement, custom movement stops, broken parent links, independent `pathTo` and
`board` replacements, replaced target/threat projections, square8/hex geometry,
custom weapon resource holders, supplied resource AI weights, recruitment account
replacement, and finished actors. Native construction in this harness is a
consumer test fixture, not an alternate engine factory or evidence of composed
kernel integration.

The source intentionally traces the returned move field locally for AI paths;
`space.pathTo` is not the AI path policy. Native `MoveField.TracePath` owns the
single parent-link traversal shared with default movement. Similarly AI geometry
uses its resolved grid, not the replaceable `space.board` query. The guards expose
both accidental extra couplings as well as discarded dependencies.

Run `tools/godot/ai-checks/run.sh` for the existing exact default choice/replay and
focused agenda checks. The planning change also updates that harness to construct
`AiTurnContext` from `planner.Planning` rather than the engine.

For an isolated overlay without its own source/data tree, set `EMPIRE_SOURCE_ROOT`
to the original repository and provide `packages`/`node_modules` links for the
live TypeScript imports. The native root always remains this script's repository.
Frozen mode still uses the original repository's existing `tools/godot/env.sh`
toolchain when `EMPIRE_SOURCE_ROOT` is set; it does not invoke TypeScript.
