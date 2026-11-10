# Independent integrated gravekeeper idle review

## Result

No concrete correctness or blocking coverage issue was found in the integrated data/test diff against `f358d5bc762a6830ef15ba3f5cd88ec998acf1e2`. This is static integration and source-registration acceptance only. The reviewer did not build or run Godot, the focused runtime suite, negative runtime controls, native battle captures or the full aggregate.

## Exact production scope

An independent Python comparison confirmed that `selected-assets.json` equals baseline after removing only `gravekeeper-breathe-v1` and `gravekeeper.breathe`, and that `art-bindings.json` equals baseline after restoring only `CANDIDATE_01_UNIT_ART.c01.gravekeeper.actionClips.idle` to `static.idle`. Therefore the original keeper base asset, casting clip, movement/hit/death/celebration maps, Mirelle appearance and explicit action map, all other units, and weapon/ability policies are unchanged in these documents.

The live runtime PNG is byte-identical to the accepted QA atlas, SHA256 `dc0edb5b4f3607b04ad9fbab8d350392ee61c024a712295dc36482da172ce876`. Its live 1709×502 geometry, all four frame rectangles and anchors, frame names, scale 0.0792, and direct 700/500/500/700 ms sequence match the accepted source review. The clip has only unit/appearance/frames, so it introduces neither release nor death policy. The import uses the existing lossless/no-mipmap texture convention.

`BoardView.Idle.cs`, `BoardView.Animation.cs`, `BoardView.Impact.cs`, `AssetCatalog.cs` and `ActorClip.cs` were independently compared byte-for-byte with baseline and remain unchanged. No alternate timer, membership owner, finite sampler or lifecycle path was introduced. The only production changes are the new art/import and the two JSON documents; remaining changes are tests and documentation.

## Test review

The new `AnimationGravekeeperIdleChecks` is registered in the suite. It checks exact appearance/topic/scale/frame names, four distinct padded images, interval starts and just-before ends, finite final-frame sampling, both directions, a 2400 ms board wrap, large multi-cycle sampling, stationary root/support contact, corresponding shadow, cached held textures, hidden-board freeze and reduced-motion reset/restart. It explicitly protects generic keeper inheritance and Mirelle/other healer isolation.

The cast/heal witness distinguishes source and bystander clocks: at 500 ms the source is in cast-rest, at 600 ms it returns to idle zero, and at 701 ms its phase is 101 ms while a same-type bystander has advanced to inhale. Both attack and healing, in both target directions, remain within their 720/800 ms recipient tails when tested. The existing cast suite retains release300/recovery600 and the original recipient timing/receipt checks. Cancellation and rebind expectations now sample the declared idle immediately and at 1000 ms instead of incorrectly demanding the old static base sheet.

The five-cell movement sample at 710 ms crosses the first idle boundary while still moving and demands the original gait artwork. Completion resumes idle zero and later advances. Static hit and death still select canonical artwork; death retirement cannot be overwritten by idle. The 220 ms keeper celebration is checked immediately before and at completion, then advances idle while longer archer celebrations continue owning the overall receipt. Tree removal retires idle registration; reattachment alone leaves the retained pose frozen, and render/reconfigure starts a fresh registration.

The existing lawful C04 facing/cast guard changes only its recovery-asset expectation. It retains the same route, source contact, damage, event ordering and deterministic hashes. The generic idle checker adds the keeper-specific declared-idle contract while preserving all other static expectations. Existing shared idle tests continue to own generic hidden-ancestor, visibility and finite-lifetime infrastructure checks; the unchanged production owner does not need a parallel implementation for this type.

The synthetic state and event fixtures prove presentation contracts, not lawful gameplay. The native legal-route/save checks remain necessary before claiming played acceptance. Artwork source and offline handoff acceptance are recorded separately in `source-review.md` and `independent-source-verification.json`; they are not substitutes for captured native playback.

## Evidence and limits

`integrated-verification.json` records the independent live-data/source comparisons and unchanged owner files. `git diff --check f358d5bc` passes. The new texture import and C# script UIDs each occur once among the repository's import/UID files.

No production file was edited and no build was started by this review. Runtime suite, final negative-control results, fresh legal replay/save equality, both-facing native loop/cast/heal handoffs and the final frozen-build aggregate remain the implementation task's acceptance gates.
