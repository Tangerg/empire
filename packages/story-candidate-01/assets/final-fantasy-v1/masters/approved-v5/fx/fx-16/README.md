# C01-FX-16 — Seven Towers graylight

Four independently authored 1254 × 1254 RGBA phases from the built-in imagegen tool:

1. `short-rays/master.png` — selected corrected source `732e5937...`
2. `rising-fan/master.png` — `ce9afcd2...`
3. `graylight-crest/master.png` — `fe474666...`
4. `residual-rays/master.png` — `85ddaabd...`

Use `integration-catalog.json` for the complete selected hashes and strict catalog entry. All phases use scale 28/1254, origin [2,2], source pivot [627,627] and one unchanged full-source transform. Native cells are 32 and 64 pixels. No crop-to-content, per-frame fitting, shift, alpha cleanup or code color correction was performed.

The original single-tower identity is retained. The crest's formerly clipped central ray now has a complete tip and genuine source headroom. The parent accepted the final corrected quartet's static semantics, native appearance and shared scale on 2026-10-20.

## Timing and scope

Retain four equal 12 fps frames, normal blend, anchor [16,16], loop false. Canonical `qa/12fps-once-*.apng` files encode one play with exact 1/12-second delays, total 1/3 second. `qa/slow-diagnostic-repeat-*.apng` files use 3 fps repetition for inspection only. No current authored consumer or 420 ms removal lifetime is established. This is catalog-only; no repository code, runtime or Git writes were made.

## Evidence

- `source-evidence.json`: exact original board/row/crops, story and current manifest audit
- `final-review.json`: acceptance, checks and caveats
- `qa/COHORT-REVIEW.md`: motion interpretation, ray/stone cohorts and actual limitations
- `qa/material-registration-proof.json`: fixed tower/emitter registration
- `qa/tone-repair-proof.json`: bounded imagegen stone-tone repair and remaining variation
- `qa/motion-and-registration-proof.json`: ray and stone diagnostics
- `qa/source-edge-composite-proof.json`: actual source alpha and composite perimeter proof
- `qa/apng-timing-verification.json`: exact timing and all decoded-frame checks
- `qa/native-32-64-six-backgrounds.png`: six-background native views
- `qa/full-and-terminal-comparison-labeled.png`: full sequence and nonloop terminal comparison
- `qa/*-source-black-white.png`: all four full-resolution source composite pairs
- `failures.json`, `rejected/`: both unselected candidates, including corrected classification of the first crest alternate
- `deliverable-manifest.json`: complete file inventory and SHA256 hashes

Six imagegen calls, four selected sources, two unselected candidates and seven immutable reference snapshots are preserved. Exact requests, prompts, raw/master bytes and source/reference hashes remain available for every attempt. Requested input paths may now contain selected replacements; the immutable snapshots preserve the actual generation inputs.

Remaining caveats: minor material lighting and amber-core changes; +27.0% / +42.9% / -47.0% alpha-mass cadence; a sharp final fan collapse; tiny stones less legible at 32 pixels; one final small stone contacts the rubble. Live browser/game playback is unobserved, and no seamless restart or full physical landing is claimed.
