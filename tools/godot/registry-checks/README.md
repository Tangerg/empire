# Shared native registry contracts

Run `bash tools/godot/registry-checks/run.sh` with the existing repository toolchain.
The source oracle imports the actual `registry.ts`, `AiIntentRegistry`, and
`ActionHandlerRegistry`. TypeScript executes only to produce test expectations;
native runtime code does not load it. This source mode also compares regenerated
oracle bytes with the decompressed checked-in fixture and fails if they differ.

Run `bash tools/godot/registry-checks/run.sh --frozen` for the Node-free base-verifier
path. It reads `godot/tests/oracle/registry-contracts.json.gz` using .NET gzip support;
all characterization outputs stay in the printed temporary directory, not the fixture
directory. The same 227 defined-outcome checks run in both modes. The frozen fixture
also retains the source runtime versions and mixed-NaN observations, but native
mixed-NaN ordering remains characterization rather than an exact V8-order assertion.

To deliberately refresh the frozen fixture after reviewing source/runtime changes:

```
node node_modules/vite-node/vite-node.mjs tools/godot/registry-checks/oracle.ts godot/tests/oracle/registry-contracts.json.gz
bash tools/godot/registry-checks/run.sh
bash tools/godot/registry-checks/run.sh --frozen
```

Coverage:
- 187 exact source-base outcomes exercised across 11 native registries: duplicate
  rejection, upsert, stable insertion order, detached keys/all, query versus demand,
  fluent/idempotent sealing, write rejection, and independent mutable shallow clones.
- 13 priority/cache/dispatch comparisons, including fractional AI dispatch order,
  ordinal UTF-16 ties, read-only cached ordered collections, invalidation after both
  successful and rejected writes, stable comparator ties, and shared mutable entry identity through cloning.
- 25 exact numeric-boundary/corpus cases: fractions, signed zero, infinities, two-entry
  NaN comparisons and larger finite corpora through 96 entries.
- Actual source action-key/dispatch behavior: empty/whitespace keys, prototype-looking
  keys, integer-looking keys, replacement metadata and new-key upsert. Both `HandsOffTurn` and
  `DuringDeployment` retain source defaults, clone identities and frozen-table values.
- Native Battle caller-registry snapshot isolation for both handlers and handoff metadata.
  Invalid null CLR delegates remain rejected, separately from valid source strings.

The source comparator subtracts priorities and falls back to ordinal IDs for zero or
NaN, including equal infinities. Mixed NaN and unequal numeric priorities can make
that comparison non-transitive (for example `a:2 < b:NaN < c:1 < a:2`). Such values
remain admitted, as in source, but no portable total order exists. The
[ECMAScript SortIndexedProperties specification](https://tc39.es/ecma262/multipage/indexed-collections.html#sec-sortindexedproperties)
makes sorting order implementation-defined for an inconsistent comparator. The runner explicitly
characterizes 20 larger mixed-NaN source/native corpora and writes both orders to
`non-finite-characterization.json` in its printed temporary directory. These cases
are reported separately, not counted as exact differential passes. Native sorting
uses stable `OrderBy`; it does not attempt to emulate V8's sorting implementation.
The characterization file captures exact Node/V8/.NET runtime versions, the concrete
three-entry comparison cycle and both result orders. Initial verification used
Node v24.19.0, V8 13.6.233.17-node.51, .NET runtime 8.0.32 and official SDK 8.0.426;
the executing .NET runtime version is recorded in each characterization artifact.

Prove the behavioral guards with isolated, compiled source mutations:

```
source tools/godot/env.sh
python3 tools/godot/registry-checks/mutants.py
```

These mutation runs copy the native core and test harness into a temporary directory;
they never modify the working source. Fixtures and diagnostics are temporary files.
The native engine still selects deployment eligibility through its existing dispatcher;
adding registration metadata alone does not claim open custom-action execution.

The existing `campaign-checks/run.sh` separately executes all 27 source campaign
registry cases and custom condition/effect/node frozen-clone isolation.
