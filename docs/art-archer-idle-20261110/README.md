# C01 archer's authored breathing idle

This slice changes only `c01.archer`'s idle binding. C01 begins with six archers
among nineteen units: Elin, ridge-scout, village-runner, vanguard-archer,
south-bow and camp-bow. Elin has no named appearance override and inherits the
generic type. The same type recurs in later rosters and reinforcements. This is
reuse evidence, not a measurement of play time. Base `archer`, Bran, other types
and named overrides remain unchanged.

Four distinct painted anatomical poses play as six intervals, 0/1/2/3/2/1,
over 2200 ms: 500/300/300/500/300/300 ms. The chest, shoulders and free elbow
breathe while the boots stay planted. The upright empty bow remains in the same
hand. This is neither a whole-sprite bob nor six unique drawings. The default
appearance and all non-idle bindings are retained, including the six-pose
`archer.loose` shooting action and its 320/600/740 ms release/recovery/tail.

## Generation and review

The built-in image tool used the official
`godot/assets/painted-units-v1/unit-archer-v2.png`, especially its leftmost idle.
`source-row.png` and `prompt-v1.txt` preserve the original result and prompt.
The workflow adapts [sprite-gen's component-row method](https://github.com/aldegad/sprite-gen),
not its CLI or Grok video pipeline. No external provider credential was used.

Independent source review accepted the four original pictures: identity,
costume, quiver, bow/string continuity, planted feet, gradual neighboring
anatomical changes and native-size compatibility with old idle and shot poses.
The ping-pong order reverses through adjacent pictures; wrap returns from 1 to 0.
The reviewer inspected GIF frames and metadata, not continuous playback. Final
native loop and shot-handoff acceptance remain separate below.

## Extraction and registration

All four significant alpha components (>16/255, >1000 pixels) are retained.
Original RGBA within three pixels is preserved and each component receives four
transparent padding pixels. Only 22,795 detached pixels of alpha at most 8/255
are removed. There is no redrawing, interpolation or per-pose image resizing.

`extraction-review.json` records source/runtime hashes, original RGBA hashes,
bounds, anchors, scale and timing. `verify-extraction.py` reproduces all pixels
and live geometry/clip metadata. It rejects a one-channel in-memory mutation.
The native manifest remains the only owner of runtime geometry and intervals.

One uniform scale, 0.072, matches the source figure's larger pixel density to the
established roughly 35-pixel body height. The boot stance and shared contact
line were registered against the original idle; scale is not independently
fitted per frame. `native-size-contact.png` shows old idle, the four new poses,
and shot-ready, in both directions. The 3× version is a nearest-neighbor
magnification. `anchored-loop.gif` is an offline registration/timing diagnostic,
not captured gameplay or performance evidence.

## Runtime ownership

Production integration is data-only. `BoardView.Idle` owns loop phase, membership,
texture updates, depth/shadow refresh, hidden-board freeze, reduced motion and
finite-action suspension/resumption. `ActorClip.FrameAt` remains finite; looping
is not introduced into the clip. No additional clock, event, receipt or rule is
created. The new idle declares neither release nor death policy.

Movement, static hit/death/celebration and authored shooting still select their
own existing artwork. Successful completion, cancellation and fresh binding
return to the first declared idle frame, then the existing idle owner advances.
Shooting can therefore return to breathing rather than to the old static pose.
No range, damage, legal order, save or attack-preview logic changes.

## Verification status

Source review, pixel reproduction and semantic data scope checks pass.
`verification/data-scope.txt` proves both data documents equal baseline 37811bd9
when only this asset, clip and idle binding are removed. Focused native AnimationChecks passed **5,192 assertions**, including101
new archer-idle checks. Final build had zero warnings/errors, targeted formatter
verification passed, and editor import passed the native diagnostic gate using
the repository toolchain environment. Initial ad-hoc import attempts omitted
that environment and were correctly rejected; they are not passing evidence.

Three isolated negative controls fail with exit1: idle binding switched to
static, a one-millisecond interval shift preserving total duration, and removal
of archer idle registration. Restored inputs are byte-identical and the focused
positive re-passes101 assertions. Existing Torren contracts and all other static
idle expectations remain intact; shot tests now resolve the declared idle while
retaining six poses and320/600/740 boundaries. Controlled receipts in focused
checks are synthetic, not legal played orders.

Actual native idle recordings, shot-handoff inspection and the full aggregate
are still pending. This is not full-character or full-campaign
completion. Exported performance and projectile flight are not part of this
idle slice.
