# Approved-v5 forest runtime binding

## Result

The four approved forest cells are registered in both 96 px and 192 px atlas
exports. Each keeps its complete 1254 × 1254 source canvas at 86 logical pixels,
with top-left [5,5]. Explicit authored contacts project into manifest anchors;
scene placement multiplies the manifest's presentation scale once.

| Cell | Source contact | Presentation scale | Native sampled visible height |
| --- | --- | --- | --- |
| oak-ancient | [640,1150] | 1.375 | 90.36 px |
| mixed-forest-dense | [640,1145] | 1.4375 | 89.47 px |
| fern-bed | [625,955] | 0.65 | 24.63 px |
| boulder-large | [640,1048] | 0.6 | 35.91 px |

Heights use alpha ≥128 in the exported HD cells and the sampled scene transforms.
The native contact sheet places those actual renderer groups beside the existing
44 px swordsman. Trees read about twice human height; the fern stays low and the
boulder sits below human height. Trunk/fern contacts align with the ground line;
visible roots and fronds below contact are retained. No clipping or neighboring
atlas-cell leakage is visible in the offline image.

## Evidence and limits

- `native-contact.svg` and `native-contact.png`: actual scene prop markup,
  rearranged into a 740 × 210 native-size review board
- `runtime-scale-report.json`: source-independent runtime anchors, authored and
  sampled scales, alpha silhouette measurements, and exact renderer markup
- `integration-check.json`: baseline commit and decoded-pixel comparison proving
  only forest-temperate indices 0, 2, 7, and 13 changed at either density
- The other 12 forest-temperate cells, all forest-existing/scenery-regional
  cells, and all unrelated manifest metadata remain unchanged
- Source PNGs were not modified; footprints and obstruction values are preserved
- Inkscape uses explicit clip paths equivalent to the renderer's nested SVG
  viewports. This fixes offline atlas leakage without cropping any source PNG
- This is offline native-scale/contact evidence, not browser gameplay, animation,
  or map-level occlusion acceptance

## Verification

- TypeScript typecheck: passed
- Focused environment, map-scene, and raster tests: 18 passed
- Scale regression mutation: omitting presentationScale failed the new guard;
  original code was restored and the focused tests passed again
- Python art suite, including viewport regression coverage: 88 passed
- Environment exporter `--check`: passed with unchanged projected anchors
- Diagnostic extraction and Inkscape rasterization: passed
- Git whitespace check: passed

Reproduction commands are in `tools/art/README.md`.
