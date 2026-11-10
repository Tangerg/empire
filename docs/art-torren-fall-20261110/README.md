# Torren's authored collapse and retirement

This slice replaces only `campaign-torren`'s death binding. Six independently
painted poses show buckling, kneeling, loss of support, side contact, settling and
a still prone body. Intervals are 60/70/90/110/110/180 ms: 620 ms of fully visible
collapse, followed by 160 ms of terminal fade. The final picture holds throughout
fade. This uses the existing explicit after-clip policy, not rotation or shrink
of a standing image. Combat rules, damage, event order, other actors, attack
preview, thrust, breathing and recoil remain unchanged.

## Source and registration

`source-row.png` was generated with the built-in image tool from `prompt-v1.txt`,
using the official original Torren and accepted recoil row as identity and material
references. It adapts [sprite-gen's component-row method](https://github.com/aldegad/sprite-gen),
not that repository's CLI or Grok pipeline. No other provider was used.

The six drawings preserve the youthful face, kettle helmet, brown quilted armor,
scarf, sword in the right hand and buckler on the left arm. Knees, hip and shoulder
contacts form the fall; prone poses retain the same physical body/equipment scale.
Independent source and native-size reviews found no clipping, identity, anatomy or
equipment blocker. The final anchor was shifted 15 source pixels right to remove
an approximately 1.6 map-pixel upper-body slide between the last two drawings.

`extraction-review.json` records original RGBA hashes, component bounds, rectangles,
support anchors, a single 0.1066 presentation scale and the clip/fade intervals.
Components above alpha16/255 and 1000 pixels identify all six figures. Original
nonzero-alpha pixels within three pixels are retained with four transparent
padding pixels; 14,828 distant pixels of at most 5/255 alpha are excluded. No
retained picture is repainted, interpolated or individually scaled. Late anchors
follow the body's support rather than the lowest sword-tip pixel or the changing
bounding-box center. The actor's logical tile/root remain fixed.

`verify-extraction.py` reproduces original pixels and validates the live atlas,
geometry, anchors, spans and post-clip fade. It requires Pillow, NumPy and SciPy.
The manifest owns live geometry and timing; the review record is historical.
The contact sheets compare accepted breathing idle with all six collapse poses
in both directions. `anchored-fall.gif` is an offline, single-play diagnostic,
not captured gameplay; it starts with an idle reference and ends prone.

## Integration and acceptance

Production integration is data-only. Only an ordered `death` event starts the
collapse; the fatal hit's existing recoil, impact and fatal pause precede it.
`BoardView.AnimateDeath` already owns clip playback, then terminal fade, then one
standing/node retirement. A killed flag or zero HP cannot start it early. The
receipt and any following result wait for both stages. Cancellation, rebind,
tree exit and reduced motion retain their existing settlement owner.

New production-art checks and the narrow Torren expectations cover exact pose
intervals, both mirrors, full padded silhouettes, root/contact/shadow and RGBA,
clip/fade boundaries, terminal hold, one-time retirement, result waiting,
cancellation/rebind/exit, coarse sampling, hidden art and reduced motion. Generic
static death fixtures retain their original meaning.

### Native battle witness

The legal checkpoint in `verification/legal-death/` derives from the official
campaign save by 22 normal application actions. No HP, controller, receipt or
combat-rule fields were edited. Loading its before-save in an isolated native
save namespace lets the normal AI deal 16 lethal damage to Torren. The lethal
state digests are `c1578a7a` → `ebb17884`, with an actual ordered death event.
Original user saves were not modified.

`verification/native-fall-lossless.mp4` retains all 78 full 1000×700 viewport
observations at stride 1 and a fixed 60 Hz simulation step. Every decoded RGB
frame was compared byte-for-byte with its source PNG. The proof records the
input save, assembly, video and frame hashes, plus all original actor traces.
The capture is visual evidence, not a real-time performance benchmark.

Observed frames 31/34/38/44/50/57 begin the six authored poses. The final picture
holds through the terminal fade; observation 77 is the first with Torren absent
and `presenting=false`. Root and support-plane coordinates remain identical
throughout his presence. The native trace does not record opacity: fade is
checked in the actual images and separately by controlled-clock assertions.
The diagnostic phase sheet is a nearest-neighbor enlargement of a fixed native
viewport crop, not redrawn artwork. Full viewport stills are also retained.

Independent frame-by-frame review accepted the footprint and equipment scale at
30→31, knee/hip/side contacts, 50→57 settling and final cleanup. Full viewport
RGB is identical at observations 57–67; subsequent pixels fade without a new
pose or transform. This review used PNGs and traces, not direct continuous video
playback. The existing brief ready/idle interval at 22–30 remains before the
ordered death begins.

The adjacent enemy shield occludes Torren's head/left equipment in the later
native poses. Their complete silhouettes and consistency are therefore covered
by the separate unobstructed source/contact-sheet review, not claimed as fully
visible in this one battle witness. Native observations cover this one legal
right-facing death, with both mirror directions covered by controlled checks.

### Focused checks and negative controls

The reviewed build completed with zero warnings/errors; `AnimationChecks`
passed **3,787 assertions**. Test expectations changed only for Torren's
intentional 220 ms placeholder → 620+160 ms authored death. An initial focused
run exposed a 0.001 ms inherited test sampling offset at half fade; the sampling
steps were corrected without changing production logic or relaxing tolerance.

In isolated project-data copies, changing the last pose span 180→179 ms or the
terminal fade 160→159 ms each caused the fatal-recoil death contract assertion
to reject with exit 1. The extraction verifier also rejects a one-channel RGBA
mutation. Original data and recorded inputs remained unchanged. Detailed logs
are in `verification/`; `data-scope.txt` records semantic equality of all old
manifest/binding data after removing just this addition.

The full aggregate is still running. Exported-build and complete-campaign
acceptance are not claimed, and the remaining character actions remain separate
work.
