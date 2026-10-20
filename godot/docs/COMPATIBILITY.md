# C# engine compatibility

The migration uses the original TypeScript engine as the oracle for valid states, actions, ordered events, and deterministic state digests. Intentional behavior changes are recorded here rather than hidden in fixture expectations.

## Off-board movement destinations

The C# port rejects a command whose destination is outside the board before performing a flattened-index lookup. `MovementRules.PathTo` and `MoveCostOf` return `null` for an off-board coordinate. Rejection emits no events and does not mutate state.

The original TypeScript `pathTo` and `moveCostOf` flatten coordinates as `y * width + x` without checking bounds, and command path validation trusts that lookup. On a width-six board, destination `(6, 0)` aliases `(0, 1)`. If `(0, 1)` is reachable, the TypeScript command can move the actor to the invalid position `(6, 0)` while emitting a canonical path ending at `(0, 1)`. Negative coordinates can alias other valid cells in the same way.

This malformed-input defect is deliberately not reproduced. Valid movement costs, canonical path selection, occupied-cell rules, path-interior normalization, and event order are unchanged. The focused tactics harness tests the aliasing regression in addition to the original-engine query and wait-action oracles.
