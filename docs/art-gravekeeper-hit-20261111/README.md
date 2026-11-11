# Shared C01 gravekeeper recoil

This slice replaces only the generic `c01.gravekeeper` hit binding. Field-cleric
and bridge-cleric inherit this appearance; Mirelle retains her explicit static
hit. The existing breathing, casting, gait, death and celebration bindings stay
unchanged. No battle rule, attack preview or runtime animation owner changes.

Four original paintings show recoil onset, peak, recovery and readiness over
40/110/90/80 ms, totaling 320 ms. The grey-haired keeper retains his beard,
scarf, dark coat, pouches and flask. His right hand keeps the pointed steel shovel
and his left keeps the oil lantern. Boots stay planted; shoulders, torso and arms
react without blood, dropped gear or new magic. These are distinct anatomical
poses, not translations or rotations of one drawing.

## Source and registration

The source and exact image-tool prompt are preserved. Extraction retains all
612,081 original RGBA pixels assigned to the four figures and removes 98,383
detached pixels whose alpha never exceeds 7/255. Every pixel above alpha 16 is
retained. The 1965×576 runtime atlas has twelve transparent padding pixels around
each crop, original unmodified pixels, recorded boot-contact anchors and one
uniform scale of 0.0688. There is no per-pose fit or rescaling.

Both-facing contact sheets and a nonlooping handoff GIF are offline registration
diagnostics, not real-time gameplay. Independent review accepts their anatomy,
equipment identity and continuity with the existing breathe/cast artwork. The
status inside `extraction-review.json` records the original art-only proposal;
this document records subsequent integration acceptance.

`verify-extraction.py` reproduces exact retained pixels and rejects a one-channel
mutation. `verify-registration.py` checks the actual PNG, frame geometry, scale,
anchors and clip intervals. `verify-data-scope.py` proves both production JSON
files match baseline `9dc6580` after removing only the new appearance, clip and
keeper hit binding. To reproduce offline review images, pass the repository root
to `prepare-review.py --reference-root`; this never edits production assets.

## Ownership and timing

The existing receipt clock still owns reaction and impact progress. Positive
nonhealing drawn-recipient damage selects the finite hit clip. It restores the
keeper's declared breathing idle at 320 ms; the existing damage feedback remains
420 ms. A generic source releases at 75 ms, so recoil ends at 395 ms and the
receipt at 495 ms. A keeper magic source still releases at 300 ms and recovers at
600 ms; its recipient recovers at 620 ms and the impact finishes at 720 ms.
Healing and regeneration do not select recoil.

Both attack/counter orders retain their 1215 ms composition. Area receipts stay
ordered, including a repeated same recipient starting a fresh pose at 495 ms.
A fatal generic hit still publishes at 495 ms, waits the existing 40 ms fatal
beat and starts the unchanged 220 ms static death at 535 ms, ending at 755 ms.
HP zero or a killed flag alone does not create a death action.

Focused tests cover exact half-open frame boundaries, flat/dimetric geometry,
both facings, source and recipient ownership, attack/counter/support/parting,
collision/status, zero damage, magic versus healing, area ordering, static death,
cancellation/rebind/tree exit, coarse steps, reduced motion and hidden actors.
Synthetic fixtures test presenter contracts, not newly legal battle commands.

## Verification status

Source extraction, independent art/integration review, registration and semantic
production-data scope pass. Native builds and editor import pass. Formatter and
215 unique script UIDs pass. Complete AnimationChecks pass all 7,487 assertions
with strict exit 0. Four isolated mutations fail at their intended first
assertions, then byte-exact restoration passes all 7,487 assertions again.

The final frozen build verifies both complete 175-entry lawful routes and their
state/event histories. These retain 350 entries with a shared 164-order prefix;
the derivation executes that prefix once, totaling 186 main dispatches plus six
checkpoint replays. Official save reloads and full-document comparisons pass.
This is not 350 unique scenarios.

Both actual UI captures contain 50 original 960×640 observations. Each shows the
normal AI archer moving and inflicting 33 damage, keeper HP 37→4, all four recoil
poses, recovery and completed presentation. The normal next AI endTurn returns
player control. Both F5 saves match the lawful settled output except the two
changed writer timestamps. The same frozen source and DLLs are used throughout.
Lossless RGB video, exact trace bytes, untouched stills and nearest-neighbor 3×
crops pass verification, including 40 isolated controls and 19 actual-archive
mutations per orientation. Independent final visual/evidence review passes all
100 frames and traces, 26 original stills and six exact 3× sheets. Expected
damage and status overlays remain visible;
they partly cover torso and lower-right equipment detail during the hit.

The unchanged full native gate passes all 45 ordered stages with strict exit 0
and its terminal success marker. It ran from clean implementation commit
`27de886c111600c64a868f9d57fc6868a6db45e4`, from 00:18:25 to 00:53:23 UTC on
2026-11-11. All three runtime DLLs still equal the exact native-recording build.
Independent comparison with the accepted idle aggregate preserves all 16 AI
traces and 3,425 legal orders, six played-flow markers, fourteen progress/roster
markers and the same 41-warning multiset. Four builds have zero warnings/errors.

The actual played-flow run reports 22,257 assertions, two carried victories
across 415 legal orders and a 21-order third-battle opening. Its assertion total
includes per-process-frame deadline checks and is not a scenario count. Exact
state, event and action comparisons remain unchanged. No battle rules, attack
preview or runtime implementation changed. Native and negative-control evidence
also verifies from an isolated Git archive, without unindexed working files.

See [native evidence](verification/native/README.md),
[independent native review](verification/native-review/native-final-review.md),
[legal replay](verification/legal/README.md), and
[negative controls](verification/negative-controls/README.md), and
[full aggregate review](verification/full-native/COMPARISON.md).
Full-character completion, all-campaign playthrough, standalone export and
real-time performance remain outside this slice.
