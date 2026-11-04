# Campaign save content admission

Baseline: `7c65299f08c3c44e564bc60be8ddc26d1caa6990`.

## Reproduced player-facing defect

Using the baseline Debug assemblies and an isolated native Godot session:

1. Start New, finish the introductory story and choice, enter C01, issue a legal
   order, and save the played battle with F5.
2. Replace only the isolated manual-slot fixture with a structurally valid
   prologue application save whose `roster.laiya.unitType` names unknown content.
3. Inject F9 through the actual viewport input path. The played battle is replaced
   with the prologue and the application reports that loading succeeded.
4. F5 confirms that corrupt roster is now the live campaign. Seven ordinary story
   advances then overwrite the previously healthy autosave with the corrupt roster.

This is an externally malformed input fixture, not a claim that ordinary gameplay
spontaneously produced corruption. The native witness is headless input/application
integration, not GUI visual acceptance. Original evidence, including the source
assemblies and observed process exit codes, is retained separately.

Unknown current careers, unlocked careers, learned abilities and career mastery
names likewise passed clean campaign admission and failed only when building a
later battle opening. Pending-battle saves had additional source identity checks;
clean story and staging saves did not have an equivalent content boundary. Generic
resource account IDs are deliberately open and are not missing catalog references.

## Ownership and fix

- `CampaignSave` remains the content-independent structural codec.
- Existing unit-type and career predicates are extracted from `BattleLevelValidator`
  to `UnitContentValidation`. Level diagnostics and career rules remain unchanged.
- `BattleReferenceCheckRegistry.InUnit` owns unit-seed rule references, including
  learned abilities from the actual composed ability registry. Level validation and
  `BattleEngine.UnitContentIssues` use that same reference owner.
- Application `CampaignSaveAdmission` maps every roster member to the unit content
  contract before either manual load or autosave Continue may activate it. It does
  not limit checks to current bindings, available members or the current scene.
- Authored initial roster defects remain campaign invariants. Only diagnostics on
  structurally admitted saved roster data become stored-document rejections.
  Exceptions from composed checkers propagate unchanged.
- The existing manual load transaction and file-write owner are retained. Rejection
  preserves the live campaign, pending request, battle instance and revision, view,
  history, and both original save files. Nothing silently repairs or rewrites input.

No map, artwork, gameplay rules, persistent schema or slot semantics change. The
required `CampaignSlotStorage.Load` dependency is updated in all repository callers
and linked check projects; there is no old overload that can bypass admission.

## Verification

Verified implementation checkpoint: `6594cadeeb6eef7e4b83545a593641a4de6914f8`.
The following publication commit changes only this report.

- Debug build: exit 0, zero warnings/errors. Source metadata: 175 unique UIDs.
- Shared unit-content core check: exit 0. Its first new fixture omitted the required
  player color; that fixture was corrected, and the failed run is retained rather
  than counted as passing. No production rule was changed to accommodate it.
- Isolated autosave/menu boundary: 245 assertions, exit 0, including invalid content,
  promotion/open-account positives and explicit authored/checker invariant cases.
  The existing broad-catch mutation was correctly detected.
- Complete native campaign navigation: 9020 assertions, zero failed groups, exit 0.
- Separate copied-project native control: 167 assertions, exit 0. Bypassing stored
  content admission failed at the real prologue F9 preservation assertion. Skipping
  unavailable members failed at the fallen Torren admission assertion. Both mutants
  exited 1 for their intended assertion, not a timeout or failed build. Production
  sources and assemblies were never replaced with mutant implementations.
- Complete canonical gate: one uninterrupted run from **2026-11-04 20:24:51 UTC** to
  **21:09:15 UTC**, **44m24s**, exit **0**. This includes eight preparation checks,
  both Debug builds, 15 core groups, two boundary suites, editor import and all
  30 native scenes. All 31 import/native logs pass the strict error/leak/crash/
  unhandled-exception boundary.
- All 16 formal AI levels preserve the exact **3425** ordered action/event/receipt
  traces and final state digests, including exact save/replay round trips. These
  are independent levels, not a claim of completing the carried-state campaign.
- Forty warning headers equal the previous gate's 30 plus exactly ten deliberate
  malformed-roster refusals from the new regression. No baseline warning is removed
  or hidden, and no additional unexplained warning is accepted.
- ExportRelease build: exit 0, zero warnings/errors. The verified Debug assemblies
  remain byte-identical after that separate configuration build.

No GUI visual acceptance or packaged export was performed: this environment has
no graphical display or installed export templates. The tests establish native
input, state, file and scene/resource contracts, not rendered visual quality.

The native regression covers real New → story → choice → battle → legal move,
F9 refusals for all four application locations, exact live-owner/file preservation,
normal F9, invalid autosave startup and normal Continue. Detached cases also cover
future/unbound and fallen roster members. Core regressions check equivalent level
and standalone diagnostics, a separate custom content/ability composition, an
additional unit-reference extension, open resources, cross-type unlocked career
history and propagation of programming defects.

The boundary follows Godot's focused-owner/explicit-dependency guidance:
https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html
Serialization and activation admission remain distinct responsibilities:
https://docs.godotengine.org/en/stable/tutorials/io/saving_games.html
