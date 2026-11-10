# C01 Elin lawful archer-shot checkpoints

## Accepted route identities

Each route contains an official unexecuted before.json, after.json, complete 109-command derivation-actions.json (final shot is step108), receipt.json, source-campaign-save.json and opening-actions.json. All are derived from a new campaign choosing steady-advance; only accepted application commands and normal opponent AI advance state. No health, position, facing, controller, RNG or receipt patching is used.

- right: stationary screen-right shot. Elin unit3 (19,20) shoots enemy17 swordsman (21,20). Range2, east, mirrored=False, damage35, target98→63, source78→78. Before state e78478df, after07dcb354.
- left: retained historical path name, meaning MOVE-RIGHT. Elin follows [(19,20),(19,19),(19,18),(20,18)], then shoots enemy17 (21,20). Range3, south, mirrored=False, damage35, target98→63, source78→78. Before state e78478df, after560674d7. This input was originally mislabeled left by applying a generic dimetric projection; actual native recording disproved that assumption. Its path and bytes remain unchanged so the native move-right recording stays traceable.
- mirror-left: genuine screen-left candidate. Elin follows [(20,21),(20,20),(21,20),(22,20)], then shoots enemy12 legion-shield (21,18). Range3, north, mirrored=True, damage15, target93→78, source78→78. Before state c143a16e, afterec273a9e. A guard reaction occurs but there is only one damage hit and no counterattack. This is the earliest available retained successful mirror candidate, turn6; no claim of exhaustive global route optimality is made.

All sources are c01.archer, key campaign-elin, using c01.ranger-bow. All three final commands are nonlethal, nonterminal, with the source surviving. New mirror-left uses a separate lawful opening route, not a modification of either earlier before save. Original before byte SHA256s remain:

- right: 9c8ae5e8705ac02f18ed698621102794e82c87664317c1531b4f5d40849380b4
- left (move-right): 28b9391128dd6b230a00d085955d2c49983a91d56bd879afd72f7d9673525902
- mirror-left: 31f7ce5d8cdf3d898af1666d63c7da358db88e5c7495e6dba5b114074bd34544

## Actual C01 projection and mirror contract

C01 does not use BoardPlane.Dimetric. BoardSceneRendering loads c01-01-assembled-ground.json through the scene's assembledGroundPath. RegisteredGroundDocument.CreatePlane uses cellXPixels=(36.37115073,0.20583468), cellYPixels=(6.17017828,23.75958941), worldScale=0.8798181899041609. Both positive logical axes have positive screen X. North and west project screen-left; south and east project screen-right.

BoardView.Animation.PrepareActorAction passes Projection.FacingVector(logical source, logical target) to FacesLeft, whose predicate is direction.X<0. The final harness reads the actual scene registration, checks it against projection-registration.json, resolves Grid.Toward and Grid.Step, and multiplies that direction by the actual registered basis. It verifies expectedMirrored for all three routes and separately checks the target's actual projected horizontal displacement. It no longer treats south as left.

All listed cells are plain at elevation0. Registered board-space contacts (before camera scaling/translation) are:

- right: source(747.58232696,510.94471419), target(811.58232696,511.30690838), target delta(+64.00000000,+0.36219419), facing vector X+32.
- left / move-right: source(768.72505678,469.31757339), target(811.58232696,511.30690838), target delta(+42.85727017,+41.98933499), facing vector X+5.42863509.
- mirror-left: source(843.58232696,511.48800548), target(800.72505678,469.49867049), target delta(-42.85727017,-41.98933499), facing vector X-5.42863509.

The first two computed source coordinates match the native traces that exposed the original mistake. mirror-left has enemy17 at(24,21) to its right and the intended target at(21,18) above-left. These are geometric checks, not a claim that final pixels are free of scenery, unit, damage-FX or UI occlusion; the native recording must confirm full bow/body visibility.

## Native UI inputs and commit boundaries

Use an isolated QA user-data directory only. Copy the selected official before.json as grayflag-campaign-save.json there and press F9 in the normal game. No original player save should be replaced. Cell coordinates are authoritative, not screen pixels. After a fresh F9 binding, the ready-unit cycle is 1→3→4→5→6→7→8→9→10, so Space twice selects Elin. If a HUD button has focus, Escape releases it before keyboard navigation.

Right: select Elin in Unit mode and click the HUD Attack button. This enters Target mode even with one legal target. From origin cursor(19,20), Right Right previews target17, then Enter commits. In contrast, Enter at origin first enters Destination mode, where A immediately commits because there is exactly one target. Arm capture before A if using that alternate path. A directly in Unit mode is left navigation, not the Attack shortcut.

Historical left / move-right: Space twice, Up Up Right Enter previews destination(20,18) and canonical path [(19,20),(19,19),(19,18),(20,18)]. A chooses ranger-bow attack; this destination has three targets, so A does not commit. Down Down Right focuses(21,20); final Enter commits the full move-and-attack command.

mirror-left: Elin starts(20,21). Space twice, Up Right Right Enter previews destination(22,20), obtaining canonical path [(20,21),(20,20),(21,20),(22,20)]. A selects attack; final destination has multiple targets. Up Up Left focuses enemy12 at(21,18); final Enter commits. Destination selection itself never dispatches movement. Keyboard arrows follow logical grid axes. Check the actual highlighted source, path and target before the final commit. The verification-result files record ready order and actual origin/destination attack target counts.

## Final-build provenance and checks

Implementation commit c328a80836884ec15d1f055bf0beaa06970874e7. Frozen Empire.dll SHA256184194d4c86b09ce93104db5bb5704bb5fc27efe86e174b9ad6ee6ef46fab315; Empire.Core.dll SHA2565130e39600fa68e9f4209534be28668bb510c64a4b7fabf31150d3f741b208db. runtime-assemblies-sha256.txt also identifies GodotSharp. No game rebuild is performed by this harness.

For each of three routes, the harness replays all 109 commands from original campaign setup, comparing exact canonical actions, hashes and events. Every action is independently dispatched through a core loaded from the preceding official save, comparing all state and events. Final official before/after disk roundtrips and final-command reload replay also pass. Only detached save timestamp comparisons omit savedAt. Source and target visibility, range≥2, source survival, one nonfatal damage hit, actual registered mirror and target side are checked.

Run from any working directory with an explicit new empty output outside the repository:

bash docs/art-archer-shot-20261110/verification/legal-shot/derive.sh --repo-root /path/to/empire-work --assembly-dir /path/to/frozen/assemblies --output /new/empty/sibling-directory

portable-run records execution by absolute script path from /tmp using final frozen assemblies. It contains the three-route outcomes, formatter check and rejection checks. Repository-contained and nonempty output both fail with exit2 before writes. evidence-sha256.txt covers this complete package. The formatted final harness was verified with repository C# whitespace settings before replay.

This package covers lawful input and deterministic rules evidence. Six-pose playback, release timing, actual mirrored sprite frames, screenshots, clip synchronization and pixel/UI occlusion require the separate native evidence. No desktop operation, game rebuild, full aggregate suite, commit or push was performed by this derivation worker.
