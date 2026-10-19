# Black Camp residual decorative clearance

## Result

South-patrol native silhouette coverage falls from **85.956% to 3.097%**. All other 18 actor measurements are exactly unchanged. The complete one-decoration/16-scene/60-replay acceptance proof passes.

## Scope and ownership

Baseline: `c082e89`, captured from the dedicated sparse engineering worktree. The coordinator's main worktree was not edited. No image generation, source-pixel edits, global scaling, gameplay change, remote push, or rule-building relocation is part of this change.

The c01-04 actor `south-patrol` stands at (14,10), world foot [464,351]. Its 85.956% baseline native source-alpha coverage comes from one pure decorative `oak-ancient` drawing at forest cell (14,10), contact [464.29394,357.76]. The forest remains a rule terrain with opaque=true, half cover, obstruction height 2 and defense 0.2. The tree sprite itself owns no capture, owner, scenario target, trigger, or structure entity.

`scout-1` stands at (5,11). Its 64.291% coverage is predominantly the adjacent rule-owned village at (6,11), bound to `C01-STRUCT-JOINT-GRANARY`; that drawing alone contributes 64.268% pairwise coverage. Village terrain remains capturable with heal 20, defense 0.15 and an owner-turn grant. The grain-sack decoration adds only overlapping, tiny coverage, so it is intentionally unchanged. This obstruction is held for a separately authorized rule-building presentation decision. Remaining party actor-to-actor overlap is also unchanged.

## Every production configuration edit

Only `environment-builder-v1/scenes/c01-04.scene.json.gz` changes:

- Append `zones[3]`: id `clearance-14-10`; bounds [14,10,14,10]; role `south-patrol-understory`; authoredTerrainScenery ["forest"]
- Append `placements[3]`: id `fern-bed`; x 14.509185625; y 10.18; layer `under-units`; scale 0.48; flip false

This uses the existing authored-decoration replacement contract to substitute one existing low forest-understory fern for one generated oak. The same exact world contact and foot ordering are retained. No object is silently omitted. Existing well offset and two low-fern replacements remain byte-for-byte identical. The new source, crop, transform, contact and opacity are recorded side-by-side in `review.json.gz` under `derivedReplacement`. No atlas, renderer implementation, actor, terrain, owner, evidence site, exit, trigger, material surface, frame or original PNG changes.

## Complete evidence

- `before/runtime-standing-full.svg.gz`, `after/runtime-standing-full.svg.gz`: complete actual BoardView/SvgBoardSurface captures, 19 actors / 25 rule buildings, full 788×584 production viewport
- Each side has complete native display 1x (788×584) and 2x (1576×1168) PNGs. Both complete 1x maps were visually inspected; the coordinator also reviewed the after map
- Both `runtime-scene-provenance.json.gz` files retain all source hashes and standing draw order; `source-provenance-verification.json.gz` verifies all 25 referenced PNG inputs against captured SHA-256 values, unchanged on both sides
- `full-viewport-pixel-comparison.json.gz`: every differing 1x pixel lies inside [497,336,581,427]; 2x differences lie inside [995,673,1163,854]. These are complete-image comparisons, not acceptance crops
- Both `actor-occlusion.json.gz` files use actual viewport-derived 788×584 masks for every actual standing/frame drawing. All 19 actor results and the no-worse check are retained in `review.json.gz`
- `contract-before/after.json.gz`: all 16 levels preserve level/state, ground, over-unit and editor-ground hashes. Only c01-04's under-unit decoration hash changes
- `standing-before/after.json.gz`: all 16 actual full BoardViews preserve rule-state hashes, actors/buildings and viewport. Only c01-04's full SVG hash changes
- `replay-before/after.txt.gz`: all 60 runs (20 shipped levels × three AI profiles) have byte-identical event streams and outcome digests

## Verification

The new exact oak/fern guard was run against the old scene first: the oak identity and missing fourth authored zone failed as expected. `regression-before-fix.txt.gz` preserves that check. After the fix, focused scene/rule/board tests pass, including repeated hide/show, selected overlays, movement and standing restoration for Mirelle, south-patrol and scout-1. All 19 actors and all 25 buildings remain in the full board. Existing capture, healing, evidence-once, evidence-before-exit, retreat and Laiya-loss contracts are retained.

Typecheck passed. Four source-alpha union/order unit tests passed. The full test runner initially hit 15 missing sparse-checkout fixture reads; the full rerun uses only temporary read-only links for absent tracked asset/evidence fixtures. Main source files were not linked or modified. The full rerun recorded 1371/1373 passing tests in 119 files: one missing-document-target fixture failure and one pre-existing editor assertion expecting the superseded `surface-meadow` marker. After supplying its missing read-only documentation fixtures, all 104 architecture tests passed. The editor failure was reproduced with the baseline scene restored; the coordinator confirms current main already fixes it. No unrelated test was changed. All 13,305 temporary fixture links were removed, and sparse index flags restored before committing. Exact results are retained in the logs and validation summary.

Re-run the acceptance proof from the repository root:

`python tools/art/review_black_camp_clearance.py /workspace/shared/empire-art-rebuild/approved-v5/blackcamp-residual-clearance`

## Limits

This is initial-pose offline full-scene and alpha-mask verification, not live browser/GPU playback or a recognition score. Diagnostic captures include all actors; actual engine visibility and renderer hidden-selection/movement behavior are tested separately. The rule-owned granary/scout obstruction and existing actor-to-actor overlap remain open, so this is not acceptance of every c01-04 readability issue.

## Labeled complete-map images

The following are byte-identical hardlinks to the original full renders, with descriptive filenames only:

- `c01-04-before-residual-oak-full@1x.png` and `@2x.png`
- `c01-04-after-south-patrol-clearance-full@1x.png` and `@2x.png`

Text, JSON and SVG evidence is gzip-compressed. The verifier accepts both plain and compressed evidence.
