# Approved-v5 forest runtime binding: second quartet

## Result

Oak grove, mixed forest edge, hollow stump, and fallen hollow log now use their
approved-v5 sources at both 96 px and 192 px. Each complete 1254² source is mapped
to 86 logical pixels from [5,5]. Reviewed visual support estimates become the
runtime pivots; alpha bounds are measurements only and never determine cropping
or registration. Manifest presentation scale is multiplied once by the existing
scene placement scale.

| Cell | Source support estimate | Presentation scale | Sampled native height |
| --- | --- | --- | --- |
| oak-grove-dense | [625,1090] | 1.625 | 86.97 px |
| mixed-forest-edge | [640,1100] | 1.5 | 83.92 px |
| stump-hollow | [625,995] | 0.75 | 34.32 px |
| fallen-hollow-log | [640,875] | 0.7 | 32.76 px |

The grove and edge remain in the same roughly two-human-height class as the
first forest pair. Their less canvas-filling source composition calls for the
larger presentation factors. The upright stump remains below the 44 px human;
the log reads as a low, wide hollow tube, with its existing scene reflection.
The tree groups, coarse wood faces, olive crowns, ochre highlights, dark teal
shadows, and heavy contours are consistent with the previous quartet.

Native review confirms readable hollows, distinct grove/edge silhouettes,
retained foreground roots, and no clipping or neighboring atlas leakage.
Source support points intentionally precede the forward roots or log mouth;
these visible foreground parts extend 4.2–7.2 px below the red contact line.

## Evidence and scope

- `native-contact.svg` and `native-contact.png` contain actual scene renderer
  markup rearranged onto a native 740 × 210 diagnostic board
- `runtime-scale-report.json` records actual sampled transforms and alpha ≥128
  silhouette measurements
- `integration-check.json` records the baseline commit, unchanged-cell proof,
  source hashes, registration, and exact source-byte preservation
- Only forest-temperate indices 3, 4, 10, and 11 changed at both densities
- The first quartet, other forest-temperate cells, both other scenery atlases,
  and all unrelated manifest data remain identical to the previous commit
- Footprints, obstruction values, scene placement logic, and gameplay are unchanged
- Source provenance is copied with each original PNG; earlier generation-status
  statements in those files are historical source QA, not runtime status
- This is offline native-scale/contact review. Browser gameplay and map-level
  occlusion acceptance are separate checks

## Verification

- TypeScript typecheck: passed
- Focused environment, map-scene, and runtime-raster tests: 18 passed
- Python art suite: 88 passed
- Exporter `--check`, source-byte equality, and unrelated-cell comparison: passed
- Diagnostic extraction and Inkscape rasterization: passed
- Git whitespace check: passed

Reproduce with:

```sh
node_modules/.bin/vite-node tools/art/review_environment_runtime.ts --second
python3 tools/art/render_environment_review.py docs/art-rebuild-20261019/approved-v5/environment-runtime-second
```
