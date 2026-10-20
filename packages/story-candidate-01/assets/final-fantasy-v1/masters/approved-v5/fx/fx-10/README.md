# C01-FX-10 River shallows

Four independently authored 1254×1254 RGBA phases: ripple-near → ripple-growing → ripple-wide → ripple-renewal → ripple-near. Seven built-in imagegen calls were used. All three rejected/superseded drawings are retained, with original bytes, exact requests/prompts, hashes and immutable input snapshots. Accepted raw/master pairs are byte-identical.

The original board03 row 2 and its exact four crops are preserved under `reference/`; `source-evidence.json` records mapping, hashes and timing semantics. The subject is two stationary gray-brown stones with broken blue-gray elliptical ripple cohorts.

## Geometry and alpha

Every phase uses one whole-canvas placement: scale 28/1254, logical origin [2,2], source pivot [627,627], logical pivot [16,16]. No alpha-bbox fit, crop, recentering, generated-pixel cleanup, opacity fade or synthetic motion was applied. The output strips are 128×32 and 256×64. Each cell has 2 logical pixels of alpha-zero padding, or 4 physical pixels at 2×.

Source perimeter alpha maxima are 1/0/1/1. The alpha-1 samples are original low-level quantization residue, retained unchanged. Actual black/white perimeter composite differences are at most one channel level. Full-source and native six-background inspection found no visible card, halo, clipping or fringe.

## Timing and review

Canonical timing is exactly 12 fps, four equal 1/12-second phases, infinite repeat, and a 1/3-second cycle. There is no fourth-frame hold and no old 420 ms removal rule. Consumer lifetime is separate.

`qa/apng-timing-verification.json` verifies 28 APNG files and all 112 decoded frames: 14 exact 12 fps loops and 14 slowed 3 fps diagnostics. `qa/native-playback.html` presents them for future browser observation. It has not been observed live in this environment.

`qa/full-and-seam-labeled.png` and `qa/native-32-64-six-backgrounds.png` provide static review. Both stones remain registered to within 2 source pixels (0.045 logical pixels). Outward side cohorts and a separate near-stone renewal generation support a plausible 4→1 seam. The remaining caveats are uneven cohort spacing, ambiguous front-fragment identity, an 18.2% wide-to-renewal alpha-mass reduction, and slight water-color variation. See `qa/COHORT-REVIEW.md` and `qa/motion-and-registration-proof.json` for exact evidence and limits.

## Handoff

`integration-catalog.json` is a strict-schema registration fragment for the parent to merge. `final-quartet.json` lists the four current source hashes. `deliverable-manifest.json` inventories all retained files.

This worker did not edit the main repository, runtime, code or Git. No authored scene consumer currently references FX10. Live browser/GPU playback remains unobserved, and this handoff must not be described as gameplay deployment or verified smooth animation.
