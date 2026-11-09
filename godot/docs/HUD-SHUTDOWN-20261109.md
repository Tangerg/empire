# HUD verification-host shutdown

Implementation: `a76c70f5` on baseline `c25b99e0`.

## Evidence and scope

The [original full-gate failure](verification/combat-accents-20261109/full-native-failed.txt)
printed `TACTICAL_HUD_CHECKS_OK 1163 assertions`, then reported unsafe
AtlasTexture/StyleBox references during C# finalization and exited 139. Three
additional, unmodified runs in independent temporary user-data directories all
passed. This investigation therefore **does not establish a reproducible native
crash or its precise native cause**, nor demonstrate a production HUD leak.

The test host had a concrete lifetime gap: its large async `_Ready` still owned
art catalogs and generated texture caches when it requested engine shutdown.
It also did not assert final native HUD destruction or absence of newly orphaned
nodes before exiting. The existing `StagingOpeningChecks` already separates its
resource-owning test flow from native shutdown for this reason.

## Test-only change

- The original 1,163 checks now run unchanged in `CheckHudFlow`, which returns
  before shutdown processing. Exceptions still fail the scene.
- The final HUD must be destroyed, and the suite must have no mounted fixtures
  or new orphan nodes **before** managed collection. GC cannot conceal a native
  node ownership failure.
- After the completed async scope and deferred work have unwound, collection and
  finalization happen while Godot remains alive. Five weak references confirm
  that actual fixture cache owners are no longer reachable before shutdown.
- Only then is success printed and exit requested. Shared imported textures are
  not manually disposed. No production HUD, rule, asset, visual or save code changed.

This establishes a stronger test-host exit contract. It is not evidence that all
possible native shutdown crashes have been fixed.

## Verification

- Unmodified repeats: 3/3 strict process gates passed, 1,163 assertions each.
- Changed host: 4/4 strict process gates passed, 1,167 assertions each. Runs 2–4
  use the restored final code after the negative control.
- Negative control: a temporary `retainedCache` field was assigned the actual
  `art` owner before `CheckHudFlow` returned. The new cache-lifetime assertion
  failed with exit 1 as intended. The mutation was removed and final code rebuilt.
- Debug and ExportRelease builds passed with zero warnings/errors. Formatting,
  source metadata (201 script UIDs), and all 11 native-process wrapper tests passed.
- The earlier aggregate failure remains a failure. A new complete native gate
  has not completed at this checkpoint; these focused results do not replace it.
- This is native headless lifecycle verification, not visual acceptance.

Retained logs in [verification/hud-shutdown-20261109](verification/hud-shutdown-20261109/):
`before-1..3.txt`, `after-1..4.txt`, `negative-retained-cache.txt`,
`restored-build.txt`, `export-release-build.txt`, `format.txt`, `metadata.txt`,
and `wrapper-tests.txt`. The unchanged input-event warning headers are retained,
not suppressed. All success claims above include process exit and the existing
strict error/leak diagnostic boundary, not just a printed assertion count.
