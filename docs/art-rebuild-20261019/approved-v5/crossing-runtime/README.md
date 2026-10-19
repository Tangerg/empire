# Registered horizontal crossing binding

The three original c01-02 runs now use the approved stone crossing, each drawn
once at its unchanged run midpoint: [352,112], [352,208], [352,304]. The reviewed
full1729×910 canvas, source contact[864,418] and scale0.08098336948662328 are the
single registration owner in `registered-crossings-v1/manifest-crossing-v1.json`.

The logical canvas is140.020245842×73.694866233 and the contact is
[69.969631236,33.851048445]. It is not cropped into the old96×96 atlas or stretched
to a run length. Both140×74 and280×147 raster exports are byte-identical to the
previously approved registered images. These are independent full-canvas native
resizes, with only integer raster rounding; actual pixels/world-unit are recorded
per axis. Raw source pixels/alpha are immutable. No recolor, blur, rotated fake
vertical counterpart or new painting is used.

The run midpoint/orientation/grouping logic is unchanged. Tests cover one through
four cells in both orientations. The authored bridge remains fixed-size; wider
runs may need separate reviewed span art rather than stretching this image.
Both complete old crossings/fortifications atlases and all vertical bridge
presentation remain unchanged. An authored vertical counterpart is still missing.
The interactive three-state `C01-STRUCT-THREE-BRIDGE-SPAN` topic is a different
asset and is not repurposed or modified.

## Actual integration

The full704×448 offline BoardPieces frames retain all9 buildings and20 initial
actors, broad meadow/water, formal roads, old shore/decals and all three crossings.
Layer order is ground, buildings, under-unit scenery, units by foot y/x, then
foreground. `runtime-provenance.json` records registered crossing placements.
These are not live browser/GPU screenshots or animation acceptance.

This branch is based on b6013af and intentionally does not include the parent's
parallel building-scale/footpoint correction. Large building overlap remains
visible here; final complete-map overlap review belongs to the integrated frame.
Legacy vertical art and shoreline still visibly differ from the approved style.
Water crops/alpha remain exact; full composite crop differences are separately
reported as rasterization/antialiasing, not hidden.

## Verification

-190 TypeScript assertions initially passed, including104 architecture; final
 focused122-assertion rerun passed with an additional full legacy-atlas checksum guard
-110 Python art tests passed; source/raster graph, full-canvas resampling,
 registration rejection and approved export equality are covered
-Typecheck, production experience build and crossing export `--check` passed
-60 deterministic replay cases /48,029 lines match the pre-crossing run exactly;
 SHA-256 `af965d1720053ac60d409bf960c607714a33665e2f90854da6047e5e7f2b1cd2`
-No game rule, occupancy, movement/collision, terrain, bridge access or
 `candidate-01-runtime.ts` changes

```sh
python3 tools/art/export_crossing.py --check
GROUND_REVIEW_LAYER=water GROUND_REVIEW_OUTPUT=docs/art-rebuild-20261019/approved-v5/crossing-runtime npx vite-node tools/art/review_ground_runtime.ts
python3 tools/art/render_ground_review.py docs/art-rebuild-20261019/approved-v5/crossing-runtime
```
