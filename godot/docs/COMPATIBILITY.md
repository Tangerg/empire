# C# engine compatibility

The migration uses the original TypeScript engine as the oracle for valid states, actions, ordered events, and deterministic state digests. Intentional behavior changes are recorded here rather than hidden in fixture expectations.

## Off-board movement destinations

The C# port rejects a command whose destination is outside the board before performing a flattened-index lookup. `MovementRules.PathTo` and `MoveCostOf` return `null` for an off-board coordinate. Rejection emits no events and does not mutate state.

The original TypeScript `pathTo` and `moveCostOf` flatten coordinates as `y * width + x` without checking bounds, and command path validation trusts that lookup. On a width-six board, destination `(6, 0)` aliases `(0, 1)`. If `(0, 1)` is reachable, the TypeScript command can move the actor to the invalid position `(6, 0)` while emitting a canonical path ending at `(0, 1)`. Negative coordinates can alias other valid cells in the same way.

This malformed-input defect is deliberately not reproduced. Valid movement costs, canonical path selection, occupied-cell rules, path-interior normalization, and event order are unchanged. The focused tactics harness tests the aliasing regression in addition to the original-engine query and wait-action oracles.

## Battle save and replay documents

Native persistence reads and writes the source schema-1 battle-save and replay shapes. Saves contain only `schema`, `battle`, `ruleset`, `savedAt`, and `state`; undo history and presentation/session state are not part of a battle document. Saves, loaded states, headers, recorded actions, and emitted replay documents own detached JSON copies.

The shipped-default manifest retains the five source plugin identities at version 1: `engine.ai-planning`, `engine.content`, `engine.mission-rules`, `engine.resource-economy`, and `engine.tactical-rules`. Content-pack versions come from the explicitly supplied packs. This is the identity of the compatible shipped-default rules, not a claim of universal behavior for every possible third-party plugin. Unknown/missing/version-mismatched plugins or packs are refused. Anonymous factory overrides and catalogs without versioned packs cannot be persisted.

Semantic reference checks consume actual composed handler registries through one ordered reference owner. Optional inert payloads are admitted at the same phase as source; executing a demanded unresolved capability still fails. `BattleSaveFeatures` contains only named structural callbacks, including deployment and transport invariants. Plugin manifest identity alone does not replace actual handlers or these aggregate checks.

The reader preserves the original top-level and aggregate schema checks and content-reference checks. It additionally rejects malformed unit shapes, inconsistent row-major map-array lengths, off-board/overlapping field units, duplicate living-unit IDs or nonempty keys across field and embarked units, unknown owners/current player, nonpositive living HP, inconsistent turn-order policy, invalid random seed/counters, negative resource balances, invalid actor clocks or colliding next IDs, and casts that resolve before or at declaration. These are intentional fail-closed checks, not compatibility promises for malformed source documents.

Embarked units receive the same unit-field, catalog and capability checks as field units. Their carrier must exist on the field and have a transport profile, and a passenger cannot itself be a transport. Loading does not rerun boarding eligibility: owners may have changed, and capacity or tag admissibility is not assumed to be unchanged since boarding. Fallen-unit marker snapshots are validated as snapshots, not inserted into the living-ID uniqueness set.

Saved timestamps are generated in UTC ISO-8601 form, and source ISO timestamps round-trip unchanged. Parsing uses invariant .NET date parsing; equivalence with every legacy or implementation-defined JavaScript `Date.parse` input is not claimed.

Replay verification reproduces the source divergence index and reason for unsupported schemas, level/ruleset mismatches, initial/final digest mismatches, illegal actions, and premature battle completion. Malformed replay envelopes are refused; unexpected domain defects propagate rather than being disguised as ordinary divergence.

`tools/godot/persistence-checks` exercises all three source GrayFlag saves/replays and interruption/resumption at a midbattle boundary. Production persistence does not read oracle fixtures.

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
