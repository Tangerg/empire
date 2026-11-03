# Multi-row order tray sizing — 2026-11-03

## Reproduced defect and scope

The HUD fitted the instruction's wrapped text but assumed every main action group
needed only 78 pixels. A detached C03 recruitment fixture, using an owned generic
castle and the unchanged nine-card recruitment catalog, produced three button
rows. At 960×640 the grid extended to y=709, past the window bottom and across the
instruction at y=608. The first regression run failed on tray containment.

This fixture changes a copied tile into a recruiting castle. It does not enable
recruitment on a shipped map, execute an order, alter saved data, or establish a
new playable campaign route. The original town, selected assembly, maps, rules,
localization strings, and artwork are unchanged.

## Repair

The existing tray now grows upward using the mounted action group's native
combined minimum height, in addition to the existing shaped instruction height.
This includes the heading, multi-row grid and read-only account row. Buttons
retain their existing fonts, targets, disabled states and canonical keys. A return
to a short menu releases the extra height.

The mounted group's minimum-size signal also refits the tray after native layout
changes. Each hint owns one stored callback. Its exit disconnects that callback
only while the source and connection still exist, including repeated HUD
detach/remount. Retained action controls keep their identity and keyboard focus
across hint and projection refreshes. No second gameplay state owner is added.

## Verification boundaries

The initial geometry regression used actual projected recruitment strings and
the native theme, with art unbound. An exploratory art-bound run exposed a
separate pre-existing catalog gap: `C01-UNIT-ASSASSIN` was absent from the selected
asset manifest. The follow-up [native recruitment art repair](NATIVE-RECRUITMENT-ART-20261103.md)
adds the five missing formal resources and runs this same regression with native
art bound. Neither change enables a recruiting site on an authored campaign map.

This workspace cannot start a local graphical display, so no new screenshot or
manual visual acceptance is claimed. Headless bounds, native focus/input and
teardown checks are the evidence for this change. The existing 640×480 battle
layout boundary and full campaign/export acceptance remain outside this slice.

## Reproduction

Build with the workspace's configured official .NET/Godot toolchain, then run
`res://Scenes/TacticalHudChecks.tscn` headless. The new checks cover 960×640 and
1180×640, all nine canonical keys and affordability states, full bounds and
instruction lines, minimum button heights, repeated refresh with focus retained,
native row-height expansion/contraction, and return to a compact single order.
The fixture snapshot, revision and event stream must remain unchanged.

Use a fresh `XDG_DATA_HOME` for each scene. Require both a zero exit code and no
`ERROR:`, leaked-resource, segmentation-fault or unhandled-exception diagnostics;
a printed success line alone is insufficient.

## Aggregate gate limitation

The repository-wide `tools/godot/format-native.sh --check` fails on pre-existing
formatting in 184 untouched files, including ground-surface runtime/check sources
and historical C# art-provenance probes. The changed HUD sources pass
the same formatter in verify-only mode; none of the full-check error paths were
changed by this patch. The unrelated sources and the aggregate check were left
intact. This is not a claim that the full native gate or unrelated TypeScript
suite is green.

## Original tray-repair checks (commit 28cbc7bd)

- `dotnet build godot/Empire.csproj`: zero warnings and errors.
- `TacticalHudChecks`: 296 assertions.
- `IdleGuidanceChecks`: 32 assertions.
- `DossierFocusChecks`: 46 assertions.
- `CommanderNameChecks`: 36 assertions.
- `InitialHudCoverageChecks`: 178 C01–C07 initial display samples.
- `BattleScreenChecks`: real-session integration passed.
- `CampaignUiFlowChecks`: 200 assertions.
- `CameraIntegrationChecks`: 277 assertions. A first run hit the locally chosen
  240-second process budget and was not accepted; the completed rerun used a
  600-second budget and exited cleanly.
- All eight final native scene processes exited zero with no error or resource-leak
  diagnostics. Existing mouse-notification warnings and source readiness
  diagnostics remain visible rather than being suppressed.
- Changed-source whitespace verification and `git diff --check`: passed.
- Source metadata: 154 unique script UIDs and 14 isolated positive/negative
  metadata cases passed.
- C07 authored scene and the selected asset manifest match the base commit
  byte-for-byte. No authored art, data, campaign save or novel source was changed.
