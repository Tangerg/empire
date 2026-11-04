# Morale inspection and routed departure feedback

## Scope and reproduction

Baseline: `518ee1def9dc17a690d11dd7c2a033ce047b23d4`.

Two related player-facing omissions were reproduced before changing production code:

- In unchanged C01-05 (`62f70e22`), native actor cycling, dossier activation and the unit-details disclosure showed the refugee's 58 HP and other statistics, but not its actual `80/80` morale. The dossier assertion failed on the original implementation.
- In unchanged C01-06, a legal retreat/healing strategy produces a morale rout while the victim still has positive HP. The actual native battle journal showed a nonlethal 71-point attack and gave no reason why the victim disappeared. The route feedback assertion failed on the original implementation.

The committed `tests/native/c01-06-routed-feedback.json` is a deterministic witness, not a modified battle state. It contains 54 legal prefix actions and the final default-AI command. The original exploration moved Laiya north to legal waiting positions, used available healers on injured low-morale allies, and ended human turns; it never changed controllers, HP, morale, rules or unit positions directly. Torren was **not** one of the healed units. A fresh replay independently matched all 55 before/after hashes and ordered receipts, and strict save/load matched both sides of the last action.

| Boundary | Canonical evidence |
| --- | --- |
| Initial formal C06 | `142efa8b` |
| Before final attack | `b9123452` |
| After final attack | `9dae0ceb` |
| Final ordered events SHA-256 | `281d342ff99a9c78fd7425e68da5a11dc24739068b2aa611497da5df167ad646` |
| Final actor | Default AI unit 14, `c01.knight-charge` |
| Victim | Unit 2, key `campaign-torren`, HP 74 → 3, morale 32 → 0 |
| Departure | `attack.killed=false`, no `death`, then `markerAdded(kind=routed)` and `unitRouted` |

The native regression loads the genuine saved prefix, asks the real AI session to execute the final command, and compares the exact action, state hash and complete ordered-event digest. It uses the authored C06 roster bindings solely for presentation identity, so the journal must explicitly say **“托伦：士气耗尽，溃退离场”**. The exploratory RED run used an empty identity binding and therefore showed the generic troop name; neither run modifies battle rules. This is a legal formal-level witness, not a claim that a full carried-roster campaign was completed.

## Ownership and smallest complete fix

- `BattleDamage.ChangeMorale` remains the only rule owner for morale changes and morale-caused routing. No combat calculation or rule was copied into the UI.
- `GrayFlagSession.ProjectHud` reads the inspected instance's normalized `morale.current`, `maximum` and `resilience`. It adds the value/gauge and loss-reduction/route explanation only when the current rules enable morale. Campaign-carried values remain authoritative rather than being reconstructed from troop defaults.
- The existing scrollable unit-details section renders these two rows using the existing label/gauge components. The fixed battlefield summary, map, art, layout primitives and save format are unchanged.
- `BattleEventText` describes `unitRouted` once from the committed event. No speculative route prediction, new animation, second event, or noisy per-hit morale text was introduced.
- Departure visibility uses the actor's **before** visibility: the routed unit is correctly absent from the after-state unit set. Name resolution occurs only after that check. `BattleLogContext` freezes the visibility inputs; existing unit names resolve from live state, before-state or departure markers.
- `BoardView` and event text share the existing before-visible predicate. Continuing-unit feedback such as regeneration still requires both boundaries. This extraction is behavior-preserving for animation.
- Three localized strings live in the existing PO catalog, with placeholders handled by the existing catalog formatter. No production prose is embedded in C#.

These boundaries follow the project's one-fact/one-owner rules and Godot's guidance on [focused scene responsibilities and injected collaborators](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html). Localization remains in the established resource pipeline, consistent with [Godot's translation resource and placeholder guidance](https://docs.godotengine.org/en/stable/tutorials/i18n/internationalizing_games.html).

## Regression contract

`MoraleFeedbackChecks.tscn` is included in the normal `verify-native.sh` gate. It covers:

- Native C05 civilian and commander dossier activation at 1280×720 and 960×640: actual values 80/80 and 120/120, resilience 0% and 35%, label and gauge agreement, and fit inside the real scroll clipping area. Inspection leaves the state/revision/events unchanged.
- The true depleted 32/100 instance from a strict C06 save, rather than a guessed default; C14's 140/140, 70% golem; and formal C01/C04 levels where morale is disabled despite normalized morale data being present.
- The real C06 AI rout with exact original action/state/events, native toast at the departure event, no earlier announcement, journal exactly once, settled refresh without duplication, and strict save/rebind without replaying historical departure feedback.
- Explicit **synthetic boundary tests**, separately identified from the real C05/C06 flows: the full null/visible/hidden before×after matrix, frozen mutable sets, hidden unknown identity filtered before lookup, visible unknown identity rejected, and a fog-only C06 snapshot that cannot disclose a concealed unit through selected/inspected/cursor ids.

A preliminary layout assertion expected the rule to wrap to two lines. Native measurement showed the actual text fits one 322×22 line in the existing dossier. That unsupported line-count assumption was replaced with the real scroll clipping and native height contract; no production layout was changed to satisfy the test.

## Verification status

Code checkpoint: `9619f7cbe29af3ad3156ff64598b700a53c0b2bd`.

- Targeted native morale regression: **74 assertions, exit 0**, including the actual journal/toast and both dossier sizes.
- Three deliberately broken variants compiled successfully and exited 1 at the applicable guards: current morale replaced by the maximum; departure requiring after-visibility; and hidden departures always visible. The second runner originally expected the failure in native playback, but unchanged C06 has fog disabled and therefore unrestricted null visibility. Its later explicit visibility matrix correctly rejected the variant. The original runner mismatch/log was retained and classified, not represented as a successful runner invocation. No test or production guard was weakened; source was restored before the full gate.
- Full current native gate plus localization foundation and ExportRelease build: **43 stage processes exited 0**, including **28 project native scenes**. The uninterrupted driver ran **2026-11-04 11:05:51–11:45:12 UTC (39m21s)**. Each stage retains its start, commit and terminal exit record. No interrupted stage was counted as successful.
- All 28 project native scenes passed the error/leak/teardown scan. The aggregate's editor import exited 0 but printed `EditorSettings not instantiated yet when getting setting "export/android/android_sdk_path"` after editor initialization. This exact intermittent editor diagnostic predates this batch and is recorded in `MENU-SAVE-ADMISSION-20261104.md`. Its original log remains preserved. The **same import command** was run separately on the unchanged checkpoint at **11:46:43–11:46:50 UTC**, exited 0, and passed the error/leak scan. This is a clean supplementary import check, **not a claim that the original aggregate log was error-free or that Godot itself has been repaired**.
- Debug and ExportRelease: zero build warnings/errors. Format/source-selection checks, 169 script UID companions, all four authored-map static validators and their negative cases passed. Existing authored `runtimeReady=false` diagnostics and eight pre-existing duplicate mouse-enter warnings in the HUD harness remain visible, unchanged from the previous gate.
- Core: 13 contracts, all 16 formal independent levels and **3,425 legal orders**, with exact strict save/replay round trips. Level action counts and state digests match the previous baseline; the only core-log difference is elapsed time in a preview check. These are independent-level results, including losses, not a carried-state campaign victory claim.
- Localization: **46 source tests**, 18 formatter assertions, **1,225** validated PO messages and exact native translation-resource equality.
- Relevant integrations: animation 844; HUD 510; idle guidance 83; dossier focus 73; real-session BattleScreen; regeneration including hidden-boundary behavior; scenario announcements 337; battle-result flow 371; campaign navigation 8,977 with zero failed groups; campaign UI 205; lighting lifetime 106; lighting 1,791; scenery 8,753.
- Gate scripts and Debug binary SHA-256 values matched at completion and after the supplementary import. The checkpoint's worktree remained clean throughout the aggregate and import recheck.

Headless native controls, translation resources and geometry are **not rendered GUI, device, or export-package visual acceptance**. No screenshot or visual acceptance claim is made.

Reproduction commands, using the existing authorized toolchain:

```sh
source tools/godot/env.sh
dotnet build godot/Empire.csproj --disable-build-servers -m:1 -p:UseSharedCompilation=false
"$GODOT_NET" --headless --editor --path godot --import
XDG_DATA_HOME="$(mktemp -d /tmp/empire-native-check-data.XXXXXX)" \
  "$GODOT_NET" --headless --path godot res://Scenes/MoraleFeedbackChecks.tscn
bash tools/godot/localization-checks/run.sh
bash tools/godot/verify-native.sh
```
