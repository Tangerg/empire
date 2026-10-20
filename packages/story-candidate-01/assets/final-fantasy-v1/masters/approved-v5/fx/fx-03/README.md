# C01-FX-03 arrow hit: authored phases and QA

Status: **ART READY under explicitly approved bounded source-alpha tolerance**. Parent reported root approval for source-only perimeter alpha ≤ 1; alpha ≥ 2 still rejects, and final padded output edges remain strictly 0. Burst v2 is selected with exactly four source-edge alpha-1 pixels disclosed. No code changes, repository writes, Git actions, per-frame fitting, or source-alpha cleanup were performed by this worker. Parent owns exporter policy and regression tests.

## Deliverables

- `approach/`: short wooden arrow pointing down-right, before impact
- `impact/`: same arrow tip contacts a cream angular starburst
- `burst/`: selected v2, arrow absent, separated radial cream/brown chips
- `dissipate/`: two small cream shards and four small residual dots
- `rejected/burst-v1/`: preserved original rejected burst and rejection reason
- `burst-repair-v2/`: one targeted imagegen transparency repair, selected under explicitly approved source-only tolerance

Every generated candidate contains `raw.png`, `master.png`, exact `prompt.txt`, exact `request.json`, and `reference-hashes.json`. Raw and master are byte-identical original tool output. Five built-in imagegen calls total: one for each phase, then one parent-authorized burst-only repair. No API fallback was used.

## Mechanical verification

All source canvases are RGBA 1254 × 1254. Shared conceptual pivot is [627,627]. Offline preview uses the entire source canvas uniformly scaled to 28 × 28 and pasted at [2,2] inside a 32 × 32 cell; @2x uses 56 × 56 at [4,4] inside 64 × 64. There is no individual crop, fit, rotation, or translation. The preview resize matches the repository exporter byte-for-byte at both densities.

Historical exact-zero source perimeter results, followed by selected bounded-source result:

- approach: PASS, maximum edge alpha 0
- impact: PASS, maximum edge alpha 0
- burst v1: FAIL, 81 outer-edge samples have alpha 1
- dissipate: PASS, maximum edge alpha 0
- burst repair v2: historical exact-zero FAIL, 4 outer-edge samples have alpha 1; now PASS under explicitly approved source-only alpha ≤ 1 tolerance

Before policy approval, exact exporter errors were `burst: effect outer edge must be fully transparent` and `burst-repair-v2: effect outer edge must be fully transparent`. Historical results are preserved. Parent subsequently reported root authorization for a bounded source-only alpha ≤ 1 tolerance and is implementing the exporter tests separately. This worker did not change the gate. All derived 32/64 preview cells have strictly zero-alpha outer boundaries. Every original source pixel is preserved unchanged.

Selected v2's four source-edge pixels are [0,342], [0,343], [864,1253], [865,1253], each alpha 1. The entire outer 16-pixel band has maximum alpha 1. Alpha ≥ 2 content is bounded by [374,347,1179,1103], safely inside the canvas. Real alpha composites against black, white, olive, and blue were visually reviewed: no visible hard cutoff, opaque plate, rectangular glow, or perimeter trace. Full-resolution composites and measured coordinates are saved in `qa/` and `burst-repair-v2/perimeter-review.json`.

## Semantic and registration review

Inspected all four original high-resolution crops and accepted FX01 before generation. Offline native32/native64 contact sheets were visually reviewed on light, dark, and olive backgrounds. Arrow, flash, chip separation, and sparse final remnants remain distinguishable. The source-approved order is preserved; no actor, bow, target body, number, secondary overlay, opaque plate, or rectangular glow appears.

The authored approach tip is approximately [657,697], then travels down-right to the contact at approximately [719,741]. The contact maps to logical [18.054,18.545], approximately [+2.054,+2.545] from the logical [16,16] pivot. This is documented art content offset, not an export correction. The radial chip field surrounds the same lower-right event region; the last two shards and four dots fade by sparse residual area. No per-frame recentering is used. Contact-sheet progression is coherent at the target sizes.

Final source alpha mass is about 5.60% of impact alpha mass; final native32 alpha mass is about 6.56% of impact. This verifies a pronounced decay rather than a smaller copy of a still-full burst. The tiny source flecks retain opaque cores; antialiasing carries partial alpha. There is no claim that all residual chips are uniformly translucent. Native/timeline previews were regenerated using selected burst v2.

Timing: four semantic phases at 12 fps, starts at 0 / 83.333 / 166.667 / 250 ms, with dissipate held through 420 ms. Offline APNG uses integer-millisecond durations [83,84,83,170]. GIF previews use [80,90,80,170] because GIF time resolution is 10 ms. The APNG plays once; GIF loops are viewing previews only. Runtime loop remains false, normal blend. No actual browser or game-playback review was performed.

## Review files

- `qa/native-32-64-light-dark-olive.png`: native32/native64 plus nearest enlargement
- `qa/timeline-finalhold-420ms.png`: sequence samples through the final hold
- `qa/12fps-finalhold-{light,dark,olive}@{1,2}x.png`: offline APNG clips
- `qa/12fps-finalhold-{light,dark,olive}@{1,2}x.gif`: repeated enlarged viewing previews
- `qa/qa-metrics.json`: geometry, hashes, alpha bounds and masses
- `qa/historical-exact-zero-alpha-gate.json`: actual exporter validation results before approved policy change
- `qa/burst-repair-v2-composites.png`: black/white/olive/blue real alpha compositing review
- `first-pair.json`, `second-pair.json`: backup checkpoints for the original four outputs
- `build-qa.py`: reproducible offline full-canvas QA export only

All four selected phases are art-ready under the approved bounded-source policy. Parent still owns registration, exporter regression tests, and runtime verification. No further generation was performed after the one requested repair; no hidden normalization or threshold erasure was applied.
