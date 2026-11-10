# Independent final native gravekeeper review

Accepted for the recorded generic C01 gravekeeper idle and left-facing healing
handoff. No blocking visual or evidence defect was found. This review neither
runs nor substitutes for the separate full native aggregate.

## Identity and pixels

All three archives identify tested source
`869078976dd21f5d82932dc4151bdd03fa973c1a`, Godot tree
`cfe370846c78c10100fbcfc8d19965efd96be697` and unchanged tools tree
`afff6c24fcf143fdff3a5a4facf2f373d1ad3290`. The three DLL records match the pinned
frozen build, including Empire.dll `2a6f890c…ff6ff7` and Empire.Core.dll
`449a03ce…558662`; the live frozen bytes were also independently checked.
Each native session exits0 and records the corresponding complete capture.

The reviewer independently decoded all 254 full RGB observations: 103 right idle,
103 left idle and 48 recovery. Every decoded buffer matches the original native
PNG RGB hash. Original JSON trace-line hashes, source/save evidence, selected
untouched PNGs, and every tile in all nine diagnostic sheets verify. The PNG
alpha channel is not preserved by RGB video. Results are retained in each run's
`independent-verification.json`; 23 isolated verifier controls pass.

## Timing and state

Both idle traces use stride 3 at fixed 60 Hz and 20 fps encoding. Each spans 5.1
simulation seconds first-to-last, beyond two 2400 ms cycles, with exact direct
700/500/500/700ms phase compatibility. Right wraps are observations 29/77; left
wraps 23/71. All idle observations are nonpresenting. Keeper 4 stays at
`[896.72504,470.04196]`, equal to its projected support, with absolute scale 0.0792.
Right is unmirrored and east-facing; left is mirrored and west-facing. Every
scene actor's idle root/support and membership stays unchanged.

The 48-frame left recovery uses stride 1 at fixed 60 Hz and 60 fps encoding. Cast
poses begin at observations 0/5/11/17/23/30; declared breathing frame 0 returns at 35
(observed frame 36, 600 ms) while presentation remains active. Presentation ends at 47
(observed frame 48, 800 ms). The internal 500 ms half-open pose boundary falls just
below its mathematical value under double accumulation, explaining pose 5 at 30;
verification allows only 1e-7 ms internal-boundary ambiguity, not a full-frame
shift or changed 600/800 ms endpoints. The recipient's visible healing feedback
continues beside the source after source recovery.

All native input saves exactly match their legal inputs. Each complete F5 output
matches its expected official save except campaign.savedAt and battle.savedAt,
both freshly changed. Idle changes no battle/campaign facts. Recovery matches
`legal/source-package/heal-left/after.json`, including target 3 HP 39→69 and final
keeper west facing. Legal derivation/replay provenance remains a separate archive.

## Visual inspection and labels

The reviewer opened original full-view observations, both four-pose sheets,
the recovery transition sheet and all 254 source-actor crops. The grey-haired,
bearded keeper retains the same clothing, shovel and oil-lantern identity.
Shoulder/chest and lantern-hand changes read as a subtle breath, while feet remain
planted. Both wraps and cast-rest→idle retain coherent hands/equipment without
an apparent size or support jump. The source's full silhouette remains readable;
the selected ring and completion badge can overlay the lower-body area, and
neighboring effects lie beside rather than across the critical source handoff.
This does not cover every possible scenery/actor overlap.

`native-startup.png` was opened and is correctly labeled as the automatic title
screen, not a settled battlefield. All three proofs and run READMEs use the
correct name/description. The original path `capture/native-001.png` appears
only as provenance. No stale claim that this startup image is a settled view
was found. The parent-confirmed F8 route is preserved.

Inspection is of sampled native frames and their exact video encoding, not an
independent continuous realtime play session. Readback affects wall-clock pacing.
Trace opacity and recipient-effect lifetime are not directly recorded. Export,
realtime performance, exhaustive UI/occlusion and full-campaign acceptance are
outside this review.
