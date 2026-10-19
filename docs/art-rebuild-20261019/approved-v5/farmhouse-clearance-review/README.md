# Farmhouse visual clearance, complete c01-01

## Result

Only the farmhouse's authored decoration coordinates changed: `(12.5,7.1)` → `(10.8,6.7)`. Its world foot moved `(400,259.2)` → `(345.6,246.4)`: **−54.4 px horizontally, −12.8 px vertically, 55.8856 px total**. Scale remains **0.70**, source pixels/anchor/opacity remain identical, and ordinary footpoint ordering still places it at standing display index 94. No renderer or gameplay code changed.

`complete-before-after.png` shows both complete production viewports; `central-before-after.png` shows the identical central crop at nearest-neighbor 2x. Each view retains all **19 rule actors, 4 rule buildings, 206 scenery pieces, 408 ground pieces and 10 spatial overlays**. Two decorative farmers remain scenery. This is actual BoardView/SvgBoardSurface output rendered offline, not live browser or GPU playback.

Both comparison sides use source checkpoint **75f66fb**, including its four newly bound rural foundation cells. The earlier 7bbeb52 scale/source investigation is retained separately in `../gray-flag-scene-audit/`. Later integration changes are not silently mixed into either side.

## Ownership and safety

`frontier-farmhouse` is a presentation atlas ID and one `CandidateEnvironmentPlacement`. The scene document is imported by `candidate-01-environment.ts`; its production consumer in `candidate-01-map-scene.ts` turns it into a BoardPiece. The placement fields are id/topicId, x/y, layer, variant, scale, flip and opacity. There is no rule identity, interaction cell, trigger, navigation or terrain responsibility to move. The logical footprint metadata describes art; it does not allocate gameplay cells.

The production diff is one scene-document line. Its object, every other placement, all rule cells, building positions, actor cells/facings, navigation and triggers are retained. The regression pins this placement and its unchanged scale/layer/contact, checks exactly one rendered farmhouse, and proves scene composition does not mutate the level, map or initial battle state.

## Exact checks

- The entire before/after SVG is byte-identical after substituting the single farmhouse translation. No layer exceptions, omissions or hidden art
- Initial state SHA-256 is identical: `d8987f972460aecb8e0b811a947a6df23c6951eb62f9e3a4dddd99c84e46963e`
- Every source hash is identical except the scene JSON itself; full inventories are in each `runtime-scene-provenance.json`
- All 19 occupied actor display regions, east/west/south road approaches, east granary door, west granary steps and west entrance ground are pixel-identical
- The broad west granary facade rectangle has 482 changed pixels, each by exactly **one 8-bit channel level** from retained alpha/compositing behind the front building. It has no new physical occlusion; the front steps and entrance ground are unchanged
- Farmhouse door/frame/front-step coverage falls from **99.2348% to 0.002334%** in the manually traced diagnostic polygon; the residual is tiny alpha sampling. The building's total alpha-weighted coverage falls from approximately 95.67% to **11.17%**, retaining natural overlap at the lower/right edge. The decorative granary remains 0% covered
- `comparison.json` records the checked screen rectangles, changed-pixel bounds, counts and exact SVG check; `door-clearance-search.json` records the traced source polygon and local search criterion; the before/after `rural-building-overlap.json` files record actual alpha-area measurements

The chosen move is the nearest passing candidate among **328 tested positions** on the 0.1 authored-coordinate grid, x=8.5…12.5 and y=6.4…7.1, under the ≤0.1% door/frame/steps overlap criterion. These candidates share the same ordinary depth interval. This is a measured local grid minimum, not a claim of a continuous or global optimum. The rejected pure-left solution required 99.2 px. The search is diagnostic evidence only; no search or avoidance mechanism was added to production.

## Actors and current-source caveat

Per-actor type, topic and actual image are recorded in each provenance JSON. At **75f66fb**, 18/19 instances use approved-v5 sources across six of seven unique themes: archer 6, legion shield 4, banner guard 3, swordsman 3, heavy knight 1, knight 1, gravekeeper 1. Only actor 4 (`field-cleric`, `c01.gravekeeper`, `C01-UNIT-GRAVEKEEPER`) is legacy. Parent integration **04bebb8** subsequently binds Gravekeeper, so the current integrated first chapter is 19/19; these baseline pictures deliberately preserve their original sources.

## Verification

- `npm run typecheck`: passed
- `npx vite-node tools/replay-sweep.ts`: all 60 before/after event streams and outcome digests byte-identical; hash and size recorded in `replay-check.json`
- The new farmhouse regression deliberately rejects restoration of the old coordinates, then passes with the approved placement restored
- Full test result is recorded in `verification.json`

The first aggregate run caught an untracked temporary map-inspection script violating the existing single-owner grid-index guard. That scratch script was removed from the repository and the full suite rerun; no guard was weakened.

## Reproduction and limits

Run `STANDING_REVIEW_OUTPUT=<directory> npx vite-node tools/art/review_gray_flag_scene.ts` to capture the current complete scene. Rasterize it with the existing `tools/art/render_ground_review.py --scene-only` helper. The review tool uses the actual production BoardView, footpoint order and all source pieces, and records real actor image bindings.

This corrects one proven decorative overlap. It does not guarantee general actor visibility behind trees or buildings, and no general avoidance, fading or occlusion detection was introduced. Offline pixels are not browser/GPU validation; moving units continue to use their existing normal depth and selected-only visibility cue.
