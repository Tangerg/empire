# Gravekeeper's authored lantern cast

This slice replaces only the generic `c01.gravekeeper` attack-action binding.
The first battle's `field-cleric` is this grey-haired shovel-and-lantern keeper,
not Mirelle or archer Elin. Six drawn arm-led poses raise his lantern, extend it
toward the target, retract and return to ready. Spans are 90/100/110/100/100/100 ms,
totaling 600 ms. Release is at 300 ms, when the fourth drawing extends the lantern.

The two existing magic weapons, `c01.lantern-staff` and `c01.oath-dispel`, and the
existing `heal` ability all deliberately select this unit's stationary `attack`
action. Thus the new art applies to their normal and reactive presentations for
generic gravekeepers. Mirelle, other unit types, the keeper's other actions and
all weapon/ability policies are unchanged. No rule, damage, cooldown, forecast,
recipient effect or additional cast event is introduced. Production integration
is data-only; this is character-pose animation, not a new particle effect.

## Source and extraction

The built-in image tool generated `source-row.png` from `prompt-v1.txt`, with
`godot/assets/painted-units-v1/unit-gravekeeper-gait-v2.png` as the identity/style
reference. Its leftmost idle and existing forward-lantern pose define this troop.
This adapts [sprite-gen's component-row method](https://github.com/aldegad/sprite-gen),
not its CLI or Grok pipeline. No separate image-provider credential was used.

Independent source and native-size review accepted the mature face, beard, hair,
clothing, pouches, bottle, shovel and lantern, their correct hands, whole
silhouettes, planted feet and idle handoffs. This is a restrained arm-led cast:
head, spine and legs remain nearly still. It does not claim the more expressive
whole-body acting suggested by the original prompt. The shoulder/elbow/hand and
lantern positions genuinely change through six drawings.

`extraction-review.json` records source/component hashes, crop geometry, support
anchors, intervals, release and the uniform original 0.0921053 display scale.
Six significant alpha components (>16/255, >1000 pixels) identify the figures.
Original RGBA within three pixels is retained with four transparent padding
pixels; 24,744 detached pixels with at most 6/255 alpha are removed. The packed
runtime atlas has no repainting, interpolation or per-pose resizing.
`verify-extraction.py` reproduces the exact bytes and checks live manifest values.

The first contact-sheet column is official idle, followed by six new poses in
both directions. `anchored-cast.gif` is an offline, single-play diagnostic with
idle before and after, not captured gameplay. Existing contained lantern flame
is part of its equipment; no external glow, projectile or impact is painted in.

## Timing ownership

Only the existing committed attack activation or healing event starts the source
action. `ActorClip` remains the sole owner of release and recovery. The two weapon
policies and heal policy retain `stationary`; actor root/contact do not move.
Main damage events present recipient effects without restarting the caster.
Counter/support/parting-shot use the existing independent source-action path.
The explicit `showAccent=false` keeps the generated arm/lantern unobscured.

The source returns to its original idle at 600 ms. Existing 420 ms strike impact
and 500 ms healing feedback begin at release and outlast recovery to 720/800 ms
respectively. Visibility, cancellation, rebind, tree exit and reduced motion
retain their existing owners. No second clock or competing event fact is added.

## Verification status

Source/static review, exact extraction and independent native frame review have
passed. `verification/animation-passed.txt` records **4,528 assertions**, including
six exact half-open frame intervals, both facings, stationary root/contact,
release/recovery boundaries, unchanged recipient lifetime, counter/support/
parting-shot/heal selection, area victims, visibility, cancellation, rebind,
tree exit, coarse ticks and reduced motion. These controlled-clock receipts are
explicitly synthetic, not evidence of legal battle orders. The build has zero
warnings/errors. The extraction verifier rejects a one-channel pixel mutation;
isolated release, binding and motion mutations each fail with exit 1.

The Torren non-damage check now follows his exact accumulated breathing clock
rather than assuming idle frame zero after the longer healing wind-up. Every
unrelated binding retains its precise prior assertions. Production integration
changes only this unit's action binding and its new asset/clip declaration;
`verification/data-scope.txt` records the semantic comparison.

### Native battle evidence

Three lawful C01 actions were performed through the native interface, recorded
at 960×640 with fixed 60 Hz simulation and every rendered frame retained:

- `native-cast-*`: 73 observations of the keeper at (18,19) attacking enemy13 at
  (18,18). The target takes 16 damage (24→8 HP); the real counter deals 25
  (caster32→7). Both survive. Its complete legal route contains 133 orders.
- `native-heal-right-*`: 48 observations, keeper4 at (24,18) healing guard8 at
  (25,18), 109→112 HP. Nearby allies first move away through normal legal orders.
- `native-heal-left-*`: 48 observations of the same keeper healing archer3 at
  (23,18), 39→69 HP, with an actual east-to-west facing change. Each clear-heal
  derivation has 179 legal orders, including the final heal.

`verification/legal-cast/` contains the original saves, complete derivations,
receipts, independent state/event comparisons and a portable replay harness.
No health, position, facing or enemy state was patched to stage these actions.
Each recording input is byte-identical to its published `before.json`.

Both clear-heal recordings show the whole caster, including boots, shovel, low
lantern, raised arm and extended release. Independent review accepted identity,
equipment continuity, planted feet, both mirrors and the original idle handoff.
Six poses first appear at observations 0/5/11/17/23/30; original idle returns at
35. Each recording finishes with `presenting=false`. Actor root, support plane
and uniform scale remain unchanged. Recipient feedback appears with the release
pose, not beforehand. Exact 300/600/720/800 ms timing is established by the
controlled-clock tests; observation indices are not wall-clock measurements.

Full-frame lossless RGB videos, raw actor traces, per-frame hashes, before/after
screenshots and enlarged diagnostic phase sheets are retained. Every decoded
video frame was compared byte-for-byte against the native PNG RGB pixels. The
videos are fixed-simulation sequences, not a real-time performance benchmark.
Visual acceptance used the captured frames, not continuous video playback.

The attack scene's vertically adjacent damage label and impact FX partly cover
the caster's face/arm. The first crowded healing take was not accepted as proof
of full-body visibility and is not delivered. Clear-heal retakes resolve that
evidence gap, not every possible crowded layout. `oath-dispel` is covered by
binding and controlled-clock checks, not a separately recorded native order.
This batch remains a restrained arm-led cast, not a whole-body acting redesign.

### Build provenance

The recordings used `Empire.dll` SHA256 `23f3ea10…d9cd9c`. The restarted gate
rebuilt after implementation commit `3de50e39`, producing
`fd0bb310…152e1`. Their raw bytes differ because the SDK embeds the Git revision
and resulting build identifiers. `verification/build-identity/` independently
compares the exact frozen recording build with the gate build: all 12,606
Empire and 5,351 Core methods match, and both whole PE files match after narrowly
normalizing the documented commit/MVID/PDB metadata. There are zero unexplained
differences. An IL mutation and a field-metadata mutation both fail the audit.
This is executable-code identity under those explicit exclusions, not equal
raw hashes or identical reflection-visible versions. Asset/data inputs remain
those of the implementation commit.

### Full native gate

`bash tools/godot/verify-native.sh` completed with exit 0, no error/leak diagnostics,
**4,528 animation assertions** and **20,513 played-flow assertions**.
The earlier run was interrupted by an environment disconnect after scenery
checks. This result comes from a fresh full run, not stitched partial logs.
Complete output and
machine-readable status are retained in `verification/full-native-passed.txt`
and `full-native-status.json`. The played-flow assertion count includes
process-frame waits; it is not a count of unique gameplay scenarios.

All 16 independent-level action/event/receipt hashes, covering 3,425 legal
orders, match baseline `c080a45f`. All six played-flow result markers also match:
C01 and C02 victories after 415 legal orders, then 21 orders in C03, plus the
opponent-caused defeat/retry probe. This is not C03 victory or complete-campaign
acceptance. All 41 warning lines match the baseline multiset after normalizing
isolated temporary paths, including intentional save-rejection probes and
existing import/viewport warnings. No check was disabled or weakened.

GitHub reports zero workflow runs and zero status contexts for implementation
commit `3de50e39`. The existing workflow triggers on main-branch pushes,
pull requests and manual dispatch, not feature-branch pushes. This local native
gate is not a remote CI claim.

- Exported-build performance and complete-campaign acceptance are not claimed.
