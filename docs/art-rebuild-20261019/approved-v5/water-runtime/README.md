# Formal area-water binding, 128-unit sampling

Functional integration; visual repeat gate pending. These are offline native
1x/2x renders of the actual BoardPieces, not live gameplay screenshots.

- Full c01-02 extent: 704×448, all 28 water/bridge cells, 9 structures and 20 units
- 128 logical / 256 physical pixels: genuine 2 source pixels per world unit
- Four self-contained masked material blocks share the source texture and reusable
  256-cell alpha atlas. Each block reads actual world neighbours, including across
  block edges. No map-specific PNG is shipped as runtime scenery
- One typed world-material reader owns source, sampling, fit and density. The
  water manifest owns its exact legacy-material binding and occupancy atlas
- The approved full-interior / exposed-shore / concave-notch mask geometry is
  reused for every map. Bridges remain occupied water underneath their deck
- Terrain, owners, movement, collision, capture state, bridge runs and road
  connectivity are unchanged. Other atlas contracts and meadow pixels remain
  unchanged

## Evidence

`integration-full@1x.png` and `@2x.png` show every initial structure/unit and all
legacy shore, road, crossing and scenery layers. `field-full` isolates actual
water BoardPieces. `*-seam-crop` uses world box [288,96,128,256], crossing both
128- and 256-unit material-period boundaries without changing phase.

The full field visibly repeats its large planes and bright ribbons at
y=128/256/384. One-pixel edge matching does not remove that artistic break.
Sampling is retained as the initial approved baseline. A separately labelled
512-period diagnostic is requested; this commit does not select that alternative.
Large overlapping buildings, straight legacy shore strips and old bridge art
remain visible and are separate review topics.

`runtime-provenance.json` records the source graph, occupancy, actual pieces and
map hash. `raster-validation.json` proves exact water-only crop pixels at both
scales, exact native-2x alpha versus approved calibration, exact fully opaque RGB
world sampling, and zero alpha outside occupied cells. Full integration crop
rasterization differs by at most 2 channel values at 1x and 1 at 2x from independent
legacy antialiasing; it is not claimed pixel-exact. No browser/GPU or animation
playback is claimed.

## Verification

- 27 focused integration/metadata tests passed, then 85 presentation/backend
  assertions passed in the broader run
- All 104 architecture assertions passed after sharing the existing square-mask
  owner and registering that scoped presentation boundary
- 103 Python art tests passed, including all 256 masks at both densities and
  deliberately corrupted raster alpha/RGB guard checks
- Typecheck, experience production build, meadow and water export `--check` passed
- 60 deterministic replay cases / 48,029 output lines matched byte-for-byte
  SHA-256: `af965d1720053ac60d409bf960c607714a33665e2f90854da6047e5e7f2b1cd2`
  Baseline was the retained meadow branch 686d817; deterministic sources are
  identical to integration base b6013af (only presentation tests/assets/docs differ)
- The full repository UI suite was not rerun; the previously reproduced jsdom
  `scrollTo` issue is outside this layer

## Reproduce

```sh
python3 tools/art/export_field_material.py --manifest packages/story-candidate-01/assets/final-fantasy-v1/world-materials-v1/manifest-water-material-v2.json --check
GROUND_REVIEW_LAYER=water GROUND_REVIEW_OUTPUT=docs/art-rebuild-20261019/approved-v5/water-runtime npx vite-node tools/art/review_ground_runtime.ts
python3 tools/art/render_ground_review.py docs/art-rebuild-20261019/approved-v5/water-runtime
```
