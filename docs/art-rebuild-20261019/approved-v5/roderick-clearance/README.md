# Three local decorative clearances around mounted Roderick

Baseline: **714538b099cbe35f22865fb3b6d97a9743ca0c89**, including the named mounted Roderick. Both sides use the same PNG sources, actor appearances, camera and rules. No later main-branch source snapshot is mixed into this comparison.

## Exact changes

| Level / terrain cell | Before drawing | After drawing | World contact before → after |
|---|---|---|---|
| c01-03 / forest(2,5) | mixed-forest-autumn | fern-bed, scale0.48, same mirror | (80.18315,197.76) unchanged |
| c01-10 / forest(17,7) | oak-ancient | fern-bed, scale0.48, same mirror | (561.13346,261.76) unchanged |
| c01-10 / hill(19,6) | temperate-low-ledge | same image, scale0.5605696, direction and shadow | (623.7925,229.76) → (623.7925,216) |

The hill contact moves north13.76px into its own cell rectangle [608,640]×[192,224]. The picture physically moves with it; ordinary foot-depth sorting follows that placement. There is no actor depth override. The hill surface and elevation1 remain visible at the original rule cell, with defense0.3, vision1, non-opaque terrain, no cover and obstruction0. Its atlas metadata (`footprint:[2,1]`, `passable:false`, `obstructionHeight:1`) remains unchanged; that catalog metadata does not supply battle collision or LOS.

Both forest cells retain their forest material, opacity, half cover, defense0.2, obstruction2 and movement costs. None of the three cells is an actor position, rule structure, owned building or task-zone cell. The only production edits are two small scene declarations and their existing reader registration. No terrain, unit, hero, rule, source image or shared rendering algorithm changes.

## Complete actual views and measurements

Open `c01-03-before-after.png` and `c01-10-before-after.png`. Each shows the entire actual BoardView, not a selected crop.

| Actor | Initial source-alpha overlap before | After |
|---|---:|---:|
| c01-03 Roderick, rule(3,5), foot(112,191) | 40.0565% | 0.7161% |
| c01-10 Roderick, rule(18,6), foot(592,223) | 47.4168% | 0% |
| c01-10 pass-shield | 34.6736% | 0.2389% |

All other37 actor measurements are unchanged; none worsened. c01-03's small remaining Roderick overlap comes from unchanged Torren/building art.

- c01-03: 19 actors,9 rule-building drawings,33 identical PNG-source hashes
- c01-10: 21 actors,9 rule-building drawings,28 identical PNG-source hashes
- All58 actor/building SVG drawings and all unrelated scenery/frame drawings are unchanged
- All16 scene/rule/state contracts match except the two intended under-unit scenery projections; ground, editor ground and all rule state remain equal
- All60 replay event streams/outcomes are byte-identical,3,534,788 bytes; SHA in `review.json`

The actual production output matches the accepted diagnostic candidate pixel-for-pixel. Both before images match the original714538b complete captures pixel-for-pixel. For those before captures, candidate scene files already existed on disk but were not bound by the baseline reader; the review tool records their presence. `reproducibility.json` explicitly records this capture detail rather than presenting that worktree as byte-pristine.

## Visibility and remaining boundaries

Both initial Roderick actors are visible to both players through the real engine query. Complete diagnostic captures deliberately use an empty hidden set, so that fact was checked separately rather than inferred from the images. Backend regression fixtures explicitly hide and select Roderick in each affected map and verify that neither SVG nor Pixi projects him or a selection cue.

These are offline actual BoardView/SvgBoardSurface rasterizations and raw sprite-alpha overlap measurements. They are not live GPU screenshots, perceptual recognition scores or coverage of every animation/movement state. The existing rasterizer omits CSS shadow filters in the alpha diagnostic. Other actors still have substantial overlap from unchanged forest, cliffs and rule buildings; the whole level is not declared visually resolved.

## Verification and reproducibility

- 29 focused TypeScript tests pass, including three exact placement cases, both full scene-piece contracts and both rule/visibility contracts
- 17 SVG/Pixi backend contract tests pass, including the two added hidden-Roderick cases
- 4 Python alpha-measurement tests pass
- Typecheck and all4 app builds pass
- Targeted architecture helper-uniqueness guard passes
- All16 scene contracts,60 replay streams and40 actual actor-alpha measurements pass the evidence verifier
- The initial focused run caught a new c01-10 scene declaration width of24 instead of the real25; the declaration was corrected and the original failure log is retained
- The full repository suite is left to the integrator's fixed complete-main checkpoint

`reproducibility.json` records implementation/source hashes. `sources/` freezes all PNGs used by these four complete frames, and the compressed SVGs refer only to that archive. `archived-render-verification.json` proves each archived SVG rebuilds its PNG exactly. Raw alpha reports retain every covering drawing, not just the three changed actors.

Re-run the evidence verifier:

```sh
python tools/art/review_roderick_clearance.py docs/art-rebuild-20261019/approved-v5/roderick-clearance
npx vitest run packages/story-candidate-01/src/presentation/candidate-01-roderick-clearance.test.ts
npx vitest run packages/game-ui/src/art/__tests__/two-backends-agree.test.ts
python -m unittest discover -s tools/art -p test_scene_actor_clearance.py
```

To make a fresh complete scene, use `SCENE_REVIEW_LEVEL=c01-03 STANDING_REVIEW_OUTPUT=/tmp/roderick-c03 npx vite-node --config vitest.config.ts tools/art/review_gray_flag_scene.ts`, then the existing `render_ground_review.py:rasterize` helper. Repeat for c01-10. For an archived scene, decompress its SVG beside the PNG before calling that helper. Raw Inkscape alone omits the required nested-viewport/facing normalization.
