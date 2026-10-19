# Approved-v5 forest runtime binding: low-cover quartet

## Result

Dark bramble, berry bramble, leaf pile, and the final muted boulder/fern grouping
are registered in the 96 px and 192 px atlas exports. Each uses its complete
1254² source at 86 logical pixels from [5,5]. Source support estimates project
into manifest anchors; presentation scale is applied once by the scene renderer.
No source pixels, alpha, source canvas framing, or production selectors changed.

| Cell | Source support estimate | Presentation scale | Native sampled height |
| --- | --- | --- | --- |
| bramble-dark | [625,990] | 0.8 | 28.73 px |
| bramble-berries | [625,960] | 0.85 | 27.72 px |
| leaf-pile | [628,680] | 0.4 | 16.00 px, catalog probe |
| boulder-ferns | [625,1015] | 0.65 | 35.31 px |

The 900 × 210 native review includes the unchanged 90.36 px oak and the 44 px
swordsman. Both brambles stay below the unit's height; the berry groups remain
small readable red accents. The boulder/fern group is complete and muted, with
all plant tips visible. Leaf pile remains a shallow roughly 33 px-wide ground
patch, never tree-sized. Silhouettes, contacts, and retained foreground overhang
look coherent; no clipping or adjacent atlas-cell leakage is visible.

## Leaf pile availability

Leaf pile is registered and renderable from the catalog, but no shipped scene
selector or authored placement currently references it. Its review draw uses
the real atlas-cell renderer, the registered anchor, and presentationScale 0.4
at placement scale 1. It is explicitly labeled catalog-only, with zero sampled
scene instances. This change does not add a new production placement or claim
that leaf pile is already visible in gameplay.

The other new cells are sampled from actual procedural scenes: brambles from
forest-floor placements, boulder-ferns from mother-root scenery. Reference oak
uses its existing forest placement. All measurements use alpha ≥128 solely for
reporting; neither registration nor export infers a crop from alpha bounds.

## Provenance and scope

- `native-contact.svg` and `native-contact.png`: native scene samples plus the
  explicitly identified leaf catalog probe
- `runtime-scale-report.json`: exact markup, evidence type, sampled scales,
  pivots, and silhouette measurements
- `integration-check.json`: baseline commit, source/provenance hashes,
  unchanged-cell proof, and leaf placement limitation
- Only forest-temperate indices 6, 8, 12, and 14 changed at either density
- The previous eight approved cells, the other atlas cells, both other scenery
  atlases, footprints, obstruction values, and unrelated manifest data are unchanged
- Every copied source/provenance file was compared byte-for-byte with its shared
  original, including boulder-ferns/attempts and prompt-final.txt
- Source QA files' generation-time pending status is historical; this review
  records the subsequent registration and its actual coverage
- First- and second-quartet native diagnostic PNGs were reproduced with this
  revision and have identical decoded pixels to the committed earlier reviews
- Offline review does not establish browser gameplay or map-level occlusion acceptance

## Verification

- TypeScript typecheck: passed
- Focused environment, map-scene, and runtime-raster tests: 18 passed
- Python art suite: 88 passed
- Environment exporter `--check`: passed
- Recursive original-source equality and unrelated-cell/metadata checks: passed
- Third-quartet extraction and Inkscape rendering: passed
- Both earlier diagnostic render regressions: passed with identical pixels
- Git whitespace check: passed

Reproduce with:

```sh
node_modules/.bin/vite-node tools/art/review_environment_runtime.ts --third
python3 tools/art/render_environment_review.py docs/art-rebuild-20261019/approved-v5/environment-runtime-third
```
