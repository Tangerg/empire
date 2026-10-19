# Gray Flag environment pilot: safe tree-first remake contract

Read-only design, 2026-10-19. No generation, source edits, build or browser retry performed for this design. Paths are repo-relative. Source inspection proves bindings, not actual screen visibility or frequency. Prior browser visual gate remains pending.

## Recommended first batch

Generate **two separate transparent masters**, `oak-ancient` and `mixed-forest-dense`. They are slots 0 and 2 in forest-temperate and are selected by the actual forest-edge canopy pool and outer framing pool. The first is one ancient broadleaf oak; the second is a mixed conifer/broadleaf cluster, not one tree. Preserve these meanings. Next pair: `pine-mature-a` and `pine-mature-b` (forest-existing indices 9 and 10); then `fern-bed` for enclosed forest floor.

Why two props first: individual cutouts permit real alpha and coherent shapes without asking image generation to preserve 16 exact cell boundaries. They do not encode paths, water connectivity or collision. Do not regenerate a scenic map and use it as tiles.

## Exact input chain

- Temperate visual master: `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/masters/forest-temperate-4x4-key.png` (1254×1254 RGB keyed legacy source)
- Temperate actual builder input: `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/intermediate/alpha/forest-temperate-4x4.png` (1254×1254 RGBA)
- Existing pine actual builder input: `packages/story-candidate-01/assets/final-fantasy-v1/runtime/environment/twin-hills-forest-atlas-v1.png` (1024×1024 RGBA). Its adjacent README describes keyed generation then alpha extraction; no separate original keyed master was found in this asset tree. Treat current RGBA as the available reference, not a claimed original master.
- Ground master: `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/masters/surface-common-4x4.png` (1254×1254 RGB). Rows are meadow, forest-floor, earth, old-stone; four columns are separate material/tone variations, not interchangeable colors. Runtime currently fixes tone choices in some places.
- Builder: `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/tools/build_environment_builder_v1.py`, `grid_cell` lines36–44, `fit_sprite`154–165, `surface_atlases`299+, `module_specs`448–474
- Manifest: `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/manifest-environment-builder-v1.json`
- Semantic contract: `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/SEMANTIC-CATALOG.json`

## Minimum image-generation output contract

For each requested ID, deliver a separate high-resolution PNG RGBA (square 1024px preferred; usable detail at >=512px). Set transparent_background=true. One complete isolated object/group, transparent perimeter and real holes between branch/leaf clusters; no baked checkerboard, magenta key, solid backdrop, rectangular grass platform, text, UI or unrelated objects. Roots and small local stones are allowed only inside the original compact footprint. No broad ground shadow that changes crop bounds. Preserve high oblique camera, bottom-center contact, original tree/cluster silhouette class and relative proportions. Matte bark, restrained olive/sage foliage, separated leaf masses and readable branch/root structure; avoid bright glossy highlights and excessive microtexture that disappears at final ~71–83px canopy size.

Reference original slot crop plus approved style reference. Verify original local image before editing. Image generation must do artistic edits; slot extraction/packing and scale validation are mechanical and belong to the integration pipeline.

## Safe assembly contract (requires pipeline work, not implemented by this report)

Keep immutable generated raw masters and provenance/checksums. Add a deterministic per-cell override adapter or reproducible assembly step. Do not directly overwrite an atlas without reproducible upstream input. The canonical-topic exporter currently does not own these environment IDs; they must not be counted as completed C01 canonical topics.

For first pair, patch only source slots 0 and 2 into a reconstructed temperate RGBA source (or override those exact module cells before packing). Preserve all other source cells byte-for-byte where possible; verify their resulting atlas slices unchanged. Keep original intermediate archived/provenanced; overwriting only the old keyed master does nothing because this builder reads the alpha intermediate directly.

Source cuts use Python round(index * dimension / 4), not integer width=313: 1254 boundaries are [0,314,627,940,1254]. Oak source rectangle [0,0,314,314]; dense mixed [627,0,940,314]. Pine 1024 boundaries [0,256,512,768,1024], index9=[256,512,512,768], index10=[512,512,768,768]. All rectangles are x0,y0,x1,y1 (exclusive end).

Current builder crops nonzero alpha bbox, contains it within 172×172, and places it bottom-center in 192×192 at bottom y=182. It downsamples to96×96. Logical anchor stays [48,91] (HD [96,182]), same cell IDs, indices, footprints and obstructionHeight. Need a transparent safety ring on raw masters: stray low-alpha pixels can change getbbox and shrink visible tree. Gate on bbox and alpha hygiene before fitting, rather than silently erase artistic pixels. No content crossing cell bounds. Preserve the logical size even if raw generation is larger.

Full-sheet fallback, only if individually-generated option is unsuitable: retain exact4×4 row-major semantic order, all16 distinct isolated items, generous padding and zero cross-cell overlaps. Do not trust generated grid alignment; inspect/crop every cell. Never feed an illustrated 4×4 contact sheet directly as a replacement without slot checks.

## Atlas layout and all forest slots

Both atlases: 4 columns ×4 rows; 384×384 1x /768×768 2x; logical96×96 slices and192×192 HD slices. For index i, logical crop=(i%4*96, i//4*96,96,96), HD dimensions doubled. Runtime names remain unchanged.

### forest-existing

Output `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/forest-existing.png` and `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/forest-existing@2x.png`

- 0: pine-tall; footprint [1, 1]; obstructionHeight 2; anchor [48, 91]
- 1: pine-pair; footprint [2, 1]; obstructionHeight 2; anchor [48, 91]
- 2: pine-cluster; footprint [2, 2]; obstructionHeight 2; anchor [48, 91]
- 3: pine-sapling; footprint [1, 1]; obstructionHeight 1; anchor [48, 91]
- 4: pine-underbrush-a; footprint [1, 1]; obstructionHeight 0; anchor [48, 91]
- 5: pine-underbrush-b; footprint [1, 1]; obstructionHeight 0; anchor [48, 91]
- 6: pine-underbrush-c; footprint [1, 1]; obstructionHeight 0; anchor [48, 91]
- 7: pine-underbrush-d; footprint [1, 1]; obstructionHeight 0; anchor [48, 91]
- 8: pine-dead; footprint [1, 1]; obstructionHeight 2; anchor [48, 91]
- 9: pine-mature-a; footprint [1, 1]; obstructionHeight 2; anchor [48, 91]
- 10: pine-mature-b; footprint [1, 1]; obstructionHeight 2; anchor [48, 91]
- 11: pine-mature-c; footprint [1, 1]; obstructionHeight 2; anchor [48, 91]
- 12: pine-low-shrub; footprint [1, 1]; obstructionHeight 0; anchor [48, 91]
- 13: pine-fallen-log; footprint [2, 1]; obstructionHeight 1; anchor [48, 91]
- 14: pine-stump; footprint [1, 1]; obstructionHeight 0; anchor [48, 91]
- 15: pine-mossy-rock; footprint [1, 1]; obstructionHeight 1; anchor [48, 91]
### forest-temperate

Output `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/forest-temperate.png` and `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/forest-temperate@2x.png`

- 0: oak-ancient; footprint [2, 2]; obstructionHeight 2; anchor [48, 91]
- 1: mixed-forest-autumn; footprint [2, 2]; obstructionHeight 2; anchor [48, 91]
- 2: mixed-forest-dense; footprint [2, 2]; obstructionHeight 2; anchor [48, 91]
- 3: oak-grove-dense; footprint [2, 2]; obstructionHeight 2; anchor [48, 91]
- 4: mixed-forest-edge; footprint [2, 1]; obstructionHeight 2; anchor [48, 91]
- 5: sapling-rock-cluster; footprint [1, 1]; obstructionHeight 1; anchor [48, 91]
- 6: bramble-dark; footprint [1, 1]; obstructionHeight 0; anchor [48, 91]
- 7: fern-bed; footprint [1, 1]; obstructionHeight 0; anchor [48, 91]
- 8: bramble-berries; footprint [1, 1]; obstructionHeight 0; anchor [48, 91]
- 9: stump-low; footprint [1, 1]; obstructionHeight 0; anchor [48, 91]
- 10: stump-hollow; footprint [1, 1]; obstructionHeight 0; anchor [48, 91]
- 11: fallen-hollow-log; footprint [2, 1]; obstructionHeight 1; anchor [48, 91]
- 12: leaf-pile; footprint [1, 1]; obstructionHeight 0; anchor [48, 91]
- 13: boulder-large; footprint [1, 1]; obstructionHeight 1; anchor [48, 91]
- 14: boulder-ferns; footprint [1, 1]; obstructionHeight 1; anchor [48, 91]
- 15: forest-floor-cluster; footprint [1, 1]; obstructionHeight 0; anchor [48, 91]

## Opaque ground versus transparent trees and connected overlays

- Base ground is intentionally opaque RGB/full alpha. Do not introduce transparent holes into meadow/forest-floor, or checkerboard it to satisfy an alpha preference. The forest-floor terrain material comes from common master row1 and is used to construct the blend overlay.
- Trees, shrubs and stumps are transparent independent modules; they have no adjacency mask. Their footprints/obstruction metadata must remain intact, but game movement remains engine-owned.
- Forest ground boundary is `transition-meadow-forest`, generated as47 canonical Blob/Wang8-neighbor masks. Keep maskOrder and diagonal canonicalization untouched; do not ask imagegen to draw47 exact connected tiles.
- Roads and rivers use N/E/S/W4-bit masks (N1,E2,S4,W8),16 masks ×4 variations, cellIndex=mask*4+variant. Dirt-road has route-edge overlay. These are separate generated alpha overlays; regenerating tree props must not alter any of them.
- Base ground generator makes tiles tileable and locks family edges. Changing the common ground master can fan out to surfaces/transitions and preview outputs; postpone until tree pilot validated. For later ground work, retain row/column material semantics, seamless texture scale and deterministic edge/mask synthesis. Do not re-render finished connection atlases freely.

## Actual consumer constraints

`packages/story-candidate-01/src/presentation/candidate-01-terrain-materials.ts`: FOREST_CANOPY lines100–109 includes first two pilot IDs; forest lines154–156 uses transition-meadow-forest plus canopy scale0.74,jitter9. Outer-frame list lines364+ also selects oak-ancient and mixed-forest-dense. Forest interiors choose low understory IDs, not big trees.

`candidate-01-map-scene.ts`: environmentCellMarkup lines131–140 uses declared anchors and transforms; sceneryPieces lines529–575 chooses deterministic canopy/understory by same-blend orthogonal neighbors, scale0.74 plus0–0.12 for normal forest canopies, possible horizontal flip, bottom placement y=(tileY+1.18)*32. Therefore avoid asymmetric lighting that looks wrong mirrored; test detailed leaves at96px and actual0.74–0.86 scale. Outer framing can be larger. Taller broad canopies may obscure units even with perfect alpha: retain envelope and compare silhouette overlap.

`packages/game-ui/src/art/environment-catalog.ts` resolves png2x when available; both resolutions required. `candidate-01-environment.ts` imports the manifest. Read source as current truth; older package README runtimeReady wording can be stale and is not proof of non-use.

## Gates before a GitHub batch can be called integrated

1. Raw PNG true alpha, transparent boundary, holes, full object, no fringe on white/dark/grass; review96px and scaled71–83px equivalents, plus192px detail.
2. Same IDs/order/cell sizes/anchors/footprints; unaffected14 source cells and atlas slices identical (or documented deterministic export differences), routes/ground/masks unchanged for tree-only batch.
3. Run existing environment builder QA, check its concrete output/error report, and focused presentation tests. Current12-test baseline already passed; this proposal has not rerun them.
4. Provenance and adapter are reproducible; generated alpha is source-of-truth and not regenerated by obsolete chroma key flow.
5. Actual game screenshot and occlusion acceptance remains pending because cloud Chromium was blocked. Static composites or builder previews are useful but cannot be labeled runtime screenshots.
