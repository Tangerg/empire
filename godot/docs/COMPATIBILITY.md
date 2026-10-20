# C# engine compatibility

The migration uses the original TypeScript engine as the oracle for valid states, actions, ordered events, and deterministic state digests. Intentional behavior changes are recorded here rather than hidden in fixture expectations.

## Off-board movement destinations

The C# port rejects a command whose destination is outside the board before performing a flattened-index lookup. `MovementRules.PathTo` and `MoveCostOf` return `null` for an off-board coordinate. Rejection emits no events and does not mutate state.

The original TypeScript `pathTo` and `moveCostOf` flatten coordinates as `y * width + x` without checking bounds, and command path validation trusts that lookup. On a width-six board, destination `(6, 0)` aliases `(0, 1)`. If `(0, 1)` is reachable, the TypeScript command can move the actor to the invalid position `(6, 0)` while emitting a canonical path ending at `(0, 1)`. Negative coordinates can alias other valid cells in the same way.

This malformed-input defect is deliberately not reproduced. Valid movement costs, canonical path selection, occupied-cell rules, path-interior normalization, and event order are unchanged. The focused tactics harness tests the aliasing regression in addition to the original-engine query and wait-action oracles.

## Battle save and replay documents

Native persistence reads and writes the source schema-1 battle-save and replay shapes. Saves contain only `schema`, `battle`, `ruleset`, `savedAt`, and `state`; undo history and presentation/session state are not part of a battle document. Saves, loaded states, headers, recorded actions, and emitted replay documents own detached JSON copies.

The shipped-default manifest retains the five source plugin identities at version 1: `engine.ai-planning`, `engine.content`, `engine.mission-rules`, `engine.resource-economy`, and `engine.tactical-rules`. Content-pack versions come from the explicitly supplied packs. This is the identity of the compatible shipped-default rules, not a claim that every source extension registry has been migrated. Unknown/missing/version-mismatched plugins or packs are refused. Anonymous factory overrides and catalogs without versioned packs cannot be persisted.

`BattleSaveFeatures` declares the actual native capabilities separately. The composition root supplies supported IDs from the rule owners. The loader checks learned and unit-definition abilities, reactions, directives, turn policies, grids, objectives, nested conditions, scenario effects, referenced weapon areas/hit effects, pending-cast abilities, resource adapters, and tactic effects. Deployment, embarked-unit, and composite state require explicit `stateFeatures` entries; a manifest alone cannot enable them. The current engine enables composites after composing their handlers, but does not enable deployment or embarked-unit interaction. Additional named state guards are available for composition-specific limitations.

The reader preserves the original top-level and aggregate schema checks and content-reference checks. It additionally rejects malformed unit shapes, inconsistent row-major map-array lengths, off-board/overlapping live units, duplicate live-unit IDs, unknown owners/current player, inconsistent turn-order policy, invalid random seed/counters, negative resource balances, invalid actor clocks or colliding next IDs, and casts that resolve before or at declaration. These are intentional fail-closed checks, not compatibility promises for malformed source documents.

Saved timestamps are generated in UTC ISO-8601 form, and source ISO timestamps round-trip unchanged. Parsing uses invariant .NET date parsing; equivalence with every legacy or implementation-defined JavaScript `Date.parse` input is not claimed.

Replay verification reproduces the source divergence index and reason for unsupported schemas, level/ruleset mismatches, initial/final digest mismatches, illegal actions, and premature battle completion. Malformed replay envelopes are refused; unexpected domain defects propagate rather than being disguised as ordinary divergence.

`tools/godot/persistence-checks` exercises all three source GrayFlag saves/replays and interruption/resumption at a midbattle boundary. Production persistence does not read oracle fixtures.
