# Sparse approved ground edges: c01-02

Technical integration checkpoint, 2026-10-19. Based on integration `5d72a1a`,
including the shared standing-foot depth implementation and all four rural modules.
This is offline evidence from actual runtime BoardPieces and BoardView, not a live
browser/GPU screenshot. All 20 initial actors and all 9 buildings remain present.

## Same-camera complete production viewport

- [With fourteen overlays, native 1x](production/runtime-with-edges-full@1x.png)
- [Without those overlays, native 1x](production/runtime-without-edges-full@1x.png)
- [With overlays, native 2x](production/runtime-with-edges-full@2x.png)
- [Without overlays, native 2x](production/runtime-without-edges-full@2x.png)

These are the complete 852×584 production viewport, including the perimeter forest.
Both use shared standing depth, identical actors, building contacts, source art,
camera, and state. Removing only the fourteen ground placement groups from the
with-overlay SVG produces XML identical to the without-overlay SVG. Provenance
records equal actor data, standing draw order, and state hashes. No selective
canopy removal/fading is applied; initial-pose canopy overlap remains visible.

The separate `integration-full` / `integration-without-edges` files show the whole
704×448 logical field, excluding the production perimeter. `field-full` contains
only the transparent patches. `bank-only-full` and `road-core-full` are actual
runtime layers used for safety checks, not painted validation stand-ins.

## Exact binding and scope

The single authoritative manifest is
`packages/story-candidate-01/assets/final-fantasy-v1/edge-decoration-v1/manifest-edge-decoration-v1.json`.
It references the approved grass-edge revision 2 original (2172×724) and bank-edge
revision 3 master (1024×1536), with immutable SHA-256 provenance. The earlier
[wide calibration](../edge-registration-wide.json) supplies source registration
and visible-scale evidence; its repeated demonstration layout is not a gameplay map.

Six grass patches have 100-logical-unit visible width. Their registration centers
are (112,120), (96,168), (560,120), (560,168), (144,296), and (560,248).
The second, fourth and fifth are vertically mirrored. The first was moved from
x=96 to x=112 after the actual settlement connector mask exposed an overlap.

Eight banks use x=310 on the west and x=394 on the east, with east patches mirrored
horizontally. Each bank has y=64,160,256 at 50-logical-unit visible height and y=368
at 64-logical-unit visible height. These sparse coordinates keep the three actual
bridge rows/landings open. They are authored presentation placements for c01-02,
not universal shoreline tiling and not completion of other levels.

Every complete image is positioned once in world coordinates, crossing tile
boundaries without cell cropping or phase resets. Four PNGs serve fourteen pieces,
which share six unique picture markups. They stay in the ground layer, without a
standing-foot tag. Moving actors may cross the overlays normally and render above
them. Local terrain snapshots are fail-closed rendering preconditions: the full
export canvas plus one cell of neighbours must still match actual terrain, or that
placement is omitted. They never replace or write map state. Changed road/water
geometry dependency hashes also require renewed export/layout review.

## Source preservation and native export

Raw masters are unchanged. The entire source RGBA canvas, including alpha-1
residue and transparent RGB, is copied into a transparent margin before independent
1x and 2x Lanczos resizing. Grass adds 63 source pixels per side; bank adds 68.
Registration includes the same margin offset, so visual placement does not move.
This preserves resampling support around faint source-edge alpha; resizing the
unpadded grass canvas had caused an alpha-4 output border. No crop, alpha erasure,
painting, mask alteration, recolor, blur or seam matching is used. The output
perimeters are alpha 0. The exporter report records rounded raster sizes, actual
pixels per world unit, source/output/alpha hashes and native-density checks.

Meaningful coverage is explicitly alpha ≥2/255; alpha-1 authored residue is retained.
At both 1x and 2x the maximum overlay alpha is **0** in each protected mask:

- Actual opaque route cores, including settlement connectors
- Actual horizontal bridge cells and one-cell left/right landings
- All 20 initial actor-foot rectangles (half extent 12.5×4.2 logical units)
- Water beyond the 13-logical-unit near-bank overhang band

## Camera/crop evidence

World placement is exactly camera-independent in runtime tests. Fractional image
dimensions cause independent Inkscape crop rasterization to differ slightly:
maximum alpha and premultiplied RGB difference are **2/255** at both densities.
This explicit tolerance applies only to transparent authored-edge diagnostics;
existing opaque world-material crop gates remain pixel-exact. Raw unassociated
RGB maxima are 85 at 1x and 255 at 2x in nearly transparent pixels; these are
reported, not called exact pixel matches. Whole integration crop maxima are 3 at
1x and 2 at 2x because it also contains independently antialiased legacy props.
A deliberately shifted one-pixel overlay crop fails the regression guard.

## Visual status and limitations

The broad grass-edge direction was accepted for this technical checkpoint.
**The eight mirrored bank instances still show regular repeated notches.** This
is registered visual debt for later review, not a completed shoreline or complete
scene claim. Existing canopy/actor crowding and building presentation remain
visible in the full production pair. This group adds no new coverage elsewhere
and makes no changes to occupancy, movement, collision, roads or bridge logic.

## Reproduce

Run from the repository root with installed local dependencies:

```sh
python3 tools/art/export_edge_decoration.py --check
GROUND_REVIEW_LAYER=edges \
  GROUND_REVIEW_OUTPUT=docs/art-rebuild-20261019/approved-v5/edge-runtime \
  node_modules/.bin/vite-node tools/art/review_ground_runtime.ts
python3 tools/art/render_ground_review.py docs/art-rebuild-20261019/approved-v5/edge-runtime
STANDING_REVIEW_COMPARISON=edges \
  STANDING_REVIEW_OUTPUT=docs/art-rebuild-20261019/approved-v5/edge-runtime/production \
  node_modules/.bin/vite-node tools/art/review_standing_depth.ts
python3 tools/art/render_ground_review.py docs/art-rebuild-20261019/approved-v5/edge-runtime/production --scene-only
```

Verification: 41 focused TypeScript tests; 135 Python art tests; typecheck;
experience build; 60 exact replay cases with byte-identical transcripts.
The replay transcript SHA-256 is
`af965d1720053ac60d409bf960c607714a33665e2f90854da6047e5e7f2b1cd2`.
All 104 architecture assertions pass with a command-only 15-second test deadline.
The default 5-second deadline timed out on the import-heavy public API guard
(approximately 10.3 seconds); no assertion or repository timeout was weakened.
Full repository Vitest and live browser/GPU pixel tests were not run.
