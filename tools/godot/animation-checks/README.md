# Source-backed native animation checks

Run `bash tools/godot/animation-checks/run.sh` from the repository. It builds the
native application, imports its existing resource files headlessly and executes
`godot/Scenes/AnimationChecks.tscn`. It does not install software, launch a desktop,
run runtime TypeScript, mutate user saves, dispatch test actions to a live game,
or publish anything.

The 177 assertions cover the original exported nine FX strips, source actor and
effect frame contracts, deterministic timing, cancellation and disposal,
defensive ownership of transition inputs, lethal attacker/removed structure
lookup semantics, explicit pre-action visibility, and deliberate silence for
source-unhandled events. A controllable clock advances native nodes without
sleep-dependent expectations; real PNG/AtlasTexture resources are still loaded.

`godot/docs/ACTION-PRESENTATION.md` identifies the original source owners,
durations, event fields, asset IDs and scope boundaries. These are native tests
derived from those contracts, not claims of full frame-by-frame browser parity
or desktop visual proof. Application-level once-only revision consumption and
input/load/rebind handling are covered separately by BattleScreenChecks.
