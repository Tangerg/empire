# C01 archer's authored shooting action

This slice replaces only the generic `c01.archer` attack binding. First-battle
Elin (`campaign-elin`, unit3) has no separate appearance and inherits this type;
other friendly and enemy C01 archers deliberately share it. The base `archer`,
Bran, Torren, gravekeepers and every other type retain their existing actions.
The archer's idle, movement, hit, death and celebration are unchanged.

Six genuinely drawn poses retrieve one arrow, nock it, draw, release, recover
and return to ready. Spans are 100/100/120/80/100/100 ms, total 600 ms. Release
at 320 ms selects the first empty-bow drawing. The arrow is present during nock
and full draw, then absent from the source for release/recovery. Bow stays in
the left hand and the right hand retrieves/draws/releases throughout.

## Art generation and correction

The built-in image tool used the official
`godot/assets/painted-units-v1/unit-archer-v2.png` reference. Its first idle and
third attack define the blue hood, face, clothes, gloves, boots, quiver and
wooden bow. This adapts [sprite-gen's component-row approach](https://github.com/aldegad/sprite-gen),
not its CLI or Grok pipeline. No separate provider credential was used.

`source-row-v1.png` was generated with `prompt-v1.txt`. Independent review
accepted five poses but rejected the recovery bow, which visibly shortened.
`prompt-v2.txt` requested a localized bow/string correction. The runtime atlas
uses only index4 from `source-row-v2.png`; the other five pictures remain the
original v1 RGBA. Review of this exact combination accepted identity, full bow
length, string/arrow geometry, articulated arms, planted feet and idle handoff.
The rejected v1 recovery is retained only as source history, not in playback.

Both source rows have six significant alpha components (>16/255, >1000 pixels).
Original RGBA within three pixels is retained, with four transparent padding
pixels. Detached background pixels removed from the complete source rows have
maximum alpha 8/255 and 11/255 respectively. `extraction-review.json` records
hashes, exact per-frame source selection, source bounds, anchors and timing.
`verify-extraction.py` reproduces all pixels and checks the live manifest.
There is no repainting, per-pose resizing or interpolated animation frame.

The new sheet uses one uniform scale, 0.0893, to match the established body and
equipment size. Reusing the original sheet's 0.0843373 made this newly drawn
body visibly shorter; the original idle asset and its scale are not changed.
Both mirrored contact sheets show original idle first, then the six new poses.
`anchored-shot.gif` is a single-play offline registration diagnostic, not
captured gameplay or performance evidence.

## Timing ownership and scope

`ActorClip` owns the one source timeline and release. The existing committed
attack activation starts it; main damage does not restart it. Counter, support
and parting shots use their existing source-action path. The `c01.ranger-bow`
policy remains stationary. Source root/contact do not move during shooting,
and the original idle returns at 600 ms. `showAccent=false` avoids placing the
old crescent over the bow and hands. No new clock or combat event is introduced.

Existing `C01-FX-03` is a **target impact**, not a flying arrow. It appears at
release and keeps its 420 ms lifetime, so the complete shot settles at 740 ms.
This batch draws the character letting go of the string; it does not implement
an arrow travelling between cells. Damage, range, accuracy, counter rules,
canonical receipts and attack previews are unchanged. Production integration
is data-only, with no changes to presentation C# or combat rules.

## Verification status

The exact selected source combination and native-size registration passed
independent review. Extraction reproduces the live PNG exactly, and a single
in-memory RGB-channel mutation is rejected. The focused native suite passed
**5,091 assertions**, including exact six-pose sampling, both facings, stationary
root/contact/shadow, 320/600/740 ms boundaries, main and reactive attacks,
visibility, cancellation, rebind, tree exit, coarse ticks and reduced motion.
Its controlled receipts are explicitly synthetic, not legal battle orders.
Build and targeted formatting passed with zero warnings/errors. Three isolated
negative controls each exit1: release320→319, binding→static.attack, stationary
policy→lunge. Production inputs remain unchanged after the checks.

`verification/data-scope.txt` proves the two data documents equal the baseline
when only this asset/clip/binding is removed. All existing independent type and
actor contracts retain their exact expectations. The focused build occurred
before the implementation commit; native recording and the aggregate will use
the post-commit build and retain their own identities.

Actual native battle recordings and the full aggregate remain pending; source
previews and focused checks alone are not final gameplay acceptance.
Exported-build performance and complete-campaign acceptance are not claimed.
