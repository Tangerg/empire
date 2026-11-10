# Independent native acceptance review

## Result and scope

No new blocker found for the **east-facing Elin archer-fall slice** in this native witness. Reviewed production commit `90952ac7a0d74f3f43b7dbeb0971a27df29b6dac`, Godot tree `9d962e7735bedd02698701bf3b08cb17d632e4c8`. Independently confirmed the current Godot tree has no diff from that commit. This is not approval of other animation families, native west-facing death, real-time performance, or the still-running full native aggregate gate.

## Native pixels and provenance

- Read all 93 actual consecutive PNGs in `native-final/capture/motion-20261110-210154-001` and independently decoded the archived lossless MP4 with ffmpeg. Every decoded RGB byte equals its corresponding original PNG; there are exactly 93 frames and no extra decoded frame. Verified all PNG/RGB hashes and the video hash against `native-fall-proof.json`.
- Independently compared every one of the 93 crop-sheet image cells against the original `(850,400)-(915,485)` crop enlarged 3× by nearest-neighbor. All match exactly. Visually inspected all 93 cells, the twelve-phase sheet, the five full-viewport stills and the settled-turn still. All five presentation stills match original full-frame PNG hashes.
- The crop preserves Elin's complete hood, quiver, body, hands, boots and bow across the sequence. Its right edge intentionally clips part of the adjacent attacker, not Elin. Full-viewport views establish that Elin remains on visible open ground; the nearby attacker does not obscure her falling silhouette. The shots show no unexplained terrain penetration, foreground occlusion, viewport clipping or visibility leak for this actor.
- The accepted source identity is preserved: blue hood/tunic, white sleeves, leather equipment and quiver remain recognizable through recoil and all six falling poses. Kneeling and collapse have distinct silhouettes. The corrected central bow grip survives native downsampling, with both bow limbs present in the grounded poses. No obvious anatomy pop, body-scale pumping or contact slide was found. The final drawing visibly holds, then fades without a shrinking body, and disappears once. The subsequent corpse marker is a distinct legal board marker, not a revived actor or an unremoved falling sprite.

## Timing and trace interpretation

Compared all 93 archived trace records with their original per-frame JSON. They are identical, stride is 1, observedFrame increments from 1 to 93, and processFrame increments consecutively. The following observation numbers are zero-based filenames/sheet labels:

- Recoil begins at 18, idle returns at 37, authored fall begins at 45.
- Six fall poses begin at 45, 49, 53, 58, 65 and 72.
- Pose 5 holds through 82; visible alpha reduction follows in 83–91. Actor 3 is present through 91, absent at 92, when presenting is false. No intervening disappearance or reappearance occurs.
- Actor position and supportPlane stay exactly `[843.5823,511.488]`; fall/fade sprite scale stays `[0.096,0.096]`, unmirrored.

The raw trace contains no opacity. Opacity acceptance comes from the pixels and controlled-clock assertions, not an invented trace channel. The fixed-60-fps simulation samples agree with a 620 ms fall plus 160 ms fade; they are not a measurement of wall-clock frame delivery or real-time performance. Exact boundary tests remain necessary alongside these samples.

## Legal state and executed-build evidence

- Independently hashed frozen `final-assemblies`, current native runtime copies, and final executed legal-harness copies: all three match the archived provenance:
  - Empire.dll: `097bd10747f9710295d064a7549b6d2e1b42bd30503b74fc32cdb64e7cd6016a`
  - Empire.Core.dll: `4b9fdbcdd66dde4e9bc898539ee03266ac5cf997b2ed740b61bb0639c7b983c8`
  - GodotSharp.dll: `2142e62894176d7340f50581f8b2fe84e36ae64e1a85f1b2bd7737a7e8a5c8ba`
- Verified the archived harness source hash and byte equality with the executed copy, all legal-package evidence-manifest hashes, and archived derivation JSON equality with final-run JSON. Read the harness and launch scripts: normal campaign/AI dispatch owns state changes; the harness compares independently loaded core state and full receipts, and launch copies only the official before-save into an isolated save directory. This audit did not rerun the derivation harness.
- Independently compared the first 151 derivation actions with the earlier accepted southern-bows prefix: exact equality. Step 167 is the ordinary stationary attack by unit 18 against Elin 3, 4 damage, with ordered events attackActivated → attack → death → markerAdded → rankProgressChanged → attackCompleted. The death and corpse marker target `(22,20)`; no facing/HP/position patch appears in the reviewed harness.
- Archived native input equals actual native input and before.json byte-for-byte. Archived native output equals actual F5 output byte-for-byte. Independently compared the complete settled derivation save with that native output after deleting **only** campaign.savedAt and battle.savedAt in memory: exact equality. No other field was normalized or omitted. This is full state/event preservation, separate from visual acceptance.

## Test evidence and remaining gate

Read the focused positive log ending in **6,115 assertions passed**. Verified negative-control log hashes and observed recorded failures for static death binding, a one-millisecond frame-interval transfer, a one-millisecond shorter fade, and duplicated fall-frame selection, followed by restored-positive success. These controls are scoped to the identical Godot source tree, not falsely described as byte-identical final DLLs; the archive explicitly records their pre-commit assembly hashes.

The legal native witness and focused evidence support acceptance of this authored fall slice. The required full native aggregate was still running when this audit was written; **no aggregate pass is claimed**.
