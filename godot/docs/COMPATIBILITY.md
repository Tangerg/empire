# C# engine compatibility

The migration uses the original TypeScript engine as the oracle for valid states, actions, ordered events, and deterministic state digests. Intentional behavior changes are recorded here rather than hidden in fixture expectations.

## Off-board movement destinations

The C# port rejects a command whose destination is outside the board before performing a flattened-index lookup. `MovementRules.PathTo` and `MoveCostOf` return `null` for an off-board coordinate. Rejection emits no events and does not mutate state.

The original TypeScript `pathTo` and `moveCostOf` flatten coordinates as `y * width + x` without checking bounds, and command path validation trusts that lookup. On a width-six board, destination `(6, 0)` aliases `(0, 1)`. If `(0, 1)` is reachable, the TypeScript command can move the actor to the invalid position `(6, 0)` while emitting a canonical path ending at `(0, 1)`. Negative coordinates can alias other valid cells in the same way.

This malformed-input defect is deliberately not reproduced. Valid movement costs, canonical path selection, occupied-cell rules, path-interior normalization, and event order are unchanged. The focused tactics harness tests the aliasing regression in addition to the original-engine query and wait-action oracles.

## Battle save and replay documents

Native persistence reads and writes the source schema-1 battle-save and replay shapes. Saves contain only `schema`, `battle`, `ruleset`, `savedAt`, and `state`; undo history and presentation/session state are not part of a battle document. Saves, loaded states, headers, recorded actions, and emitted replay documents own detached JSON copies.

The shipped-default manifest retains the five source plugin identities at version 1: `engine.ai-planning`, `engine.content`, `engine.mission-rules`, `engine.resource-economy`, and `engine.tactical-rules`. Content-pack versions come from the explicitly supplied packs. This is the identity of the compatible shipped-default rules, not a claim that every source extension registry has been migrated. Unknown/missing/version-mismatched plugins or packs are refused. Anonymous factory overrides and catalogs without versioned packs cannot be persisted.

`BattleSaveFeatures` declares the actual native capabilities separately. The composition root supplies supported IDs from the rule owners. The loader checks learned and unit-definition abilities, reactions, directives, turn policies, grids, objectives, nested conditions, scenario effects, referenced weapon areas/hit effects, pending-cast abilities, resource adapters, and tactic effects. Deployment, embarked-unit, and composite state require explicit `stateFeatures` entries; a manifest alone cannot enable them. The current engine enables these three features after composing their handlers. Additional named state guards enforce deployment and transport invariants.

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

Engine composition validates the complete supplied catalog, not only definitions
already used by living units. The original content invariants and default-rule
references therefore cover future recruitment, unused weapons, and unlearned
career mastery abilities before a battle starts. Authored levels pass the
source-compatible structural/reference checks before state construction.
Capability IDs come from the actual composed rule owners via `BattleSaveFeatures`,
not a GrayFlag content whitelist or a plugin name.

Native admission additionally refuses unregistered resource adapters and tactic
effect kinds, condition kinds nested in `failOn` objectives, and unsupported
ability/reaction/directive/resource overrides in future spawn declarations. These
are explicit fail-closed unsupported-extension guards: the source admission code
does not catch every one of these declarations before its handlers encounter it.
`BattleLevelValidator.Issues` preserves the source error-level checks; the extra
execution-capability guard is a separate composition step. Source linter warnings
and arbitrary plugin-defined payload validation are not claimed.

Saved scenario payloads also validate their declared catalog, zone, objective,
map-cell/edge, directive, resource, and composite references. They do not pre-run
selectors or future occupancy/placement legality. Neutral selector owners and
currently absent selected IDs remain valid; missing former structures remain
valid for damage/destroy checks where source behavior is a no-op or successful
absence. Save/load rejection remains atomic and does not mutate the supplied JSON.
