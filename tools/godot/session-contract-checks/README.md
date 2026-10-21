# Session handoff and trusted undo contracts

Run from the repository root with the existing official toolchain:

```sh
bash tools/godot/session-contract-checks/run.sh
bash tools/godot/session-contract-checks/mutants.sh
```

`oracle.ts` executes the original `GameSession`, engine and handler registry. It compares 138 dispatch/undo/load/restart operations, including 45 source AI choices with undo/replay where applicable. The native runner compares acceptance, every canonical state hash, complete semantic event history, handoff metadata and undo eligibility. Additional source fixtures cover terminal non-handoff combat and deployment handoff. No TypeScript is called by native runtime code.

The native contract checks also exercise:

- Exact nested state restoration (RNG, accounts, casts, transport and scenario fields), detached receipts, and attempted mutation of receipt data.
- Foreign, out-of-order, already consumed, abandoned-branch and pre-handoff checkpoints, without state mutation.
- Failed dispatch after draft mutation/event emission; reentrant dispatch and restore, including preservation of the previous valid checkpoint.
- Frozen registration metadata, replacement, unknown metadata queries, and a custom handoff name versus a non-handoff handler named `endTurn`.
- Stale undo and post-undo callbacks, monotonically advancing session revisions, cleared presentation transition and action/event truncation.
- The deliberate native resume difference: previously recorded campaign semantic events survive load and subsequent undo.

`mutants.sh` copies only the required native source tree into a temporary directory. It compiles each mutant before running the real harness, rejecting hardcoded handoff names, a no-op restore, omitted event truncation and disabled stale revision checks. Build failures do not count as detected mutants. It never edits the working tree.

## Runtime API and ownership

`BattleActionRegistry.Register/Replace(kind, handler, handsOffTurn = false)` stores metadata with the handler. `HandsOffTurn(action)` returns false for unknown kinds. `BattleEngine.HandsOffTurn(action)` delegates to the same composed registry used by its battles, so an attempted AI action can be queried before dispatch. Only the native registrations for `endTurn` and `finishDeployment` declare true, matching the source handlers.

`BattleReceipt` is created only by `Battle`, exposes detached `Before`/`Events`, and carries `StateHash` and `HandsOffTurn`. `Battle.Restore(receipt)` accepts only the current trusted non-handoff receipt issued by that battle. Successful restore consumes that checkpoint and exposes its predecessor. Handoff clears the chain. No client-provided JSON is accepted as a restoration checkpoint; snapshots remain read projections, not writers.

`GrayFlagSession.CanUndo` additionally gates computer-controlled and terminal turns. `Undo(expectedRevision)` returns the ordinary `BattleDispatchResult`, restores through `Battle`, truncates its two histories, advances the revision and clears the presentation transition. `LastActionHandsOffTurn` is nullable: successful commits set it, new sessions/resume/undo leave it null. Failed commands do not change this successful-action fact or the undo stack.

Native load/restart replace the session through the existing campaign owner. New instances start with no undo/checkpoint/action history and require the existing screen-binding generation guard for callbacks from an old instance. Resume keeps supplied semantic campaign events; standalone source `GameSession.load` clears them. Within a session, dispatch and undo revisions only increase. The original source `undo()` itself does not check terminal eligibility; native explicitly enforces its source `canUndo`/UI gate at the command boundary.

Presentation busy gating, keyboard/menu wiring, cosmetic “已撤销上一步” messages, cancellation of active animation, interface exposure and AI fallback/delay are separate integration work. In particular source fallback timing uses the attempted action's metadata, not necessarily the successful fallback receipt. These headless checks are not real Godot interaction acceptance.

Semantic refusals are exercised through real `GrayFlagSession.Dispatch`: two explicitly injected catalog mappings produce different messages for the same rejected order while state hashes, revisions and semantic/action histories remain identical. The check also covers detached nested refusal facts, missing presentation registrations and independent registry extensions. `run.sh` generates and passes a catalog JSON file explicitly. The isolated `flattened-refusal` mutant replaces the presentation call with the exception diagnostic and must be rejected.
