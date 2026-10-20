# c02-skill-switch-rail source review

## Identity and source evidence

Canonical label: 切换道岔. The existing manifest contract is a skill icon at 48×48, anchor [24,24], without an HD entry or animation sequence.

The original actual 48px runtime icon was visually inspected and matched to skills-board-02.png, row 4, column 1 (one-based). Exact generation-reference crop: [34, 913, 315, 1193]. Full board, original runtime and exact cropped reference are preserved. Identity was not inferred from alphabetical final-manifest ordering. The topic catalog independently corroborates the label and function. Board 02 has nonuniform card spacing. The legacy uniform-grid crop [5, 945, 309, 1249] is separately retained as source-legacy-grid-cell.png. Uniform quarter-grid extraction does not match the actual card bounds. The toggle-bulkhead legacy crop includes a cyan sliver from the lower-row gravity card; this contamination survives as a horizontal bar beneath the original runtime. The switch-rail legacy crop starts below its true top edge and omits upper rail portions. The preserved complete-card reference, not the legacy crop, is used for generation. The full actual subject-card boundary was visually verified before generation. No source pixels were recolored or keyed.

No label/pixel conflict was found. The C02 art direction and current static-icon export contract were read. Source and reference hashes are preserved. This work regenerates the same functional identity in the approved chunky matte style.

The original circular rail-switch hub, four splayed paired-rail branches, cyan left/down routing arrow, orange right arrow and selected orange upper-right rail are retained. The old square ground card is removed by generation. Sleeper and small bolt detail is reduced for the icon scale. This is the original functional rail-routing motif, not a new map topology.

## Generation and visual acceptance

Exactly one built-in image_gen call used transparent_background=true. The returned native image is 1254×1254 RGBA. master.png, raw.png and raw-native-v1.png are byte-identical. No generated pixels were repainted, recolored, cropped, sharpened, alpha-keyed or cleaned. There are no rejected generation attempts.

The full native source, source512 composites and actual 48px white/black/olive composites were visually reviewed. Broad outlined masses replace texture noise. Energy and motion are confined to the original skill's bounded action glyphs; they are not free-floating ambient glow. No fake checkerboard, square card background, clipped subject or broad haze is present.

At actual 48px, the four rail branches, circular hub, cyan left arrow and orange right arrow remain recognizable on white, black and olive. Source512 composites show clean open spaces between the branches and no opaque square backdrop. Source side margins are 50/51px, narrower than the prompt target, but the source is unclipped and the full-canvas export keeps its required padding.

## Measured transparency and deterministic preview

Alpha range is 0–255; maximum source perimeter alpha is 1, with 0 perimeter samples at alpha 2 or higher. This satisfies the existing allowance for one-step alpha residue. The actual 48px perimeter is 0. Near-opaque source pixels at alpha 240 or above: 762871. The supplemental 8px-eroded alpha mask has minimum alpha 251 and 0 samples below 240; this remains a supplemental diagnostic, not a substitute for visual inspection.

Actual-size diagnostic export fits the entire authored canvas into 44×44 and centers it on 48×48 with 2px padding using LANCZOS. It never crops to visible bounds. The separate 6× nearest-neighbor image is a debug aid only. The native source is preserved unchanged.

## Handoff boundary

Source-ready for parent integration at packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/skills/c02-skill-switch-rail/master.png. No repository, main/code, selector, manifest or Git edits were made by this worker. The proposed source-overrides fragment is integration metadata only. No formal category count, existing gameplay deployment or live-game proof is claimed here. The parent owns subsequent formal export, registration, tests and any deployment claims.
