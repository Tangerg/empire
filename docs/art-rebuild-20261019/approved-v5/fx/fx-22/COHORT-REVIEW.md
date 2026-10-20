# FX22 engineering assembly review

Original atlas 06 row 2 contains brass nut, steel bolt, stones, gold impact and blue repair effects. The original 1536 x 1024 RGBA board carries a cloudy backing; mode alone is not alpha proof. The replacement sources are genuine transparent 1254-square images. No wrench or hammer has been invented.

## Phase interpretation

1. loose-hardware: tilted threaded bolt beside the open nut hole, two tiny spark strokes and two airborne chips.
2. seating-impact: angled bolt tip enters the hole at a localized gold flash; four airborne chips accompany it.
3. repair-swirl: upright fitted bolt, warm reflected head light and a blue ribbon passing behind/in front of the shaft.
4. finished-fit: upright completed hardware, cool steel head, one blue star and three blue shards. Four grounded stones remain throughout.

## Motion and registration caveats

- Bolt articulation is real semantic motion: the loose tip sits outside the visible empty opening, the next angled bolt seats into the opening under a gold flash, then it stands upright. Approximate visual lean angles are about 27, 41, 0 and 0 degrees from vertical, so the motion is not monotonic straightening or a physically simulated screw rotation.
- The nut central alpha128 baseline is y1095,1107,1082,1083 in source coordinates: a 25-pixel spread, or 0.558 logical pixels. Fixed ground-stone ROI best offsets reach 28 source pixels, or 0.625 logical pixels, in seating-impact. This exceeds the earlier first-pair 0.34-pixel observation and may produce a small vertical jitter in actual playback. Common canvas placement remains exact; no per-frame fitting or corrective pixel shifting was applied.
- The selected repair-swirl and finished-fit nut/stone ROIs differ by at most 1 source pixel. Four grounded stones are retained in every selected phase. The rejected first swirl omitted those stones and was repaired in one bounded imagegen call, without compositing or alpha cleanup.
- The airborne chip cohort changes from two small fragments to four during impact, then retires before the blue swirl. These are separate from the four persistent ground stones. Unique trajectories or complete landings are not claimed.
- A warm amber reflection appears on the bolt head and thread highlights during the repair/impact poses, then returns to cool steel in the final pose, following the original material staging. It is an imagegen-authored lighting change, not a replacement brass bolt or programmatic recolor.
- The blue orbit is a single authored phase: the far ribbon is occluded by the shaft and the near ribbon crosses in front. The final blue star and three shard glints replace it. Four poses do not establish a full revolution or smooth blue-ribbon particle correspondence.
- Source alpha mass changes by -0.23%, +30.82%, then +1.01%. The upright hardware and broad completion glints increase visible area; the last phase is not a fade or disappearance. Fine thread shading, small stones and sparks are subordinate at native 32, especially on dark backgrounds, and clearer at 64.
- No authored FX22 consumer, scene removal rule, extra hold, 420/500 ms lifetime or runtime loop was found. The sequence is four equal 1/12-second frames, one play. Static native/source review and APNG encoding are verified; actual browser/game playback remains unobserved.

## Verification

Actual full-source black/white composites and colored overview were inspected. Native 32/64 cells were inspected on black, white, bright stone, green ground, dark earth and checker. Full 1–2–3–4 and terminal 3–4–1–2 comparisons were inspected. The terminal comparison is not a runtime loop. Parent accepted the full quartet and terminal comparison as static phase review on 2026-10-20; actual playback remains gated.

Source perimeter maxima are 0, 0, 0, 1 with clean composites; all exports have two logical pixels of alpha0 border. All 28 APNGs and 112 decoded frames match expected exports. Four equal 1/12-second frames and one play are encoded in 14 canonical previews; 14 slow repeating diagnostics are separate. Actual playback remains unobserved.
