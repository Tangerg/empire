# Structure-target collateral forecast — 2026-11-04

## Formal witness and scope

Baseline: `857fc8caa62f3e07a52ef94ee432128d10abc65f`.

The unchanged C01-09 opening (`6fefe68e`) plus 58 original legal commands reaches
`2bc44d8c`. Player 1's engineer (unit 7) can move from (8,9) through (9,9),
(10,9), (10,8) to (10,7), select the satchel charge, and target the east chain
at (9,6). Four admitted targets keep this in explicit target-preview mode.

The original native HUD showed only the chain's 57 damage, HP 57→0 and destruction.
The canonical `CombatPlan` also predicts a visible enemy at (10,6), unit 22,
taking 46 damage, HP 98→52. Confirmation emits that exact area hit and concludes
at `d5c97410`; the ordered event SHA256 is
`25e367bb85084fadea362d4e8af737799820cda31b02fbe33b25a76a6a82a0dc`.
The existing area overlay already highlighted coverage. The defect was the
missing collateral warning in the structure card, not absent area highlighting,
friendly fire, or a proven missing status effect in this witness.

The new native regression selects the real actor, uses keyboard destination and
target navigation, presses the native satchel order button, inspects the mounted
labels, and confirms the original action. Before the fix it exits 1 at the missing
warning assertion after proving the exact original state and ordered events.
After the projection fix it passes at 1180×760 and 960×640. It checks state/save
purity before confirmation and the original action and receipt afterwards.
This is an independent formal-level replay and native control test, not a full
carried campaign, physical-pointer test or rendered visual acceptance.

## Ownership and presentation

`CombatPlan` remains the sole source of targets, primary-hit identity and effects.
`GrayFlagSession.ProjectHud` now shares collateral and conditional-effect
projection between the structure and unit cards. It does not reconstruct damage,
movement, statuses, collision or future delayed-cast outcomes.

The displayed subject determines which hit is the main one. A unit card excludes
its primary unit but retains a structure under that unit; a structure card
excludes its primary structure but retains unit hits. This matters because a plan
can contain both kinds of primary hit. Unit visibility is checked before counting,
and zero visible additional hits do not produce a zero/zero warning. Existing
public structure disclosure is unchanged.

Conditional effects consume the plan's existing payloads, retaining source and
recipient visibility checks, authored order, stable emphasis, one summary per
phase, the complete tooltip and expanded details. A structure-only plan does not
create a counter or support phase. The formal satchel witness has no hit effects;
separate explicitly synthetic fixtures exercise the common effect contract.

The original structure card's generic row renderer wrapped a deliberately long
effect summary onto four lines. The native layout guard rejects that result;
structure summaries now use the same one-line/ellipsis policy as unit summaries,
with complete information kept in the existing scrollable details. No artwork,
map, battle rule, save format or save meaning changes.

## Verification checkpoint

- Formal missing-collateral native RED: expected exit 1 at the disclosure check.
- Full `BattleScreenChecks`: exit 0 after the projection change, including both
  formal resolutions and existing movement, recruitment, status, combat and
  session tests.
- Synthetic projection contracts cover unit, structure and occupied-structure
  targets, no effects, partial/all hidden collateral, original effect order,
  exact phase counts and unchanged session/state/save data.
- The first native test attempt used semantic clicks without pointer-hover state
  and therefore navigated from the actor rather than its destination. That test
  setup failure is retained separately. The accepted RED/GREEN path establishes
  its destination with real keyboard input instead of changing production input.
- Long structure-summary layout RED: expected exit 1, four visible lines at 960px.
  The full fixed `TacticalHudChecks` exits 0 with 520 assertions. Both widths show
  one summary line, a 388×213 plate at (278,419), accessible details and Cancel,
  complete scrollable detail rows and focus restored to the current opener.
- Two deliberate production mutations compile and fail at their intended native
  checks (exit 1): counting concealed units, and globally excluding primary
  structures even from unit cards. Their diagnostic names identify the fog and
  occupied-structure fixtures. The exact checked-in production source is restored
  after both tests; the Core tree is unchanged from the published baseline.
- Canonical aggregate verification and final ExportRelease build remain pending
  at this checkpoint.

The implementation follows [Godot scene organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html)
by keeping domain facts in the injected session and native controls responsible
for presentation, and [Godot input propagation](https://docs.godotengine.org/en/stable/tutorials/inputs/inputevent.html)
by testing through the actual Viewport and GUI order controls. Repository
`AGENTS.md` and `PROJECT_RULES.md` require one owner, observable regressions and
explicit separation between native checks and GUI acceptance.
