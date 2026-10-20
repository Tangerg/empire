# Native battle feedback

The original descriptive event registry is ported independently of rule execution.
The session resolves names through the canonical content/resource owners and
computes one immutable description batch from the committed final state, exactly
as the source controller creates one log context for each order. A departed unit
is described as `单位`, not reconstructed from an invented previous-state name.
The board emits a notification after each event's animation. The screen then adds
its corresponding line to the single bounded/coalescing `BattleLog` owner and
shows its last four entries, matching the current source HUD.

Initial battle binding uses the source deployment/opening line. Restart clears
messages without replaying that opening. Loading clears old messages and shows
one load line, rather than replaying saved historical actions or effects. Save
and failed-load notices append their source messages. Removed screens cannot
submit orders or trigger stale Save/Load/Restart callbacks.

The campaign application obtains result signals/outcome from its existing bridge.
`BattleEventFacts.StrikeCount` is the one core owner of the source payload-shape
rule (both attacker and damage keys), not a presentation list of attack names.
The result projection includes turn count, perspective-player survivors, enemy
team survivors, fallen-unit names and ordered scenario signals. Source victory
headings/details are shown only for a canonical victory; defeat is never labeled
as success. Summaries do not decide outcomes or advance the campaign.

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

## Evidence

`bash tools/godot/feedback-checks/run.sh` passes 1,320 source comparisons, including
647 descriptions, six log sequences, five result projections and canonical label
queries. Those include 142 actual replay actions. The original feedback/campaign
source suites also passed 35 tests; a corrupted oracle was rejected.
Native controller checks verify delayed text delivery, exact ordered log contents,
no repeated log on refresh, load clearing and stale-callback rejection alongside
the existing animation cancellation checks. The application continuation check
compares postbattle summaries before and after a saved-game continuation.

This establishes data and headless UI contracts, not a new desktop visual
acceptance. The existing desktop responsiveness gate remains pending. See
`../../tools/godot/feedback-checks/README.md` for source
references and isolated helper scope.
