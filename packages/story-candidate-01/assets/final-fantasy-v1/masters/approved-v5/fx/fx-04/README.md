# C01-FX-04 blunt ground-impact sources

Selected order: **contact → expand → peak → settle**. All four `phase/raw.png` and `phase/master.png` files are byte-identical copies of genuine built-in imagegen RGBA outputs. The selected settle is `settle-repair-v3`; rejected v1 and unselected v2 are preserved unchanged.

## Identity and placement

The exact original is FX atlas01, row4, columns1–4: hollow ochre elliptical shock ring, paired tan side dust puffs, peak flying stone flecks, then residual ring/puffs. Original full-board and slot evidence is recorded in `source-evidence.json`; the source-crop copies are in `source-reference/`. Consumers are lantern-staff, engineer-hammer, rune-hammer and golem-slam.

Every source is1254×1254. One shared transform is scale28/1254, origin[2,2], sourcePivot[627,627], logical anchor[16,16]. There is no per-frame crop, independent fit, coded shift, alpha threshold, opacity normalization or painted cleanup. The authored impact region is approximately source[627,671], about0.98 logical pixels below the runtime pivot. Natural asymmetric and broken ring contours vary less than one logical pixel in estimated center; details and measured/estimated centers are in `final-review.json`.

## Visual and alpha review

Contact/expand/peak grow distinct rings, dust lobes and stone trajectories; settle loses most geometry and preserves sparse translucent residual arcs/puffs. This is not one image with changing opacity. Final source alpha max106. Source perimeter maxima are0/1/0/0; expand has only9 alpha1 perimeter samples. Full source black/white/color composites and native32/64 five-background sheets show no visible exterior backing, halo or cutoff. All eight native cells have exactly zero alpha across their outer two physical pixels.

- `qa/native-32-64-five-backgrounds.png`: native32/native64 and nearest enlarged views
- `qa/phase-strip@1x.png`, `qa/phase-strip@2x.png`: exact native sheets
- `qa/qa-metrics.json`: alpha, source/output hashes and common transform
- `qa/exporter-plan-verification.json`: real current exporter `plan()` against an isolated temporary fixture passes, with both output densities pixel-exact to the QA strips; no repository writes

## Exact timing and remaining runtime check

Twelve APNGs cover transparent and five composite backgrounds at both densities. They encode four distinct frames with exact rational delays1/12,1/12,1/12,17/100seconds and numPlays1. Total is exactly420ms, with settle beginning250ms and held170ms. `qa/apng-timing-verification.json` verifies all chunk delays and decoded frames. `qa/playback-review.html` supplies a one-play inspection wrapper with removal after420ms.

A standalone APNG keeps its last pixels after playback ends; the actual game's `animateHit` removes the drawing at420ms. The game also draws a separate white expanding circle and damage number, neither baked into the art. Own-cloud headless Chromium failed before opening any page because its socket operation was disallowed; the browser attempt is recorded honestly. No live-game or browser-playback observation is claimed. Art-frame review and exact encoded timing verification passed; parent handles registration and actual runtime integration.

## Registration

`integration-catalog.json` contains the exact selected hashes and owning-pack paths. `final-review.json` is the final selection, semantic, alpha and lifecycle review. Per-phase requests, prompts, reference hashes, provenance, source evidence and file hashes are preserved. All six generation/repair attempts used the built-in imagegen tool. No Git or production files were edited by this worker.
