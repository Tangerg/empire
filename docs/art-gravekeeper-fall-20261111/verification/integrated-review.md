# Independent gravekeeper fall integration and focused-check review

**Finding: no concrete defect or blocking contract gap found in the reviewed integration.** This is a static source/data/test review. It does not claim that builds, imports, native tests, legal gameplay, the full native gate or performance checks passed.

Reviewed against baseline `ea0eb0513e48ed650c2c04d259bfc4baee7f3021`.
The new 308-line `AnimationGravekeeperFallChecks.cs` has SHA256 `c8902484d0ce7daf972533c2c171604f85b4c786eacf010133e993830eeb3455`.

## Exact production scope and accepted pixels

Independent semantic comparison removes precisely the `gravekeeper.fall` clip and `gravekeeper-fall-v1` appearance from `selected-assets.json`, and reverses only `CANDIDATE_01_UNIT_ART.c01.gravekeeper.actionClips.death` to `static.death` in `art-bindings.json`. Both complete parsed documents then equal baseline exactly. Other appearances, clips, non-death actions, actor overrides, weapon data and unrelated bindings remain unchanged.

The live atlas is byte-for-byte equal to the independently accepted source-art packet, SHA256 `b6467bfb6ad3c5c6a09332034798a0afc2478962817fa3060d44917c2b364326`. Its 2154×405 dimensions, six source-order regions/anchors, 0.1 shared scale, pixel density 1 and linear texture filtering match source acceptance. The clip contains only the six 60/70/90/110/110/180 ms phases, no attack release, and the explicit 160 ms after-clip fade.

The PNG import companion exists, points to the correct source and uses lossless compression with no size limit. The new script UID occurs once, and all 378 `.cs.uid` files under `godot` have unique contents. The new check method is registered exactly once in `AnimationChecks`. The tracked source delta consists only of the new/updated test files; no runtime implementation, battle-rule, attack-preview or engine source file changed. `git diff --check` against baseline is clean. These are static checks, not a claim that Godot successfully imported or ran the files.

`../verify-integration.py` reproduces these source/data comparisons; `integrated-verification.json` records the current exact result. Source-art acceptance and its independent pixel/registration verifier are documented separately in `source-review.md`.

## Focused test structure and ownership

The declaration checks preserve the canonical keeper gait identity and every non-death action. Field-cleric and bridge-cleric are required to inherit the shared clip without actor overrides. Mirelle, base cleric and C01 druid retain their static appearance, 220 ms duration and during-clip policy. Six exact half-open intervals, terminal holding, RGBA crop padding and six distinct images are checked. Actual anatomy is established by independent pixel review, not inferred from unequal image hashes.

The shared authored-fall boundary helper is invoked for both west/east facings on flat and dimetric Square4 projections. The helper samples every interval edge, 620 ms clip endpoint, fade onset/midpoint, 779.999/780 ms retirement and coarse completion. It checks selected texture/region/anchor/scale/filter, source shadow composition, unchanged logical contact/root/rotation, no attack accent, captured opacity, no early publication, exact single retirement and duplicate-death behavior.

The new source matrix covers three incoming owners across attack, counter, supportAttack and partingShot:

- Generic sword: release 75 ms, source recovery 150 ms, keeper idle restoration 395 ms, death begins 535 ms, retirement 1315 ms.
- Archer: release 320 ms, source recovery 600 ms, keeper idle restoration 640 ms, death begins 780 ms, retirement 1560 ms.
- Keeper magic: release 300 ms, source recovery 600 ms, recipient idle restoration 620 ms, death begins 760 ms, retirement 1540 ms.

These expectations agree with the existing `BeginAttack`/reactive-source action owner, `AnimateImpact`'s 320 ms recoil followed by its 420 ms damage-feedback endpoint and separate 40 ms fatal beat, and ordered `death` dispatch. The tests check the pre-release state, recoil acquisition, independent source recovery, idle tail, pre-death boundary, death ownership, final 180 ms opaque hold, half-alpha terminal fade, no premature completion, complete receipt ordering, cleanup and unchanged input state. Main attacks retain their activation/completion events; reactive kinds retain their own source windup. Duplicate death adds neither duration nor a second actor exit.

Collision/status tests preserve their different receipts: collision with a killed flag begins death at 460 ms and retires at 1240 ms; statusTick with hpAfter zero has no extra fatal beat, begins death at 420 ms and retires at 1200 ms. Both restore idle at 320 ms and wait for their explicit death event.

The double-fatal area fixture uses one main attack activation and two ordered damage/death pairs. The second recipient cannot start until the first keeper retires at 1315 ms. It then receives its 460 ms impact/fatal beat and full 780 ms death, finishing at 2555 ms with ordered event publication and one remaining actor. No second source windup or concurrent corpse owner is introduced.

The active-caster fixture places death immediately after the keeper's 300 ms attack release. The dying source must keep the cast drawing until its full 600 ms recovery completes, then acquire the fall and retire at 1380 ms. The later attack-completed event cannot resurrect it; an additional long tick checks stale-callback absence. This matches the unchanged `death` dispatch that awaits a matching active source's recovery before `AnimateDeath`.

The reused color/scope/result/lifetime/visibility helpers additionally cover preserved captured RGB/alpha and transform, killed-only and hpAfter-zero reports that cannot invent death, result waiting until terminal removal then its own 220 ms beat, cancellation at fall/boundary/fade for both living and dead committed outcomes, rebind and tree exit, no stale idle resurrection, exact actor counts/effect cleanup, reduced motion, hidden-actor bypass before resolving missing death art, and explicit failure/cleanup for visible missing art.

## Legacy assertion changes

All five semantic updates are narrow:

1. Cast action declarations expect keeper fall instead of static death; casting checks remain unchanged.
2. Idle-to-death handoff expects the declared fall appearance at 50 ms and retains no-resurrection checks.
3. Recoil fatal handoff retains 75/395/535 ms ownership, then brackets frame 4 at 974.999 ms and grounded frame 5 at 975.001 ms, retains frame 5 at 1154.999 ms, opaque clip endpoint 1155, half-alpha/no-shrink 1235, unpublished positive-alpha frame 5 at 1314.999, and exact retirement 1315.
4. Shipped reaction exceptions add only the generic C01 keeper to the already authored Torren/archer deaths. Other static durations/appearances remain guarded.
5. Shipped death-policy exceptions add only generic keeper with six frames, 620 ms, no release and 160 ms after-clip fade. Existing invalid-policy fixtures and unrelated 220 ms static paths remain.

The shared-helper comment is generalized without changing its implementation. No checks were disabled and no unrelated assertion was weakened in the reviewed delta.

## Reviewed sampling correction

After the initial review, both source-matrix and legacy recoil receipt samples were corrected to bracket the last-pose transition: deathStart + 439.999 ms requires frame 4, and deathStart + 440.001 ms requires frame 5. The legacy generic receipt uses absolute 974.999/975.001 ms. This addresses the existing seconds-to-milliseconds floating-point projection: `(0.975 - 0.535) / 0.620 * 620` is `439.99999999999994`, not exactly 440. The direct `ActorClip.FrameAt(440)` check and exact authored 440 ms interval boundary remain unchanged. Both sides are asserted rather than weakening the requirement or modifying runtime timing. No production/runtime semantic change accompanies this test-sampling correction.

## Remaining acceptance boundaries

No build, editor import, runtime suite, native game process, source mutation or production edit was started by this reviewer. Parent-owned build/import/AnimationChecks results, negative controls, lawful lethal routes and native capture/save verification, full aggregate gate and final artifact/archive verification remain separate. Synthetic receipts demonstrate presentation contracts when executed; they do not establish new legal battle commands or native performance.

The static review is tied to the exact source/data snapshot above. Rerun the integration verifier and review any substantive later test/runtime changes before final acceptance. During the original review only independent files under the QA review directory were written by this reviewer. This archived copy and its verifier were subsequently packaged under `docs/art-gravekeeper-fall-20261111`; no production source or data changed during packaging.

## Archived reproduction

From this directory, run `python ../verify-integration.py --report integrated-verification.json`. The default packet and repository paths are derived from the script location, not a temporary QA workspace. Baseline source/data comparison requires Git history containing the recorded baseline commit. See `ARCHIVE.md` for the complete offline check commands and the separate provenance-path limitation.
