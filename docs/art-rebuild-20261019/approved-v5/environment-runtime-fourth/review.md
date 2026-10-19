# Approved-v5 temperate forest: final quartet

## Result

The final four temperate slots are registered at 96 px and 192 px, completing
all 16 approved-v5 catalog registrations. Each complete 1254² source canvas maps
to 86 logical pixels from [5,5]. Explicit support estimates project into runtime
anchors; authored presentation scale is applied once by existing scene placement.

| Cell | Source support estimate | Presentation scale | Native sampled height |
| --- | --- | --- | --- |
| mixed-forest-autumn | [640,1090] | 1.5 | 86.30 px |
| sapling-rock-cluster | [650,1060] | 0.9 | 38.86 px |
| stump-low | [627,880] | 0.8 | 23.99 px |
| forest-floor-cluster | [628,750] | 0.65 | 17.26 px |

The native review places all four actual scene samples beside the unchanged
90.36 px oak and 44 px swordsman. Autumn forest remains in the mature-tree class,
sparse saplings stay below human height, the broad low stump is distinctly lower
than the earlier hollow stump, and the moss/leaf/fern/pebble cover stays shallow.
Full silhouettes, root overhang, and plant tips are visible without clipping or
adjacent atlas leakage. Palette and coarse outlined forms match the prior family.

## Forest-floor provenance

Forest-floor-cluster is a documented concept reconstruction of unrecoverable
legacy slot 15, not exact recovery of its original subject. Its source, evidence,
review label, and runtime report preserve that distinction. It retains its
existing nonblocking 1×1 footprint and obstructionHeight 0.

All 16 slots now have approved-v5 atlas data and presentation registration.
Leaf-pile's earlier limitation remains: it is catalog-registered but no current
shipped scene selector references it. This change adds no production placements.

## Shared viewport fix

The architecture guard's duplicate declaration was reproduced between
`extract_calibration_scene.ts` and `review_environment_runtime.ts`. Both now use
`candidate_review_viewport.ts`, which owns their shared candidate grid, runtime
tile unit, and scene profile construction. The original construction is removed
from both callers; this is not a variable rename or guard exemption.

Before changing this quartet's atlas data, calibration extraction through the
shared helper produced byte-identical `pieces.json` and `actual-scene.svg` to the
pre-refactor tool. Two focused helper tests cover the runtime extents/insets and
independent viewport creation. The entire 104-test architecture suite passes
with explicit 30-second test/hook timeouts, avoiding the earlier loaded-run
5-second timeout without weakening any assertion.

## Scope and evidence

- `native-contact.svg` and `native-contact.png`: 900 × 210 native renderer samples
- `runtime-scale-report.json`: exact markup, pivots, scales, silhouette measurements,
  and explicit reconstruction flag for forest-floor-cluster
- `integration-check.json`: baseline commit, source hashes, byte preservation,
  unchanged-cell proof, and viewport-refactor equivalence
- Only forest-temperate indices 1, 5, 9, and 15 changed at either density
- The previous 12 cells, both other scenery atlases, footprints, obstruction,
  gameplay, and unrelated manifest metadata are unchanged
- Original source and provenance files are copied without modification
- The previous three diagnostic PNGs reproduce with identical decoded pixels
- Offline evidence does not establish browser gameplay or map-level occlusion acceptance

## Verification

- TypeScript typecheck: passed
- Architecture suite: 104 passed
- Shared viewport helper tests: 2 passed
- Focused environment, map-scene, and runtime-raster tests: 18 passed
- Combined TypeScript run: 124 passed with explicit 30-second timeouts
- Python art suite: 88 passed
- Environment exporter `--check`: passed
- Source equality, four-cell isolation, and unrelated metadata checks: passed
- Calibration JSON/SVG equivalence and previous three PNG regressions: passed
- Native extraction, Inkscape rendering, and Git whitespace check: passed

Reproduce with:

```sh
node_modules/.bin/vite-node tools/art/review_environment_runtime.ts --fourth
python3 tools/art/render_environment_review.py docs/art-rebuild-20261019/approved-v5/environment-runtime-fourth
node_modules/.bin/vitest run packages/battle-engine/src/__tests__/architecture-boundaries.test.ts tools/art/candidate_review_viewport.test.ts --testTimeout=30000 --hookTimeout=30000
```
