# Stone world-material integration

## Result and scope

[Full same-camera comparison](before-after-labeled.png): actual c01-07 BoardView, 884 × 616, all 23 actors and 16 structure drawings. Both frames use the same older wolf-rider appearance from the engineering branch. Do not compare this before frame with main's later wolf rider and call that a material-only comparison.

The 285 existing `surface-old-stone` tone-0 ground pieces sample one world-aligned material, at a 128-logical-unit period. The 250 existing street route pieces and 250 edge pieces use a second world-aligned material through their original connector alpha. No terrain, actor, building, deployment, objective, collision, visibility, route topology, or scenery-layout edits occur here. Existing grass-square artifacts are outside this change.

This is a shared material binding, so it also updates ground/editor previews in c01-03, 07, 08, 09, 11, 13, 14 and 16. The other eight levels' ground is unchanged. All 16 levels retain their map/state and under-/over-unit layers exactly; see [scope](scope-comparison.json) and the complete [before](contract-before.json)/[after](contract-after.json) contracts. Four source catalog slots remain owned by the separate surface catalog exporter. This patch does not modify that catalog or its original 32/64-pixel atlases.

## Source identity and sampling

Both masters are opaque RGB 1254 × 1254 generated stone materials, not painted by the exporter. Paths are relative to `packages/story-candidate-01/assets/final-fantasy-v1/`:

- Base: `masters/approved-v5/environment/surface-old-stone/old-stone-1/master.png`, SHA-256 `922476bb7015b80492caf4a8192af63bfd93de813f7bcbebc67c7ec971246067`
- Paving: `masters/approved-v5/environment/surface-old-stone/old-stone-4/revision-2/master.png`, SHA-256 `1c6ee055ab6b72aadcac9e1084b883585abec3e9cf626cd21cd6908ac20af975`

Paving uses revision 2, not the noisier first source. Existing `export_field_material.py` processes the full canvas without crop/rotation/recolor, downsamples to 512/1024 pixels and matches only opposing outermost one-pixel edges using the established averaging operation. Native masters are unchanged. A 128-unit period gives 4/8 raster pixels per logical world unit. Equal boundary pixels do not prove an invisible repeating pattern; the complete scene is the visual evidence. [Base export](base-export.json) and [paving export](paving-export.json) record exact operations and hashes.

`manifest-stone-base-v2.json` owns source-0 identity and UV density. `manifest-stone-paving-v2.json` owns source-3 identity and UV density. Source-3's catalog tone identity does not change terrain selection: it supplies the road/edge RGB. Existing tone-1/2/3 base-atlas behavior stays with its existing owner.

## Geometry and alpha

`manifest-stone-connectors-v1.json` identifies precisely `route-stone-road` and `route-edge-stone-road`. `export_stone_masks.py` retains every alpha byte of both legacy densities and changes only RGB to technical white. This is 2 × 64 existing connector states, not 128 new artwork sources. Mask indexing stays `NESW mask * 4 + variant`, including all four variants and original half-pixel bleed. [Mask export](mask-export.json) records original atlas hashes and every cell's alpha hash.

The renderer samples approved paving RGB with continuous world UV at origin (0,0); no legacy connector RGB is rendered. Tile pieces are self-contained for the existing SVG/GPU bake path. Integer outer bounds avoid applying antialiasing twice at the preserved half-pixel edge. Actual 128-state SVG projections are exactly RGBA-equal to the legacy alpha projection at both display 1x and 2x. A coordinate-gradient technical fixture proves tile phases, internal boundaries and period seams exactly equal a continuous world plane at both densities. [Four raster comparisons](geometry/comparison.json) all have maximum channel difference 0. These are technical fixtures, not new texture art.

## Verification

- Focused TypeScript: 23/23; Python stone: 7/7; existing field exporter: 8/8
- Typecheck and all four app builds passed
- All 60 shipped replay runs are byte-identical before/after, SHA-256 `3ebce01a551fe642db90380e9fb774f0682c1d3a546e8ba81bb8fcab4d90e0d9`; [compressed full stream](replay-events.txt.gz)
- All 16 level/map/state and standing-layer hashes unchanged; only declared ground bindings change
- Complete-scene actor positions/bindings/facing/order/state/camera unchanged; all common PNG sources share identical hashes, detailed in [scene comparison](scene-comparison.json)
- Full sparse-worktree run: 985 tests passed, 3 failed, one further suite failed collection. All four failed files reference missing sparse-worktree inputs: candidate-02 catalog, native fortification material, 45 documentation targets, or the crossing baseline. Exact logs and [status](validation/summary.json) are retained. This is not a complete-suite pass; integration in the complete main tree is required
- Actual BoardView/SvgBoardSurface offline output was reviewed. No live browser/GPU validation is claimed

## Reproduction

Run from repository root, with both registered native sources present:

```sh
python3 tools/art/export_field_material.py --manifest packages/story-candidate-01/assets/final-fantasy-v1/world-materials-v1/manifest-stone-base-v2.json --check
python3 tools/art/export_field_material.py --manifest packages/story-candidate-01/assets/final-fantasy-v1/world-materials-v1/manifest-stone-paving-v2.json --check
python3 tools/art/export_stone_masks.py --check
python3 -m unittest discover -s tools/art -p 'test_stone*.py'
npx vitest run packages/story-candidate-01/src/presentation/candidate-01-stone-material.test.ts packages/story-candidate-01/src/presentation/candidate-01-world-material-tile.test.ts --maxWorkers=1 --minWorkers=1
STONE_REVIEW_OUTPUT=/tmp/stone-geometry STONE_UV_FIXTURE="$PWD/docs/art-rebuild-20261019/approved-v5/stone-world-material/geometry/uv-fixture.png" npx vite-node tools/art/review_stone_material.ts
python3 tools/art/render_stone_review.py /tmp/stone-geometry
SCENE_REVIEW_LEVEL=c01-07 STANDING_REVIEW_OUTPUT=/tmp/stone-scene npx vite-node tools/art/review_gray_flag_scene.ts
```

The full-scene renderer's output must use `render_ground_review.rasterize` to normalize nested viewport clipping and CSS facing for offline Inkscape. Rendering raw nested SVG directly with Inkscape is not equivalent.

### Frozen before/after

Baseline code was `f168558fed31c0645c76fec17e8bbb3140e1c6c1`; engineering tests ran against sparse support inputs while main artwork continued advancing. [Reproducibility](reproducibility.json) records every captured source hash, implementation hash and original SVG hash. Every PNG actually referenced by these two SVGs is frozen under `sources/`. Compressed SVGs have only their href paths relocated to that archive. Decompress each beside its gzip file, invoke `render_ground_review.rasterize(directory, densities=(1,))`, and compare the resulting image. [Archive verification](archived-render-verification.json) records exact full-frame RGBA parity after this relocation. This snapshot remains reproducible even when main's artwork advances.
