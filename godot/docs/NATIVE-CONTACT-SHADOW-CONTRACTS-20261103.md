# Restore assembled-ground test contracts — 2026-11-03

## Reproduction and authority

After the format gate was repaired, the full native aggregate failed in
`AssembledGroundChecks` at `Tree/SourceStructureContact`. An independent
`LightingChecks` run failed its corresponding per-standing-object source check.
Both failures were reproduced before this repair. After the contact checks were
corrected, the same scene exposed another pre-existing assertion requiring six
vertices per physical surface despite the existing consecutive equal-depth draw
grouping. That failure was reproduced before its test was corrected too.

The shallow accepted baseline `1375167b7` already contained both the corrected
renderer and those obsolete test assertions. The original
[contact-shadow depth audit](art-provenance/c07-layered-v1/contact-shadow-depth-audit/README.md)
records why a standing-child ellipse was hidden behind the floor and why its
existing pixels moved into a bounded ground projection. This repair does not
reverse that intentional renderer correction or alter the original art evidence.

`AssembledGroundView` owns one sibling contact aggregate per declared shadow:

- One hidden source retains the texture, owned by its cache or by the uncached
  procedural source. Instance scale is applied once to this source's transform.
- One `PaintedSurface` borrows that texture, clips registered ground tops and
  derives absolute Surface depth from `BoardProjection`.
- The contact follows the base floor at equal depth. It never becomes a standing
  depth entry, a picking owner, or another canonical game-state writer.
- A replacement projection replaces and frees floors, preserving sources and
  textures. Repeated render preserves identities. Unload releases nodes and
  event subscriptions; the cache and procedural source retain their distinct
  texture ownership responsibilities.

No runtime code, project compilation rule, map, scene, artwork or save schema is
changed. The existing MapStructure source-child checks remain intact because
canonical map structures still implement that different, valid contract.

## Test repair

`AssembledGroundChecks` now verifies the actual fixture's sibling/source/floor
relationships, centering, local and global scale, UV alignment, absolute floor
depth, strip uniqueness and separation from standing/picking ownership. A sweep
of the map's cell contacts compares Pick results with contact floors visible and
hidden. Existing material, transparency, pointer, movement, strip-coverage and
canonical-state assertions remain present.

Twelve reversible real-node mutations exercise missing or duplicate contact
parts, source visibility, double scale, stale placement, wrong source center,
relative or standing depth, and restoration of the old standing-child source.
Each must fail at its intended diagnostic, then the restored fixture must pass.
Cached and uncached lifecycle cases verify repeated render, replacement, unload,
subscriptions and texture release, with negative controls for a replaced texture
and premature replacement/unload assertions. Rebind checks immediate detachment
and queued deletion, then awaits real scene-tree frames before requiring native
instance invalidation. A first test revision incorrectly demanded immediate
freeing; it was corrected to match the existing QueueFree contract, not by
changing production cleanup.

The material fixture now reads actual visible `Polygon2D.Polygons` indices and
compares the complete ordered triangle stream with its unchanged authored JSON:
geometry, global placement, UVs, material and tint. It requires the supported
consecutive same-depth draw grouping while retaining distinct physical fronts,
coverage and picking owners. It does not accept arbitrary vertex counts or
remove the original RGB gap/native-alpha hole checks. Seven reversible mutations
per material mode reject a wrong texture or UV, missing or duplicated primitive,
a visible redundant follower, reordered geometry and wrong tint. The prior
[draw-grouping checkpoint](art-provenance/c07-layered-v1/depth-draw-batch-checkpoint/README.md)
and the existing `SceneSurfaceChecks` define the preserved representation.

`LightingChecks` follows the formal scene's assembled document and object catalog
to obtain declared contacts. It verifies one contact per declaration, not one per
standing strip, plus texture, centering, scale and floor-depth contracts. It is
now part of `verify-native.sh` using the existing checked-in lighting oracle and
the same strict process-exit/teardown gate as the other native scenes.

Freed references are tested with instance validity, never by invoking an instance
method after deletion. This follows the [Godot 4.6 Object lifetime contract](https://docs.godotengine.org/en/4.6/classes/class_object.html).
Explicit subscription cleanup is verified rather than assuming that all C#
connections disconnect automatically; the [Godot C# signal guidance](https://docs.godotengine.org/en/4.6/tutorials/scripting/c_sharp/c_sharp_signals.html#disconnecting-automatically-when-the-receiver-is-freed)
explains the relevant lifetime caveats.

## Verification

- Baseline AssembledGroundChecks and LightingChecks: failed at obsolete contracts.
- Focused official formatter and native build: passed, zero warnings/errors.
- Focused AssembledGroundChecks: 17,654 assertions, all 32 deliberate contact,
  lifecycle and material rejections observed, exit zero with clean teardown.
- Focused LightingChecks (1,791 assertions) and SceneSurfaceChecks (60) passed.
- Full aggregate passed, exit zero: 2026-11-03 12:52:52–13:29:29 UTC
  (36 minutes 37 seconds), with the supported bounded command
  `EMPIRE_CHECK_TIMEOUT_SECONDS=900 EMPIRE_REPLAY_TIMEOUT_SECONDS=1200 bash tools/godot/verify-native.sh`.
- All 30 formatter selector regressions, 385-source formatting, source metadata,
  four static scene validators, native builds, all 11 Core groups, objective/
  outcome and external catalog boundaries passed. The 16 independent level
  runs performed 3,379 legal actions with exact save/replay round trips.
- All 22 native scene stages completed, including the repaired checks, C04–C07,
  16-level initial presentation, camera (313), HUD (379), dossier focus (73),
  real battle screen, battle results (371), campaign navigation (7,064),
  campaign UI (205), lighting lifetime (106 across three real-session cycles),
  lighting (1,791) and scenery (8,753) checks.
- No error/leak/crash/unhandled-exception diagnostics. Known nonfatal output
  remains visible: an excluded nested historical project, redundant viewport
  mouse notifications, authored-readiness diagnostics, and deliberate missing-
  art/invalid-save/storage-fault warnings from exception-path fixtures. This is not described
  as a warning-free aggregate.
- Final diff review, `git diff --check` and unchanged art/map/scene/provenance
  checks passed. No native runtime source was modified.

Independent level replays are not a victorious carried-state campaign route;
detached/no-AI mission fixtures do not establish normal-AI combat balance.
Export packaging was not rerun for this test-only repair.

These checks inspect actual headless Godot nodes/resources and functional
contracts. They do not provide graphical or GPU pixel acceptance.
