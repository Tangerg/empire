# Native battle feedback

The session's descriptive event registry is independent of rule execution. It resolves
names through the composed identity/content owners, using detached before/after
snapshots and observed creation events. Missing identities fail explicitly; they are
not replaced by invented generic names. The board publishes each description after
its event animation. `BattleLog` alone owns ordered retention: 60 consecutive runs,
consecutive-repeat compression, and silent empty descriptions.

The battle screen exposes that log directly in the top bar and through the system
menu. A bounded scrollable, selectable plain-text journal preserves full receipts;
localized close and Esc do not dispatch to the board. The latest nonempty event is
also shown as a short toast. An empty event never erases a readable prior toast.
Attack, counter, support, parting and healing receipts identify both participants.
Forced movement and collision are distinct committed receipts. Collision HP comes
from its explicit `hpAfter`; ordinary strike events do not invent an intermediate
HP from the final batch snapshot.

New binding clears history and installs the supplied opening/load line. Refresh does
not replay old events. Removed screens cannot submit orders or trigger stale
Save/Load/Restart callbacks. Refused input is shown on a contrasting panel; a new
successful selection or cancellation clears it. Terminal conclusion feedback is not
silenced by ordinary selection cleanup.

The application receives outcomes from its existing campaign bridge.
`BattleEventFacts.StrikeCount` owns strike payload classification (attacker + damage),
not a UI list of event names. Result projections show actual perspective survivors,
fallen identities, ordered signals and canonical outcome; presentation never decides
victory or advances the campaign implicitly.

## Attack preview ownership

`TacticalSelectionController` owns an explicit `PreviewTarget` separately from its
live activation cursor. Clicking an enemy to arm an attack, or hovering another valid
target, updates the preview. Traversing ordinary ground to reach the HUD keeps that
preview visible without making Enter on that ground a combat command. Held-preview
wording directs the player back to a highlighted target. Cancel, new revision,
rebind, excluded/outside map, window exit and focus loss clear the appropriate context.

The HUD renders the same session projection: primary strike HP, counter, interception,
reaction, support, collateral and death facts. Extra effects are visible, not hidden
in tooltips. The primary HP bar is labeled as the primary strike, not a computed
whole-action final HP; force-move/collision forecasts remain outside the core forecast
contract. A direct calculation-details entry expands the existing modifier projection.

Map-to-HUD ownership is resolved after native input dispatch, because Godot clears the
old hovered control before `MouseExited` and installs its replacement afterwards.
The deferred boundary is invalidated by newer map input, focus/window exit and binding
changes. The HUD does not cache a competing target or recompute battle rules.

## Campaign navigation

The interactive shell deliberately distinguishes core campaign results from user
navigation. The original campaign shell commits only a human victory after the
explicit `结算战果` action. The native completion view therefore offers retry and
camp exit for all outcomes, and settlement only for victory. Settlement commits
once before showing the immutable metrics/signals/fallen summary; its next action
opens the following story node. Defeat does not silently mark the campaign failed.
The existing autonomous campaign-route oracle still exercises legitimate core
failure results, which are not a specification for the interactive shell.

Retry reconstructs the same contextual initial battle request and does not mutate
prior campaign choices. Camp exit discards in-battle progress but retains that
request; reentry starts it again. The native application save's optional `atCamp`
field preserves this navigation state. Old native saves without it remain battle
saves. This native mid-battle save format is an application extension, not the
original browser campaign-shell storage format.

Staging projects the source roster filters, resources, relation ordering and
labels through an immutable adapter. Original portraits, chapter and briefing
images are exported with their source paths and SHA-256 hashes. No new artwork
or scene authored for missing recovered levels is substituted. Presentation
callbacks are single-use and invalid after rebinding/unmount. Save/load are
blocked during an in-flight animation batch, like the source controller.

## Evidence and historical boundary

Current acceptance is the native scene/selection contracts: `BattleScreenChecks`
(receipt identities/order/lifetime), `CampaignUiFlowChecks` (long journal, keyboard,
window bounds), `CameraIntegrationChecks` (actual viewport pointer transfer and stale
boundaries), `TacticalHudChecks` (visible prediction facts and expanded details), and
the pure `tactical-selection-checks` controller harness. See the UI checkpoint report
for executed results and GUI evidence; headless assertions are not visual acceptance.

The older `tools/godot/feedback-checks` source differential records the former
TypeScript wording, payloads and identity fallback behavior. Its historical 1,320
comparisons are provenance, not a current native PASS. Current localization and
identity contracts intentionally diverge, including the newly explicit collision and
forced-movement presenters. It is not part of `verify-native.sh`; do not weaken current
identity admission or remove native receipts merely to reproduce the old source text.
