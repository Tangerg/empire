# Damage and departure parity checks

Run from the repository root:

```sh
./tools/godot/damage-checks/run.sh
```

The runner loads `tools/godot/env.sh`, generates reference results by executing the original TypeScript rules, and compiles the C# damage implementation with its direct dependencies into a temporary directory. It never builds or changes the main Empire.Core output. No external NuGet packages are needed. Generated JSON and compiled output are removed on exit.

## Verified result

2026-10-20: isolated .NET 8 build passed with 0 warnings and 0 errors. All 33 cases matched the TypeScript oracle across four comparisons: emitted events, intermediate event-time state observations, returned outcomes, and the complete final state.

Coverage:

- Damage clipping, JavaScript rounding, negative/zero damage and missing targets.
- Nonlethal damage at 1, 8 and maximum HP.
- Morale clamping, resilience, routing and damage-induced routs.
- Death, capture reset and independent corpse snapshots.
- Commander defeat, shaken status, subordinate routing and cast cancellation.
- Fatal and nonfatal transport passenger markers and event ordering.
- Surrender with and without a recipient; withdrawal with and without a corpse.
- Ally-defeat morale geometry on square4, square8 and hex grids.
- StatusLifecycleContext.damage parity, including departed targets.

Inputs are the checked-in content packs and initial Grayflag state. The generated cases run against the original TypeScript engine, not hard-coded expected values. This focused suite does not claim a full campaign or main-project integration pass.
