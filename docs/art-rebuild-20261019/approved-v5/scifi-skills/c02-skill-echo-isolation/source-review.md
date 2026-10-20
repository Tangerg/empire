# 回声隔离 — c02-skill-echo-isolation

## Exact original evidence

Canonical asset `C02-SFV1-skill-echo-isolation` is a 48 × 48 skill icon with center anchor [24,24]. Actual original runtime `packages/story-candidate-02/assets/final-scifi-v1/runtime/skill/skill-echo-isolation.png` has SHA-256 `e82bc1333c847e33f96ba13d8d5b29c8210d41d15bf5a5f97a9f22f831ceba70` and is preserved unmodified as `source-runtime.png`, including its inherited crop/color artifacts.

The matching full source board is `packages/story-candidate-02/assets/final-scifi-v1/masters/skills/skills-board-02.png`, SHA-256 `1e7fcd63eff342b1f3fc129a9913d78944603a4c19eeaed68e0b9128e2b4d50c`. The cell is row 2, column 4 (one-based), global catalog index 23 (zero-based). Exact legacy grid crop is [945, 319, 1249, 622]; visually located complete motif reference crop is [940, 339, 1222, 618]. Full board, grid crop, complete motif and enlarged original runtime are retained separately. The match was made from actual pixels, not alphabetic ordering. Replaying four original pure builder functions and original chroma postfilters in memory reproduces all 2,304 RGBA pixels with zero differences. Those legacy rules were never applied to generated artwork.

The actual runtime includes the dark scene-card ground and translucent cylinder haze. The redraw preserves the containment silhouette, inner anonymous human symbol and paired echo arcs with opaque bounded forms; it does not depict an actual trapped person or a new gameplay effect.

## Selected source and semantics

One tan/graphite cylindrical canister encloses a pale cyan anonymous human-memory graphic against an opaque blue chamber. Three bounded cyan echo brackets on each side remain distinct. The two end caps, side rails and orange bottom latches preserve the source containment motif.

One separate built-in image_gen call produced this source, with transparent_background true. The selected original is `/workspace/scratch/84c9fe948755/generated_images/exec-3764cf4e-da5a-4a3f-a7e2-f9716c2be449.png`. `raw.png`, `raw-native-v1.png`, `native.png` and `master.png` are byte-identical 1254 × 1254 RGBA files with SHA-256 `147392d223171cc6bf0c1aba909dbd75c49ad89e32dae6d29f3ab37cae0d6d9f`. No generated-pixel editing, cropping, alpha cleanup, color keying, normalization, recoloring or programmatic drawing occurred. The approved cargo-crate reference supplies contour/material style only. Exact prompt, request, reference hashes and generation provenance are retained.

## Alpha and visual review

Actual source alpha spans 0–255, with 766,704 zero-alpha pixels and 778,207 near-opaque pixels at alpha ≥ 240. The generated source's many near-opaque alpha values are unmodified. Source canvas-edge maximum alpha is 1; there are 0 edge pixels at alpha ≥ 2. Alpha1 edge values were accepted only after full-source and actual 48 white/black/olive composites showed clean edges with no visible broad glow or light curtain. The alpha ≥ 8 bbox is [66, 66, 1188, 1194], margins L/T/R/B [66, 66, 66, 60]. Optional 8px eroded-core diagnostic minimum is 251; no eroded-core pixel is below 240.

The entire authored source is contained into 44 ×44, centered in 48 ×48 with 2px margin using Lanczos, matching the inspected static exporter geometry. The complete final 2px border has alpha 0. The QA derivative is separate from the original and is not installed runtime. Full1254 source, source 512/256 three-background proofs and actual 48 three-background proofs were visually checked; the parent independently checked full and 48px composites and passed static readability.

At 48px the simple person and cylinder are readable; small end-cap seams and rim details merge. The original translucent haze is represented as opaque blue chamber planes, and the figure is a symbolic graphic, without a new claim about imprisonment. Visible margins [66,66,66,60] are below the requested 8 percent, but all bracket ends and the canister fit.

## Integration boundary

Source-ready for parent binding. The canonical 48px and [24,24] anchor remain unchanged; no HD entry is proposed. Scoped read-only searches in candidate02 source and apps found no per-icon live-game consumer. A generic skill-art catalog test exists, but it is not evidence of actual campaign deployment. This worker did not write the repository or Git, run formal export, run game regressions, install consumers or verify live game/UI rendering. These steps remain the parent's responsibility.
