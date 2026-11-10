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

## Native gameplay acceptance

Three real native sessions loaded unmodified official campaign saves and used
normal UI commands. `verification/legal-shot/` preserves all 109 legal orders
for each route and replays their complete actions, events, state and official
save roundtrips against the exact final build. Its portable replay passes from
/tmp; repository-contained or nonempty output is rejected with exit 2.

- `native-right-*`: stationary eastward shot, 45 consecutive observations.
  Elin78→78; swordsman98→63, matching preview35. Six poses start at observations
  0/5/11/19/23/30; original idle returns at35, feedback finishes at44.
- `native-move-right-*`: ordinary three-cell movement before shooting, 74
  observations. Six shot poses start at28/34/40/47/52/58; idle returns64, the
  transition finishes73. Target98→63, preview35. Source root is constant only
  during shooting, not during the preceding locomotion.
- `native-mirror-left-*`: three-cell movement followed by a genuinely mirrored
  shot, 74 observations, with the same pose starts as move-right. A guarding
  shield soldier takes15, HP93→78, matching preview; the result panel shows
  78/120 maximum. There is no counter or death in any of these three shots.

An initial planned left route actually faced right. The C01 assembled-ground
registration differs from the generic dimetric basis. Native actor traces and
pixels exposed the mistaken assumption; the retained input `legal-shot/left/`
is explicitly move-right, not left-facing evidence. The derivation harness now
reads the actual registration and checks projected facing and target side.
The separate `mirror-left/` route was then replayed and recorded; its shot
trace is `mirrored=true`. No production projection or combat rule changed.

Independent pixel review accepted right and true left: articulated hands,
blue-hood identity, full bow length, string/arrow continuity, stable feet and
ready-to-original-idle handoff. Full-size frames show no source occlusion in
these two layouts. On observations18→19 (right) and46→47 (left), the nocked
arrow disappears and the target damage indicator begins. Target impact covers
part of the target, not the archer. No travelling arrow is visible or claimed.
Review was frame-by-frame and at native size, not continuous playback or a
performance benchmark. The moved-right session is additional timing/route
coverage, not the basis for the independent two-facing visual verdict.

Each `native-*-lossless.mp4` decodes to RGB bytes exactly equal to every saved
960×640 source PNG. Frame hashes, all raw actor traces, lossless videos,
preview/release/result PNGs and diagnostic phase crops are retained. The
readback capture uses fixed 60 FPS simulation and stride 1; it can slow wall-clock
rendering. Original PNG alpha is not asserted by the RGB video comparison.
All shots use implementation commit `c328a808`, Empire.dll SHA256
`184194d4…fab315`, Core `5130e396…08db`. The full gate passed using these exact same DLL bytes, without metadata
normalization or a build-identity exception.

Exported-build performance and complete-campaign acceptance are not claimed.

## Full native gate

`bash tools/godot/verify-native.sh` completed with exit 0 and no error/leak
diagnostics. It passed **5,091 animation assertions** and **21,783
played-flow assertions**. The latter includes process-frame waits and is not a
count of unique gameplay scenarios. Full output and status are retained in
`verification/full-native-passed.txt` and `full-native-status.json`.

All 16 independent-level action/event/receipt hashes covering 3,425 legal orders
match baseline `36582c1e`. All six played-flow result markers also match:
C01 and C02 victories after 415 legal orders, then 21 orders in C03, plus the
opponent-caused defeat/retry probe. This is not C03 victory or full-campaign
acceptance. All 41 warning lines match the baseline multiset after normalizing
isolated temporary paths; these include intentional save-rejection probes and
existing import/viewport warnings. No valid check was disabled or weakened.

GitHub returned zero workflow runs and zero status contexts for implementation
commit `c328a808`. Its workflow triggers on main pushes, pull requests
and manual dispatch, not this feature-branch push. This is local native gate
evidence, not a remote-CI pass. Export acceptance remains unperformed.
