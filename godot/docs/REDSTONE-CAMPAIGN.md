# Redstone campaign battlefield

`c01-03` is the 101×52 painted battlefield in the formal GrayFlag campaign.
`data/grayflag-campaign-levels.json` owns its gameplay; `data/scenes/c01-03.json`
binds the registered painting. There is no separate Redstone sample or F6 entry.
Run the project normally and continue through `c01-02 → brief-03 → battle-03`.
Campaign roster health, rank, resources and unavailable/dead characters are
projected by the existing campaign bridge, not replaced with a sample roster.

## Mission and interaction

- Win by capturing the enemy HQ **or** neutralizing the control chain. Laiya's
  death takes precedence and defeats the mission. Turn 18 remains playable;
  if no mission succeeds, the normal limit ends the battle on entry to turn 19.
- The `redstone-node` structure owns the chain fact. Its existing one-part
  composite projects disabled/destroyed status for both the objective and the
  release of surviving oathbound. There is no `gate_breached` victory variable.
- Any friendly P1 unit can close the node by reaching (80,24). Destroying the
  node and defeating the gate shield are also explicit solutions. Dead units
  are never revived by the release.
- From turn 15, a living P1 engineer at the inner south village (78,30) connects
  waiting support and cuts the chain. An engineer already present triggers at
  the T14→T15 handoff; a late arrival triggers after its command. The ordinary
  one-shot trigger ledger prevents repetition. This deliberately replaces the
  old unconditional turn-15 rescue. Presence, not capture, is the support condition.
- The village retains ordinary capture, 20 HP healing at the stationed owner's
  next turn, 100 funds income and no recruitment. Walking onto a village is not
  a capture action. The initial friendly outpost at (38,23) retains 15 HP healing
  and no recruitment. No site gained invented recruit options.

Click a site to pin its owner and use instructions while moving into the HUD.
A visible actor body selects that actor; the independently visible site facade
selects the site, without inferring an occupant from its cell. Click another site
or actor to switch; right-click, Esc or the close button clears it.
Drag or scroll to pan, and use Ctrl/Cmd-scroll or −/+ to zoom. The default camera
remains 1.25; map scale, actor proportions and the original painting are unchanged.

## Rendering ownership

`data/scenes/c01-03-painted-ground.json` registers
`assets/native/scenes/redstone.png`, whose SHA-256 remains
`0557263dd9d53f3cb95a357586975dec24aa37bd0806eb6026ef8407db3181d1`.
Its source-image material polygons own visual occlusion. Independently authored
pointer contours own building targets; typed ground hits cannot reveal a hidden
occupant. Neither polygon owns movement legality or combat state. See the
[painted interaction contract](PAINTED-INTERACTION-CONTRACT.md). The level checksum uses canonical JSON,
not whitespace. Fixed painted terrain cannot support arbitrary elevation editing.
All new mission, support and objective-progress text is in `Localization/zh_CN.po`.
Alternative (`any`) progress says “任选其一”; it is not a mandatory stage sequence.

## Save contract

Campaign-only progress before battle 3, including the automatic staging slot,
continues by projecting the new canonical level and the saved roster. A camp
save has no live battle and similarly rebuilds the new battlefield. Active old
22×15 battle-3 application saves are rejected by the existing level-dimension
admission check, without changing the current session. No migration or fallback
is provided, and this change does not delete user files. The retired standalone
sample's files may still exist in its old user directory, but no shipped entry
loads them. Other battles and completed campaign history are unchanged.

The painting checksum protects authored registration, not battle-save identity.
Battle saves validate their self-contained state and ruleset; exact replay checks
its initial-state digest. Neither is a compatibility layer for the retired map.

## Verification boundary

Focused native checks cover each P1 node arrival, attack destruction, shield
casualty, HQ capture, failure priority, living-only release, T15 presence/absence/
death/departure/late arrival, once-only behavior, T18, site capture/recovery,
ordered events, save continuation, undo and exact bounded replay. Such positioned
and wounded fixtures are semantic checks, not naturally played assault routes.

Formal entry/continue/settlement and two full intentional GUI routes (front gate
and south-village support) require their own runtime evidence. Data integration
alone is not route, balance, visual-clearance or performance acceptance. The
known cloud llvmpipe limit is not evidence of hardware-GPU performance and cannot
excuse an observed gameplay failure. The AI's composite advisor targets attackable
parts; it does not yet choose the authored shutdown or support route deliberately.
