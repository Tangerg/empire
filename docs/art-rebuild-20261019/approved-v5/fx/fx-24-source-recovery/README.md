# C01-FX-24 source recovery

All three missing capture phases are generated with the built-in image tool and preserved byte-identically as raw/master PNGs. The approved neutral-standard source was not regenerated or modified. Exact requests, prompts, reference hashes, source hashes and provenance are stored beside every phase.

## Integration recommendation

Use `neutral-standard`, `capture-spiral`, `blue-standard-peak`, `settled-standard` in that order. Use every entire 1254×1254 source at the same scale 28/1254, origin (2,2) inside a 32×32 frame, pivot (16,16). For 64×64, double both scale and origin. Never fit individual content bounds, shift, crop or fade phases independently. The prepared four-frame strips are `qa/phase-strip@1x.png` (128×32) and `qa/phase-strip@2x.png` (256×64) beneath the FX24 master directory.

Preserve four frames, 12 fps, non-looping playback. Each frame is exactly 1/12 second; the clip is 1/3 second. There is no current FX24 consumer or established removal/hold lifetime. Do not add team tint, gameplay ownership changes, or a lifetime borrowed from another effect.

## Review and verification

- Full-resolution source black and white composites reviewed for each new phase; no broad background, vignette or opaque backing is visible.
- Native 32/64 exports reviewed on black, white, bright stone, green ground, dark earth and checkerboard. Pale flag → capture spiral → blue peak → settled blue flag is readable; stone plinth and pole remain visually registered without size pumping.
- All sources are 1254-square RGBA; source perimeter alpha max is 1, within the approved conditional threshold. No alpha was erased or cleaned. Export padding is exactly zero alpha for two logical pixels.
- Gold landmark diagnostic differs by at most two source pixels (0.045 logical px). This is a thresholded registration diagnostic, not a claim that independently generated landmark pixels are identical.
- The QA builder verifies 28 single-play APNG files, 112 decoded frames against exports, and exact rational timing. Browser/live-game animation was not observed here.
- Original neutral SHA-256 is 96a04c13c45c8d146fa4c4f45cec5f5b5f499179d08dbf18ab37661606c775af before and after.

Reproduce: run `build-qa.py` and `check-registration.py` in the FX24 master directory. See `integration-recommendation.json` for complete paths and hashes; see master `qa/qa-metrics.json`, `qa/apng-timing-verification.json`, and `qa/landmark-registration.json` for measured evidence.

No shared catalog, runtime manifest or runtime asset was changed by this source-recovery task, and no commit was made.
