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

Legal-battle native recordings and the full aggregate gate are pending.
No full-character, full-campaign or export acceptance is claimed.
