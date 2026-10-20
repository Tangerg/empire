# FX17 healing source and consumer contract

Approved by parent on 2026-10-20 at 05:58:28 UTC after source and lifecycle audit. Original atlas05 row1 and current runtime strip were viewed directly.

## Four phases
1. **healing-onset:** compact sage orbit, olive plus, two beige bandage pairs, a few leaves.
2. **leaf-spiral:** developing broader spiral, more lifted leaves, same fixed central plus.
3. **healing-peak:** fullest sage ellipse/orbit, medical plus and bandages, surrounding olive leaves. Generate and review this baseline first.
4. **orbit-dissipation:** sparse leaves and thin broken arcs; cross and bandages disappear, matching the original terminal phase. No fixed leaf bed.

## Actual lifetime
The heal event calls BoardView.animateHeal. The candidate presentation selects C01-FX-17. Its manifest uses four 32-square logical frames at 12 fps, loop false, normal blend, anchor 16,16. The HD source is selected while logical dimensions stay 32.

Under normal motion, frame starts are 0,83⅓,166⅔,250 ms. Frame4 remains visible until the 500 ms tween completes and the drawing is removed. Its total visible dwell is 250 ms, of which 166⅔ ms is after nominal clip completion at 333⅓ ms. The hit-only 420 ms lifetime is not applied. The +amount overlay rises/fades independently; it is not baked into the art. The strip itself is not faded or scaled by this consumer.

## Export and proof
Four independently authored 1254-square RGBA sources use one 28/1254 transform at origin 2,2 and source pivot 627,627. Equal-duration single-play APNGs prove the authored clip. A separately labeled consumer diagnostic may extend only the final displayed dwell to 250 ms and show subsequent removal; this does not create a fifth authored frame or change the runtime contract. Parent owns integration/runtime tests. Static source and encoded evidence do not establish observed browser playback.

Detailed current code excerpts and SHA-256 values are in consumer-audit.json. Current manifest anchor 16,16 takes precedence over historical legacy exporter anchor 16,24.
