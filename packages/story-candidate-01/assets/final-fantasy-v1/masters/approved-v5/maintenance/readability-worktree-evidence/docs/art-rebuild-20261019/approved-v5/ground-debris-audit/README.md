# Legacy ground-debris source audit

Read-only: no art, layer, placement, opacity, cell or gameplay changes. The actual
complete-scene capture is checkpoint `36555c4`. At main checkpoint `14b043f`, both
decal runtime pixels and the material table are byte-identical; the scene renderer
only appends a separate edge-decoration pass after the existing ground consumer.

## Eight precise first candidates

All eight below are distinct cells in this existing source chain:

- Master: `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/masters/decals-small-6x4-key.png`
- Key-removed intermediate: `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/intermediate/alpha/decals-small-6x4.png`
- Runtime 2x: `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/decals-small@2x.png`
- Runtime 1x: `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/decals-small.png`
- Semantic registration: `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/manifest-environment-builder-v1.json`, atlas `decals-small`

The atlas is **6 columns × 4 rows**, 24 cells, 64×64 logical pixels / 128×128 at
2x, anchor (32,60). Indices and crops below are zero-based; crop ends are exclusive.
The old master is 1536×1024, so its per-cell crop coordinates are twice the listed
runtime 2x coordinates. Do not use a four-column indexing formula.

| Priority | Cell id | Index | Runtime 2x crop (left,top,right,bottom) | c01-02 instances | Actual scales | Rendered cell-window side |
|---|---|---:|---|---:|---|---|
| 1 | stone-cluster-a | 3 | (384,0,512,128) | 11 | .56,.58,.60 | 35.84–38.40px |
| 2 | stone-cluster-b | 4 | (512,0,640,128) | 8 | .50,.58 | 32.00–37.12px |
| 3 | mud-patch | 6 | (0,128,128,256) | 8 | .56,.58 | 35.84–37.12px |
| 4 | exposed-roots | 13 | (128,256,256,384) | 11 | .56,.58 | 35.84–37.12px |
| 5 | grass-tuft-a | 0 | (0,0,128,128) | 13 | .58 | 37.12px |
| 6 | grass-tuft-b | 1 | (128,0,256,128) | 5 | .58 | 37.12px |
| 7 | grass-tuft-c | 2 | (256,0,384,128) | 9 | .56,.58,.60 | 35.84–38.40px |
| 8 | fallen-leaves | 11 | (640,128,768,256) | 7 | .58 | 37.12px |

Those eight account for 72 of the 88 c01-02 ground decals. Other actual cells are
mossy-boulder (7), animal-tracks (5), cart-ruts (3), bootprints (1). Stone-cluster-c
is a declared river/oathway choice but does not occur in either inspected initial
scene, so it is not counted as a visible priority.

## Actual consumer and size contract

`packages/story-candidate-01/src/presentation/candidate-01-terrain-materials.ts` owns
the consumers: `MATERIALS.plain.decals` (0.58), hill (0.6), road (0.55),
`CANDIDATE_SHORE_DECALS` (0.56), and `CANDIDATE_RIVER_STONES` (0.5). Shore/river
choices take precedence over the material's decals where they apply.

`packages/story-candidate-01/src/presentation/candidate-01-map-scene.ts`:
`groundPieces()` selects a stable cell using tile hashes and emits
`environmentCellMarkup()` at world foot ((x+.5)×32,(y+1)×32). The procedural ground
decals use **opacity 0.72**, with the existing random horizontal flip; they have no
standing depth metadata. This is an actual source-style/registration issue, not a
request to delete low-opacity details or move them above actors.

`debris-inventory.json` gives every actual source index and footpoint, exact hashes,
both maps' counts, and alpha≥128 bounds at every renderer scale. For example,
grass-tuft-a has a visible alpha box 31.90×20.59px at .58; grass-tuft-b is
32.19×22.91px; stone-cluster-b is 27.50×15.50px at .50 or 31.90×17.98px at .58.

## Important old-sheet defect

Some old cells include fragments from adjacent rows: mud-patch contains a lower
burned-twig sliver; exposed-roots contains a separated lower wood fragment;
fallen-leaves contains a lower mossy-rock fragment. These are visibly present in
the **actual runtime cells**, not introduced by the contact sheet. Whole-cell alpha
bounds therefore include contamination and are not clean subject bounds. New art
should be generated/registered per subject and validated per cell, preserving
source alpha and the declared footpoint rather than processing the atlas as one
painted composition.

`debris-source-and-scene-contact.png` pairs the exact existing runtime cell at 2x
with a crop from the complete actual c01-02 scene (1x pixels enlarged 2x nearest).
The source ids and screen footpoints identify each example; objects obscuring the
examples remain present. The complete scene lives in the sibling
`rural-scale-audit/c01-02-actual@1x.png`.

## Similar brown clutter with a different owner

Several larger brown heaps beside buildings are **not** mud-patch: actual standing
`abandoned-cart-stop`, atlas `camps-foundations`, index 13 (row 3, column 1), 144
logical / 288 at 2x, anchor (72,139). Its source is
`packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas/camps-foundations@2x.png`.
The four c01-02 standing examples have scales .308/.357/.302/.340, from
`CANDIDATE_SETTLEMENT_LIFE` via `sceneryPieces()`, yielding 43.49–51.41px square
cell windows. They must retain their standing-object foot/depth contract.

The same atlas also supplies actual flat foundations: village-square-foundation
index 3 (five instances) and watch-post-ground index 11 (four), scale .3, 43.2px
square windows, via `CANDIDATE_FOUNDATIONS` in `groundPieces()`. These are separate
subjects and consumers, not interchangeable with the eight decal priorities.

This audit deliberately excludes rural-life 0–3 to avoid overlapping that work.
