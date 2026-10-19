# Broad 1024-unit sampling promotion

Selected runtime sampling is now **1024×1024 world units** from origin (0,0).
The physical files remain exactly 512² and 1024² pixels, byte-for-byte unchanged.
Schema 2 declares physical `rasterSize` separately from world `logicalSize` and
reports their actual ratio. The selected 1024px file provides **1 source pixel
per world unit**. Rendering it at display 2× is enlargement, not new native-2×
source detail. No source or runtime raster was upscaled for this promotion.

## Evidence retained without duplication

Actual runtime was rendered again after promotion. `promotion-comparison.json`
proves eight full-frame comparisons are pixel-exact: selected broad field and
integration at 1×/2× match the earlier 1024 comparisons, while the high-density
alternative matches the earlier 512 runtime images at 1×/2×.

- Selected full map: [1×](../ground-runtime/integration-period-1024-diagnostic@1x.png), [2× display](../ground-runtime/integration-period-1024-diagnostic@2x.png)
- Selected field: [1×](../ground-runtime/field-period-1024-diagnostic@1x.png), [2× display](../ground-runtime/field-period-1024-diagnostic@2x.png)
- Preserved 512-unit high-density alternative, including its honest x512 seam:
  [full field](../ground-runtime/field-full@1x.png), [integration](../ground-runtime/integration-full@1x.png)

The original filenames still say “diagnostic” because that was their role in
the first commit. Their decoded pixels now exactly match actual selected runtime
output. New generated PNG/SVG duplicates stay local; the source graph, actual
BoardPiece markup and comparison hashes are checked in here.

To select the preserved high-density alternative, set `logicalSize` to the
manifest's `rasterSize` ([512,512]). No raster changes are needed; `--check`
passes with the same bytes, now at 2 source pixels per world unit. A regression
test exercises both configurations and verifies those exact densities.

## Scope and limitations

The 704×448 map has no material repeat at the selected 1024 period. Wider/taller
maps can still reveal a non-periodic source edge at x1024/y1024. This does not
claim the master became seamless. Large building overlap, legacy water/road and
bridge pieces, all terrain masks, occupancy and gameplay rules are unchanged.
These remain offline static renderer diagnostics, not live gameplay screenshots.

## Verification

- Typecheck and experience production build passed
- 162 focused TypeScript tests passed, including all 104 architecture guards,
  every candidate presentation suite, shared viewport and both backend contracts
- All 98 Python art tests passed; physical export `--check` passed
- 8/8 retained-comparison pixel checks passed exactly
- Camera crops of the field pass exactly at display 1× and 2×
- Full-suite baseline was established in the first commit: 791 assertions passed
  with one independently reproduced pre-existing jsdom `scrollTo` error
- Runtime PNG SHA256 values are unchanged:
  - smaller: `99954c969c1e47df23953ddbcf9fc1b7de488b0b29c192dec5ca038a8b46114d`
  - selected: `dc4e5b01150ed1dfed638553fcfef2cc5c5e5ea09d4ab33b9f3ceb89cb0ef003`

Reproduce with `review_ground_runtime.ts`, then run `render_ground_review.py`
against this directory with `--compare-directory ../ground-runtime` expressed
relative to the working directory. The normal commands are in `tools/art/README.md`.
