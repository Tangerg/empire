# Native scenario differential checks

Run from the repository root with the already provisioned toolchain and Node dependencies:

```sh
./tools/godot/scenario-checks/run.sh
./tools/godot/scenario-checks/run.sh --verify-guard
```

The script executes the original TypeScript rules to generate a fresh oracle, then builds the current C# core and this runner in an isolated temporary directory. It does not install software or change production files. Generated vectors, diagnostic diffs, and binaries remain in the printed temporary directory, not in the repository. TypeScript is a test oracle only, never a native runtime dependency.

## What is compared

- 466 direct rule cases: complete state, ordered events, the state digest at every emission, query results, and expected typed failures. The supported effect and condition ID lists must also exactly match the live source registries: all 34 built-in effects and all 16 conditions.
- 29 real-engine dispatch steps: scenario trigger ordering and chaining, repeated chance conditions and trigger ledgers, map/status/resource effects, round-clock overlay expiry, and transaction rollback after a later effect fails.
- Both engines save and reload through JSON text before the first action and after every integration action. The reloaded state and digest must match before continuation.
- Seven repeated lone-surrogate stream vectors, plus persisted trigger sequences for lone high and low surrogates in stream IDs, trigger IDs, variable keys/values, and signal text. These are valid JavaScript inputs and ordinary required parity checks, not skipped or expected failures. They stay distinct from the replacement character U+FFFD. C# text boundaries and comparisons use `EcmaJson`.
- Three native public-API checks reject NaN and positive/negative infinity as status durations before mutation. These are separate from the JSON oracle, whose numeric boundary is finite binary64.

The direct cases include authored level payloads and deliberately constructed boundary cases: status duration and stacking; resource limits, unlimited and absent accounts; objective transitions and unchanged-event behavior; owner/team changes; all registered overlays; terrain/elevation/cliff/cover edits; structure repairs; forced movement on every tiling; teleport occupancy and reading order; marker filters and revival; withdrawal, surrender, passenger departure, and commander consequences; default/custom chance streams, percentage thresholds, and nested short-circuit evaluation.

Direct rule failures compare any partial mutation and already-emitted events because those functions are not transaction boundaries. The separate dispatch failure case verifies the actual engine restores state, RNG counters, trigger ledgers, readiness, statuses, and events. Successful integration fixtures assert success in the TypeScript generator, so an early battle conclusion cannot accidentally turn an intended success test into matching failures.

## Guard verification

`--verify-guard` copies the terrain module to the temporary directory and deliberately delays overlay expiry by one round. A second build must fail the specific source event comparison for `overlay round-clock 1`. An unrelated crash or compilation failure does not count as successful guard verification. Production files are never mutated.

## Verified checkpoint

2026-10-20: all 466 direct comparisons, 29 persisted integration steps, and three non-finite API guards passed, with zero build warnings or errors. The deliberately delayed overlay-expiry mutant was rejected by the intended event assertion. This is focused source differential coverage, not an exhaustive proof for every combination of scenario effects or a campaign victory claim.

## Native health contract (2026-10-21)

The original `restore stops at full reading-order zone and resets snapshots`
vector returns a corpse at zero HP without a revival policy. It remains tested
as an explicit native `DomainInvariantException`, with no events and unchanged
whole state. A new positive-snapshot version preserves exact source coverage of
reading-order placement, a full zone, and return resets. The suite now generates
467 vectors: 466 source comparisons and one native health-invariant assertion.
Explicit `reviveMarkers` still restores corpses through its canonical health-ratio
policy; valid withdrawn snapshots retain their exact HP.
