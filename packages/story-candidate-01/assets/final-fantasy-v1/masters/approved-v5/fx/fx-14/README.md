# C01-FX-14 — Graveyard pollution

Four independently authored 1254 × 1254 RGBA phases, produced with the built-in imagegen tool:

1. `low-fumes/master.png`
2. `rising-fumes/master.png`
3. `plume-crest/master.png`
4. `settling-wisps/master.png`

## Ready for catalog integration

Use `integration-catalog.json` and its exact selected source hashes. Every phase shares scale 28/1254, origin [2,2] and source pivot [627,627]. Native cells are 32 × 32 and 64 × 64. No per-frame fitting or generated pixel cleanup was used.

Retain the existing nonloop contract: four equal frames at 12 fps, normal blend, anchor [16,16]. Exact one-play APNGs are `qa/12fps-once-*.apng`; each delay is 1/12 second and the sequence totals 1/3 second. The separate `qa/slow-diagnostic-repeat-*.apng` files repeat at 3 fps for inspection only. No scene consumer or 420 ms lifetime is established.

The parent accepted the native/full/terminal static review on 2026-10-20. This bundle is catalog-only. No main repository, runtime or Git writes were made. Browser and live game playback remain unobserved because of the known blocked browser/GPU environment.

## Evidence

- `source-evidence.json`: exact original board, row, crops, current manifest and story identity
- `final-review.json`: checks, acceptance and actual caveats
- `qa/COHORT-REVIEW.md`: phase interpretation and cadence limits
- `qa/material-registration-proof.json`: fixed paper, grass, stone and soil registration
- `qa/motion-and-registration-proof.json`: upper-plume and airborne-paper diagnostics
- `qa/source-edge-composite-proof.json`: original source alpha and actual black/white edge comparisons
- `qa/apng-timing-verification.json`: acTL/fcTL timing and every decoded frame
- `qa/native-32-64-six-backgrounds.png`: native six-background review
- `qa/full-and-terminal-comparison-labeled.png`: 1-2-3-4 and explicitly nonloop 3-4-1-2 comparison
- `qa/*-source-black-white.png`: four full-resolution source review pairs
- `failures.json` and `rejected/`: all four unselected attempts retained
- `deliverable-manifest.json`: complete file inventory with hashes

## Provenance and limitations

Eight imagegen calls produced four selected sources and four unselected attempts. Each attempt retains exact raw/master bytes, prompt, request, provenance, integrity hashes and immutable reference snapshots. Ten input snapshots are verified. Requested input paths may now point to a later selected candidate; immutable snapshots preserve the actual inputs and are authoritative for provenance.

The final soil bounds vary by at most 2 source pixels (0.045 logical pixels), while fixed material ROIs vary by at most 1 pixel. The first rise is large, the crest grows in area, and the final alpha mass drops 26.7%; this is an authored collapse with potentially abrupt cadence, not a fade. Ground papers stay fixed; a separate airborne paper cohort rises and descends. Tiny fragments are less legible at 32 pixels. The terminal pose retains material and wisps, with no seamless restart or physical landing claim.
