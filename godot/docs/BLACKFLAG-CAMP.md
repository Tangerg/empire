# Blackflag camp battlefield

`c01-04` is the 50×30 forest camp in the formal GrayFlag campaign. The C04 member
of `data/grayflag-campaign-levels.json` is its only gameplay authoring source;
`data/scenes/c01-04.json` binds `data/scenes/c01-04-painted-ground.json`. The scene
opens around (19,19), with no retired tile placements, frame placements or zones.
This is an authored map and balance change, not a visual-only replacement.

## Space and ownership

Coordinates are zero-based. The 323 canonical outside cells define the irregular
1177-cell domain. Do not add a competing ground outline to hide a scene warning.
The painting is 1660×948, registered at origin (30,24), axes (32,0)/(0,30), and
uniform world scale 1.25. Its SHA-256 is
`6f8c48c500dd26fc3a735b892d646bd9ec2f7e7de2e0cbef00f12d662af17926`.
The registration checksum is SHA-256 of ECMAScript `JSON.stringify` of the C04
level object, not sorted-key JSON, the whole campaign file, or its whitespace.
Recalculate it whenever canonical C04 authoring changes.

Closed foundations belong to canonical terrain, not roof silhouettes. The four
complete house masks cover 37 cells: 33 interior walls and four cells already
outside. A foundation cell is blocked when its source-image rectangle has a
positive-area intersection with the conservative ground envelope. Boundary-only
contact does not count. Visible edges are hand-traced with approximately ±5
source-pixel uncertainty; hidden rear edges are inferred and include an 8-pixel
conservative allowance. These are not measurements of invisible geometry.

- Southwest arrival house: row 18, columns 5–6; rows 19–20, columns 4–7.
  Entrance (7,21); approach (8,21).
- Northeast rear house: row 3, columns 43–45; row 4, columns 42–46; row 5,
  columns 43–44. Entrance (42,5); approach (42,6). Cells (44,3), (45,3),
  (45,4), (46,4) remain outside.
- Woodkeeper house: rows 13–14, columns 13–16. Entrance (16,15);
  approach (17,15).
- Southeast caretaker house: row 24, columns 34–36; row 25, columns 34–37;
  row 26, columns 35–36. Entrance (37,26); approach (37,27).

Never carve a convenient door cell out of a foundation. Move its entrance onto
real ground instead. The southwest entrance deliberately shares the first cell
of the west exit. Thirteen substantial boulders have separate ground-envelope
traces covering 70 cells: 54 interior blockers and 16 already outside. Seven
additional standing cells are occupied by the archive table, witness bedding,
north and west supply tables, and the east cart. They are not whole-canopy floor
masks. The final map has 94 interior blockers and 1083 connected foot-walkable
cells. Small loose stones, shrubs and passable forest are not indiscriminate
walls. See [the physical trace audit](art-provenance/blackflag-camp-geometry.json).

The material palette edit retained the composition but regenerated texture and
fine contours. It is not a lossless color transform. Registration and house
traces were rechecked against this actual image; sampled structure drift was
approximately zero to one source pixel, within the stated tracing uncertainty.
Fresh material pieces, rather than the older broad silhouettes, provide drawing
and pointer coverage. Staging retains its separately authored narrative art; it
is not a second map-preview registration. UI panel colors and friend/enemy/healing
semantic colors are unchanged by this scene batch.

The six open field posts remain traversable at their interaction cells:
A (21,12), B (24,15), C (28,18), north supply (27,8), west aid (14,18), and east
watch (43,16). Their approaches are respectively (21,13), (24,16), (28,19),
(27,9), (15,18), and (44,16). Open canopy floors are not closed house bodies.
Each site has one canonical terrain function and one logical painted binding,
even when its material is split into roof, curtain and post pieces.

Canonical terrain owns navigation, cover and opacity. Freshly authored material
polygons own visual occlusion; independently authored pointer contours own
building hits. Neither can create a second collision map or expose a concealed
unit. See [the painted interaction contract](PAINTED-INTERACTION-CONTRACT.md).
Do not reuse coarse proposal silhouettes as accepted final draw/pointer shapes.

## Preserved mission and campaign context

Any allied unit may end an action at A, B and C in any order to investigate;
capture is not required. The three independent, one-shot triggers increment
battle-local evidence. At three evidence, the existing sequence permits at least
one of Laiya or Mirelle to reach (7,21), (8,21) or (9,21), with Laiya alive.
The retreat directive now points to (8,21). Evidence is a scenario fact, not an
inventory item carried by a particular actor. Defeating all nine enemies remains
the ordinary last-team-standing alternate victory; no duplicate objective is
introduced. Laiya's existing critical failure condition is unchanged.

The two pre-battle choices retain their exact campaign-only effects:

- `rescue-and-evidence`: set `black_camp_rescue`, add two campaign witnesses.
- `split-the-force`: set `black_camp_split`, add two campaign evidence.

Both enter a fresh battle whose local evidence is zero. Campaign context does
not grant two free investigations or rearrange the C04 starting positions.
The existing bridge projects the available Laiya, Torren, Elin and Mirelle roster
records, including persistent health, rank and resources; these remain distinct
from authored map positions. The canonical roster still has ten friendly and
nine enemy actors. No actor identity, type, allegiance, facing, formation,
commander, objective, rule or nonspatial trigger has been changed. The descriptive
`tacticalTheme` now names the open camp, split investigation and west retreat
instead of the retired ring-patrol composition.

There is no new alarm, reinforcement, prisoner actor, teleport, interaction
command, fog, hard turn limit or movement-speed rule. The recommendation remains
12 turns, not a failure countdown or a proven completion budget. Update it only
after actual route evidence, not a geometric walking estimate.

## Explicit balance changes

- The old 20×14 board becomes 50×30, with redistributed roles, forest-cost routes,
  clearings, complete house and rock foundations, seven furniture obstructions,
  and longer investigation/withdrawal legs.
- Capturable sites change from 25 to 10: field posts 19→6, outposts 4→2,
  villages 2→2. The old field posts were open, capturable facilities, not walls.
- Initial ownership remains one friendly and four enemy sites. Neutral sites
  change from 20 to 5; initial targets available for enemy capture change from
  21 to 6. Fewer diversion targets may concentrate enemy pressure.
- Both villages retain 100 funds per owned turn, 20 HP healing and 15% defense.
  Field posts retain 15 HP healing and 15% defense; outposts retain 15 HP healing
  and 20% defense. Neither post type grants income or recruitment. Total village
  income capacity remains 200, but access to healing and capture growth changes.
- Six old elevation-1 cells become elevation 0. This changes height/line-of-sight
  opportunities even though the rules themselves are unchanged.

These changes require play and balance review. More walkable area, preserved
rules and successful file loading do not establish a good pace or fair pressure.

## Saved-battle admission

This is a breaking authored-map change. An in-battle C04 snapshot from the retired
20×14 map cannot resume on the new 50×30 battlefield. The campaign bridge rebuilds
the battle request from the currently registered level; `GrayFlagCampaign.Load`
rejects the old dimensions before publishing a runtime or pending battle. The
application prepares the candidate separately, so rejection must leave the live
campaign, request, battle session, events and revision unchanged. No migration,
silent repair, compatibility path or automatic write-back is added.

A valid campaign-only save before C04 can still continue into the new map, with
its actual available and fallen roster records. A valid completed C03 campaign
was used for the desktop inspection: Torren and Elin remained fallen, producing
eight friendly and nine enemy actors rather than the nineteen-actor authoring
baseline. Current C04 in-battle saves remain accepted. The retired-level fixture
in `tests/native/blackflag-retired-level.json` preserves the exact C04 level from
commit `f2b74e5eb20265dcfc23e2b1fcc02d586dd6cbcf` for this rejection regression.

## Verification and acceptance boundary

Run `node tools/godot/validate-blackflag-camp.mjs --self-test` from the repository.
It checks shape, catalog terrain symbols, all 19 role positions, all ten sites
and approaches, complete house/rock masks and furniture occupancy, connected four-neighbor foot space,
scenario coordinates, unchanged nonspatial facts, campaign choice effects and
scene binding. Its in-memory mutations prove the guard rejects real authoring
errors. This is a static authoring check, not another pathfinding or mission
implementation. When `runtimeReady` is true it also checks the registered image
and canonical level checksum. False means binding admission is not claimed;
true alone never means route, visual or balance acceptance.

The authored-map checkpoint passed the 11 deliberate static mutations and 335
focused native assertions, including all ten site bindings, shelter mouths,
canonical foundations, mission probes and the retired-save rejection above.
The serial native build reported no warnings or errors. Desktop inspection
confirmed the actual surviving campaign roster, readable default framing,
normal zoom and pan, and one legal moving archer attack followed by F5 saving.
The 0.5 zoom overview is for spatial inspection, not the default play scale.
These results do not establish a completed natural route or twelve-turn balance.

Pending runtime acceptance consists of two normal-command routes:

1. Split investigation and withdrawal. Exercise both pre-battle choices and
   different A/B/C orders. Have Laiya or Mirelle personally visit an investigation
   point before withdrawing. Retain at least one enemy so elimination cannot
   mask extraction behavior; verify Laiya's loss still fails the mission.
2. Frontal advance and elimination. Defeat all nine enemies normally; check
   concentrated AI pressure, capture progress, owner-turn healing and village
   income, save/load continuity, and campaign settlement.

Use real mouse input for legal roads, entrances, visible bodies, roof-covered
ground, foreground/background material, selection rings, zoom and pan. Walk the
entire closed-foundation perimeter and test open shelter mouths. Check one
logical object and banner per site. Keyboard navigation or directly dispatched
engine commands cannot substitute for mouse acceptance. Record actual turns,
combat pressure and empty-walk time; do not describe static connectivity or
positioned engine fixtures as natural-play or balance passes.
