# Silverwood remaining rule-building overlap: read-only audit

Conclusion: the three overlaps follow the existing standing-depth contract. The
engine already supplies a deliberately quiet above-scenery cue for a visible
selected actor, and the actual GameController input path restores inspection and
selection without moving the rule buildings. No production change is recommended
for this bounded audit. This does not mean the three idle actors are fully legible.

## Exact identities and instances

All coordinates below are logical world coordinates before the viewport offset
(74,62). The same-camera complete board is 916×648.

| Actor | Actor cell / world foot | Covering rule village | Visual instance / world foot | Raw sprite coverage |
|---|---|---|---|---|
| actor:12, controlled-longbow-1, c01.silver-longbow, owner2 | (14,5) / (464,191) | village (15,5), owner0 | terrain:3 / (496,191) | building61.2945%; total69.7712% |
| actor:13, controlled-longbow-2, c01.silver-longbow, owner2 | (14,9) / (464,319) | village (15,9), owner0 | terrain:5 / (496,319) | building61.2945%; total63.7379% |
| actor:6, campaign-tasha, c01.tasha, owner1 | (4,8) / (144,287) | village (5,9), owner0 | terrain:4 / (176,319) | building43.5649%; total54.5988% |

The shared source is C01-STRUCT-JOINT-GRANARY (三方共管粮仓), normal frame0 of
runtime-approved-v5/structure/struct-joint-granary@2x.png. The logical source frame
is172×172, scale0.6, anchor(87.4482758621,154.9793103448). Its local transform is
translate(-36.4689655172,-61.9875862069) scale(0.6). The full canvas draws at103.2².
Measured nonzero-alpha world bounds (exclusive right/bottom):
- terrain:3 [451,113,539,195]
- terrain:5 [451,241,539,323]
- terrain:4 [131,241,219,323]

These are capture/economy terrain villages, not extra entries in state.structures.
Their one rule cell remains traversable at cost1 for foot/mounted/heavy/flying,
with defense0.15, capture, income100 and heal20. The art record's visual footprint
[2,2] does not create a2×2 collision or LOS blocker. Mother-root is a separate
rule structure and is not implicated here.

## Depth interpretation

`BoardDepthOrder.ordered` sorts standing pieces by footY, then footX, then source
layer/index (packages/game-ui/src/art/board-surface.ts:188–198). Buildings and
actors use CELL_FOOT=(16,31); terrain artwork explicitly declares that foot in
candidate-01-runtime.ts:176–181.

Tasha is northwest/behind her covering village, so its later draw is spatially
coherent. Both longbows share a footY with the village immediately east; the
rightward village wins the defined X tie-break. That is not a mistaken sprite
sort or changed rule coordinate. Its broad canvas extends into the west actor's
cell. A correct sort does not by itself make that passive composition ideal.
The renderer intentionally permits normal occlusion and provides a separate
selection cue rather than forcing every actor into the foreground.

## Actual interaction evidence

`tools/art/review_silverwood_interaction.ts` mounts the real GameController and SvgBoardSurface from main and
sends scene-coordinate moves/presses through the surface's actual pointer
contract. It does not call private handlers or assign selection state. The
opening announcement is allowed to finish before capture. No gameplay action is
dispatched by these selection/hover inputs. The output is offline JSDOM/SVG,
not a live browser or GPU screenshot.

`interaction.json` records these observed results:
1. Idle: zero selection cues
2. Hover longbow(14,5): HUD “银林长弓 / 敌对部队”,76/76, one cue at(448,160),opacity0.12
3. Hover longbow(14,9): same HUD/HP, one cue at(448,288),opacity0.12
4. Hover Tasha: HUD “塔莎 / 灰旗军”,92/92, no cue
5. Click Tasha: one cue at(128,256),opacity0.12; she becomes the commandable selection
6. Keep Tasha selected and hover longbow: HUD changes to the longbow; the sole cue
   intentionally remains on Tasha, preserving the current command selection

Complete actual full-board evidence:
- hover-longbow-1@1x.png / .svg.gz
- selected-tasha@1x.png / .svg.gz
- selected-tasha-hover-enemy@1x.png / .svg.gz

Sources and code contracts:
- board.ts:296–329 maps scene pointer position to the tactical cell, not sprite
  pixels. Building artwork therefore does not steal the neighboring actor's hit target
- game.ts:370–376 sets inspection only for a rule-visible occupant
- game.ts:418–429 routes cell clicks through Selection, then updates visible inspection
- game.ts:805–821 gives selected friendly units priority; without one, inspected
  enemies supply selected coordinates and threat coverage
- hud.ts:662–692 renders the inspected actor portrait, name, team and HP
- board.ts:522–536 applies hidden before selected; board.ts:557–562 places cursor
  and selection rings above the field
- DrawingSelection.shown in board-surface.ts:250–260 is selected && !hidden
- SELECTION_CUE_OPACITY=0.12 and the pale-gold matrix are shared by both renderers
- svg-board-surface.ts:244–278 copies the already-clipped current actor frame into
  effects, follows its facing/transform/opacity and adds no second animation clock
- BOARD_LAYERS places effects above standing objects and cursor above effects
- game.ts:314–326 and cycleIdleUnit:514–525 support keyboard cycling; arrows and
  Enter provide an alternative to discovering the partly occluded sprite itself

Existing tests passed in the preceding40-test focused run:
`two-backends-agree.test.ts` checks shared SVG/GPU cue geometry/current animation,
hidden precedence in either state-update order, removal/disposal, and actual
BoardView hidden-selected behavior. This is a structural backend contract, not
proof of live GPU pixels.

## Minimum next step, only if product requirements change

For the present requirement, preserve the current depth and cue behavior; no
building movement, global shrink, transparency or hidden-enemy reveal is needed.
The faint cue plus cell ring and HUD recover operability, but the cue intentionally
does not make the idle full-color actor readable.

If “show the hovered visible enemy while a friendly unit remains selected” becomes
an explicit requirement, add a distinct inspect/hover cue as presentation state.
Do not hijack o.selected or the command selection, and do not infer visibility
from sprite occlusion. The visibility predicate must remain visible && inspected,
with hidden winning every update order. Tests must cover active friendly command,
enemy hover/leave, fog loss, stale inspection, death/removal, repeated moves,
current animation frame, no second clock and SVG/GPU parity. This is a separate
bounded UX change requiring review, not part of this read-only audit.

No production files, actors, structures, map cells, source pixels or rules were
modified by this audit. The three representative full frames, compressed original SVGs and input/source-hash JSON are retained here. SVGs retain their capture-time relative source paths; rerun the tool for portable paths in a new output directory.

## Reproduction and verification

Run from the repository root:

```sh
INTERACTION_REVIEW_OUTPUT=/tmp/silverwood-interaction node_modules/.bin/vite-node tools/art/review_silverwood_interaction.ts
```

The tool writes six observed input states and three complete SVGs, and asserts
the actual HUD/cue contract. It reads source PNGs and hashes them without editing
assets. Rasterize those SVGs with the repository's offline SVG review workflow
if PNGs are needed. The retained PNGs are the reviewed full main-scene captures.
The checked-in tool was rerun successfully: all six reports matched the original
capture exactly. `npm run typecheck` passed.

The JSON keeps capture and reproducer source hashes. 25 of 26 PNG sources matched;
the reproducer branch still held the older woodland-walker atlas, while all three
retained full images show the newer four-frame atlas from main. The interaction
subjects (both longbows and Tasha), granary, scene and cue sources matched. This
is a behavioral reproduction, not a claim of pixel-identical full-scene output.
The sparse worktree uses shared art support links, so a branch name alone is not
an asset snapshot.

This evidence does not change selection behavior. Hidden actors never qualify
for the above-scenery cue; existing SVG/GPU structural tests cover hidden winning
in either update order. No live GPU/browser validation is asserted.
