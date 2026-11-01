# C07 independent fence — bounded static design study

2026-11-01. **Not production-bound and not gameplay.** External fixture composes the existing BoardGroundView/BoardView projection, depth and picking owner. No engine command, AI, campaign carryover or real save is accessed. Canonical initial actor clones stand only on legal paved cells. Production `c01-07.json`, rules and terrain are unchanged.

## What is original versus designed

Three original wood feet remain (950,208)/(963,215)/(987,228), with original cap-interior witnesses (949,177)/(965,185)/(987,198). The middle cap proposal initially omitted its frozen witness; its corrected top contour retains the foot. Cap-interior-to-foot heights are 31/30/30 source pixels; total silhouette heights are 31/31/30. Old cap-shape negative is explicitly a reconstructed historical fixture, not an original retained run.

Three original confirmed gaps (954,194)/(972,203)/(977,207) remain empty. The original upper beam (976,194) stays wood. Background cargo cannot safely be traced as a full X: two small warm edges do not establish complete braces. Therefore **two narrow connected braces are a new timber design**, not pixel recovery; their ends meet the upper/lower structural frame. The same three old gaps and all positive landmarks are retained.

Texture comes only from verified plain wood in rejected whole-fence v2: UV rectangle [95,180,132,600), 15540 actual pixels, alpha252–253. The rejected silhouette is never used. UV grain follows each member's longitudinal axis; face modulation tones down the orange. No full-canvas alpha deletion/threshold change or new generation cycle. Source polygons are the single geometry owner for visible material and coverage. Vertical partitions use the support line between real posts for suspended rail depth; they are not claimed to be extra physical feet. This is a static renderer experiment, not an adopted production-format extension or performance acceptance.

## Final native checks

Fresh fixture DLL compiled with 0 warnings/errors. All 11 frozen material witnesses passed, renderer issues0 and original state digest unchanged. Torren behind at (60,12): 102/102 shared wood pixels hidden; at (61,12): 7/7 hidden. Front (62,14): 48/48 shared pixels remain pickable. Forcing the real wooden columns behind the actor exposes all 5 sampled hidden pixels (depth negative control).

The three frozen original gaps did **not** overlap Torren's opaque pixels in these poses; this is not reported as an actor-through-original-hole pass. The **new designed opening** has 9 real opaque/pickable actor pixels at legal (60,12), including (957,191). This point is explicitly not claimed to be a verified old original gap. An intentionally solid-pane mutant makes the exact actor pixel non-pickable and fails all three original gap assertions (expected process exit1); good design exits0. No illegal pose was introduced to force a hole overlap.

Actual same-camera native images: `c07-static-preview/fence-closeup-painted-v2.png` and `fence-closeup-designed-braces-v2.png`; diagnostic zoom4, 1180x812. Earlier frame-only 1.25 images are `atomic-fence-61-12-v1.png`/`atomic-fence-62-14-v1.png`. Captions state isolated study/legacy placeholders; surrounding city, foundations and other objects are not restored. All shots and reproducible fixture project are in the private recovery bundle, not claimed as a complete city/art/gameplay acceptance. Canonical real C06 remains paused at its previously saved T1.

## Reproduction

Restore the external `c07-static-preview` project from recovery and point its assets/data symlinks and Empire/Empire.Core references to this checkout. Build `Probe.csproj`, run `res://AtomicFenceProbe.tscn` headlessly; default geometry is designed-braces-v2 copied as `atomic-fence-geometry.json`. Set `C07_ATOMIC_GEOMETRY_PATH=res://atomic-fence-geometry-filled-negative.json` and `C07_STATIC_RESULT_TAG=filled-negative` for the expected-failing mutant. These switches exist only in the isolated fixture, not the game. GUI captures are launched by its supported script through the native terminal.
