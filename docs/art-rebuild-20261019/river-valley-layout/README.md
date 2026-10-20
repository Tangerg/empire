# Three Bridges: readable river valley checkpoint

This is a bounded layout/readability iteration, not final style acceptance. All
images are **offline production BoardView/SvgBoardSurface renders**, with the real
actors, buildings, scenery, CSS, and full board included. They are not live browser
or GPU playback evidence.

## Full scene comparison

- Before: [native 1x](before/full-board@1x.png), [native 2x](before/full-board@2x.png), [SVG](before/full-board.svg).
- After: [native 1x](after/full-board@1x.png), [native 2x](after/full-board@2x.png), [SVG](after/full-board.svg).

The before frame is the committed river-clearings checkpoint. No actor/building
is removed or cropped to make the after frame look better. There are 20 actors,
9 rule buildings, and the original three bridge runs in both scenes.

### Chosen visual changes

- Replace the regular outer tree fence with 24 unevenly clustered frame trees and
  an irregular ground outline. River exits meet the outline rather than stopping
  against a strip of grass.
- Join disconnected road stripes into a readable approach network. The final
  river-only trail profile is 20px wide, with 16px tangent rounding and no plain
  overhang. Unlike the existing 32px road profile, it does not fill 2×2 aprons.
- Keep the tactical northern river and all bridge gateways fixed. A restrained
  authored channel bends within those cells and flares south of the crossings.
  The original, approved water/earth RGB and native material periods are unchanged.
- Water alpha is clipped strictly to water/bridge cells. The sandy bank may extend
  at most 5px into explicitly permitted plain/road ground, never into buildings,
  forest, hills, or other blockers. Every material piece owns its SVG definitions.
- Keep the previously reviewed four low-vegetation forest clearings. Do not remove
  the original opaque eastern forest at (14,10), or move/shrink any building.

Three deployment moves remove the remaining house/actor overlaps:

| Actor | Before | After |
| --- | --- | --- |
| Elin | (2,10) | (3,11) |
| Western banner guard | (1,10) | (1,12) |
| Southern enemy archer | (16,11) | (15,11) |

All other deployments, identities, equipment/stat definitions, building cells,
ownership, objectives, escort zones, and truce rules are unchanged.

## Why the larger meander was rejected

A wide pilot and a one-row southward archer move were not shipped. The archer
move changed the refugees' nearest-enemy tie, diverting the whole opening convoy
south. The original scenario wins by keeping Laiya behind that convoy while the
archers suppress artillery. A horizontal archer move restores that ordering.
The shifted northern river then changed the turn-5 route, so the final layout
preserves the original northern tactical gateway. No AI, stats, survival rule, or
assertion was weakened to accept the new scene.

## Gameplay evidence

[All 60 replay comparisons](sweep-comparison.json) show:

- **57 runs are byte-for-byte identical**, including their outcome digests.
- Only the three c01-02 runs differ. Every profile retains the original winner,
  completion turn, action count, truce/escort sequence, and victory reason.
- The identical event suffixes extend all the way through gameOver: 698 events
  at aggression .35, 648 at .58, and 524 at .8. Each after trace has one fewer
  opening facing event. State digests intentionally differ because they include
  the new terrain/deployment data.

| Aggression | Before/after winner | Before/after turn | Before/after actions |
| --- | --- | --- | --- |
| .35 | Player | 17 | 294 |
| .58 | Player | 13 | 243 |
| .8 | Player | 11 | 217 |

[Opening frame comparison](opening-frame-comparison.json) and
[tactical diagnosis](tactical-comparison.json) retain the causal checks. All 20
units' positions and HP match at the checked turn-end frames from enemy turn 1
through turn 4 for .35/.58. Full reproducible streams are stored as
[sweep-before.txt.gz](sweep-before.txt.gz) and [sweep-after.txt.gz](sweep-after.txt.gz).

[Final tactical validation](after/validation.json) checks legal starts, available
opening moves, access to every opposing actor/building/safety cell using each
movement class, and each bridge's independent bank-to-bank connectivity.

## Render and test evidence

- [Visibility comparison](visibility-comparison.json): every actor remains below
  35% later-drawing alpha overlap. Elin is 9.7%, the moved guard 16.1%, and the
  southern archer 1.4%. This is an offline alpha-overlap metric, not a perception
  or live renderer score. See [complete after measurements](after/occlusion.json).
- [River geometry](after/river-geometry.json): at native 1x and 2x, zero water
  pixels on dry cells, zero bank pixels on blocked terrain, zero pixels beyond
  the bounded rim, and no empty/disconnected channel scanlines.
- [Focused tests](focused-tests.txt) cover the real scene declaration, canonical
  material/editor behavior, immutable source identity, crossings, road geometry,
  roster/objective/ownership contracts, reachability, and redraw non-mutation.
- [Architecture and tactical tests](contract-architecture-tests.txt): 104 ownership/architecture guards plus the 3 tactical contract tests pass.
- [Typecheck](typecheck.txt) uses the existing shared board harness with GameState
  from its real owner. The upstream import correction is not part of this packet.

Reproduction uses the existing review tools:

```sh
RIVER_LAYOUT_OUTPUT=/tmp/river-contract npx vite-node tools/art/review_river_layout.ts
SCENE_REVIEW_LEVEL=c01-02 STANDING_REVIEW_OUTPUT=/tmp/river-scene npx vite-node tools/art/review_gray_flag_scene.ts
python3 tools/art/review_scene_actor_clearance.py /tmp/river-scene /tmp/river-visibility
python3 tools/art/review_river_geometry.py /tmp/river-scene/runtime-standing-full.svg /tmp/river-contract/level.json /tmp/river-geometry
npm run --silent sweep > /tmp/river-sweep.txt
```

## Remaining aesthetic limits

The southwest deployment remains dense, small grass islands remain in the trail
network, and the approved water material/bank edge retains angular facets. The
three original tactical crossings are deliberately preserved rather than forced
into an aesthetically wider meander at the cost of gameplay. This checkpoint is
accepted as an incremental readability improvement, not complete level-art or
runtime-browser acceptance.
