# FX05 ordinary fire source quartet

Completed source staging: ignite → rise → flare → embers. Original identity is atlas02 row1 (ordinary orange fire), distinct from FX06 gold oath rune rings in row2. Actual consumer is Ivra’s c01.dragon-breath hit at the target cell; no projectile or rule changes.

Every phase has unmodified raw/master RGBA PNG, prompt, request, reference hashes, source evidence, hashes and provenance. All share 1254×1254 canvas, scale28/1254, origin[2,2], pivot[627,627] → logical[16,16]. No per-frame crop, fit, shift, alpha cleanup, opacity normalization or opacity multiplication.

## Registration

The shared effect pivot is at the target centre. The flame root is approximately12 logical pixels BELOW target centre, not at it. Main-flame lower-band estimates span x=16.04..16.67, y=27.74..28.19 in32px cells. This subpixel base-shape variation was retained in the authored sources. Initial requested root[627,1000] was not reproduced by generation; actual accepted first-root registration around[637,1153] was used for later imagegen edits, rather than postprocessing sources.

## Verified offline

- Source perimeters alpha0 in all four selected sources
- Whole-source32px and64px QA transforms have zero alpha on every2px output border
- Native five-background review shows clean orange fire growth/curl/fragmentation/decay, no panel or visible clipping
- Final phase is genuinely translucent: max alpha121, no opaque core; geometry is a collapsed flame plus five detached embers
- All12 APNG files decode to four distinct matching frames, exactly1/12+1/12+1/12+17/100 seconds (420ms), one play; final residue begins250ms and holds170ms
- Full original board and legacy32/64 strips are preserved and decoded-pixel verified against repository sources

## Files to review

- qa/native-32-64-five-backgrounds.png
- qa/12fps-420ms-heldfinal-transparent@2x.apng
- qa/playback-review.html
- final-review.json and qa/qa-metrics.json
- qa/apng-timing-verification.json
- source-evidence.json and source-reference/
- integration-catalog.json is a proposed handoff catalog, not registered/exported by this worker

Rejected initial final phase is preserved at embers-rejected-v1 (correct geometry but near-opaque alpha247). Its imagegen-only correction is preserved at embers-repair-v2 and copied byte-exact to canonical embers. All attempts retained.

## Unverified

No current real-time browser animation or live-game playback is claimed. The existing consumer’s independent white burst circle and damage number are deliberately absent from the art. No repository, Git, exporter or production runtime writes were performed by this worker.
