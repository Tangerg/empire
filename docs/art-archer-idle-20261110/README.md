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
The reviewer inspected GIF frames and metadata, not continuous playback. Native
loop and shot-handoff acceptance is recorded separately below.

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
when only this asset, clip and idle binding are removed. Focused native
AnimationChecks passed **5,192 assertions**, including 101
new archer-idle checks. Final build had zero warnings/errors, targeted formatter
verification passed, and editor import passed the native diagnostic gate using
the repository toolchain environment. Initial ad-hoc import attempts omitted
that environment and were correctly rejected; they are not passing evidence.

Three isolated negative controls fail with exit 1: idle binding switched to
static, a one-millisecond interval shift preserving total duration, and removal
of archer idle registration. Restored inputs are byte-identical and the focused
positive re-passes 101 assertions. Existing Torren contracts and all other static
idle expectations remain intact; shot tests now resolve the declared idle while
retaining six poses and 320/600/740 boundaries. Controlled receipts in focused
checks are synthetic, not legal played orders.

Native idle and shot-handoff inspection passed as detailed below. The full
aggregate passed using the exact same native DLL bytes, without metadata
normalization or a build-identity exception. This is not full-character or full-campaign
completion. Exported performance and projectile flight are outside this slice.

## Native battle acceptance

All three runs use the implementation at `287f2e42`, the same frozen Empire/Core
DLL bytes as the completed full gate. Their exact hashes are in each native proof.
The current build independently replays the earlier archer-shot legal routes,
including official save serialization, reload and final-action execution. See
`verification/legal-input-replay`. Historical `left` means moving right on the
assembled-ground board; the actual left-facing route is `mirror-left`.

Normal native UI loads those preserved, legally derived saves into isolated
manual slots. No actor, health, facing or battle state was directly edited.
F5 saves the actual UI state after each recording. Each entire saved JSON equals
the expected core-produced before/after JSON, excluding only the two official
`campaign.savedAt` and `battle.savedAt` timestamps:

- Right idle: 84 captures over 336 rendered frames, stride 4. The source remains
  at `[747.5823, 510.94473]`, unmirrored. No command is executed; its final save
  equals the input. Low-breath wraps occur at observations 18 and 51.
- Left idle: the UI first executes the lawful move-and-shot route, with target
  health 93 to 78. The settled source is truly mirrored at
  `[843.5823, 511.488]`. Another 84 captures over 336 rendered frames show
  low-breath wraps at 11, 44 and 77. Its final save equals the expected shot result.
- Right shot recovery: 45 consecutive captures show all six shot poses starting
  at observations 0/5/11/19/23/30. Breathing pose 0 returns at 35; presentation is
  complete at 44. The native preview and actual target health agree: 35 damage,
  98 to 63. Its final save equals the expected stationary-shot result.

Each idle recording covers 5.6 seconds of fixed-60 simulation, longer than two
2200 ms periods, and captures at least two wrap boundaries. These are sampled
observations, not continuous realtime playback. Independent review inspected all
213 captured source-actor crops and their traces. Both directions retain coherent
identity, costume, bow/string and quiver. The chest and free arm move while the
boots remain planted. The shot-ready to idle boundary has no visible size pop
or foot jump. Scale is fixed within each asset: shot 0.0893, breathing 0.072;
the differently sized source drawings render at compatible character size.

The two idle traces keep every actor position constant. Recovery keeps the source
position and support plane constant while the target uses its existing reaction.
The critical source handoff is unobstructed; other scenery/unit overlap layouts
are not exhaustively reviewed. A completion badge appears beside the lower body
at recovery observation 44, after the handoff.

`verification/native-*-lossless.mp4` preserves every captured RGB pixel, verified
by decoding every frame against the original PNG. Original PNG alpha is not
preserved in RGB video. Playback rates are 15 fps for the stride-4 idle samples
and 60 fps for recovery. Proofs retain source PNG/RGB hashes, video hashes, frame
metadata, actual UI saves and selected full-viewport PNGs. The 3× phase sheets
are nearest-neighbor diagnostic crops. Capture readback affects wall-clock pacing;
these recordings do not establish realtime or exported performance. A second
read-only verification independently decoded all 213 video frames, compared
the original PNG/JSON data, fitted both sampled loops to the declared 2200 ms
intervals, and checked the actual UI saves and current/replayed DLL identity; see
`verification/independent-native-verification.json`.

## Full native gate

`bash tools/godot/verify-native.sh` completed with exit 0 and no error/leak
diagnostics. It passed **5,192 animation assertions** and **21,682
played-flow assertions**. The latter includes process-frame waits and is not a
count of unique gameplay scenarios. Full output and status are retained in
`verification/full-native-passed.txt` and `full-native-status.json`.

All 16 independent-level action/event/receipt hashes covering 3,425 legal orders
match baseline `37811bd9`. All six played-flow result markers also match:
C01 and C02 victories after 415 legal orders, then 21 orders in C03, plus the
opponent-caused defeat/retry probe. This is not C03 victory or full-campaign
acceptance. All 41 warning lines match the baseline multiset after normalizing
isolated temporary paths; these include intentional save-rejection probes and
existing import/viewport warnings. No valid check was disabled or weakened.

GitHub returned zero workflow runs and zero status contexts for implementation
commit `287f2e42`. Its workflow triggers on main pushes, pull requests
and manual dispatch, not this feature-branch push. This is local native gate
evidence, not a remote-CI pass. Export acceptance remains unperformed.
