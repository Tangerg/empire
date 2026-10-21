# Actual engine composition checks

This suite exercises `BattleEngineFactory.Create`, not a kernel-only simulation or an engine wrapper.
`GrayFlagEngine(JsonArray)` delegates to the same composition operation. The five source plugin IDs
install all 24 typed capabilities. Their declared provisions, requirements and versions are compared
with the original TypeScript recipe. The installed plugin manifest, never a fixed replacement map,
is carried into saves and replay records.

The root seals all 19 resolved mutable registries and passes the exact resolved instances into their
canonical consumers. Scalar progression, randomness and save-reader policies keep their source
contracts. `BattleLifecycleRules` requires content, turn order, scenarios, victory, combat effects,
resources and status lifecycle explicitly. No lifecycle constructor reconstructs defaults.

## Verification

Run with the existing official .NET installation, without installing software:

```sh
EMPIRE_TOOLCHAIN_ROOT=/path/to/existing/empire-tools bash tools/godot/engine-composition-checks/run.sh --frozen
EMPIRE_TOOLCHAIN_ROOT=/path/to/existing/empire-tools bash tools/godot/engine-composition-checks/run.sh --verify-guard
```

The live runner type-checks and regenerates all three source oracles, compares their frozen fixtures,
then executes the native engine. `--frozen` executes only .NET and Python; it needs neither Node nor
TypeScript. `--verify-guard` deliberately breaks 42 independently compiled native consumers/sealing
boundaries. Live guard mode additionally mutates four original TypeScript root/consumer paths.
Failures to compile a mutant are invalid controls, not successful rejection evidence.

`oracle.ts` covers named custom engines with versions 1, 2147483648 and 1e21; resource replacement
captured by a later ability installer; custom action/menu/command/AI execution; exact states and ordered
events; save/load/restored dispatch/replay; failed-command rollback; anonymous persistence rejection;
full-state scenario randomness and its rollback; spatial targeting replacement; and actual combat
modifier/hit-effect execution. It also checks open capability ordering and catalog identity.

`all-capabilities-oracle.ts` covers the ten further source slots together through a real named plugin:
status hooks, rank progression, turn order, reactions, departure listeners, reference checks, save
reader, scenario conditions/effects and objectives. Three integrated cases cover successful play,
status failure and departure failure. They compare complete states/events, callback catalog identity,
custom turn initialization/advancement, custom reaction forecasts, objective-aware AI, live save
admission, reader/reference phase traces, save/load/replay and all eight additional registry seals.
The explicit failures prove transaction rollback without suppressing callback exceptions.

`state-grid-oracle.ts` registers a new grid and replaces square4 through named plugins. It compares
complete initial and spawned states, default and explicit authored facing, the installed grid identity,
save/load and replay. The same injected `BattleStateRules` owns initial and spawned unit creation;
the standalone factory overload delegates to that path with the shared core grid recipe.

## Ownership and deliberate boundaries

Source default spatial construction captures the tactical plugin's local grid registry. A later grid
capability replacement does not rebind that space. In-place mutation and replacement are different
operations. Final sealing affects resolved registries, not displaced values. Registry clones are
shallow entry-table copies, and caller-supplied registries are sealed in place like the source.

`ReadOnlyContent` is the explicit catalog boundary. It owns normalized detached pack data and returns
detached read values. Callback ports receive the resolved catalog object, not another catalog rebuilt
from a pack copy. The full-state `IBattleRandomSource` default adapter delegates numeric generation to
the existing `SplitMixRandom` owner rather than maintaining another RNG implementation.

Reference checks consult resolved registries and handler-owned metadata. The root supplies existing
structural deployment/transport guards to the save environment, not a copied table of built-in IDs.
The reader capability owns save admission; writing, loading and restored command dispatch reuse the
same composed rule graph. Source-admitted inert metadata is not silently turned into a new rule.

Kernel-only semantics, each policy owner's full differential suite, the sixteen-battle default gate
and Godot presentation checks remain separate acceptance gates. Successful gameplay composition alone
does not establish custom-rule rendering parity.
