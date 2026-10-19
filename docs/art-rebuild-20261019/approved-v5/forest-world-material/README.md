# Forest floor through the existing Blob47 topology

## Result and scope

Selected **128 logical world units**, sampled from the unchanged full native `forest-floor-1` painting. The 256-unit alternative is retained as a diagnostic. In the complete c01-15 comparison, 128 keeps leaf groups at roughly 18–28 logical units, reading as ground litter; 256 enlarges those groups to roughly 37–55 units, approaching the scale of nearby decorative ferns. Neither candidate changes standing vegetation.

- [Complete c01-15 before / selected 128 / diagnostic 256](c01-15-before-128-256.png)
- [Complete c01-01 before / selected 128](c01-01-before-after.png)
- [Exact 16-level scope](scope-comparison.json)
- [Full-scene actors, sources and standing-layer comparison](full-scene-comparison.json)

Actual consumers: c01-01 126; c01-02 13; c01-03 9; c01-04 17; c01-05 11; c01-06 12; c01-09 11; c01-10 13; c01-11 24; c01-15 364; c01-16 25. Total **625 projected ground cells over 11 levels**. Counts are material consumers, not promises that every cell is visible beneath scenery. Five other levels retain identical scene projections.

All four original `surface-forest-floor` slots are exported to their original 32/64 catalog sizes. Only source zero has an actual consumer, through `transition-meadow-forest`; sources 1–3 remain catalog-only. Four independent native sources count as four material sources. The 47 technical masks and world raster exports do not add native-art counts.

## Source and coverage ownership

Native source zero SHA-256: `3a7afefff262e7ff3d9baef1f2e86bd03cefe35a40807715a5d3bde4d21ad14d`. All four sources are 1254×1254 RGB opaque native paintings. The last two native inputs arrived after the frozen consumer baseline and are saved by parent commit `963b17b`; this branch does not duplicate their master files. [Source and interior verification](source-and-interior-verification.json) records all four identities and proves source bytes equal the saved parent-main originals.

The world texture uses a full-canvas LANCZOS resize to 512/1024 raster pixels at the independent 128 logical period. Only the outermost one-pixel rows/columns are matched to their opposite edges. Every interior pixel equals the direct resize. No crop, rotation, recolor, whole-image blur or procedural texture painting is applied. Matching the outermost pixel does not prove a naturally seamless painting: the source contains cloudy soil planes and sparse leaf groups, so repetition remains an explicit visual limitation.

The original transition atlas and its manifest remain byte-identical. Its original 16×3 layout, 47 indices, bit order and normalized Blob47 topology own coverage. Both original 32/64 alpha channels are copied exactly to white-RGB technical masks, including the empty 48th padding slot. Runtime uses the existing `blobMask` and `blobIndex`, then samples the new material at each cell's absolute world coordinates through that mask. No old transition RGB is drawn back over the material. Existing half-pixel tile bleed is retained; no new coverage overhang is introduced.

The shared alpha exporter was renamed from `export_stone_masks.py` to `export_material_masks.py` because it now serves two concrete consumers. It accepts only the existing ordered stone route/edge pair or the single forest blend. Existing stone outputs remain pixel-identical, and the stone tests still run. Other transitions, earth, snow, hills, roads, water, bridges and standing scenery are outside this change.

## Controlled comparison

Baseline: `2e72444fd27fed5fe4c9b8586970ab932040e5c3`, including named Laiya and the approved local scenery clearances. Both original before views were captured before changing the forest consumer. The archived c01-01 before is the first, frozen legacy-atlas capture; a later temporary capture was not used.

c01-01 has 19 actors / 4 rule buildings; c01-15 has 22 actors / 8 rule buildings. In both pairs, all 26 common PNG source hashes match, actor bindings and positions match, the complete standing-layer SVG is byte-identical, and initial state hashes match. The only removed image consumer is the old forest transition; the only added image consumers are the new world floor and its technical mask.

Each full view includes its original provenance, a portable compressed SVG and all required frozen runtime PNGs. [Reproducibility](reproducibility.json) records source hashes and implementation hashes. [Archive verification](archived-render-verification.json) rerenders all five archived SVGs and checks every RGBA channel against their complete PNGs. This is actual BoardView/SvgBoardSurface output, offline-rasterized with the repository viewport/facing normalizer, **not live browser or GPU capture**.

Dense forest decorations and existing actor/building occlusions remain. This material integration does not resolve c01-15's whole composition, does not fade foreground objects, and does not reveal concealed actors. The two-backend visibility tests retain that boundary.

## Verification

- 22 focused TypeScript tests across forest, map scenery, stone, graveyard and forge materials
- 15 SVG/Pixi backend contract tests, including concealed selected actors
- 12 Python alpha-mask tests, including all 47 cells at both densities, original RGB discard, semantic-order rejection and unchanged source bytes
- 10 Python surface tests and 8 world-material exporter tests
- Typecheck and all four application builds
- [Actual alpha and UV rendering](plane/comparison.json): all 47 rendered alpha cells match legacy output exactly at 1x/2x; world-tile projection equals the continuous source plane in every RGBA channel
- All 16 scene contracts: only ground/editor-ground change in the 11 enumerated levels; all rule/initial-state/standing projections match
- [All 60 replay streams](replay-comparison.json): byte-identical events and outcomes
- Exact helper-uniqueness guard passes. A wider architecture subset has one sparse-worktree failure on 45 missing historical document links; its log is retained. No full-repository pass is claimed here; fixed main validation is separate

The first local test run caught an accidental diagnostic JSON key replacement, an overly broad assumption that editor roads equal play-mode roads, and missing sparse native files for unrelated material tests. The key was corrected, the test now checks the actual forest projection in both modes, and the small known native inputs were materialized from the frozen commit. Original failure logs and corrected passing logs are retained; no production geometry or assertion tolerance was changed to force passage.

## Reproduce

From repository root:

```sh
python tools/art/export_field_material.py --manifest packages/story-candidate-01/assets/final-fantasy-v1/world-materials-v1/manifest-forest-floor-v2.json --check
python tools/art/export_material_masks.py --manifest packages/story-candidate-01/assets/final-fantasy-v1/world-materials-v1/manifest-forest-blend-masks-v1.json --check
python tools/art/export_material_masks.py --check
python tools/art/export_surface_overrides.py --check
PYTHONPATH=tools/art python -m unittest tools/art/test_forest_masks.py tools/art/test_stone_masks.py tools/art/test_surface_overrides.py tools/art/test_field_material.py
npx vitest run packages/story-candidate-01/src/presentation/candidate-01-forest-material.test.ts packages/game-ui/src/art/__tests__/two-backends-agree.test.ts --maxWorkers=1 --minWorkers=1
FOREST_REVIEW_OUTPUT=/tmp/forest-plane npx vite-node tools/art/review_forest_material.ts
SCENE_REVIEW_LEVEL=c01-15 STANDING_REVIEW_OUTPUT=/tmp/forest-scene npx vite-node tools/art/review_gray_flag_scene.ts
npm run typecheck
npm run build:apps
npm run --silent sweep > /tmp/forest-replay.txt
```

Rasterize review SVGs through `tools/art/render_ground_review.py:rasterize`, which handles nested atlas viewports and production facing. To reproduce an archived view, decompress its SVG beside its PNG; the relative `../sources/` references resolve only to this package's frozen images. For the 256 diagnostic, change only the world manifest's `logicalSize` and the forest reader's reviewed-size guard to 256 in a temporary checkout, then render the same c01-15. Do not globally replace number text or alter the source hash, raster sizes, masks or source PNG.
