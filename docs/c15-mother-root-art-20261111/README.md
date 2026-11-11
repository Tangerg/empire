# Mother-root art, fallback picking and diagnostic classification

Base: `1f62bebb`. The C15 960×640 view showed a gray object at the protected
mother-root location and a one-item artwork notice. Investigation separated the
causes rather than assuming the warning explained the object.

## Independent causes

- Mother-root had no structure art binding. DynamicStateView drew its intentional
  rule-derived SVG fallback; this adds no missing-resource diagnostic.
- The living fallback did not provide its sprite to the standing pick scene.
  In the formal board probe, 521 opaque samples produced zero mother-root Object
  hits (274 ground, 247 other/null). This explains why clicking its visible body
  could not select it. The fallback now supplies its actual alpha-tested visual;
  destroyed fallback retains the existing projected surface behavior.
- The sole player-facing issue was the informational absence of an authored
  ground outline, with canonical rectangular ground and generic perimeter used.
  That normal fallback remains in Diagnostics but is no longer counted as failed
  artwork. Genuine missing actor/texture failures still count and surface.

## Art ownership

No generated or altered bitmap is added. Mother-root reuses the game's existing
forest-temperate@2x atlas: oak-ancient while healthy/captured, stump-hollow while
heavily damaged/disabled, stump-low when destroyed. Lifecycle health/ownership
remain canonical; imagery does not change obstruction, movement or hit points.
Frame anchors are exactly the environment cells' physical contacts. Explicit
per-frame regions now admit interactive structures under the same strict geometry
checks used by actors, avoiding duplicate cropped files or a second renderer.

The native art contract checks all five states, exact source regions/anchors,
logical scale and ground contact. An isolated picking fixture explicitly removes
the mother-root binding so its fallback test remains effective after authored art
is installed. Disabling the fallback fix fails at opaque body selection (exit 1).

## Real-window acceptance

At 960×640 in the isolated C15 entry fixture, continued into staging and started
battle through the normal controls. The failure-objective locator framed the
mother-root at (11,7). The former gray cylinder is now the existing painterly oak,
consistent with the surrounding forest. Clicking its visible crown selected
“银林母根”; its dossier opened with 500/500 HP, neutral allegiance, repairable and
full-cover properties. Closing the dossier restored the battlefield. No one-item
missing-resource notice was present. The fixture used the “立即停止转移” prior
choice, not a user campaign save. The game window was closed after verification.

Two intermediate checks caught setup issues and were corrected before acceptance:
the newly indexed SVG needed explicit physical ground-contact metadata, and the
C15 diagnostic assertion needed ConfigureGround because the existing dynamic
fixture intentionally skipped ground geometry. Neither fix changes gameplay
rules or suppresses genuine asset failures.

## Verification and limits

- Build: zero warnings/errors.
- DynamicStateChecks: 1779 assertions passed, including unbound fallback opaque/
  transparent picking, destroyed surface retention, and formal C15 art/diagnostics.
- PresentationChecks: passed, including all five mother-root frame contracts and
  existing terrain/resource/occlusion contracts.
- Catalog boundary: 76 assertions passed. Native formatting and source metadata
  checks passed.
- Full BattleScreenChecks did **not** pass: it stops at the pre-existing C14
  recorded-status-feedback fixture, expected initial `10b86025` instead of current
  `0e41b5f8`. Both campaign levels and that fixture are unchanged versus `1f62bebb`.
  The previous C14 targetZones metadata addition changes the state digest; the
  old fixture must be independently replayed (130 actions) before migrating its
  five hashes. This slice does not alter those hashes or bypass that full gate.
- No fresh Core suite, all-native aggregate, full campaign playthrough, or remote
  CI pass is claimed. No private saves, image binaries or prior withheld evidence
  are included.

The related `BattleScreenChecks.Check()` + `CheckLocalizedInteraction()` subset
passed separately, including genuine missing-art notice behavior and its supported
960×640 / 1280×720 / 1920×1080 sizes and resize back. Its injected
MissingArtResourceException warning is intentional. This was a temporary local
entry-point selection; the complete production test entry point was restored
byte-for-byte and rebuilt with zero warnings/errors. It is not a full integration
pass and does not hide the recorded-status blocker above.
