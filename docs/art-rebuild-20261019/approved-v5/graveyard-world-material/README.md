# Graveyard surface integration: fixed 256-unit world sample

## Result and scope

The existing `surface-graveyard` tone-zero consumer now samples one world-aligned material at a 256 × 256 logical period. This affects 258 cells in c01-11 and 296 in c01-16. All other 14 candidate levels retain identical ground and editor-ground projections. Terrain definitions, maps, objectives, visibility, buildings, deployments, actors, scenery and standing draw order are unchanged.

All four approved native sources are registered in their original `graveyard-1` … `graveyard-4` catalog slots, exported at the existing 32/64-pixel cell sizes. Only source zero has an actual terrain consumer. The other three are catalog-only. Four native source identities are counted; the world material and two density exports are derivatives, not extra source subjects.

The reviewed complete c01-11 comparison is `before-after-labeled.png`; separate full frames are in `before/` and `after/`. Both use the same 6dd8193 baseline art, 852 × 648 camera, 21 actors and 8 structure drawings. The generated SVG is from the actual BoardView/SvgBoardSurface. It is rasterized offline with the repository's nested-viewport and actor-facing normalization, not a live browser/GPU capture. This older fixed actor baseline is deliberately preserved on both sides; later main-branch hero registrations are not part of this comparison.

## Source and sampling contract

The four immutable RGB 1254 × 1254 masters are under `packages/story-candidate-01/assets/final-fantasy-v1/masters/approved-v5/environment/surface-graveyard/graveyard-{1..4}/master.png`. `source-and-interior-verification.json` records exact SHA-256 identities and equality with baseline Git blobs. Source 1 (tone 0) is `17d2b4c721069da94d4e348d7601e8f551cfd3ac354d12e8c037022069d54c2d`.

The existing field exporter resizes the full source canvas with LANCZOS to 512² and 1024². World period remains 256 logical units, giving 2 and 4 raster pixels per world unit respectively; the runtime reader selects the higher-density image. Only the opposing outermost one-pixel edges are averaged by the already approved seam operation. Every interior pixel equals the direct full-canvas resize. No native input is overwritten, blurred or recolored. The existing tile projection and its technical subpixel overlap are unchanged. Neither graveyard terrain consumer has a legacy connected-road or edge RGB layer that could paint the old texture back over the new base.

`world-export.json`, `world-check.json` and `catalog-export.json` record the derivative provenance. The strict surface whitelist adds only this known four-slot atlas, retaining original cell identity/order and rejecting reordered identities. Existing meadow and old-stone catalog entries remain unchanged.

## Verification

- 19/19 focused TypeScript tests across four files passed, including actual 16-level consumer counts, source hashes, PNG dimensions/modes, world phase, camera independence, editor projection and immutable rule data
- 8/8 surface-export Python tests and 8/8 field-material Python tests passed
- Typecheck and all four application builds passed
- All 60 replay streams/outcomes are byte-identical before/after; compressed full output and SHA-256 are retained
- All 16 level/state and under-/over-unit hashes are identical; only c01-11/c01-16 ground/editor-ground change (`scope-comparison.json`)
- The actual material projected through 32-pixel tiles equals one continuous reference plane in every RGBA channel at display 1× and 2× (`plane/comparison.json`)
- Both archived complete SVGs reproduce their stored full PNG exactly using the frozen source archive (`archived-render-verification.json`)

The first focused attempt is retained in `validation/focused-first.log.gz`: one new assertion incorrectly compared *all* editor ground with play ground, including c01-01's intentionally different road preview. It was corrected to compare the graveyard pieces; the independent before/after 16-scene snapshots still guard unrelated editor behavior. The other failure was a missing pre-existing stone master in the sparse checkout; both stone input copies were restored from exact baseline Git bytes. Final focused results pass. The entire repository suite was not rerun in this known-incomplete sparse environment; integrated main validation belongs to the complete workspace. No full-suite success is claimed here.

## Reproduce

From the repository root with native masters present:

```sh
python tools/art/export_field_material.py --manifest packages/story-candidate-01/assets/final-fantasy-v1/world-materials-v1/manifest-graveyard-base-v2.json --check
python tools/art/export_surface_overrides.py --check
python -m unittest discover -s tools/art -p test_surface_overrides.py
python -m unittest discover -s tools/art -p test_field_material.py
npx vitest run packages/story-candidate-01/src/presentation/candidate-01-{graveyard-material,stone-material,world-material-tile,map-scene}.test.ts --maxWorkers=1 --minWorkers=1
npm run typecheck
npm run build:apps
npm run --silent sweep > /tmp/graveyard-replay.txt
CONTRACT_OUTPUT=/tmp/graveyard-contract.json npx vite-node tools/art/review_candidate_scene_contract.ts
SCENE_REVIEW_LEVEL=c01-11 STANDING_REVIEW_OUTPUT=/tmp/graveyard-scene npx vite-node tools/art/review_gray_flag_scene.ts
python tools/art/render_ground_review.py /tmp/graveyard-scene --scene-only
GRAVEYARD_REVIEW_OUTPUT=/tmp/graveyard-plane npx vite-node tools/art/review_graveyard_material.ts
python tools/art/render_ground_review.py /tmp/graveyard-plane --scene-only
```

The full catalog check needs every registered family master; the sparse development export used a temporary catalog containing only the same four graveyard entries. Production catalog and committed atlas output remain complete. For the frozen full-scene proof, decompress either `runtime-standing-full.svg.gz` beside its PNG and call `render_ground_review.rasterize(directory, (1,))`; its relative image URLs resolve to `sources/`. Only URLs were relocated from captured SVGs. `reproducibility.json` retains original/relocated SVG hashes, source hashes and implementation hashes.

The initial before PNG was rerendered against the exact baseline atlas after the catalog export, avoiding an accidental new-atlas/old-code comparison. The archived source rerender verifies the final delivered baseline and candidate independently of the mutable workspace.

## Visual limits

The accepted 256-unit sample makes the cemetery base quieter and keeps warm cemetery earth distinct from gray stone paths. The source still contains small facets. Square boundaries between different terrain materials and dense existing tomb/tree/building occlusion remain visible in the complete frame. This is a completed surface-sampling integration, not a claim that the entire level composition or actor clearance is resolved. A separate read-only responsibility/footpoint audit is the next step; it is not part of this patch.
