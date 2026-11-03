# Native recruitment asset coverage — 2026-11-03

## Failure and scope

Binding production art to the generic nine-card castle recruitment HUD threw
`MissingArtResourceException` for `C01-UNIT-ASSASSIN`. The same declaration audit
found five unit topics missing from the native `data/selected-assets.json`:

| Rules unit | Formal topic | Approved-v5 sheet |
| --- | --- | --- |
| rogue | C01-UNIT-ASSASSIN | unit-assassin@2x.png |
| cleric | C01-UNIT-CLERIC | unit-cleric@2x.png |
| mage | C01-UNIT-MAGE | unit-mage@2x.png |
| ogre | C01-UNIT-TROLL | unit-troll@2x.png |
| dragon | C01-UNIT-ANCIENT-DRAGON | unit-ancient-dragon@2x.png |

All 39 unit bindings already had HUD portrait declarations. The affected generic
units deliberately use their own idle frames as portraits. The missing selected
resources therefore broke both recruitment icons and inspection portraits.
Generic barracks and castles offer these units; none of the sixteen shipped
campaign starting states uses them. This repair does not add a recruiting site,
change a campaign map, or claim a new playable campaign route.

## Resource provenance and repair

The five sheets already have formal entries in
`packages/story-candidate-01/assets/final-fantasy-v1/manifest-final-fantasy-v1.json`
and `manifest-tactical-runtime-hd.json`, marked
`approved-chunky-v5-export-verified` and `runtimeReady: true`. Their approved
animation exports are in
`docs/art-rebuild-20261019/approved-v5/animation/<character>/export/sheet@2x.png`.
Each native PNG is a byte-for-byte copy of both that export and the corresponding
`runtime-approved-v5/combat-unit` source, not generated, recolored, resized, or
substituted art. Transparent alpha values, including the source maximum of 254,
are preserved.

The native manifest gains exactly five records. Logical frame geometry, anchors,
footprints, frame order, and 2× density come from the formal source manifests.
Native presentation uses the existing approved-v5 convention: scale 0.62 and
ground contact equal to the registered anchor. Godot generated the adjacent
texture import sidecars. All 68 existing selected records and every existing
asset byte remain unchanged, as do town scenes, art bindings, rules, and content.

The historical export selection considered placed units, which explains how
declared but initially unplaced recruits were omitted. That exporter now writes
an oracle fixture, not the authored native manifest. It was not rerun over native
art. Instead, the native resource contract now covers every declared unit binding
and every frame, including units absent from starting maps.

## Regression coverage

- Before adding the resources, the art-bound HUD regression reproduced the
  missing-assassin exception. The check was retained, not bypassed with unbound
  art or a fallback.
- `PresentationChecks` resolves every declared unit and loads every animation
  frame, checking canonical topic, resource path, region, and dimensions.
- The nine-card recruitment layout check uses production art at 960×640 and
  1180×640. It checks each icon against that unit's bound idle frame while
  retaining the existing layout, affordability, focus, and lifetime checks.
- A detached funded C03 fixture recruits all nine units through the selection
  controller and real session dispatch, separately for each unit. Fresh and
  saved/restored sessions display the correct inspection portrait without
  changing canonical state. All declared animation roles resolve with uniform
  scale and the registered ground-contact offset.

## Completed checks

- `dotnet build godot/Empire.csproj`: zero warnings and errors.
- Headless editor import: completed with no error or resource-leak diagnostics.
- `TacticalHudChecks`: 379 assertions, including the nine real recruitment flows.
- `PresentationChecks`: all declared unit/frame contracts, sixteen authored scene
  projections, terrain contracts, and resource ownership checks passed.
- `InitialHudCoverageChecks`: 178 C01–C07 samples passed.
- `BattleScreenChecks`: real-session integration passed.
- All four final scene processes exited zero with no `ERROR:`, leaked-resource,
  segmentation-fault, or unhandled-exception diagnostics. Existing source-readiness
  and mouse-notification warnings remain visible.
- Changed-source whitespace verification and `git diff --check`: passed.
- Source metadata: 154 unique script UIDs and all resource sidecars present;
  14 isolated positive/negative metadata cases passed.
- Exact-copy provenance checks, existing-record equality, and absence of these
  five units from all sixteen campaign starting states: passed.
- Independent code/data review found no blockers.

Scene checks used fresh `XDG_DATA_HOME` directories and a 600-second process
budget. The pre-repair failure is negative evidence, not counted as a passing run.

## Verification boundaries

These tests verify native resources, control layout, and recruitment/save/load
integration. Sprite assertions are not visual animation playback or pixel-level
foot-registration acceptance. The source approval documents themselves distinguish
export verification from live-game acceptance. No new current-Godot GUI acceptance
or screenshot is claimed: this workspace cannot start a graphical display.

The full native aggregate remains blocked by pre-existing whitespace failures in
184 untouched files, documented in the [tray repair report](UI-MULTIROW-ORDERS-20261103.md).
Targeted checks do not imply that gate, unrelated TypeScript tests, or export
acceptance passed. No production rules, content, or save schema changed.
