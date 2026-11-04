# Staging opening-health contract — 2026-11-04

## Reproduction and ownership

Baseline `c288f6001aeffca1d9cdca2025d8a23298e2f2ed` has a player-visible mismatch in the first campaign choice. The actual native New → story → “抢占北丘” → staging → Start → system menu → camp confirmation flow showed Torren as `生命 100% · 军衔 0` both before battle and at camp, while the actual opening was 72/98 HP. The same route with “稳步推进” correctly opened at 98/98. The regression ran both paths and exited 1 for the rush mismatch. The initial harness mistakenly addressed the hidden system-menu action; its separate failed visibility check is not evidence of a game bug. Opening the menu first produced the actual RED above.

The persistent campaign roster correctly retains a full-health ratio before this authored opening penalty. `GrayFlagCampaignPolicy.ApplyBattleContext` remains the sole owner of the rush injury and position. Changing the persistent roster or copying the 72-HP policy into the UI would create a second rule owner.

`GrayFlagCampaign` now shares its existing detached request preparation between entry and an immutable `OpeningRosterHealth` read projection. The projection uses the same `BattleEngine.CreateState` initialization as a real battle. At camp it reads the original pending request, not a played battlefield. The application supplies this projection explicitly to staging; available bound actors render their actual opening ratio. Unbound reserves retain persistent health and unavailable actors retain their disposition. Missing bound health is an explicit failure, not a fallback to misleading full health.

The display remains the existing localized percentage: rush 72/98 rounds to 73%; steady remains 100%. No policy, map, artwork, layout, save schema, save-slot behavior or battle rule was changed. The two opening digests remain `78e7c7b9` and `9dae091b`, identical before and after the fix.

This follows the repository's one-fact/one-owner rule and Godot's [scene organization guidance](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html): the parent supplies the view's explicit read dependencies, while the scene renders them without reaching into campaign state.

## Regression scope

- New `StagingOpeningChecks` uses actual focused Space events and native menu/confirmation controls for both choices, entry, camp, F5, F9 and reentry. Repeated queries cannot alter campaign facts, pending request, battle digest/RNG, automatic bytes/timestamps or the explicit manual checkpoint. Read-only collections reject mutation. Serialization timestamps are excluded only from comparisons of separately generated in-memory save envelopes; actual file bytes/timestamps remain exact.
- Existing `CampaignNavigationChecks` now compares mounted staging facts with all actual bound C02 actors after its legally played C01 victory. Its separate existing historical two-victory C03 admission checks the two survivors and fallen Elin; that boundary is not claimed as a new current-engine C02 playthrough.
- Historical 45-view presentation fixtures explicitly supply their frozen health ratios. They remain presentation fixtures, not proof of campaign preparation. Independent injection and missing-health rejection guard the new boundary.
- The historical fixture runner also exposed an already-stale action-note expectation: baseline native copy includes the 960×640 minimum-window guidance, while the old oracle did not. The oracle stays unchanged. The runner asserts its original note first, then the exact already-shipped suffix, preserving all other comparisons.
- The new native scene is part of `verify-native.sh`; its wrapper test expects 30 isolated scene invocations plus one editor import.

## Verification checkpoint

Focused RED: build exit 0; corrected native reproduction exit 1 at the intended rush-health assertion. Fixed initial native flow: exit 0, 46 assertions. Expanded actual native flow including save/load and read-only guards: exit 0, 154 assertions. Two Debug builds passed with no warnings/errors. One intermediate test-only compilation failed on a missing local variable and was fixed before the expanded run. Source metadata passed with 170 unique script UIDs.

The native-process harness initially hit its existing 15-second simulated clean-gate timeout under concurrent validation. An unchanged recheck passed all 10 tests; no timeout or diagnostic rule was relaxed.

Logs and exit records are retained in the task workspace. Headless native input/text/resources/state verification is not a GUI/pixel or physical-device visual acceptance claim.

## Completed canonical run and later focused checks

The actual `verify-native.sh` completed without interruption at code checkpoint `786565ce277ae21afe47c969373af23f01c61952`, from 14:20:50 to 15:13:53 UTC (53m03s), exit 0. It covered all 44 stages: wrapper/source/format/metadata and four scene validators, both Debug builds, core contracts and all sixteen formal independent battles, outcome/catalog boundaries, editor import and 30 native scenes. The 3,425 admitted orders and final replay digests exactly match the previous published baseline. All 31 Godot logs satisfy the strict diagnostic boundary. The 30 expected warning headers match the previous gate after normalizing temporary paths; warnings from deliberate invalid inputs and missing-art fixtures were not suppressed.

Relevant actual results: campaign navigation 8,797 assertions with zero failed groups; opening health 154; tactical HUD 510; battle result 371; arrival feedback 96; UI flow 205; lighting lifetime 106; lighting contracts 1,791; scenery 8,753. The separate pure staging runner passed 731 comparisons across all 45 frozen views, plus the unchanged 1,226-message and narrative-reference checks.

After this canonical run, the extra historical `CampaignStagingChecks` scene reproduced its stale short-note assertion as exit 1. Updating that assertion to the exact full current wording passed 155 assertions. This is a test expectation repair, not changed UI copy.

A further opening-flow rerun completed all 154 behavior assertions but failed at native shutdown: nonexistent `changed` signal disconnection, StyleBoxFlat/AtlasTexture unsafe references, and process exit 139 (wrapper exit 1). This failed run remains failed and its log is preserved; the earlier complete gate does not erase it. Only the new test host was changed: the flow completes in a separate task, queues native teardown, checks the game/screens/boards and absence of newly orphaned nodes **before** managed collection, releases explicitly owned input events, yields after the completed async flow, and drains managed finalizers while Godot remains alive. It does not manually dispose shared imported textures or alter production resource ownership.

Three independent fresh-user-data reruns then passed all 160 assertions each, with actual native exit 0 and no rejected diagnostic. This establishes the tested host cleanup behavior, not a claim to have discovered or fixed a production application lifetime bug. The canonical run predates these two test-only refinements; production sources are unchanged from the fully tested checkpoint. The targeted historical scene and repeated opening scene checks are additional runs, not a second complete canonical run.

The final historical native rerun passed 155 assertions. ExportRelease build passed with zero warnings/errors; the complete active-source format check and metadata guard passed (171 unique script UIDs). The final production source diff against the fully tested checkpoint is empty: only the two test refinements and this record followed the canonical run. No artwork, map, rules, translations or save migration is involved.

Publication uses the existing `feat/godot-grayflag-recovery-20261020` branch in `Tangerg/empire`, by ordinary fast-forward only. The repository workflow triggers on main pushes/PRs/manual dispatch; this feature-branch push is not evidence of a hosted CI run. Remote identity and commit verification are separate from local test results.
