# 记忆显影 — c02-skill-memory-reveal

## Exact original evidence

Canonical asset `C02-SFV1-skill-memory-reveal` is a 48 × 48 skill icon with center anchor [24,24]. Actual original runtime `packages/story-candidate-02/assets/final-scifi-v1/runtime/skill/skill-memory-reveal.png` has SHA-256 `e1b6e93093def63a4cd19882bbe9b6201cc4adeba0e2def0275de1f994376239` and is preserved unmodified as `source-runtime.png`, including its inherited crop/color artifacts.

The matching full source board is `packages/story-candidate-02/assets/final-scifi-v1/masters/skills/skills-board-02.png`, SHA-256 `1e7fcd63eff342b1f3fc129a9913d78944603a4c19eeaed68e0b9128e2b4d50c`. The cell is row 2, column 3 (one-based), global catalog index 22 (zero-based). Exact legacy grid crop is [632, 319, 935, 622]; visually located complete motif reference crop is [638, 339, 919, 618]. Full board, grid crop, complete motif and enlarged original runtime are retained separately. The match was made from actual pixels, not alphabetic ordering. Replaying four original pure builder functions and original chroma postfilters in memory reproduces all 2,304 RGBA pixels with zero differences. Those legacy rules were never applied to generated artwork.

The original scene card includes dark floor/wall and a reader back/right housing whose outer extent is clipped by the scene-card edge. The complete card is retained; the redraw reconstructs a compact complete housing around its visible tray and front interface. No unseen mechanical or gameplay effect is asserted.

## Selected source and semantics

One complete stout tan/graphite archive reader carries a front-left upright cyan memory panel showing a dark industrial tower, low remembered buildings and broad clouds. Upper projecting tray and lower control/card pad retain the original relative arrangement. Rear/right housing is reconstructed to complete the portion clipped by the original scene card.

One separate built-in image_gen call produced this source, with transparent_background true. The selected original is `/workspace/scratch/84c9fe948755/generated_images/exec-93a1bb23-72a5-428c-9bb9-0357e1c04f85.png`. `raw.png`, `raw-native-v1.png`, `native.png` and `master.png` are byte-identical 1254 × 1254 RGBA files with SHA-256 `fce89a0c9bd4d34f21efcb083257b0ed40c3d49bebe930a35b8cb48b6d943710`. No generated-pixel editing, cropping, alpha cleanup, color keying, normalization, recoloring or programmatic drawing occurred. The approved cargo-crate reference supplies contour/material style only. Exact prompt, request, reference hashes and generation provenance are retained.

## Alpha and visual review

Actual source alpha spans 0–255, with 730,718 zero-alpha pixels and 828,053 near-opaque pixels at alpha ≥ 240. The generated source's many near-opaque alpha values are unmodified. Source canvas-edge maximum alpha is 1; there are 0 edge pixels at alpha ≥ 2. Alpha1 edge values were accepted only after full-source and actual 48 white/black/olive composites showed clean edges with no visible broad glow or light curtain. The alpha ≥ 8 bbox is [90, 103, 1188, 1175], margins L/T/R/B [90, 103, 66, 79]. Optional 8px eroded-core diagnostic minimum is 251; no eroded-core pixel is below 240.

The entire authored source is contained into 44 ×44, centered in 48 ×48 with 2px margin using Lanczos, matching the inspected static exporter geometry. The complete final 2px border has alpha 0. The QA derivative is separate from the original and is not installed runtime. Full1254 source, source 512/256 three-background proofs and actual 48 three-background proofs were visually checked; the parent independently checked full and 48px composites and passed static readability.

At 48px the tower and skyline simplify into a compact image on a clearly framed blue screen; fine architecture and tray seams merge. The generated olive panels echo the approved style reference rather than exact original tan surfaces. Visible margins [90,103,66,79] are below the requested 8 percent on some sides, but no component is clipped.

## Integration boundary

Source-ready for parent binding. The canonical 48px and [24,24] anchor remain unchanged; no HD entry is proposed. Scoped read-only searches in candidate02 source and apps found no per-icon live-game consumer. A generic skill-art catalog test exists, but it is not evidence of actual campaign deployment. This worker did not write the repository or Git, run formal export, run game regressions, install consumers or verify live game/UI rendering. These steps remain the parent's responsibility.
