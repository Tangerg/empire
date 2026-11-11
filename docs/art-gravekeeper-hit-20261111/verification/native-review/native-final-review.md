# Independent final native evidence review

**Conclusion: accepted for the two recorded native keeper-hit witnesses. No new concrete implementation or evidence defect found.** This conclusion includes the visibility limits below. It is not a full-gate, export, wall-clock performance, or all-layouts acceptance.

Reviewed the frozen packet under `verification/native/{hit-right,hit-left}` against the original `native/{hit-right,hit-left}` recordings. No production, build, import, game UI, camera, source capture, or packet file was modified by this reviewer. Review outputs are confined to this `review/` directory. The live UI route was performed by the separate operator; this reviewer independently inspected its actual captured pixels and files rather than replaying the game.

## Actual visual findings

- Native 960×640 full frames show keeper 4 and the attacking archer 18 on-screen throughout the recorded exchange. The archer moves one cell, performs its shot, and returns to breathing. Neither principal actor disappears behind the HUD. The keeper is recognizable as the accepted gray-haired, bearded character carrying the pointed steel shovel and lit oil lantern, rather than a substitute character.
- Both native sequences visibly contain the four authored recoil stages: compressed onset, backward peak with raised lantern, forward recovery, and ready stance. At zero-based observations 24, 27, 33 and 39, traces identify recoil poses 0, 1, 2 and 3 respectively. Breathing pose 0 resumes at 43; presentation first completes at 49. Both full-frame endpoints and all 50 sampled keeper crops per facing were inspected. The native stages are distinct in silhouette, although small facial and torso details are easier to inspect in the explicitly labeled 3× diagnostic crops.
- The canonical artwork retains the right-hand pointed steel shovel and left-hand oil lantern. The left-facing game presentation is the corresponding horizontal mirror. The lamp is clearer on the screen-left side in that orientation; the neighboring banner is close to the screen-right shovel. The two captures do not support a claim that every prop detail is unobscured in every native frame.
- The original `-33` number covers a substantial portion of the torso during recoil, and impact artwork briefly overlaps the contact area. The normal shield/status marker overlaps the lower-right body/prop region, particularly reducing right-facing lantern visibility. These real overlays are preserved, not cleaned up or treated as new recoil defects.
- Feet remain visually registered to the terrain as the body recoils and recovers. Traces independently keep keeper root and support at `[864.72504, 469.86084]` for every observation, with the requested mirror flag and the accepted per-pose registration. There is no observed root slide, accumulated drift, detached prop, clipped keeper silhouette, or abrupt unrelated replacement on recovery.

Useful exact inspection files in **each** facing directory are `native-observation-000.png`, `native-observation-024.png`, `native-observation-027.png`, `native-observation-033.png`, `native-observation-039.png`, `native-observation-043.png`, `native-observation-049.png`, `native-keeper-transitions-3x.png`, `native-keeper-all-observations-3x.png`, and `native-lossless.mp4`. The 3× sheets are exact nearest-neighbor crops of actual pixels, not new game renders. `native-startup.png` is a title screen, not a settled battlefield or post-F5 screenshot.

## Independent checks completed

- All 100 original native PNG RGB buffers equal their decoded lossless-video frames byte-for-byte. Every original PNG and trace hash matches the packet proof. All 100 original JSON records equal the archived JSONL bytes in order, with exactly the documented LF separators.
- All 26 selected full-frame PNGs are untouched original files. All six diagnostic sheets contain exact declared nearest-neighbor 3× tiles. Original logs, saves, source identities, assembly manifests, exits and save-comparison files equal their archived copies.
- Both actual input saves equal the immutable `legal-package/{right,left}/before.json` bytes. Each complete official output equals the independently produced legal `ui-settled.json` after removing **only** `campaign.savedAt` and `battle.savedAt`; both actual writer timestamps changed. Keeper HP changes from 37 to 4 after the nonfatal 33-damage archer hit, followed by the normal AI endTurn settlement. The packet's motion endpoint is receipt completion, not a claim that its final frame already shows that later turn transition.
- Final `verify-native-evidence.py` passes in portable archive mode and raw-plus-archive mode for both facings, using `--legal-root legal-package`. Its 40 isolated controls pass. On detached copies, all 19 actual-archive tamper controls per facing are rejected, including semantic mutations with recalculated archive hashes. The final recursive `EVIDENCE-SHA256.txt` verifies all 77 files after those controls.
- Both native processes exited 0. Their unchanged logs contain no error or resource-leak diagnostics. The software-renderer V-sync warning and environment-catalog diagnostic remain visible in the logs; they are not hidden or counted as a performance pass.

## Exact tested identity

- Source commit: `27de886c111600c64a868f9d57fc6868a6db45e4`.
- Godot tree: `02b18b80aff45880bb58e503574b858c921449aa`.
- Tools tree: `afff6c24fcf143fdff3a5a4facf2f373d1ad3290`.
- `Empire.dll`: `9c26ba406a55703a0d5a0b07141cfc094ece56b0842157ec47a03d465f0ec9c0`.
- `Empire.Core.dll`: `ab6a38e4b863a6a09acce6505aac93a4fe72975f0c9e41329d1b8645a7c56faa`.
- `GodotSharp.dll`: `2142e62894176d7340f50581f8b2fe84e36ae64e1a85f1b2bd7737a7e8a5c8ba`.

The current source commit/trees, both capture manifests, frozen identity, `final-assemblies/` files and the live `godot/.godot/mono/temp/bin/Debug/` DLLs all match those values. Source artwork was inspected as identity context, not substituted for native-image evidence.

## Review records and boundaries

Independent records are `native-final-independent-verification.json`, `native-final-live-frozen-identity.json`, `native-final-{hit-right,hit-left}-{archive,raw}-verification.json`, `native-final-verifier-controls.txt`, `native-final-{hit-right,hit-left}-tamper-controls.{json,txt}`, and `native-final-evidence-checksums.txt` in this directory.

The videos preserve RGB, not PNG alpha. Their 60 fps encoding and 50 observations represent sampled simulation frames; readback affects real pacing. Timing samples are compatible with the frozen 320 ms recoil and 420 ms impact contracts, but do not directly measure sub-frame timing, effect opacity, or effect-node lifetime. The packet and hashes establish consistency against the trusted frozen anchor, not authenticity against wholesale replacement of every anchor. The ongoing aggregate gate is outside this review and remains separately reportable.
