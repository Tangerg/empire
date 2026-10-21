# Native career and formation differential checks

Run from the repository root:

```sh
bash tools/godot/progression-checks/run.sh
```

The runner invokes the original TypeScript implementations to generate temporary
fixtures, then compares the native implementation's values, ordered events,
domain-invariant diagnostics and complete state snapshots. Native illegal-action
codes/details and ordered option reasons are declared in `native-refusals.json`;
source snapshots remain generated and unmodified. The output prints the temporary diagnostic directory.

Current coverage: 175 cases, with 172 exact source comparisons and three explicitly native invariant rejections, including 19 individual engine dispatches, 14 raw
handlers and three multi-action sequences. The sequences compare every step and
include rejected-action rollback, formation changes that do not consume the
actor, career changes that do, and switching back after turn handoffs.

- Career root traversal (including cycles and missing predecessors), unlocked
  paths, rank/mastery gates, stable sorting and ability deduplication.
- Aggregated resource costs, affordability, exact payment events, health ratios,
  compatible weapon/account retention and incompatible weapon removal.
- Mastery and rank thresholds, fractional rounding, learned abilities and event
  ordering, including already-mastered and unknown-career cases.
- Formation menu order, missing definitions, hypothetical validation without
  mutation, allies versus enemies, all three grids and actor/phase restrictions.
- Derived movement fields and combat modifier breakdowns after career/formation
  changes, using the canonical consumers rather than alternate test mechanics.

The shipped Ancient Empires soldier does not declare formations. A valid custom
content variant explicitly enables them to test successful changes; a separate
case verifies the original type rejects an undeclared formation. Other custom
variants test resource costs and compatible equipment. Deliberately malformed
career graphs and health maxima are passed directly to the source query/command
functions after catalog setup. These cases characterize those functions, not
acceptance by the independent content-install or save-validation gates.

Production files are under `godot/Empire.Core/Progression`. Career progress
reuses `CombatEffects.AwardCareerProgress`; movement/combat formation activation
uses the same `FormationRules.InEffect` predicate as the new query/action API.

## Intentional health contract change

The source's `health ratio 0`, `health ratio 200`, and `health invariant direct mutation order` cases are retained, not skipped. Native career changes reject a dead/over-maximum current unit or invalid target maximum with `DomainInvariantException` before resource costs, events, career/type changes, or any state mutation. Normal fractional health, career cost order and all valid progression cases still compare to source exactly. `UnitHealth` owns the health-preserving type transition; career policy chooses the type and preflights that same invariant before announcing costs.

## Native refusal contract

`native-refusals.json` declares the intentional native API break for the existing
source cases: illegal orders and refusal-bearing queries expose exact semantic
`code`/`details`, not localized prose. The runner checks the complete ordered case
inventory and each affected query/action slot before substituting only those
expectation fields on detached copies. Missing or stale entries fail. Acceptance,
reason order, invariant failures, state, events and hashes keep their existing
assertions. Original source fixtures and generators are unchanged; regeneration
that changes these boundaries requires a reviewed update to this independent
expectation data. No native rule or presentation formatter produces expectations.
