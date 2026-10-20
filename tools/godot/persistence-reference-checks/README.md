# Deferred payload admission checks

Run `bash tools/godot/persistence-reference-checks/run.sh` from the repository root.

The runner tests actual `GrayFlagEngine.SaveBattle`, `LoadBattle`, composition, and
`CreateState` boundaries. A rejected load/save must leave its input unchanged; an
accepted save must round-trip to the same deterministic state digest.

Checks cover deferred directives, spawn overrides, resource adapters, statuses,
terrain and overlay IDs, objective references, map cells and edges, composites,
structure targets, and nested conditions. It also round-trips the opening state
of all 16 GrayFlag campaign levels. Composition refuses latent unsupported unit,
weapon, reaction, account, and mastery declarations before any recruitment or
scenario spawn can use them.

Dynamic eligibility is deliberately not pre-executed. Empty selectors, neutral
selector owners, absent selected IDs/keys, occupied future spawn cells, and
currently colliding future unit keys remain valid declarations. Damage to a
missing former structure and removal of an absent engagement rule retain their
source no-op semantics. A missing repair target is refused because its canonical
handler requires an existing structure.

The separate native execution-capability cases cover condition kinds nested in
`failOn` objectives, future spawn ability/reaction/directive/resource overrides,
and level resource adapters. These strengthen unsupported-extension admission
beyond the source linter and are not counted as source differential parity.
