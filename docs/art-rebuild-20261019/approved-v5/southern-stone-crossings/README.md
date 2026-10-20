# Southern stone crossing handoff

## Result and scope

The two southern c01-12 stone crossings are implemented as two complete five-cell plus components, each rendered once with two complementary source-alpha clips. The four inner ports are visually open in the actual 884×616 BoardView before/after images. The east/west opening is 12 world pixels high; the north/south opening is 34 world pixels wide. The unchanged native end bars read as low steps between the taller corner posts. No new bank landing is claimed.

Only 2 of the 4 active legacy vertical drawings are closed by this batch. Both northern wood drawings and their watergate stacks are byte-identical and remain explicitly pending.

No gameplay, map, collision, actor, structure, target, gauge, rules or standing-foot changes. No generated texture, recoloring, alpha normalization, crop, non-uniform stretch or filler pixels. The existing approved horizontal source and all its runtime bytes are unchanged. The approved vertical native canvas and pivot are retained, with independent 154×154 and 307×307 exports.

## Integration files

- Prepared worktree: `/workspace/scratch/84c9fe948755/empire-layout-engineering-20261019`
- Base: `e9e9669`; branch: `fix/southern-stone-crossings-20261020`
- `files/` contains all 14 changed/new files at repository-relative paths, 161,355 bytes total
- `files-manifest.json` lists paths, sizes and exact SHA-256 hashes
- `tracked-files.patch` covers only the four tracked edits; use the complete file bundle for new files and PNGs
- No Git writes, commits or pushes were performed. Main integration remains the parent's responsibility
- The native vertical master and the two legacy baseline atlases were copied read-only from main to fill sparse-checkout omissions; their bytes are unchanged and they are not part of the delivery bundle

Production changes are limited to the crossing module, one crossing topology owner, the existing map-scene consumer, two registration/junction manifests, and the two native-density vertical runtime images. Other files are tests and reproducible review/export helpers.

## Verification

- 28 TypeScript tests passed across crossing, topology, map-scene, road-material and water-material suites
- 6 horizontal/vertical crossing Python tests passed
- Typecheck passed; `git diff --check` passed
- Both exporters passed `--check`; independently derived 1×/2× source RGBA is exact
- All 16 level/state hashes unchanged; all 15 unrelated complete scene projections byte-identical
- c01-12 ground and foreground projections unchanged; only crossing scenery changed
- Exact standing-layer/order markup preserved, including all 19 actors, six terrain buildings and three target structures
- Both northern wood markups and all 35 pre-existing raster source hashes unchanged
- Actual SVG repeated redraw, selection/cursor/path round trip, and editor-preview projection round trip passed without state mutation
- Isolated 50%-HP and disabled probes retain all three watergates, the correct damaged frame and exact 10.4/20.8 px HP gauge widths; normal state restores exactly
- Actual isolated 1×/2× final composite outer-edge alpha: 0/0
- Maximum new alpha on adjacent dry columns: 0/0
- Minimum rendered through-deck centerline alpha across both full axes: 245/250
- Malformed-manifest mutations cover missing/extra windows, source-canvas bounds, erased center/outer port, non-complementary windows, swapped windows, and over-wide openings
- Topology mutations cover a missing actual port cell, wrong center mask, wrong span and wrong pivot

### Numerical rendering caveat

Native source and exported pixels are unchanged. Outside the cut windows and a one-pixel antialias boundary, the vertical source and both 1× renders match exactly. Inkscape's clipped 2× horizontal image differs at 51 pixels by at most one 8-bit RGBA level; four of those are edge-alpha pixels. This is measured offline resampling/clip-surface rounding, not a source edit. The detailed report retains this distinction instead of claiming exact downstream RGBA equivalence.

### Limits

This is actual BoardView/SvgBoardSurface output rendered offline with the existing Inkscape pipeline. Live browser/GPU rasterization, playable interaction and real gameplay traversal were not verified. The parent will run the full suite and all 60 replay comparisons at the integrated checkpoint; neither was duplicated in this sparse worktree. No large build was run.

## Evidence

- `before/runtime-standing-full@1x.png`, `after/runtime-standing-full@1x.png`: native 884×616 complete actual board
- `before/runtime-standing-full@2x.png`, `after/runtime-standing-full@2x.png`: 1768×1232 complete actual board
- Matching SVGs and exact source provenance beside them
- `before/scene-contract.json`, `after/scene-contract.json`: pure all-level state/layer hashes
- `before/runtime-consumers.json`, `after/runtime-consumers.json`: complete all-level emitted projections and source hashes
- `isolated/junction@1x.png`, `isolated/junction@2x.png`: actual self-contained composition, with both complete/clipped directions beside them
- `isolated/junction-review.json`: topology, four outer port coordinates, source registration, redraw and target-state probes
- `validation.json`: exact actor/structure/target/wood hashes, source-window coordinates and all numerical checks
- `horizontal-export-qa.json`, `vertical-export-qa.json`, `focused-tests.log`, `python-tests.log`, `typecheck.log`

## Re-run commands

Run from the integrated repository. Set OUT to a fresh review directory when creating another checkpoint; retain the original `before/` evidence. The audit helper supplied in this delivery has an explicit worktree path; change its `root` and import prefixes to the integrated repository before using it there.

```sh
OUT=/workspace/shared/empire-art-rebuild/approved-v5/southern-stone-crossings
node_modules/.bin/vitest run packages/story-candidate-01/src/presentation/candidate-01-crossing.test.ts packages/story-candidate-01/src/presentation/candidate-01-crossing-topology.test.ts packages/story-candidate-01/src/presentation/candidate-01-map-scene.test.ts packages/story-candidate-01/src/presentation/candidate-01-road-material.test.ts packages/story-candidate-01/src/presentation/candidate-01-water-material.test.ts
python -m unittest discover -s tools/art -p 'test*crossing.py'
python tools/art/export_crossing.py --check
python tools/art/export_vertical_crossing.py --check
npm run typecheck
SOUTHERN_CROSSING_REVIEW_OUTPUT=$OUT/isolated node_modules/.bin/vite-node tools/art/review_southern_stone_crossings.ts
STANDING_REVIEW_OUTPUT=$OUT/after SCENE_REVIEW_LEVEL=c01-12 node_modules/.bin/vite-node tools/art/review_gray_flag_scene.ts
CONTRACT_OUTPUT=$OUT/after/scene-contract.json node_modules/.bin/vite-node tools/art/review_candidate_scene_contract.ts
CROSSING_AUDIT_OUTPUT=$OUT/after node_modules/.bin/vite-node $OUT/audit-runtime-consumers.ts
python tools/art/render_ground_review.py $OUT/isolated --scene-only
python tools/art/render_ground_review.py $OUT/after --scene-only
python tools/art/validate_stone_junction_review.py $OUT
```
