# Native kernel contract checks

Run from the repository root:

```sh
bash tools/godot/kernel-contract-checks/run.sh
```

Uses the existing official .NET installation via `tools/godot/env.sh`, installed
TypeScript tooling, and no downloads. An optional first argument retains evidence
in the supplied directory. The test run typechecks its oracles, regenerates both
source outputs, byte-compares frozen fixtures, runs the native comparison, then
kills semantic mutants in disposable copies. No shared source file is mutated by
the negative controls, and TypeScript is not a dependency of the native runtime.

## Source ownership and coverage

- `packages/battle-engine/src/kernel.ts` owns plugin identity, installation,
  capability attribution, declared consumption, and dependency-edge construction.
- `packages/battle-engine/src/dependency-order.ts` owns the reusable DFS planner.
- `packages/battle-engine/src/ruleset-manifest.ts` owns persisted identity and
  anonymous-override refusal. `battle-save.ts` supplies actual source document
  admission for the wide-version cases.
- Source tests: `dependency-order.test.ts` and the kernel/composition sections of
  `kernel-resources.test.ts`. This suite does not claim to port that file's
  unrelated resource-account tests.

The main frozen fixture contains 63 source-evaluated cases: 10 planner cases and
53 kernel cases. Additional differential checks cover retained scopes/repeated
composition and persistence identity. Native contract checks exercise typed-key
aliasing/conflicts and immutable snapshots. The version fixture exercises real
source and native save/load admission for positive integral binary64 versions
beyond Int32 and the JavaScript safe-integer limit, including the largest finite
double. It also checks malformed stored versions and unchanged default manifest
identity/hash. Exact trim-blank semantics cover every ECMAScript whitespace
character plus BOM, next-line, removed Unicode whitespace and unpaired surrogate
boundaries at registration, manifest creation and stored admission. Actual source
content-pack ID and campaign battle-level admissions also cover BOM/U+0085. The full native run currently reports 121 checks.

There are 13 actual TypeScript-owner mutants and 15 compiled native mutants.
They remove ordering, external dependency satisfaction, cycle slicing, duplicate
rejection, declared consumption, provides/overrides validation, introducer
identity, override uniqueness, typed-ID safety, manifest registration order,
wide-version admission, ECMAScript whitespace classification, or ECMAScript numeric error formatting. A native mutant
must compile before a semantic test failure counts as a kill. A source mutant
must finish oracle generation and produce a different result.

## Public contracts

`Empire.Core.Composition` has no game-rule owner and performs no discovery.

- `CapabilityKey<T>(id)` is an open typed contract. The stable string ID, not key
  object reference, addresses the slot. Two keys with the same ID and exact CLR
  type alias that slot. Incompatible types fail deterministically, including
  across declarations and operations. Assignability does not silently change a
  slot's declared type. The contract table is per composition, never global.
- `EnginePlugin(id, version, install, provides, overrides,
  requiresCapabilities, requires)` snapshots its declarations. Versions are
  positive finite integral doubles, matching `Number.isInteger` plus `>= 1`,
  rather than artificially restricting composition to Int32.
- `SrpgMicrokernel.Use/UseAll/Compose` register identities, plan and install into
  a fresh store, and expose `KernelCapabilities`. Dependencies are visited in
  declaration order and plugins in registration order. This is the source DFS
  algorithm, not a globally sorted/Kahn-style topological order.
- `KernelPluginContext` attributes writes and tracks `Require`. Actual provides
  and overrides match declarations in both directions; every read must be
  declared. A declared but unused requirement is legal. `Has` and `ProviderOf`
  are probes, not consumption. Native probes still enforce the typed-ID contract.
- Overrides run after the introducer and before consumers. `ProviderOf` always
  identifies the original introducer, never the replacing plugin. Capability
  replacement requires an existing slot; it is not registry upsert.
- The composed `PluginManifest` is an immutable, detached registration-order
  ID/version snapshot, not installation order. Installation contexts have empty
  manifests, matching the source. Existing persistence canonicalizes the
  ID/version map separately. Its original Int32 overload delegates to the new
  validated double overload without changing existing default manifest hashes.

## Important source boundaries

**A scoped context is not a revocable lease or sandbox.** Source callbacks can
retain their context. After installation they can still read, provide and
replace values in that composition's store, and those later changes are not
revalidated against the plugin's declarations. A failed composition does not
undo callback side effects or destroy retained scopes. A subsequent `Compose`
uses a fresh store, but callback closures and supplied capability objects are
not deep-cloned. The suite intentionally characterizes this source behavior;
it does not silently promise stronger immutability, rollback or isolation.

The public native manifest and declaration collections enforce their read-only
contracts at runtime; TypeScript's `ReadonlyMap`/readonly arrays enforce those
contracts statically. Neither form makes the installed rule objects immutable.

The version save checks install a named identity-only plugin into the kernel,
then run the existing persistence owners with source-authored valid state and
explicit feature declarations. This proves composition metadata can be saved
and admitted without narrowing the numeric version domain. It does **not** prove
custom rule execution or that native engine defaults already use the kernel.

`engine.override.*` names retain the source's anonymous-override semantics:
existing persistence refuses them, including recorder and save creation.
No new anonymous-ID factory or duplicate prefix owner is introduced here.
Named plugin/version metadata identifies configuration; it cannot serialize
arbitrary delegates or prove that two different implementations with the same
name/version are equivalent.

Default engine construction and domain-specific extension integration remain
outside this primitive-kernel batch. Passing this suite is not end-to-end
extension parity, and does not relax existing integer-domain restrictions on
entity IDs, coordinates or other battle data.
