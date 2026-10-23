# Native semantic objectives and battle conclusions

## Ownership

`ObjectiveHandler.Assess` owns both the objective outcome and progress facts. Its
`ObjectiveAssessment` snapshots facts as an immutable `JsonElement`.
`ObjectiveHandlerRegistry.Project` provides immutable definitions, facts and
nested child projections. Stored terminal status is authoritative. Presentation
never reselects units, recounts sites, coerces score variables or decides success.
The objective registry remains open; extensions register their own assessment
and their own `ObjectiveTextFormatter`.

`VictorySystem` owns objective transitions and defeat ordering. `BattleLifecycle`
is the only writer that concludes a battle. `BattleConclusion` owns the allowed
conclusion shape and reference invariants. State has exactly one `conclusion`:
null while playing/deploying, nonnull exactly when phase is `over`.

| kind | team | additional fields |
| --- | --- | --- |
| allDefeated | null | none |
| lastTeamRemaining | existing team ID | none |
| objectiveCompleted | winning player's team ID | player, objective |
| turnLimitReached | null | none |
| noActors | null | none |

All fields are exact. Unknown fields/kinds, obsolete `winnerTeam`/`endReason`,
wrong phase, unknown team/player/objective and a noncompleted winning objective
are rejected. Objective references are player-scoped and refer to the completed
top-level objective selected by the victory owner. Validation does not rerun the
victory rules.

The event is `{type: "gameOver", conclusion: {...}}`. Its conclusion and the
campaign result are detached projections, not another outcome transition.
`CampaignBattleResult` owns the seven-field result wire contract and is used by
both the bridge and campaign admission. Campaign history still stores only its
existing route/turn/signal fields, not another conclusion or description.

## Presentation authoring

Native gameplay levels no longer carry `players[].name` or objective `label`.
`grayflag-battle-text.json` is addressed by level ID, player ID and objective ID;
objective IDs need not be globally unique. The shipped document contains all
34 players and 74 effective nested objective descriptions across 16 levels.
All 34 players explicitly own their objectives. The 16 shipped levels have no
unused top-level `victory` templates; generic level-default inheritance remains
available to levels whose players do not declare objectives.

A player name entry is exactly `{key: "authored.po.key"}` or
`{literal: "user-authored text"}`. An objective entry can additionally be exactly
`{generated: true}`, selecting its registered generic kind formatter. Every
identity is explicit; missing/unknown identities, duplicate JSON keys, mixed
entry modes and missing messages fail at composition. Keys are authored semantic
names, never array positions or hashes. Literal text is not interpreted as a key.
Native controls disable automatic translation because `GameText` is the explicit
formatter, including for literal text.

`BattleText` shares player/description/conclusion formatting across HUD, logs,
completion and summary screens. All rule facts and saved conclusions remain
locale-independent. Level names, unit/catalog names and remaining summary labels
are outside this bounded authoring migration and are not claimed localized.

## Breaking persistence contract

- Battle save schema: 2 (was 1).
- Replay schema: 2 (was 1), shared by recorder and verifier.
- Application envelope: 3 (was 2), including strict stored game-over event admission.
- `engine.mission-rules`: version 2 (was 1).
- Campaign save schema/history container: unchanged.

There is no legacy reader, automatic data migration, field stripping or hash
exclusion. Old saves are refused without deleting their bytes. An unsupported
load cannot replace the active session. New-game/overwrite remains an explicit
user action.

The TypeScript v1 fixtures are historical evidence of the previous contract.
Their prose-bearing states/events and raw digests are not the native v2 oracle.
Unchanged gameplay can be compared explicitly, but must not be reported as full
state/hash parity. Current native fixtures author the new semantic inputs and
expected conclusions independently. Native save/replay checks still compare the
entire state, event stream and digest; changing presentation wording must not
change any of them. No frozen fixture is rewritten from native output.

`tools/godot/objective-outcome-checks` is the bounded external-Core consumer check.
The native BattleScreen, result-flow and campaign-navigation scenes exercise the
same services through actual controls and save/load transitions.

## Verification entry point

Run `bash tools/godot/verify-native.sh` from the repository. It builds the canonical
Core project and native application sequentially, validates catalog/source metadata,
runs the independent objective/result contract and catalog-boundary consumers,
and exercises actual battle, result and campaign navigation scenes with isolated
save directories. Native process exit and teardown diagnostics are part of the gate.

Core checks compare the opening source trace's legal actions and unchanged events;
only its intentionally replaced terminal event is compared by semantic winning team.
They also compare whole native state and digest after save/load and replay, and
replay each shipped level to its native terminal outcome within an explicit action
safety bound. Independent full levels are not carried-state campaign-victory evidence. Historical source-v1 suites retain
their original expectations and are not silently admitted through field stripping.
The old `--full` aggregate mode is removed because it mixed incompatible contracts
and repeated compilation, not because a failing current-contract assertion is optional.
Manual desktop input and freshly exported package acceptance remain separate gates.

## Checkpoint evidence, 2026-10-21

The current Core gate completed all sixteen independent shipped battles and
verified complete native save/replay state and hashes. The opening battle also
matched 119 legal source actions and unchanged events; the terminal event is the
declared semantic-schema delta. This is not a victorious carried-state campaign.
The focused external-consumer check passed, and actual headless Godot battle,
result and navigation scenes passed with clean process teardown. Navigation
includes rejected old-version saves whose bytes and timestamps remain unchanged.
The last aggregate API correction requires an admitted `CampaignBattleResult`,
not a caller-convention raw dictionary.

Manual desktop acceptance and rebuilt exports are not established by those
headless checks. Existing pre-schema-change packages are historical artifacts,
not builds of this checkpoint. Built-in content names, computed descriptions,
level titles and remaining summary wording are tracked as the next i18n slice.

## All-field digest revision

The next native contract removes both historical digest exclusions: levelName and
players[*].controller. Native gameplay state no longer contains a level title,
and every remaining serialized field participates in the digest. Changing a
controller therefore changes the digest; changing an injected wording catalog
still cannot change state, events or the digest. Replay schema3 identifies this
hash contract; battle save3 also rejects obsolete levelName in its state/header.

`tools/godot/native-digest-vectors.mjs` independently authors the current numeric,
UTF-16 and all-field vectors with ECMAScript built-ins and the specified 32-bit
hash. It is development fixture generation only, not a runtime TypeScript bridge.
The old ignored-field vectors remain historical evidence and are not counted as
current-contract passes. No source state is stripped or normalized to recover its
old digest.

### Creation-event identity facts

The current native `unitSpawned` and `recruit` contracts carry `unitType` and
`owner` copied from the created unit, in addition to the existing unit ID. These
are detached semantic facts, not localized labels. The log projection observes
creation events in order, so a unit spawned, changed and removed within one
committed action can still be described without inventing an identity or retaining
it in gameplay state. Application event admission requires the current shape.

This is an intentional event-contract change. The frozen 119-action opening has
neither event kind, so its nonterminal event comparison remains exact and needs
no field-stripping adapter. Native replay/save checks use the complete new state
and event contract; the focused transient-unit flow checks the emitted identities.

### Content, identity and all-field checkpoint verification

The final production build is clean. The focused external-consumer check covers
content metadata, semantic modifier formatting, locale independence and an actual
transient-unit scenario. Native Godot checks passed battle interaction, result
flow, staging and navigation, including four malformed creation-event loads that
preserve the active battle and original file bytes/timestamps. All processes exited
cleanly. These are actual scene executions, not manual window/visual acceptance.

All 16 independently initialized native levels completed legal play with exact
full-state save/replay round trips. The opening retained 119 source-matched legal
actions and unchanged nonterminal events. All-field digest vectors and catalog
isolation passed. The first ownership regression expected the wrong exception
layer from SaveBattle; production correctly rejected invalid live ownership. Its
expectation was corrected and that named check passed without repeating the
already-completed full-level runs. The normal verification command still runs
all checks; `Empire.Core.Tests ... --only <exact-check-name>` supports bounded
reruns and rejects unknown names. No valid assertion was removed.

Remaining work is explicit: inject the event presenter through the session
composition port, complete the API/readability/formatting pass, and verify the
current exported package. Manual desktop interaction remains infrastructure-blocked.

## Instance-name ownership, 2026-10-22

`grayflag-story.json.identityNameKeys` is the shared identity-to-message binding;
`zh_CN.po` owns each personal-name translation once. The seven character-specific
content labels reference those same messages, while generic archetype labels keep
their separate content keys. These native documents have no generator: the current
TypeScript exporter writes historical source-v1 fixtures, not native text resources.
The localization authoring guard rejects both competing bindings and resurrected
personal-name PO entries.

Battle text metadata is now schema 3. Each level explicitly declares its `actors`
map for non-roster named units. Campaign roster identities still come exclusively
from the reconstructed pending request's `rosterBindings`. `BattleTextDocument`
validates unit keys and refuses overlap between these two disjoint sources;
`BattleUnitNames` freezes the resulting projection and validates every referenced
identity against the shared name owner. No portrait, key prefix or shared unit
class determines a person's identity. Generic units retain their current type label.

Fresh, restarted and restored sessions reconstruct the same projection. HUD,
event-log full-unit records and fallen-unit summaries consume it. Instance keys
already survive active, embarked and fallen records; no identity or translated
name is added to gameplay state, events, saves or digests. Recruitment cards and
type-oriented refusals still describe content archetypes, not personal instances.

Creation events continue to own emission-time type and owner facts, including
units created and erased within one committed action. The shipped spawn effects
create generic units. This change neither adds personal identity facts to creation
events nor claims support for transient named spawns that lack those facts.
