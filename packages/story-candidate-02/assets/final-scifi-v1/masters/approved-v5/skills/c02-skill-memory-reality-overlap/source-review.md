# 记忆现实重叠 — c02-skill-memory-reality-overlap

## Exact original evidence

Canonical asset `C02-SFV1-skill-memory-reality-overlap` is a 48 × 48 skill icon with center anchor [24,24]. Actual original runtime `packages/story-candidate-02/assets/final-scifi-v1/runtime/skill/skill-memory-reality-overlap.png` has SHA-256 `e60bc17a96ccb44af86b3bab8d01207d94813bf7265d4193952840f24b76f88c` and is preserved unmodified as `source-runtime.png`, including its inherited crop/color artifacts.

The matching full source board is `packages/story-candidate-02/assets/final-scifi-v1/masters/skills/skills-board-03.png`, SHA-256 `2105e869b9263fa4a4ba750ea245e912c60a98fa6d474f4cdf53545e91043e6b`. The cell is row 4, column 3 (one-based), global catalog index 46 (zero-based). Exact legacy grid crop is [632, 945, 935, 1249]; visually located complete motif reference crop is [632, 945, 935, 1249]. Full board, grid crop, complete motif and enlarged original runtime are retained separately. The match was made from actual pixels, not alphabetic ordering. Replaying four original pure builder functions and original chroma postfilters in memory reproduces all 2,304 RGBA pixels with zero differences. Those legacy rules were never applied to generated artwork.

The legacy chroma cutout retains magenta/purple fringe and loses delicate contour information at 48 px. Three profiles and stepped overlap are directly visible in the complete board. Fine pixel blocks simplify; overlap is represented with opaque planes, not transparent glass or a broad aura.

## Selected source and semantics

Exactly three bust profiles overlap: dark left-facing rear profile, blue right-facing middle profile and pale-cyan right-facing foreground profile. Broad opaque planes, separated contours and a large stepped digital seam retain the opposing/overlapping memory-reality cue.

One separate built-in image_gen call produced this source, with transparent_background true. The selected original is `/workspace/scratch/84c9fe948755/generated_images/exec-dbc1d4b9-cc7f-4938-a418-19009dcdcf5a.png`. `raw.png`, `raw-native-v1.png`, `native.png` and `master.png` are byte-identical 1254 × 1254 RGBA files with SHA-256 `f29e62835179680742e8832b505bba0e4f3f967716d20ba56c45c2b8954ac0a5`. No generated-pixel editing, cropping, alpha cleanup, color keying, normalization, recoloring or programmatic drawing occurred. The approved cargo-crate reference supplies contour/material style only. Exact prompt, request, reference hashes and generation provenance are retained.

## Alpha and visual review

Actual source alpha spans 0–255, with 779,044 zero-alpha pixels and 779,814 near-opaque pixels at alpha ≥ 240. The generated source's many near-opaque alpha values are unmodified. Source canvas-edge maximum alpha is 1; there are 0 edge pixels at alpha ≥ 2. Alpha1 edge values were accepted only after full-source and actual 48 white/black/olive composites showed clean edges with no visible broad glow or light curtain. The alpha ≥ 8 bbox is [120, 133, 1146, 1124], margins L/T/R/B [120, 133, 108, 130]. Optional 8px eroded-core diagnostic minimum is 251; no eroded-core pixel is below 240.

The entire authored source is contained into 44 ×44, centered in 48 ×48 with 2px margin using Lanczos, matching the inspected static exporter geometry. The complete final 2px border has alpha 0. The QA derivative is separate from the original and is not installed runtime. Full1254 source, source 512/256 three-background proofs and actual 48 three-background proofs were visually checked; the parent independently checked full and 48px composites and passed static readability.

At 48px the dark profile becomes lower contrast on black but its gray edge still identifies the left-facing contour. Nose and mouth notches simplify; the three heads remain distinct. More than the requested three or four step blocks and several larger memory squares remain, matching the original pixelated idea. No fine facial detail or identity is asserted.

## Integration boundary

Source-ready for parent binding. The canonical 48px and [24,24] anchor remain unchanged; no HD entry is proposed. Scoped read-only searches in candidate02 source and apps found no per-icon live-game consumer. A generic skill-art catalog test exists, but it is not evidence of actual campaign deployment. This worker did not write the repository or Git, run formal export, run game regressions, install consumers or verify live game/UI rendering. These steps remain the parent's responsibility.
