# c02-skill-rail-calibration source review

## Identity and source evidence

Canonical label: 轨道校准. The existing manifest contract is a skill icon at 48×48, anchor [24,24], without an HD entry or animation sequence.

The original actual 48px runtime icon was visually inspected and matched to skills-board-03.png, row 3, column 3 (one-based). Exact generation-reference crop: [632, 632, 935, 935]. Full board, original runtime and exact cropped reference are preserved. Identity was not inferred from alphabetical final-manifest ordering. The topic catalog independently corroborates the label and function.

No label/pixel conflict was found. The C02 art direction and current static-icon export contract were read. Source and reference hashes are preserved. This work regenerates the same functional identity in the approved chunky matte style.

A receding pair of gray rails on broad dark sleepers remains beneath one cyan calibration reticle. Four separated outer quarter-circle brackets, four cardinal strokes, four outer ticks and the central ring/dot preserve the exact alignment motif. Minor track seams are simplified. Original chroma-key magenta contamination is not carried into the new source.

## Generation and visual acceptance

Exactly one built-in image_gen call used transparent_background=true. The returned native image is 1254×1254 RGBA. master.png, raw.png and raw-native-v1.png are byte-identical. No generated pixels were repainted, recolored, cropped, sharpened, alpha-keyed or cleaned. There are no rejected generation attempts.

The full native source, source512 composites and actual 48px white/black/olive composites were visually reviewed. Broad outlined masses replace texture noise. Energy and motion are confined to the original skill's bounded action glyphs; they are not free-floating ambient glow. No fake checkerboard, square card background, clipped subject or broad haze is present.

At actual 48px, the cyan calibration ring and center dot remain distinct, with the paired rails visible behind them on white, black and olive. Source512 composites confirm bounded opaque graphics without a broad projection glow. The shortest source margin is 41px, narrower than the prompt target; no clipping occurs and actual output perimeter alpha is 0.

## Measured transparency and deterministic preview

Alpha range is 0–255; maximum source perimeter alpha is 1, with 0 perimeter samples at alpha 2 or higher. This satisfies the existing allowance for one-step alpha residue. The actual 48px perimeter is 0. Near-opaque source pixels at alpha 240 or above: 844252. The supplemental 8px-eroded alpha mask has minimum alpha 250 and 0 samples below 240; this remains a supplemental diagnostic, not a substitute for visual inspection.

Actual-size diagnostic export fits the entire authored canvas into 44×44 and centers it on 48×48 with 2px padding using LANCZOS. It never crops to visible bounds. The separate 6× nearest-neighbor image is a debug aid only. The native source is preserved unchanged.

## Handoff boundary

Source-ready for parent integration at packages/story-candidate-02/assets/final-scifi-v1/masters/approved-v5/skills/c02-skill-rail-calibration/master.png. No repository, main/code, selector, manifest or Git edits were made by this worker. The proposed source-overrides fragment is integration metadata only. No formal category count, existing gameplay deployment or live-game proof is claimed here. The parent owns subsequent formal export, registration, tests and any deployment claims.
