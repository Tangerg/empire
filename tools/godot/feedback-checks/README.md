# Historical battle-feedback source differential

This directory is retained as a forensic comparison with the original TypeScript
feedback, **not current native acceptance**. The executable intentionally still
compares literal source expectations and can report incompatibilities with current
strict identities, localization and committed-event coverage. In particular, the
native journal now names both participants and includes forced movement/collision.
Do not interpret a historical count below as a newly executed PASS, or weaken the
native contract to make this old oracle green. Current ownership and tests are in
[`godot/docs/BATTLE-FEEDBACK.md`](../../../godot/docs/BATTLE-FEEDBACK.md).

The remainder describes the historical comparison that produced the original evidence.

Run from the repository root:

    bash tools/godot/feedback-checks/run.sh

The harness generates its expectations from the current TypeScript source, then runs the pure C# presentation helpers without Godot or a TypeScript runtime bridge. Pass an optional output directory to retain the generated oracle at a known location.

## Owners and API

- `godot/Presentation/Feedback/BattleEventText.cs`: descriptive half of `packages/game-ui/src/ui/event-presentation.ts`. `BattleEventText.CreateDefault()` returns the sealed default registry. `Clone`, `Register`, `Replace`, `Keys`, and `Seal` preserve explicit composition. `Describe(context, event)` deliberately returns an empty string for unknown events, including `scenarioSignal`.
- `BattleLogContext.cs`: `new BattleLogContext(committedState, unitTypeName, careerName, resourceName)`. The state is detached. Label functions come from the caller's composed ruleset; the helper owns no content catalog or resource-name table. Unit lookups use the final committed batch state, matching `game.ts:logContext`; removed units read `单位`, unknown players read `？`. Recruitment uses `state.currentPlayer`, not the recruited unit's owner.
- `BattleLog.cs`: the only log retention policy, ported from `battle-log.ts`. `Add`, `Clear`, and `Recent` preserve order, ignore empty text, coalesce only consecutive equal descriptions, retain 60 runs by default, and return a detached array.
- `CampaignBattleSummary.cs`: `Create(title, chapter, perspectivePlayer, state, unitTypeName, signals, strikeCount)` projects the original result metrics and detail text. It counts only the perspective player's on-field units as allies, not all friendly-team or embarked units. Enemy count is team-relative. Fallen names come from every marker with a matching `fallenUnit.owner`, without adding a casualty-type rule or removing duplicates. Signals retain their source order and duplicates.
- `godot/Empire.Core/Combat/BattleEventFacts.cs`: the canonical native port of `combat.ts:isStrike/strikeCount`. A strike is an event carrying both `attacker` and `damage`; it is not a closed list of event type names. Presentation receives the resulting count rather than reimplementing this predicate.
- Ordered signals are read from the existing canonical `CampaignBattleBridge.Result(...)["signals"]`, not filtered a second time in presentation.

`CampaignBattleSummary.Heading` is the source's fixed `战斗胜利` heading. The original `StoryCampaignController.completeBattle` constructs and renders this screen only after a `victory` result. The application must preserve that gate and must not label a loss as a victory. `Outcome` is the original `state.endReason`, not a computed win/loss.

## Integration semantics

These files contain no UI integration and do not dispatch actions or alter canonical state.

`game.ts:playEvents` builds one context from the final state of the committed order, then describes each event after that event's animation. Feed one resulting line at a time into the single screen log. `lastSeenPositions` exists for animation coordinates, not for log names.

The original controller's log lifecycle is:

- New battle: `战前部署 · 调整站位后确认部署` during deployment; otherwise `第 1 回合 · {first human player's name, or empty} 开始行动`.
- Restart: clear the log, with no replacement opening message.
- Successful load: clear the log and append only `已读取第 {turn} 回合的存档`. Do not replay historical event lines.
- Failed load: retain the log and append `无法读取存档：{message}`.
- Undo: retain the log and append `已撤销上一步`.

## Verification coverage

The oracle calls the actual default presenter registry and `BattleLog`. It extracts and transpiles the actual private `GameController.logContext`, `StoryCampaignController.summarizeBattle`, and `renderBattleResult` methods for execution with isolated test receivers. The summary is compared with both the source DTO and text extracted from its rendered result HTML, rather than with a second handwritten expected formatter.

Cases cover every registered description and lethal/nonlethal branch, missing actors, caller-owned labels, resource-holder kinds, all four shipped resources and an unknown resource, three ranks, careers, punctuation, raw HTML-looking text, UTF-16 lone units, invariant numeric formatting under a French locale, silent unknown events, log truncation/coalescing/clear/reader isolation, explicit AI perspective, another allied player, repeated fallen names, non-corpse fallen markers, ordered duplicate signals, custom strike types, and the real 142-order Grey Flag replay.

The harness also checks registry cloning/sealing/replacement, detached context lifetime, no state/event mutation, summary input detachment, and canonical signal/count parity. Build outputs stay under ignored `bin`/`obj`; generated fixtures stay outside the repository.
