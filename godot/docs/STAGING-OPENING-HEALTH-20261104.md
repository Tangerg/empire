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

Full canonical gate, extra historical staging scene, ExportRelease and final publication are pending at this checkpoint. Logs and exit records are retained in the task workspace. Headless native input/text/resources/state verification is not a GUI/pixel or physical-device visual acceptance claim.
