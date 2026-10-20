# FX07 siege-debris source quartet

C01-FX-07 攻城碎屑: airborne-burst → falling-pieces → falling-remnants → settled-heap. Original identity is FX atlas02 row3: brown broken timber and gray stone fragments. The final phase is a solid settled heap, not a fade. All four independent selected sources were generated with the built-in image_gen tool.

## Source and actual consumer

source-evidence.json and reference/ preserve original full board, exact full-grid cells, row, inset crops, legacy-alpha references and32/64 runtime snapshots. Reference verification proves decoded equality to original repository board/cells and audit snapshots. Consumer source snapshots preserve actual bindings: c01.knight-charge, c01.satchel-charge, c01.forge-burst. The current manifest anchor is[16,16], unlike the historical original audit metadata[16,24]. candidate01FxPicture uses the current anchor and4 frames at12fps, nonlooping. BoardView.animateHit places the effect at target-cell center and removes it after420ms. The white circle and damage number remain separate renderer overlays, not baked into this source.

## Authoring and export

Each phase directory contains untouched byte-identical raw.png/master.png, prompt.txt, exact request.json, reference hashes, source evidence and hashes. Four generation attempts produced four accepted phases; no rejected generation or hidden cleanup. Reference crops are source audit inputs only.

One full-canvas transform:1254×1254 →28×28, origin[2,2] on32×32; sourcePivot[627,627] →logical[16,16].2x uses56×56 at[4,4] on64×64. No per-phase fit, crop, translation, painted repair, color-keying, alpha cleanup or opacity multiplication. Natural silhouette bounds and low settled ground vary about one logical pixel between phases and do not drive the transform. qa/registration-pivot-proof.png makes the common origin visible.

## Verified offline

- Full1254 black/white/green-ground composites inspected for every phase; native32/64 reviewed on black, white, bright stone, green ground and dark earth
- Source edge maxima are[1,1,0,0], with[20,52,0,0] nonzero perimeter samples. Alpha1 numerical residue in first two sources was visually reviewed and preserved. No matte panel or exterior haze
- All32/64 final2px borders are alpha0. First phase reads as a dense burst, second as descending large chunks, third sparse low chips, fourth a solid low wood/stone heap
- Last phase preserves opaque cores; runtime removal at420ms does not imply source fading
-12 APNGs each contain4 distinct decoded frames matching exports, durations1/12,1/12,1/12,17/100 seconds,420ms total, one play,170ms final hold. This is offline timing evidence, not live-game observation

## Integrator handoff

integration-catalog.json lists exact source paths/hashes and transform. final-review.json and final-quartet.json summarize acceptance; deliverable-manifest.json hashes every package file. Start visual review with qa/native-32-64-five-backgrounds.png and qa/registration-pivot-proof.png. Integrator owns runtime copy, binding, tests and Git. This worker made no repository/runtime/Git edits and claims no live-game test.
