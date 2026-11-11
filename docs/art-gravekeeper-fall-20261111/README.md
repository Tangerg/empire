# Shared C01 gravekeeper fall

This slice replaces only the generic `c01.gravekeeper` death binding. Field-cleric
and bridge-cleric inherit it; Mirelle retains her explicit static death. The
existing breathing, casting, recoil, gait and celebration bindings stay unchanged.
No battle rule, attack preview or runtime animation owner changes.

Six original paintings show buckle, kneel, collapse, contact, settle and still
poses over 60/70/90/110/110/180 ms, totaling 620 ms. The final grounded pose then
fades for 160 ms without shrinking, moving its root or returning to idle. This
intentionally extends the old 220 ms static death presentation to 780 ms, while
preserving the event that starts death and the single retirement path.

The grey-haired keeper retains his beard, scarf, dark coat, pouches and flask.
His right hand keeps the pointed steel shovel and his left keeps the oil lantern.
His head comes to rest on his bent sleeve, with both tools physically supported.
These are distinct anatomical poses, not rotations or translations of one image.
The internal `c01.lantern-staff` capability name does not introduce a visible staff.

## Source and registration

The original 2172×724 generation and exact prompt are preserved. Extraction keeps
341,766 exact original RGBA pixels and removes 187,405 detached pixels whose alpha
never exceeds 8/255. All original pixels above alpha 8 remain. The 2154×405 runtime
atlas adds twelve transparent pixels around each crop, preserving original pixels,
recorded body-support anchors and one uniform scale of 0.1. No per-pose rescaling
or warping is used. Foreground shovel tips may project below the body's support
line; metadata distinguishes this from the anchor rather than fitting each pose.

Both-facing contact sheets and the 26-stage nonlooping handoff GIF are offline
registration diagnostics, not gameplay recordings. Independent review accepts the
source and registration against existing gait, breathe and recoil poses. The
status inside `extraction-review.json` records its original art-only proposal;
this document records subsequent integration acceptance.

`verify-extraction.py` reproduces exact retained pixels and rejects a one-channel
mutation. `verify-registration.py` checks the runtime PNG, geometry, scale, anchors
and intervals. `verify-data-scope.py` proves both production JSON files match
baseline `ea0eb0513e48ed650c2c04d259bfc4baee7f3021` after removing only the new asset,
clip and keeper death binding. `prepare-review.py --reference-root REPOSITORY`
reproduces the offline registration views without editing production assets.

## Ownership and timing

Only the ordered `death` event acquires the death pose. A killed flag, HP zero,
corpse marker or absent final unit does not create another death owner. Existing
recipient recoil ends at 320 ms and damage feedback at 420 ms. Fatal attack damage
keeps its separate 40 ms beat. A generic source releases at 75 ms, so death starts
at 535 ms, the last grounded drawing begins at 975 ms, fade begins at 1155 ms and
retirement finishes at 1315 ms. Archer and keeper-magic sources retain their
320/600 ms and 300/600 ms release/recovery; their fatal receipts end at 1560 and
1540 ms. An active outgoing caster retains its pose until its existing recovery
ends before ordered death acquires it.

The existing receipt clock owns both finite playback and terminal fade. The fall
policy captures initial tint and preserves root position and scale. The final
frame remains opaque for its full 180 ms interval, then holds throughout the
separate fade. The existing path retires geometry and actor exactly once. Results
and later area receipts wait for this full presentation. Duplicate deaths, hidden
units, cancellation, rebind, exit and reduced motion keep their existing semantics.
No timer, death registry, animation callback or combat event is added.

Focused checks cover exact half-open frame intervals, both facings and flat and
dimetric projections, incoming attack/counter/support/parting sources, collision
and status timing, sequential fatal area receipts, caster recovery, tint, ground
contact, root transform, single cleanup and results. These synthetic presenter
fixtures do not create new legal commands or a lifesteal rule.

## Verification status

Source extraction, independent art/integration review, runtime registration and
semantic production-data scope pass. Initial build and editor import pass with
zero build warnings/errors. Formatter and 216 unique script UIDs pass.

Complete AnimationChecks, final-build legal replay, actual native capture,
negative controls and the unchanged full native gate remain pending. Initial
focused playback exposed a demonstrated floating-point boundary sampling error
in an adapted assertion. Corrected integration checks bracket that boundary by
one microsecond while direct exact ActorClip boundary checks stay unchanged. The
second run passed the new fall section but hit the focused wrapper's default
300-second budget. Its failure and shutdown log are preserved. A rerun uses the
full repository gate's existing 900-second per-process budget. Neither runtime
timing nor the official gate script changed.
No native visual, full-campaign, export or real-time performance acceptance is
claimed by this implementation checkpoint.
