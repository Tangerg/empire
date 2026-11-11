# Independent review of gravekeeper recoil integration and focused checks

**Finding: no concrete defect or blocking contract gap found in the reviewed integration.** This is a read-only source/data/test review, not an assertion that runtime tests, native play or the full gate passed.

Reviewed against baseline `9dc658041886002d35f9b89647a701852d6a2047`. The focused test file reviewed has SHA256 `a8e1a30e03fac21e94cb528ff88abf2c9d8b98bd8dae7ddf38007ef9e59596ce`. New tests are registered exactly once in AnimationChecks. The new script UID is unique among current script UID files.

## Production scope and accepted artwork

Independent semantic comparison removes exactly the new `gravekeeper.recoil` clip, `gravekeeper-recoil-v1` appearance and generic keeper hit-binding replacement. Every other value in both production JSON documents then equals baseline. No runtime implementation, battle rule, event, preview, other actor, Mirelle appearance, idle, cast, gait or death setting changes.

The live PNG is byte-for-byte identical to the independently accepted extracted atlas, SHA256 `412f0aca8abefd0ce039c24fedeb0ef283d60fef207cc16430b2b126564ae5d0`. Live dimensions1965×576, all four source-order rectangles/anchors, single scale0.0688, pixel density1 and linear filter match accepted registration. The clip contains precisely the four40/110/90/80ms phases with no release or death policy. See `integrated-verification.json` and the portable `../verify-data-scope.py` and `../verify-registration.py` checks.

## Focused test expectations

- Exact frame-boundary checks protect all four half-open spans and finite final-frame holding. Pixel padding/distinctness checks supplement, rather than replace, the actual source-art and source-pixel review.
- Flat and dimetric square-grid fixtures use a real generic swordsman type and clear named overrides. Both keeper facings and attack/counter/support/parting-shot source kinds preserve the75ms generic release,320ms recipient pose,420ms impact and495ms total. Source projection/contact, crop anchor, uniform scale, shadow input, tint, opacity and effect identity are checked.
- Positive collision/status damage selects recoil. Zero damage, heal and regen remain outside the hit owner. A separate keeper magic source retains300ms release and600ms recovery; recipient recoil ends620ms from action start, leaving its existing impact alive until720ms. Healing uses the unchanged500ms feedback owner instead of damage recoil.
- Both attack/counter orderings use the actual keeper300ms source release rather than copying the archer320ms release. Keeper-first: cast ends600, first impact ends720, generic counter releases795, recoil ends1115. Generic-first: recoil begins75, ends395, first impact ends495, keeper counter releases795 and cast ends1095. Both last impacts complete1215. Tests preserve original event ordering and state bytes.
- Distinct and repeated/same-recipient area receipts cannot restart at the320ms pose boundary. They wait for the495ms first impact endpoint, start a new pose zero/effect, and settle after the second420ms impact at915ms.
- Fatal recoil returns to breathing before its separate death event. Existing static death starts535ms after generic release+damage+fatal beat, retains its half-duration alpha/scale behavior, and completes755ms. Duplicate death receipts retire the actor once; a killed flag alone cannot create death.
- Cancel, rebind and tree exit are checked before release, during early/late recoil and during the impact-only tail. The tests verify canceled completion, effect cleanup, restarted breathing progression and no stale damage callback. Coarse ticks, reduced motion and hidden-source/hidden-recipient cases retain their respective existing ownership and timing.

The four legacy assertion updates are narrowly valid: admit only generic keeper recoil in existing static-hit guards, select it in the cast action table, and inspect the actual recoil pose in the idle handoff. Unrelated exact timing and appearance assertions are preserved.

## Remaining acceptance boundaries

No builds, imports, game processes or runtime test suites were started by this reviewer. Parent-owned strict import/AnimationChecks, mutation controls, legal native captures, full native gate and their evidence still need their own result/review. Main slice documentation was not yet present. Synthetic receipts are not legal gameplay proof. Offline source diagnostics are not realtime performance evidence or proof that every native layout is unobscured.
