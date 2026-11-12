# C08 grain-road first materialized pass

This scene retains the canonical 22 × 14 level, roster, objective, choices and
ledger triggers unchanged. Canonical SHA-256:
`c3895c5463000c022ceae230e700a5370e803e69c95812e2a468c0252e9604fd`.
No new levels are added; C09 is not modified. Runtime readiness covers a playable
first materialized pass, not a claim that C08 now fully matches C01–C07 art quality.
Regularity of the warehouse/wall composition remains a follow-up art direction task.

The registered 1536 × 1024 ground uses origin (128, 220), cell vectors (48, 0)
and (6, 40), and world scale 0.8. Continuous urban surfaces are decorative
projections, never a second movement or occupancy model. The existing shared
C03 object catalog owns source geometry; C08 instances do not duplicate anchors.
Sixteen canonical capturable sites have independent building bodies. Twelve
wall segments describe thirty impassable cells. Four small loading piles and
one ledger desk are nonblocking, noninteractive landmarks; they cannot capture,
change terrain or trigger an event. The canonical event remains authoritative.

## Art provenance

Built-in image generation, not CLI, created these transparent/ground assets:

- `ground-v4.png`: registered ground-only edit, three warehouse courtyard
  materials linked by worn cart lanes; quiet central ledger paving; no baked
  buildings, walls, tall props, fake building shadows or additional obstacles.
- `grain-wall-v2.png`: independent substantial masonry with visible top,
  side, front and ground contact, then a subdued gray-brown material edit.
- `grain-stock-v1.png`: small linen grain sacks and a wooden shipping crate.
- `ledger-desk-v1.png`: low desk with open ledger, inkwell and quill, no text.

All were visually inspected, then copied without image processing into the
project. Actual returned dimensions, rather than requested dimensions, own the
catalog registration. Ground and geometry were checked again after each edit.
Existing C01/C02/C03/C04 houses, warehouses, towers and trees are reused.
Rejected uniform paving, thin-wall and overly bright-wall variants are external
candidate evidence, not shipped resources.

## Verification status

`GrainRoadChecks` checks canonical hash, registered ground selection, all sites,
all thirty visible wall-cell centers, sixteen accessible building facades,
unmodified state, twenty-one initial actors, no generic duplicate structures,
and direct ground picking of both ledger trigger cells. Removing the ground
binding deliberately fails the regression. This is not visual acceptance.

The formal main menu loaded the isolated C07 continuation export documented in
`../c08-entry-fixture/README.md`, then traversed C08 briefing, pension-ledger
choice, preparation and battle. This is a detached C07 legal continuation,
not a claimed C01–C08 full campaign run. Formal GUI checks include actual
movement through the left warehouse-side lane and undo, plus map panning and
selection of the north-east outpost outside the HUD. At 960 × 640 a guard moved behind a roof; its occlusion callout remained visible
and clickable and opened the correct unit details, then undo restored the unit.
HUD controls remained usable. Broad scene and continuation gates are recorded
in the change validation report; GUI capture is not a frame-rate benchmark.

## First-pass validation (2026-11-12)

- Debug and ExportRelease builds: zero warnings/errors.
- Native formatter, source metadata (224 unique script UIDs), and diff check passed.
- GrainRoad: 55 assertions; removal of assembled-ground selection failed as expected.
- AssembledGround: 18,032 assertions; SceneSurface: 60; PaintedSurface and
  Presentation resource/terrain gates passed.
- Camera: 74; CameraIntegration: 493; BattleScreen real-session integration passed.
- TacticalHud: 1,324; its existing duplicate mouse-enter notifications were warnings,
  not ignored errors; strict native process/diagnostic wrapper exited successfully.
- Luojia: 38,652 without export; verified C07 normal-AI continuation (263 orders,
  106 computer orders, 699 events, terminal `ce680ba7`), both C08 choices retain
  `d9e1ef40` / `ef5b44a4`. Earlier export-enabled pass had 38,666 assertions.
- Core suite: 17 passed / 0 failed, including sixteen independent shipped-level
  full battle/save/replay checks. This is not sixteen-level manual GUI acceptance.
- Formal GUI: 1180 × 812 and 960 × 640, C08 preparation/battle, movement preview,
  actual movement/undo, roof-obscured unit callout selection, pan to north-east site.

Only the listed affected gates were run for this art/registration slice; the entire
`verify-native.sh` aggregate was not rerun. GrainRoad is now included in that gate.
The first no-restore Core build found missing local restore assets; a normal build
restored successfully and the full Core execution passed. Failed intermediate art
registrations were corrected and are not counted as acceptance passes.
