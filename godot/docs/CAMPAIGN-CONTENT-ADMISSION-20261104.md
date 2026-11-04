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

Implementation and focused regressions are in progress. Final process exits,
negative controls, full deterministic replay/save gate and export-build results
will be recorded here before publication. No interrupted or incomplete run counts
as passing.

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
