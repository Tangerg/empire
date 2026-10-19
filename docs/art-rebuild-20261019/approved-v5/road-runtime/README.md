# Formal approved dirt-road connectors

The selected dirt-road and blank edge companion resolve through the existing
EnvironmentCatalog atlas bindings. The22logical core, NESW16×4 slot contract,
all building/bridge threshold connections and actual terrain state are unchanged.
`connector-overrides.json` owns checksummed source/geometry provenance and the
approved2x hashes. No scene-specific background or authored material repaint is
installed. All four material variants intentionally share the approved earth
revision2 source.

## Native derivation and seam correction

Both2x PNGs are byte-identical to the reviewed connector-v2 outputs. Its old1x
whole-sheet downsample leaked neighbouring atlas slots: up to14alpha and23
premultiplied color values in sampled open joins. That version is retained under
`previous-whole-sheet-1x` and is not selected.

Each2x cell is now independently resized to1x, then packed. This alone removes
all alpha mismatches; remaining RGB edge differences are repaired by the approved
outermost-pixel-only average of existing opposite open-port RGB. All alpha and
all interior pixels are unchanged. There is no mask painting. The final1x/2x
exports pass exactRGBA matching across every opposing open-port pair and all
variants, and disconnected side centres remain closed. The report records
changed pixels, maximum raw/premultiplied/opaque RGB deltas and before/after
hashes. The blank companion remains transparent.

## Actual full-map evidence

`integration-full@1x.png` / `@2x.png` retain the complete704×448 map, all9 buildings,
20 actors, broad512water, the approved road, all old shore strips/decals and old
horizontal bridges. The native road has broad quiet tan sections; its32-cell
texture repetition still exists and is not claimed as a broad world material.

Layer order is now ground → terrain buildings → under-unit scenery → units
sorted by foot y/x → over-unit scenery, matching the runtime order reported by
integration review. Earlier water evidence placed scenery before buildings;
its source/mask/crop proofs remain valid, but its occlusion ordering is historical.
This isolated branch predates the parent's later building-scale/footpoint changes:
large building overlap is visible here and is not hidden. No browser/GPU playback
or animation review is claimed. The field/crop files isolate unchanged water for
sampling comparison; full integration is the road review target.

## Verification

-187 TypeScript assertions, including104 architecture and all16 actual route-mask
 configurations, passed
-107 Python art tests passed, including all native port pairs, exact2x approved
 exports, unchanged1x alpha/interior and deliberate broken-alpha/topology guards
-Typecheck, production experience build and connector export `--check` passed
-60 deterministic replay cases /48,029 lines match the pre-road run exactly;
 SHA-256 `af965d1720053ac60d409bf960c607714a33665e2f90854da6047e5e7f2b1cd2`

```sh
python3 tools/art/export_connector_overrides.py --check
GROUND_REVIEW_LAYER=water GROUND_REVIEW_OUTPUT=docs/art-rebuild-20261019/approved-v5/road-runtime npx vite-node tools/art/review_ground_runtime.ts
python3 tools/art/render_ground_review.py docs/art-rebuild-20261019/approved-v5/road-runtime
```
