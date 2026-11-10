# C01 archer's authored hit reaction

This slice changes only `c01.archer`'s hit binding. The first battle contains six
units of this shared type, including Elin and friendly/enemy archers. Base
`archer`, Bran and unrelated named overrides remain unchanged. Existing breathing
idle, six-pose shooting, movement, static death and celebration are retained.

Four independently painted poses show onset, peak recoil, recovery and ready.
Their 40/110/90/80 ms intervals total 320 ms. This intentionally replaces the
archer's **150 ms static hit hold**, not an earlier 320 ms archer animation.
The authored reaction remains inside the existing 420 ms impact window, so it
adds no delay to the command endpoint, counterattack or separate fatal beat.

## Source and registration

The built-in image tool generated `source-row.png` with `prompt-v1.txt`, using
the official `unit-archer-v2.png` and accepted breathing row as references.
This adapts [sprite-gen's component-row method](https://github.com/aldegad/sprite-gen),
not its CLI or Grok pipeline. No external provider credential was used.

Independent review accepted four actual anatomical redraws, not transformed
static pictures or procedural effects. Hood, face, clothing, quiver, bow/string
and holding hand remain coherent. The empty bow stays held throughout.
Initial unregistered-sheet concerns about stance width and recoil strength were
resolved by inspecting the common-scale, common-contact comparison. No source
redraw was needed. Runtime visual acceptance is separate below.

The four significant alpha components (>16/255 and >1000 pixels) retain their
original RGBA within three pixels. Extraction removes 47,301 detached pixels
of alpha at most 6/255 and adds four transparent padding pixels. There is no
painting, interpolation or per-pose resizing. `extraction-review.json` records
source/atlas/pixel hashes, rectangles and support anchors; `verify-extraction.py`
reproduces the exact atlas and live manifest geometry/timing. A one-channel
in-memory pixel mutation is correctly rejected.

The atlas is 1495×563. All four poses use one scale, 0.072, matching the accepted
breathing art. `native-size-contact.png` compares breathing pose 0, the four hit
poses and the older default standing pose; the second row mirrors them. The 3×
copy uses nearest-neighbor magnification. `anchored-loop.gif` is an offline
idle→hit→idle registration diagnostic, not gameplay or realtime performance.

## Ownership and unchanged boundaries

Production integration is data-only. `BoardView.Impact` remains the sole owner
of recipient hit playback, sharing the existing receipt clock with its impact.
Positive damage starts the declared hit after the attacking source's release.
At 320 ms, the receiver resumes its declared breathing idle; the independent
impact still lasts 420 ms. Healing, regeneration and zero damage select no hit.

The same owner handles main/area/counter/support/parting attacks and collision
or status damage. Existing source recovery and same-actor exclusivity are
retained. Fatal damage retains the separate 40 ms beat, then only an ordered
death event starts the unchanged 220 ms static fade/shrink. No timer, event,
battle rule, save schema or attack-preview logic is added or changed.

## Verification status

Source art, common-scale double-facing registration, exact extraction and a
single-channel negative control pass. `verification/data-scope.txt` proves that
both data documents equal baseline `900717b1` after removing only the new asset,
clip and shared hit binding. Focused native AnimationChecks passed **5,596
assertions**, including 404 new archer-recoil assertions. The final build has
zero warnings/errors; isolated editor import and targeted formatter verification
passed. The production-art timing checks use explicitly synthetic receipts, not
legal played orders.

Five isolated negative controls each fail with gate exit 1: static hit binding,
a one-millisecond interval transfer preserving total length, one-millisecond
early recoil sampling, recoil on zero damage and an extra millisecond of impact
tail. Restored inputs are byte-identical, the 404-assertion positive re-passes,
and production runtime/data hashes remain unchanged. New test fixtures sample
absolute clock targets or explicit before/after microsecond bounds to avoid
fractional accumulation changing which side of a boundary they observe.

Legal-battle native recordings passed within the visibility limits below. The full
aggregate is still pending. No full-character, full-campaign or export acceptance
is claimed.

## Native legal exchange

The accepted recording uses the existing southern route, with normal AI moving
enemy archer 18 from (26,20) to (25,20), shooting Elin 3 at (22,20) for 39 damage,
and Elin countering for 20. Both survive at 39/78 and 58/78 HP respectively.
No state, HP, facing, camera or receipt was patched. The route is reproduced
through actual campaign/core operations and independently compared at every
order; see `verification/legal-hit/README.md` and its rerunnable harness.

Normal AI then ends its turn. The actual F5 save at human round 9 equals the
complete official `southern-bows/ui-settled.json`, excluding only the two writer
timestamps. This includes normal status removal, resource grants, regeneration,
readiness, events and all other fields, not just the two archers' HP. The earlier
settlement route also matches its legal save but is visually roof-occluded.

`verification/native-southern-lossless.mp4` preserves **95 consecutive 960×640
frames**, stride 1, encoded at the fixed simulation rate of 60 frames/s. Every
decoded RGB frame matches its original PNG byte-for-byte. PNG alpha is not stored
in the video. JSONL preserves original actor records; the proof records per-frame
PNG/RGB hashes, video hash, exact DLL hashes and full saved-state comparison.
Capture wall times include screenshot readback and are not a performance test.
The capture does not establish an exact zero-time first-frame offset.

All 95 frames were reviewed for both archers, totaling 190 actor crops. Both
facings visibly show four anatomical recoil poses, stable root/support anchors,
consistent identity/equipment and correct idle/counter handoffs. See the two
`native-southern-unit-*-phases-3x.png` sheets and `native-visual-review.md`.
Existing damage numbers cover central body details through both recoils; a
completion badge covers part of Elin's lower leg/bow. Native acceptance concerns
the visible silhouette, head/shoulders, bow, grounding and handoffs, with the
unobstructed source-art inspection supplying the covered-detail review.

The first settlement capture preserves 99 frames but a house roof hides most of
unit 9. Its `native-dual-*` files document legal inputs/state and the visibility
failure; they are **not** counted as a complete two-direction art pass. No scene
or UI changes were made to disguise either recording's limitations.

Native capture, legal replay and the full gate use exactly the same frozen DLL
bytes from implementation `8893ad55`: Empire SHA256
`827d5a4edf0f3fb076f5d6d17ec2031fff1656e836f16ea5e5120d3b714169fd`, Core
`e757b4cd21b9d0c3a552a1b5b7a803f3573caa097cd9ea1737a34cd6eb9fb476`.
The native recording itself does not test cancellation, reduced motion or death;
the focused native assertions cover those presentation contracts separately.

## Recheck the archived native evidence

A second read-only verification independently decoded all **194/194** frames
against the original PNGs and checked JSONL against the original capture JSON.
It also verified the two official inputs/results, four recoil actor traces,
production anchors, all 20 crop-sheet panels and three DLL identities. That is
95 visually accepted southern frames plus 99 diagnostic roof frames, not 194
frames of full visual acceptance. See `native-independent-verification.json`.

The actual native input copies are byte-identical to their original capture
inputs. The rerun legal harness writes fresh savedAt fields, so comparing those
archived official before documents excludes only `campaign.savedAt` and
`battle.savedAt`, just like the output comparisons. No save is edited to make
bytes match.

The persisted artifact check can be repeated without raw QA PNGs or DLL copies:

    python3 docs/art-archer-hit-20261110/verification/verify-native-artifacts.py --archive-only --repo /path/to/empire --output-dir /tmp/archer-hit-review

It requires Python/Pillow and ffmpeg/ffprobe. This mode checks decoded RGB against
the recorded hashes plus traces, anchors and complete saves; it cannot redo the
original-PNG or DLL comparison. Full verification additionally accepts
`--qa-root /path/to/original-captures`. Reports are separate by mode so a limited
archive-only run cannot overwrite the original full-evidence result.
