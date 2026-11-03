# Native healing target inspection

## Formal witness and defects

`tests/native/c01-12-healing-target.json` preserves the original 95-command
prefix from the shipped C01-12 level, not a modified map or hand-written battle
state. Before zero-based action 94, Mirelle (unit 4) is at `(7,4)` and Tasha
(unit 6) is at `(8,4)` with 65/92 HP. The first 94 legal commands reach digest
`c05a250e`.

In a mounted `BattleScreen`, selecting Mirelle, pressing the actual Heal command
button and moving the keyboard cursor right entered the correct healing target
mode. However, no recipient card existed. The regression failed with native exit
1 before the projection change. The actor was correctly pinned; the missing
counterpart was caused by generating target information only for weapon orders.

After adding the card, a second real native pointer test failed with exit 1:
moving from an opaque Tasha sprite pixel to the card's Cancel button removed the
preview and button. The map-to-HUD ownership handoff unnecessarily required a
weapon. The repeated-order test then exposed a third failure: cancel and re-arm preserved
`CursorHit` but cleared `PreviewTarget`; the screen suppressed the same pixel hit
before the selection owner could establish a new preview. This also failed with
native exit 1. All three failures belong to the same preview/cancel/confirm flow.

## Owners and scope

- `TacticalSelectionController` still owns target selection, held preview and
  live confirmation. No new selection state or target cache exists.
- `GrayFlagSession.ProjectHud` projects a generic nonweapon unit recipient's
  canonical name, existing unit artwork identity and current/max HP into the
  existing compact target plate. `VisibleHit` checks visibility, ID and exact
  coordinate before any recipient data is read for the card. Target subjects
  remain the admission boundary.
- The actor stays pinned in its original tray. The target card is labeled
  "Choose target", not an attack or healing prediction. It does not invent an
  effect amount or depend on an ability ID.
- `BattleScreen` retains any armed unit target preview while pointer ownership
  transfers into its HUD, using the existing deferred hover resolution and
  stamp/gesture guards. Actual window leave still clears it.
- `BattleScreen.HoverField` routes Hover/Leave to the selection owner before
  comparing the resulting cursor, selection and inspection tuple. It suppresses
  redundant projection work only after the owner has had a chance to update the
  preview; it does not duplicate target eligibility or keep another cursor cache.
- Existing abilities and `UnitHealth` remain the only healing-rule/mutation
  owners. Confirming the original order still heals 27 HP and reaches
  `44ae7c85`, with ordered event SHA-256
  `42442348edd3a34590835880faa990a1616d19180fb497c86d565479a6af82ed`.

There are no engine, map, art, translation, save-format or combat-rule changes.
The implementation follows Godot's [scene organization guidance](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html):
views receive projected dependencies, and the owning screen mediates the map/HUD
interaction rather than introducing peer lookups or another state owner.

## Regression boundaries

`TacticalHudHealingChecks.cs` is part of the existing mandatory
`TacticalHudChecks` native scene and excluded from `ExportRelease` by the
existing `*Checks.cs` compile boundary.

- Formal original prefix, native Heal button, keyboard target and exact final
  state/event receipt through the actual session.
- Mounted identity, artwork, current HP bar and confirmation guidance at 960
  and 1180 pixel widths.
- Actual native map-to-HUD pointer transfer, real Cancel click, re-arm and
  window leave; no inspection/cancel path advances the battle.
- Wrong identity, coordinate, ground/object hit and missing unit rejection.
  Held preview over invalid ground cannot authorize Enter or a click there.
- Leave, Back, rebind and completed dispatch discard the recipient card.
- Explicitly detached projection-switch coverage uses two visible real units.
  This is not a claim that C01-12 has two legal healing recipients at this point.
- A separate visibility boundary changes only the fog flag on a detached legal
  snapshot, then supplies a hidden-unit subject/preview. No card may disclose it.
  This is a privacy negative, not an ordinary played healing command.

## Verification record

The initial missing-card native RED and the later pointer-handoff native RED
returned exit 1; the same-target re-arm regression also returned exit 1. A draft test initially used an incorrect actor key; that
failed run is not the handoff evidence. The corrected test uses the actual
`campaign-tasha` identity.

The final focused Debug build passed with zero warnings/errors, and the complete
`TacticalHudChecks` scene passed 437 assertions with native exit 0 and no strict
error/leak diagnostics. Format write and source metadata checks passed. The three
formal interaction regressions, exact healing receipt and privacy boundaries are
included.

The original `tools/godot/verify-native.sh` commands were run as separately
recorded stages against code commit
`f8584534516484e4adab46e4bcc243068c3693d4`, with an additional ExportRelease build.
All **39 stages**, including **25 native scenes**, completed with exit 0 between
22:54:42 and 23:31:26 UTC on 2026-11-03. The environment restarted during
`CampaignNavigationChecks`; the previous 34 confirmed stage results and exact
binary hashes were preserved. That incomplete stage's evidence was archived,
the same command reran from its beginning, and the remaining four stages then
completed. **This was not one uninterrupted aggregate invocation.**

The completed gate includes source-selection negatives, formatting, metadata,
four formal-map validators, 13 core contracts with 16 independent native AI
battles (3425 legal actions) and exact save/replay round trips, 141 objective and
outcome assertions, 76 catalog-ownership assertions, and all native UI, camera,
map, animation, battle/campaign flow and resource-lifetime scenes. The existing
844 animation assertions and attack-target pointer handoff regression still
pass. The independent levels include losses and turn limits; they are not a
carried-state campaign victory or fresh historical TypeScript AI parity proof.

Debug and ExportRelease builds both completed with zero warnings/errors. Actual
MSBuild ExportRelease evaluation includes all three changed production files
among 108 compile items and excludes both HUD check sources. Every native
process exited normally without the gate's error/leak diagnostics; existing
warning-path and authored runtime-readiness notices remain.

The tested Debug binary hashes, rechecked after the restart and at completion:

- Empire.Core.dll: `aac13baaffc1d6f8ce1c61a0476241306ad4c6d957c8761c567714d01562c2fb`
- Empire.dll: `fed3a395bb35248bc6df4efc1e73e17c3fce158bb0345f4b1112e60533f51efd`

No engine, map or art source changed relative to the previously published
`4eb564cf2` baseline. This final verification update changes documentation only.

Headless native node/layout/resource checks are not GPU screenshot review or
exported-executable acceptance. Existing authored runtime-readiness diagnostics
and native warning-path checks are not evidence of visual acceptance.
