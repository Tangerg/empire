# C# engine compatibility

The migration uses the original TypeScript engine as the oracle for valid states, actions, ordered events, and deterministic state digests. Intentional behavior changes are recorded here rather than hidden in fixture expectations.

## Off-board movement destinations

The C# port rejects a command whose destination is outside the board before performing a flattened-index lookup. `MovementRules.PathTo` and `MoveCostOf` return `null` for an off-board coordinate. Rejection emits no events and does not mutate state.

The original TypeScript `pathTo` and `moveCostOf` flatten coordinates as `y * width + x` without checking bounds, and command path validation trusts that lookup. On a width-six board, destination `(6, 0)` aliases `(0, 1)`. If `(0, 1)` is reachable, the TypeScript command can move the actor to the invalid position `(6, 0)` while emitting a canonical path ending at `(0, 1)`. Negative coordinates can alias other valid cells in the same way.

This malformed-input defect is deliberately not reproduced. Valid movement costs, canonical path selection, occupied-cell rules, path-interior normalization, and event order are unchanged. The focused tactics harness tests the aliasing regression in addition to the original-engine query and wait-action oracles.

## Battle save and replay documents

Native persistence reads and writes the source schema-1 battle-save and replay shapes. Saves contain only `schema`, `battle`, `ruleset`, `savedAt`, and `state`; undo history and presentation/session state are not part of a battle document. Saves, loaded states, headers, recorded actions, and emitted replay documents own detached JSON copies.

The native default manifest contains `engine.ai-planning`, `engine.content`, `engine.mission-rules`, `engine.resource-economy` and `engine.tactical-space` at version 1, plus `engine.tactical-rules` at version 2. Content-pack versions come from explicitly supplied packs. Separating tactical producers from the spatial consumer deliberately changes assembly identity: old five-plugin saves/replays are refused as incompatible, with no upgrade, fallback or alias. State/event/digest parity for default battles is a separate guarantee from document identity. Unknown/missing/version-mismatched plugins or packs are refused. Anonymous factory overrides and catalogs without versioned packs cannot be persisted.

Semantic reference checks consume actual composed handler registries through one ordered reference owner. Optional inert payloads are admitted at the same phase as source; executing a demanded unresolved capability still fails. `BattleSaveFeatures` contains only named structural callbacks, including deployment and transport invariants. Plugin manifest identity alone does not replace actual handlers or these aggregate checks.

The reader preserves the original top-level and aggregate schema checks and content-reference checks. It additionally rejects malformed unit shapes, inconsistent row-major map-array lengths, off-board/overlapping field units, duplicate living-unit IDs or nonempty keys across field and embarked units, unknown owners/current player, nonpositive living HP, inconsistent turn-order policy, invalid random seed/counters, negative resource balances, invalid actor clocks or colliding next IDs, and casts that resolve before or at declaration. These are intentional fail-closed checks, not compatibility promises for malformed source documents.

Embarked units receive the same unit-field, catalog and capability checks as field units. Their carrier must exist on the field and have a transport profile, and a passenger cannot itself be a transport. Loading does not rerun boarding eligibility: owners may have changed, and capacity or tag admissibility is not assumed to be unchanged since boarding. Fallen-unit marker snapshots are validated as snapshots, not inserted into the living-ID uniqueness set.

Saved timestamps are generated in UTC ISO-8601 form, and source ISO timestamps round-trip unchanged. Parsing uses invariant .NET date parsing; equivalence with every legacy or implementation-defined JavaScript `Date.parse` input is not claimed.

Replay verification reproduces the source divergence index and diagnostic reason for unsupported schemas, level/ruleset mismatches, initial/final digest mismatches, and premature battle completion. Illegal actions retain their divergence index and expose the native semantic refusal separately from the diagnostic reason. Malformed replay envelopes are refused; unexpected domain defects propagate rather than being disguised as ordinary divergence.

`tools/godot/persistence-checks` exercises the three source GrayFlag gameplay vectors under the explicitly declared native document identity, rejects the original source identity, and checks midbattle interruption/resumption. The test-only fixture declaration changes only ruleset metadata, never gameplay state, actions, events or digests. Production persistence neither reads oracle fixtures nor rewrites their identities.

## Composition installation contracts

The TypeScript default tactical installer captures its local grid registry before declared replacements.
Native composition deliberately repairs that defect: the tactical producer provides grids and a separate
spatial plugin declares content/grids consumption, so their overrides run before spatial construction.
Movement and vision share that final registry. Replacing a built-in grid or adding a new ID works through
actual engine commands, named-plugin save/load, resumed execution and replay.

TypeScript also permits retained installation contexts to provide, replace and require after composition,
bypassing manifest and dependency validation. Native contexts close when their install callback returns
or throws. All later writes and consumption fail, including calls made from another plugin or through a
base-class view. The completed `KernelCapabilities` retains public read access. Metadata probes do not
consume capabilities. Closure is not rollback of callback side effects or a sandbox for rule objects.

## Reaction, facing and hit-effect admission

The next six-plugin native identity also includes these intentional contract repairs.
Unknown reaction commands no longer commit an unpersistable stance: they fail with
`IllegalActionException` before any state/event change. Facing commands similarly
admit only directions declared by the composed tiling, including custom direction
IDs. `UnitDisposition` owns the live writes and automatic facing transitions;
invalid internal requests remain `DomainInvariantException`. Authored levels and
saved field, embarked and fallen units use the same tiling direction predicate.
Initial, recruitment and scenario unit construction also use the composed reaction
registry and direction predicate. Invalid authored spawn seeds fail before consuming
an ID or changing state; the raw-pack factory overloads that rebuilt defaults are removed.
No fixed built-in direction/reaction list or invalid-state repair is retained.
The frozen TypeScript unknown-stance vector is preserved as defect evidence, not
counted as a native parity success.

Weapon hit-effect handlers own `Validate`, `Apply` and `Describe`. Catalog admission
and direct execution dispatch validation through the selected handler; replacing
`forcedMove` or `addStatus` replaces payload/reference semantics as well as execution.
The generic validator no longer interprets these kind strings. Registered kind
references still belong to the ordered reference registry. A handler with no declared
validation accepts opaque payloads, rather than inheriting another kind's schema.
The default add-status handler shares the positive-integral duration invariant with
`StatusTransitions`; malformed durations are now rejected during content admission.
Default forced-movement admission also refuses an unknown mode. Direct effect batches
retain per-effect ordering; a failing battle command rolls back the whole transaction.

Support attack eligibility/multiplier and triggered rank progress are declared by
`ReactionBehavior`, not inferred from the literal `support` ID. Defaults retain the
0.6 support modifier and 20 triggered progress; custom IDs can opt in and replacements
can remove/change these behaviors. Valid default events and state digests remain
covered by the unchanged source vectors and the separate complete battle gate.
`turn-reaction-extension-checks` and `combat-policy-checks` exercise these native
contracts through composed engines, dispatch, save/load/replay and compiled bypass
mutants. They do not claim arbitrary plugin validation or presentation correctness.

## Deployment

Deployment rearranges existing owned units. It never spawns units, creates a reserve, consumes an actor turn, or requires every offered slot to be filled. Assignment selection and roster ordering come from canonical state construction; zone overlap remains visible in declared order. Placement has one refusal owner shared by queries and commands, validates both sides of a swap, clears only moved units' capture fields, and emits one placement event. A placement onto the same cell is a true no-op. Confirming advances the declared side order; the last confirmation calls the canonical lifecycle's `BeginPlaying`. Neither confirmation invokes post-action triggers or victory evaluation.

Persisted deployment additionally requires a nonempty unique known-player order, an in-range current index matching the current player, valid assignments and zone coordinates, unique living assignees with the declared owner, and an assignment for every ordered player. Deployment data must be absent outside the deployment phase and present within it. These fail-closed document checks intentionally reject malformed states the source reader may accept. Empty assignments, overlapping zones, assignments for players outside the turn order, and units initially outside their zones remain source-valid. `tools/godot/deployment-checks` verifies source parity and engine save/resume through the first actor turn.

## Native vision boundary

Canonical fog queries preserve original terrain-only concealment, allied sight,
owned cells and cover adjacency. The native integer grid rejects fractional vision
radii explicitly; original content admission does not enforce this and its grid
loops can produce fractional coordinates. Exported GrayFlag definitions use integer
vision. This is input hardening, not exact behavior for arbitrary numeric radii.

## Definition and deferred payload admission

The real composition root resolves all 24 capabilities and uses one ordered ten-check reference registry for content, levels and saved state. Handler-owned reference/children metadata and the actual resolved registries determine semantic IDs. `BattleSaveFeatures` contains aggregate structural guards only; it is not an ID whitelist. A named replacement save reader is invoked by both Save and Load, with the actual composition manifest and reference owners.

Source-admitted inert metadata remains inert. Unknown unused resource accounts, future spawn declarations and `failOn` payloads are not rejected merely because an invented native whitelist cannot interpret them. If the source resolves a declaration only when it executes, native resolves it at that boundary too; transaction failure restores the prior state. The reference suite retains all 94 former future-reference cases under their original names and derives their expected decisions from TypeScript rather than relaxing assertions by hand. Its classification map records each corrected earlier native restriction.

Source reference issue order and first-seen deduplication are significant. In particular, teleport/return destination zones precede selector zones; explicit null is not the same as an absent threshold; blank IDs use ECMAScript whitespace rather than .NET Trim. Unknown handler reference metadata is empty where the source says so; executing or traversing a demanded unknown handler still fails.

Structural safety differences remain explicit: native validates nested state shapes, nonnegative accounts and finite live grant amounts, map-array dimensions, integer clocks/generated IDs, generated-ID monotonicity, player/current-player consistency, matching stored turn-order policy, uint RNG seeds and nonnegative integer counters, in-bounds/nonoverlapping living positions, living-ID/key uniqueness, existing owners and positive live HP. Pending casts retain structural and resolve-after-declaration timing checks. Deployment/transport consistency remains with their aggregate owners. These checks do not guess custom payload semantics or reintroduce fixed resource/type lists. Timestamp syntax still uses .NET DateTimeOffset rather than arbitrary JS Date.parse compatibility.

See `tools/godot/persistence-reference-checks/compatibility-map.json` and the frozen level/metadata suite for the exact tested admission boundaries. Source linter warnings, arbitrary plugin correctness and custom presentation are not claimed by these engine contracts.

## Non-transitive priority comparators

Native registries retain accepted double priorities, including NaN, and ordinary finite/fractional ordering follows the source. Mixed NaN priorities can make the original comparator non-transitive. Native stable sorting preserves its comparator semantics but does not promise V8-specific permutations for inconsistent comparisons. The registry suite characterizes 20 such mixed-NaN orders separately from its exact ordinary-order assertions; those 20 differ and are not counted as parity passes.

## Native semantic action refusals

`ActionRefusal` is the sole refusal value: a scoped semantic `Code` and a detached, immutable JSON-object `Details`. The producing rule owns eligibility and required details. Detail values are identifiers, counts, coordinates or nested semantic refusals, never localized display names or sentences. Queries return `ActionRefusal?`; option requirements are read-only refusal lists. Commands throw `IllegalActionException` containing that same value. Formation query and command validation now share one refusal owner.

The exception's `Message` is a developer diagnostic (`Action refused: <code>`), not player text. Replay divergence retains the semantic value. `CommandableUnit` no longer accepts a presentation-only order fragment. Its action ownership, acted-state and initiative checks are unchanged. No obsolete string constructor, string-reason API, runtime TypeScript adapter or stored localized text remains in this path.

`ActionRefusalText` is an open, explicitly injected presentation registry over `GameText`. The application composes and seals it against its engine's actual content and ability registry, then passes it through campaign and session construction and restore. It resolves definition labels from that catalog, formats nested requirements, and fails explicitly for missing keys or unregistered codes. Extensions register their semantic code and catalog-key argument projection at composition time. Session dispatch, AI feedback and disabled formation/career/carrier hints consume this same formatter. Expected save/input failures use fixed catalog messages; detailed exceptions go only to diagnostics.

This deliberately replaces TypeScript refusal prose and punctuation, including order-specific sentence fragments, while preserving refusal precedence, transaction rollback, event ordering and state hashes. Existing source-oracle checks retain exact state/event/digest comparisons and invariant diagnostics; named refusal cases now assert native code/details independently of the source wording. Pure application checks receive an explicit catalog JSON projection generated by the existing catalog validator, not an ambient locale or a second PO parser. Session checks exercise real dispatch with different injected wording and require identical unchanged battle state, event/history counts and revision. Objectives, outcome reasons, content names and other HUD prose are separate localization slices, not claimed by this refusal contract.
