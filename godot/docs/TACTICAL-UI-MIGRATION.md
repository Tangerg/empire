# Tactical UI migration contract

Audit: 2026-10-20. This is an implementation plan based on the inspected shared working tree, not an implemented-feature or GUI-acceptance claim. Only this document was changed for this audit. Scope is the original GreyFlag campaign and its native tactical interface; other themes, editor and engine-demo screens are not part of this batch.

## Finding and smallest complete batch

The native engine already executes the relevant actions. The missing vertical slice is the player's composition of an order and the facts needed to understand it. `GrayFlagSession.GetLegalOrders` currently expands every movement origin, command and target into complete actions. `BattleScreen` groups these by a coarse role and then asks the player to confirm one of the actions at a target. That is not the original destination-first menu. It also discards rejected support options, most content descriptions, command keys/hints, and source interaction state.

Implement one cohesive **destination selection, tactical read models and undo** batch:

1. Close the small core query gaps below and add handler-owned handoff metadata and battle-owned checkpoint restoration.
2. Replace the all-origins menu consumer with one source-derived selection model and revision-bound session queries. Preserve complete-action dispatch and immutable presentation receipts.
3. Wire destination menus, forecast and overlays, inspection, objective/account/status information, support/recruit/deployment panels, keyboard/back/undo, and the application AI delay into that same slice.
4. Compare executing original selection/HUD/session behavior and run native interaction acceptance. Do not describe a DTO-only or core-only implementation as a complete tactical UI.

These are sequential implementation checkpoints inside the batch, not alternate controllers to retain. Existing generic orders may remain during development only until their consumers/tests are migrated; remove the superseded path rather than maintaining both interaction semantics.

## Existing owners and missing boundaries

Paths below are relative to the repository. API names marked **proposed** are a plan, not current exports.

| Fact or behavior | Original owner | Native owner to use | Remaining work |
| --- | --- | --- | --- |
| Destination commands, key/name/hint, weapon, self-targeting, target ordering | `packages/battle-engine/src/session.ts` delegates to engine; `packages/game-ui/src/ui/hud.ts:renderCommands` renders | `godot/Empire.Core/Engine/GrayFlagEngine.cs:CommandsAt`, `CommandOption` | Already present. Project the ordered result at one chosen destination, preserving `Key`, `Hint`, `Weapon`, `SelfTargeted` and `Engagement`. Do not re-sort by widget role or merge distinct weapons/origins. |
| Movement, path, threat and hostile control zone | Session spatial queries; `selection.ts:paint` | `MovementRules.ComputeMoveField`, `PathTo`, `ThreatTiles`, `HostileControlZone` | Session/overlay projection only. Cursor and path are UI intent; reachable cells are core answers. |
| Forecast and area preview | `game.ts:hudView`, `selection.ts:TargetSelection.paint`, `hud.ts:renderForecast` | `BattleCombat.Forecast` returns `CombatPlan`; `CombatForecasts` computes exchanges | Already present. Project plan plus attacker, intended defender and actual damage recipient. No damage calculation in controls or renderer. |
| Visibility and inspection eligibility | `game.ts:isVisible`, hover/click inspection | `BattleVision` | Reuse the same viewer and visibility answers as the board; never expose a hidden unit through hover/forecast/readout. Public cast telegraphs remain public. |
| Unit/type/weapon/status/terrain/structure names and definitions | Composed `rules.content`, art provider chain | Engine-owned `StateContent`, existing asset bindings | `GrayFlagEngine.Content` is internal and existing public name helpers cover only unit type/career. Add a detached read-only content projection from that composed catalog, not a presentation-owned catalog reconstructed from another pack array. Art keys remain asset references, not a second names table. |
| Careers and formations, current/unlocked/mastered flags and reasons | Session options; `hud.ts:unitControls` | `CareerRules.Options`, `FormationRules.Options`, `Active` | Existing eligibility/reason answers. Stop filtering ineligible entries. A current formation must still offer removal even when its formation is no longer eligible. |
| Carrier refusal and blocked disembark | Session options; `hud.ts:renderTransport` | `BattleTransport.CarrierOptions`, `PassengerOptions`, `PassengersOf` | Existing queries. Preserve rejected nearby carriers and passengers with zero spots; source shows the reason or no-space message. |
| Deployment roster, zones and bidirectional swap | Session deployment queries; selection/deployment HUD | `DeploymentSystem.Roster`, `Spots`, `Refusal` | Existing queries. Show roster order and hovered swap identity; do not independently test the reverse swap in the widget. |
| Recruitment cards versus executable recruit orders | `packages/battle-engine/src/state.ts:recruitOptions`; `hud.ts:renderRecruit` | `RecruitmentOrders.Refusal`, `Options`, `BattleResources` | A real query gap: native `Options` filters out every refused type, including unaffordable ones. Add a catalog-ordered card query at `RecruitmentOrders` preserving owned-site offers, copied costs and affordability, separately from full command refusal. |
| Tactic availability and cost | `tacticOptions`; idle-only `game.ts:hudView` | `SupportOrders.Tactics` | Availability/targets exist; `TacticOption` omits costs. Include detached costs from the same tactic definition. Source does not show unavailable tactics as disabled, so do not invent that menu. |
| Player/unit/weapon accounts | `BattleResourceSystem.inspect`; HUD `accountSummary` | `BattleResources.HasAccount`, `Balance`, `Name` | Add read-only account inspection exposing current and capacity, plus canonical enumeration order. Preserve null current as infinity and null capacity as unbounded. Do not read three storage layouts independently in UI. |
| Rank next threshold | `rules.progression.nextThreshold` | `CombatEffects.AwardRankProgress` owns native rank thresholds | Extract a single progression query used by awarding and inspection. Do not copy 120/320 into HUD. |
| Commander identity, presence and aura applicability | `commanders.ts:commanderUnit`, `activeCommanderFor` | `TacticalModifiers.ActiveCommander` / `CommanderAura` (internal) | Expose a read-only query through the existing rule boundary. A multiplier of 1 does not prove no active commander; preserve identity/presence/range facts. |
| Objectives, descriptions and progress | `objective-system.ts` handlers; `victory.ts:objectiveProgress`; `hud.ts:renderObjectives` | `Lifecycle/VictorySystem.Outcome`, `Status`, `Describe`; `Scenario/CompositeRules` | Add canonical progress projection at these owners, not a second objective-type switch in the session/widget. Expose the configured objective owner from engine composition; never construct an independently configured victory system in UI. |
| Initiative preview and actor cycling | Engine turn order/actors; `game.ts` | `TurnOrderSystem.Preview`, `Actors`, `MayAct` | Existing queries. Preview six entries only when `activeUnit` is non-null; side-turn armies are not an initiative strip. |
| Active cast identity, remaining turns and progress | `activeCasts`, `SpellCastEntity.remainingAt/progressAt` | `BattleCombat.IncomingCasts`, existing cast execution | Incoming cells exist, but no reusable named cast/progress query. Add one shared cast-clock projection and consume it in telegraphs/HUD, keeping dead-caster filtering, validity checks and overwrite ordering. |
| Undo checkpoint and history truncation | `packages/battle-engine/src/session.ts:dispatch/undo/load/restart` | `Transactions/Battle` owns state; `GrayFlagSession` owns action/event histories | Actual missing capability. Add battle-owned restore of a trusted detached receipt checkpoint; session owns the undo stack and history lengths. UI must never replace snapshot state or reverse animations as an undo. |
| Whether an action hands off a turn | `action-system.ts:ActionHandlerRegistry.handsOffTurn` | `BattleActionRegistry` currently stores delegates only | Actual metadata gap shared by undo and AI pacing. Store handoff semantics with each handler and carry the resolved fact in the successful receipt/transition. Do not infer it from action strings or differences in current-player values. |

## Proposed session contract and ownership

Keep `Battle` as the only owner that advances/restores battle state. Keep `GrayFlagSession` as application adapter/history owner. Keep one local tactical selection model in presentation, replacing `_selectedId`/`_target`/`_role` as independently advancing representations. A selected unit ID, chosen destination/path, chosen option key, inspection ID and cursor are interaction state, not editable battle entities.

Extend `IBattleSession` with a small cohesive read contract rather than publishing mutable core objects:

- **Proposed `GetTacticalView(intent)`**: returns revision, phase/input eligibility, commandable/inspected identity, ordered destination options, overlay sets/path/cursor, optional forecast, support options, recruit/deployment view, objective/player/account/cast/initiative rows and the selected content definitions. `intent` contains only selection IDs/cells/keys and hover cell. The session resolves current authoritative entities and returns detached values.
- The read model must retain semantic facts rather than just flattened labels: command key and weapon, target set, eligibility/reasons, current/unlocked/mastered flags, actual damage recipient, ordered modifiers, account nulls and costs. Presentation applies source wording and layout to those facts. Keep original Chinese text/content and verified original assets.
- **Proposed order composition**: query/resolve one complete canonical action from the selected unit, destination, option key and optional target. The session builds paths/actions from canonical queries; a self-target command has no invented target. Existing `Dispatch(action, expectedRevision)` remains the only player commit boundary and revalidates in core.
- Bind option buttons/keyboard intents to the view revision and current screen binding. A stale callback cannot use a newer displayed revision with an older option. Query failure returns no answer; malformed caller state/invariants must not become a silently empty menu.
- **Proposed `CanUndo` and `Undo(expectedRevision)`**: reject stale/busy/controller-inappropriate UI requests; restore through `Battle`, monotonically increment session revision, truncate semantic events and action history, invalidate projections and cancel stale presentation. Source cosmetic recent messages are not its semantic log: it appends `已撤销上一步` rather than replaying or deleting already displayed messages.

A broad JSON snapshot can remain for existing board rendering, but adding another mutable battle aggregate or content catalog to satisfy these queries is not part of the design. Queries should be derived from one snapshot/revision; do not combine facts from snapshots taken before and after a dispatch. Caching is optional and justified only by measurement; any cache must be revision-bound.

## Exact interaction behavior to preserve

The source is `packages/game-ui/src/ui/selection.ts` plus `game.ts`; inspection is separate from command selection, not an extra gameplay phase.

- Resting selection is `IDLE` or `DEPLOYING`. Clicking a commandable unit selects it. Hover/click may inspect a visible non-commandable unit without arming it. A meaningless click returns to resting selection according to the current selection's transition.
- A unit selection paints move stops, threats excluding move cells, hostile control zones and the cursor's canonical path. A reachable destination opens its command menu; if it has exactly one self-targeted option, dispatch immediately instead of showing a redundant menu.
- Choosing a self-targeted option dispatches immediately. An option with one target also dispatches immediately; multiple targets enter `TargetSelection`. Clicking a valid highlighted target dispatches, while an invalid target click returns to idle. Do not retain the current mandatory confirm-button step and call it source parity.
- Clicking a visible enemy from unit/destination selection chooses the source best firing spot and arms a target selection. Port `bestAttackSpot` as interaction policy consuming canonical move/command/forecast facts, not as another combat rule. Preserve its exact score, move-stop/option traversal order and strict-greater tie handling. It prefers strike damage, collateral and terrain defense, penalizes counter damage and moving. It is not the AI planner's choice function.
- Back from target returns to destination; back from destination returns to unit; back from unit returns idle; back from deployment unit returns deploying. Tactic/disembark/recruit use their source default back behavior. Right-click and Esc use this one operation, not a global selection wipe. Clear inspection when returning to the source resting selection.
- Tactics appear only on human turn, not busy, with selection exactly idle. Unit selection alone hides them. A single tactic target dispatches directly; multiple targets enter tactic targeting.
- Disembark paints helpful target cells. Deployment uses one selection path: roster pick, eligible spot, immediate deploy/swap, then deploying. Hovering an eligible occupied spot names the swap partner.
- Use engagement metadata to choose hostile/helpful overlays, not `ability == "attack"`. Only weapon-bearing target selections request an area plan. Source forecast panel requires a hovered valid target, a defender unit and a primary unit exchange; source has no structure-only forecast panel, though its plan and area overlay contain structures. Adding one would be an explicit enhancement, not parity.
- While busy, preserve public fog/cast/cursor information but do not show active selection overlays or enable tactical input. Reuse existing presentation/binding-generation guards so a stale callback after load/restart/exit cannot commit.

## HUD details and source quirks

### Forecast, inspection and map facts

Render intended target and actual interceptor recipient separately. Show predicted strike/counter damage, recipient/attacker HP after and death flags, reaction/interception text, support damage, collateral counts, and ordered attack/counter modifier chains with mitigation. Do not recompute HP after by subtracting the displayed damage: the plan already owns reaction/support sequencing.

Unit inspection includes original name/art/team/blurb, current/max HP, definition stats, damage/armor/movement names, weapon accounts/cooldown/requirements/costs, status name/remaining/stacks, rank/progress, facing, career/mastery, formation state and commander status. Current reaction/facing buttons are visibly selected. Career/formation/carrier reasons remain visible even when action is disabled. The source currently derives the inspected formation's “holds” label from the commandable unit's formation options; fixture this behavior and explicitly classify any correction, rather than silently claiming exact parity from a more sensible label.

The tile panel uses the base terrain definition's defense, cover, movement-profile costs, heal and owner-turn grants plus map elevation and owner; it does not silently substitute effective overlay/structure combat totals. `TacticalBattlefield` remains the owner of effective traversal/cover if an additional effective readout is later wanted. Show structure name/owner/HP and movement/vision blocking, cover, repairable, targetable and disabled facts. Do not confuse those source fields with a complete tooltip for every scenario entity.

### Objectives, economy and casts

Choose the first human player for objectives, otherwise source `players[0]`; filter using runtime `objectiveStates[id].hidden`, retain authored order and use authored label or the canonical description/progress. Native composite ID/status text is not a substitute. Objective progress includes terminal/inactive/cancelled handling and nested objective semantics from the original handlers; a generic completed-count string is insufficient.

Show level name, turn and optional limit, current player's name/color/controller and accounts. The player-strength list counts units per owner and shows their account summaries. This is a source HUD policy, not a new inferred fog rule. Active casts from both sides are public; show weapon name, target, remaining actor turns and progress with the same clock as telegraphs.

Recruitment is a notable source/core distinction: source `recruitOptions` lists all types produced at an owned site and marks only affordability; its ordinary selection path excludes occupied tiles first. Native `RecruitmentOrders.Options` returns only fully legal types. Keep a card query distinct from executable orders, preserving source affordability semantics. Do not silently redefine the card query as `Refusal == null`. Core still validates occupancy, cap, funds and ownership on dispatch; additional disabled reasons beyond source behavior need an explicit intentional-delta fixture.

## Keyboard, undo and AI pacing

Keyboard precedence comes from `game.ts:onKey`: ignore disposed/busy; Esc first; other shortcuts only on human turn; E ends turn; U requests undo; Tab/Space cycles `Turns.Actors` in existing order; A/C/H/W choose destination commands before WASD cursor movement; arrows/WASD move cursor; Enter follows the same click path. Choose the first matching ability when a shortcut omits a weapon key, exactly as source `chooseCommand` does. Do not sort actor IDs or pick a random weapon.

Cursor movement asks the configured grid for each direction's step and takes the in-bounds candidate with greatest positive directional projection, retaining first ties. It is not `x += dx` on every tiling. No cursor/selected/inspected origin falls back to idle-unit cycling. Reveal/center the resulting cell through existing camera owner; do not create a second transform controller.

Undo checkpoints contain `receipt.Before`, prior semantic-event length and native action-log length. Push only after successful non-handoff dispatch. Any successful handoff clears the stack. Source `canUndo` is false after battle over; load/restart clears history. Keep the current native application's persisted event-history behavior explicit: native manual resume already restores recorded events for campaign summaries, unlike original standalone session load. Clear undo on resume without accidentally deleting that established campaign accounting. No inverse-action dispatch, old state revisions or undo of partially presented rules are allowed.

Native `GrayFlagGame._Process` currently waits 0.3 seconds before each AI commit and waits for screen presentation settlement. Source settles an action and then sleeps 90 ms normally or 120 ms when the action handler declares handoff. Replace the fixed delay using the same handler-owned fact used by undo. Match delay placement as well as duration: do not add an unexplained 90 ms delay before the initial AI action. Preserve settlement, scene-generation and result guards. Original refused AI suggestions catch only `IllegalActionError`, show the refusal and try `endTurn`; native `AdvanceComputer` currently does not reproduce that recovery. Port the bounded refusal path without swallowing invariant errors, and test which attempted/executed action supplies the source delay on that path.

## Verification and acceptance contract

Add a dedicated source-to-native tactical UI harness rather than citing engine parity counts as UI proof. Follow existing `tools/godot/*-checks` live TypeScript oracle patterns and `BattleScreenChecks` for Godot input integration. Compare structured facts plus rendered text/disabled states, not just snapshots or hand-written expected summaries.

Required cases:

1. Idle/unit/destination/target/back transitions; invalid clicks; self-only/single-target auto-dispatch; two weapons at one destination; duplicate target reachable from several origins; enemy shortcut tie order; stale option and duplicate click.
2. Hover paths, hostile zones, enemy inspection threats, hidden units, helpful/hostile targeting and area coverage; busy overlay/input gates and cursor leave.
3. Interceptor versus intended defender, counter/reaction/support, friendly collateral and structure collateral, death flags and ordered modifier chains; no invented structure-only panel.
4. Unaffordable recruitment cards, owned/non-owned/occupied sites, cap refusal, null/infinite/capped accounts, weapon requirements/costs/cooldowns and source card ordering.
5. Career locked/mastered/current choices, formation eligible/current-invalid removal, full/tag-refused carriers, zero disembark spots, deployment restricted zones and bidirectional swap identity.
6. Hidden/revealed/inactive/completed/failed/cancelled objectives, nested sequence/all/optional/fail-on and composite progress; named active cast progress, dead caster exclusion and overlapping incoming-cell precedence; side versus initiative preview.
7. Every keyboard shortcut and precedence, no-origin cycling, square4/square8/hex direction and tie behavior, camera reveal, keyboard versus pointer same resulting action.
8. Successful undo restores exact state digest including RNG/accounts/scenario/transport/casts; truncates events/actions; failed commands add no checkpoint; handoff clears stack; load/restart clears undo; terminal phase disables it; stale revision and stale animation cannot restore/commit.
9. AI fake-clock checks at 89/90 and 119/120 ms after settlement, no commits while presenting, initial-action timing, refused-order recovery, invariant exception propagation and cancellation after exit/rebind.

Include deliberate mutants: drop refusal rows, sort weapon keys differently, use defender HP instead of recipient HP, hardcode action-kind handoff, leak hidden objectives, copy rank thresholds into UI, or permit stale callback dispatch. Each guard must demonstrably fail when its protected behavior is broken.

Run the affected core/application/presentation suites and exact-tree aggregate command documented by `tools/godot/verify-native.sh`; do not change canonical battle hashes to accommodate a view bug. Then perform real Godot interaction from a normal campaign entry: choose/move/back, hover forecast, use support controls, keyboard cycle/command, undo, end turn through actual AI/animation, save/load/restart, deployment and later-map objective/cast inspection. Record window sizes, fixture versus naturally reached state, actual results and remaining visual differences. No new test run or GUI acceptance was performed by this documentation audit.
