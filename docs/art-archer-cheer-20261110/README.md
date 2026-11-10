# Shared C01 archer victory cheer

This slice replaces only the shared `c01.archer` celebration binding with six
independently painted poses: ready, fist lift, high, hold, lower and rest.
The 100/120/160/260/180/140 ms intervals total 960 ms, followed by the existing
breathing idle. Elin inherits the same clip without an actor override. Generic
`archer`, Bran, other units and Torren's 900 ms salute remain unchanged.

## Ownership and timing

Only the ordered `gameOver` event begins celebration. `BoardView` selects drawn,
surviving units whose committed owners belong to the conclusion's winning team
and whose units were and remain visible. All eligible actors start concurrently.
The existing receipt clock waits for the greater of 220 ms and the longest
clip, while each actor returns to its own idle at its own endpoint. Thus Torren
returns at 900 ms while a shared archer continues until 960 ms. No new clock,
victory decision, result flag or actor registration is introduced.

The same receipt owner waits for a preceding death clip, terminal fade and
retirement before starting victory. The result screen still waits for the entire
receipt. Cancellation, rebind, tree exit, repeated refresh, terminal reload and
reduced motion retain their existing ownership. Runtime presentation code,
battle rules, saves and attack-preview behavior are unchanged.

## Source and registration

The built-in image tool generated the original transparent row from official
archer and accepted breathing references. Original source, prompt, extraction
geometry and checksums are preserved here. All six distinct drawings retain
the blue hood/tunic, white sleeves, leather equipment, quiver and empty bow.
The left hand keeps the central bow grip while the free right fist lifts and
returns. Both boots remain grounded; the bow is neither aimed nor loaded.

The 1934×486 atlas preserves the six source components' original RGBA pixels.
Only 15,828 detached near-transparent pixels, with alpha at most 6/255, are
removed. Four transparent pixels pad each component. There is no per-pose
rescaling, rotation, repainting or transformed reuse of one sprite. Uniform
scale 0.0805 matches the accepted 0.072 breathing artwork using recorded boot
support anchors. Both-facing contact sheets and the nonlooping offline GIF are
registration diagnostics, not gameplay captures.

Independent source inspection and exact-pixel reproduction pass. The slight
painted boot/bow contour variation does not produce a visible scale jump in
static native-size comparison. Saturated low-alpha edge noise was inspected on
white and dark composites and is not visible at native size. The raised fist
slightly overlaps the quiver silhouette but holds no arrow.

## Verification status

`verify-extraction.py` reproduces original RGBA, complete padded silhouettes,
anchors, uniform scale and declared timing; a one-channel mutation is rejected.
`verify-data-scope.py` proves the two production JSON files differ from baseline
`d76f6fff` only by the new clip, atlas entry and shared celebration binding.

The corrected complete native `AnimationChecks` run passes 6,669 assertions
with strict process/diagnostic exit 0. Native build has zero warnings/errors;
editor import, whitespace checks and 213 unique script UIDs also pass.
Legal native victory capture, complete saved-state comparison and the full
native gate remain pending. Full campaign,
standalone exports, other unfinished animations and real-time performance are
outside this slice. No pending check is represented as passed.

The first focused run passed the new production cheer checks, then exposed an
older full-C01 announcement fixture that assumed Torren's 900 ms salute was
the longest celebration. That cohort now contains 960 ms archers. The fixture
and remounted-board counterpart retain 900 ms as a pending checkpoint and check
completion at 960 ms, with additional exactly-once publication assertions.
Torren-only 900 ms checks remain unchanged. The initial failure is retained in
`verification/`; no runtime timing or valid assertion was weakened to fit it.
