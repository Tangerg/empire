# FX06 amber oath-fire source quartet

Completed source staging: low-ring → rising-ring → sealed-ring → fading-fragments. Canonical identity is original atlas02 row2: angular golden oath-ring segments with diamond lozenges and an open center. It is distinct from ordinary orange FX05 fire. No letters or readable runes were invented.

## Exact source and consumer audit

`source-evidence.json` preserves the canonical topic audit, original four cell bounds, current metadata, binding and placement evidence. `reference/` includes original full board, full row, four full-grid cells, original/legacy-alpha inset crops, and legacy32/64 strips. Decoded-pixel checks confirm original snapshots. Actual current metadata anchor is[16,16], four frames at12fps, nonlooping. Four weapon consumers are c01.gray-oath, c01.oath-dispel, c01.inquisitor-flame and c01.oath-bolt. Board animateHit places effect at target-cell center and removes it after420ms. White burst and damage number remain separate runtime overlays.

## Source/export contract

All four phases were separately authored through built-in image_gen. Each selected phase retains byte-identical raw/master1254×1254 RGBA PNG, exact prompt/request, reference hashes and provenance. One final-phase correction was generated because the initial fragments remained too opaque; the rejected source and its exact request/provenance are preserved in fading-fragments-rejected-v1.

One common full-canvas export uses scale28/1254, origin[2,2], sourcePivot[627,627] → logical[16,16]. No individual fit, crop, shift, paint, alpha cleanup, opacity normalization or opacity multiplication. Diagnostic bounds centers differ naturally as fragments assemble; they never drive placement. The canvas and pivot are the registration authority.

## Verified offline

- Native32/64 review on black, white, bright stone, green ground and dark earth shows the distinct four-state growth/closure/breakup/decay and clear empty centers
- All native2px borders and central4×4 logical-pixel regions are alpha0
- First two source perimeters are alpha0. Peak/final contain20/3 alpha1 perimeter samples only, visually clean on black/white/color composites. Each source central100×100 region has1–3 alpha1 samples; these disappear at native size. Nothing was cleaned or erased
- Final phase has genuine generated translucency, maxalpha152, no opaque pixel, and6.65% of peak source alpha mass. It is intentionally faint during the170ms final hold
- All12 APNGs decode into four distinct matching frames with exact durations1/12,1/12,1/12,17/100 seconds,420ms total, one play. These are offline timing evidence, not a live-game claim

## Review and integration

Start with qa/native-32-64-five-backgrounds.png, qa/qa-metrics.json, qa/apng-timing-verification.json and final-review.json. Full-size five-background composites are also preserved. Integration-catalog.json specifies exact phase paths/hashes and placement. The integrator must explicitly register this phase order and retain the existing consumer contract.

No repository, runtime or Git changes were made by this worker. Runtime integration and live-game observation remain the integrator’s step.
