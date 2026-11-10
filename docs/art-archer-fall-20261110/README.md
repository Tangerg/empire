# C01 archer's authored collapse and retirement

This slice replaces only `c01.archer`'s death binding. Elin and the other C01
archers inherit six independently painted poses: buckling, kneeling, collapse,
side contact, settling and lying still. Their 60/70/90/110/110/180 ms intervals
provide 620 ms of fully visible collapse, followed by a 160 ms terminal fade.
The last drawing holds throughout the fade. Base `archer`, Bran, named overrides
and all other C01 archer actions remain unchanged.

## Ownership

Integration is data-only. `BoardView.AnimateDeath` already owns the existing
`after-clip` policy: play the complete clip, fade its last frame, then retire the
actor once. Only an ordered `death` event starts it. The fatal strike retains
its existing release, 420 ms impact and 40 ms fatal beat. Neither zero HP nor a
killed flag starts a competing timer. The receipt and following result wait for
both death phases. No combat rule, save schema, attack-preview logic or runtime
presentation implementation changes.

## Source and registration

The built-in image tool produced the original transparent row using the official
archer and accepted recoil/idle art as references. Five candidates were reviewed.
The selected fourth candidate corrects the final poses' left hand to the central
leather-wrapped bow grip. Earlier clipped or inconsistent-hand candidates and a
noisy fifth attempt were rejected, not disguised with extraction edits. The hood,
clothing, quiver, unloaded bow and physical body/equipment scale remain consistent.

Six original RGBA components form the 2111×429 atlas. Extraction retains their
pixels unchanged, adds four transparent padding pixels and removes only 8,986
detached pixels with alpha no greater than 13/255. There is no per-pose scaling,
rotation or repainting. The common 0.096 presentation scale is registered against
the accepted breathing art. Late support anchors follow the grounded body/bow;
the logical actor root stays fixed.

Independent source and both-facing native-size contact review passed, including
the repaired grip and complete bow tips. The fine string/fingers are not separately
legible at map scale, so their continuity was inspected in the original artwork.
`anchored-fall.gif` is a single-play offline registration diagnostic, not gameplay.
The source, prompts, exact geometry and hashes are retained beside this document.

`verify-extraction.py` reproduces the atlas and checks the live geometry, anchors,
scale, intervals and fade policy. Its pixel guard rejects a one-channel mutation.
`verify-data-scope.py` proves both production data files match baseline `4c563eda`
after removing exactly the new asset, clip and C01 archer death binding.

## Verification status

Source/registration review and exact extraction pass. The corrected native
`AnimationChecks` passed **6,115 assertions**, with a strict process/diagnostic
exit of 0. Build, editor import, source metadata and targeted whitespace checks
pass; the build has zero warnings/errors. A legal native battle recording and
complete saved-state comparison pass. The full native gate remains pending.
Standalone export and full-campaign acceptance are outside this slice.

The first native run reached the new archer and existing Torren fall checks
successfully, then exposed a legacy test that implicitly selected the last C01
unit and still expected its former static death. That fixture now explicitly
selects the existing `south-sword`, asserts its unchanged 220 ms `during-clip`
policy, and removes it by ID. All prior fade, shrink and pivot assertions remain.
No actor state or production policy was changed to satisfy the fixture. The
initial failure is retained in `verification/`; the corrected rerun passes.

Four isolated data mutations fail with strict gate exit 1: restoring the static
death binding, transferring one millisecond between poses while preserving total
length, shortening the fade by one millisecond, and repeating a pose. Restored
inputs are byte-identical and the isolated positive rerun passes all 6,115
assertions. Production data and runtime-code hashes are unchanged. The source
pixel guard separately rejects a one-channel in-memory change. Logs and the
rerunnable isolated-control script are under `verification/negative-controls/`.

## Legal native battle

The official campaign and normal AI produce the death without altered HP,
position, facing, camera or receipts. At action 167 enemy archer 18 kills
east-facing Elin at (22,20), taking her from 4 HP to zero. The native scene loads
the independently verified preceding save in a fresh isolated namespace and
lets the normal AI act. After the following AI end-turn, the F5 save matches the
complete independent settled document, excluding only its two writer timestamps.

The 93 consecutive native observations show the six fall poses starting at
45/49/53/58/65/72, the fully visible last pose, terminal fade and single actor
retirement at observation 92. Root/support plane and fall scale stay constant.
The corpse marker remains and normal human input resumes. The 960×640 lossless
video was decoded and compared against every original PNG's RGB bytes. All
source traces, frame hashes, unchanged full-viewport stills and diagnostic crops
are in `verification/native/`, together with a repeatable archive verifier.

Independent review checks all 93 observations, lossless bytes, source and
assembly identity, native inputs/outputs and full-save equality. This recording
covers one east-facing fatal battle. Mirrored registration and cancellation are
controlled-test coverage, not a second native recording. Readback pacing is not
a real-time performance result. The legal derivation, launch procedure and
complete official saves are in `verification/legal-death/`.
