# Ordered displacement and combat feedback

## Observed defect

The unchanged C01-06 command at zero-based replay index 5 moves knight 12 to
(22,27), attacks knight 14 at (21,27) for 44, pushes it to (20,27), and receives
24 counter damage. In a mounted native `BattleScreen` using the real human
session, the old effect appeared at (640,614.4), while the recipient remained
at (665.60004,627.2). A regression first failed on that exact discrepancy after
checking the original legal command, complete ordered events and final state.

The renderer initialized actors from `Before`, but looked up unit impact and
strike positions in final `After`. It skipped `forcedMove` and `collisionDamage`.
Thus it hit an empty destination, then snapped the defender into place for its
counter. The same rule suppressed the earlier wind-up of an attacker killed
later in the transaction, and a moved fatal recipient could be hit at its old
pre-movement position.

## Owners and complete correction

- `BattleRelocation`, combat rules and the health owner still perform every real
  move, damage and death. No core source, map, art, rule, save schema or receipt
  changes are introduced.
- The drawn actor's existing `logical_contact` is the single presentation
  position. It starts from `Before`, follows the accepted command path once,
  then follows ordered displacement paths. Effects and strike anchors use this
  current contact. Presentation never calculates a path or modifies HP.
- Push/pull use the existing cancelable timeline, original idle art and a
  120 ms slide per emitted path segment. Zero-distance collision creates no
  invented travel. Teleport places the actor directly at `to`, preserving
  facing and avoiding a fabricated route through intervening cells.
- `collisionDamage` displays its emitted actual amount once with the existing
  generic impact; the later death event owns its one fade. A living display
  ghost may attack before its later death, without pretending it survives in
  canonical state.
- Standing ordering and terrain partition preparation share current actor
  contacts. A later movement cannot erase the terrain cuts needed by an earlier
  moved actor; teleport prepares its destination cut too.
- Turn announcements refresh committed HUD information without settling actors
  ahead of later events. Recruit adds only its new actor at the event's `at`,
  reusing the same actor constructor as full rendering. This avoids a second
  art/lifetime implementation and preserves all other actors' current poses.
- Completion and cancellation still settle the whole board from `After`.
  Reduced motion preserves ordered event completion without asynchronous delay.

This follows Godot's guidance to keep scene dependencies explicit and avoid
competing animations writing a property. The existing single timeline remains
in use rather than introducing another animator. References:
[scene organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html)
and [Tween ownership](https://docs.godotengine.org/en/stable/classes/class_tween.html).

## Privacy and boundaries

New displacement and collision feedback share the existing recovery privacy
predicate: a unit must be visible in both supplied boundary sets. Null means
unrestricted; an empty set means none. No renderer invents vision ranges.
Newly recruited units absent from an explicit before-visible set can appear at
their recruit event location, but their later displacement/collision feedback is
conservatively suppressed until final settlement. Existing ordinary strike and
structure visibility policies are not redesigned in this batch.

Ordinary healing and regeneration now use the current actor contact as well.
This intentionally supersedes the settled-position explanation in the earlier
[recovery report](REGENERATION-FEEDBACK-20261103.md), because turn announcements
no longer repaint actors to the end of the transaction.

## Formal evidence and regression scope

`tests/native/recorded-displacement.json` stores only the unchanged legal command
prefixes and expected digests. There are no copied campaign/content snapshots.
C01-06 index 5 also runs through the actual session and mounted screen. The
remaining cases replay original legal commands through the current engine and
feed their genuine receipts to native controlled-clock playback; they are not
new AI route or whole-campaign victory claims.

| Level | Receipt index | Before | After | Ordered event SHA-256 |
| --- | ---: | --- | --- | --- |
| c01-06 | 5 | `5826b001` | `472f2431` | `f8e69f0241b527fbaf479882b3d0d347b169d6e2ff7d0b1469e8bcc19ff67a48` |
| c01-06 | 129 | `abced4bd` | `5efe5a44` | `801a1635f172bc44207820be80d54ecfe45f3e185fff33822452d59c4ad05f2e` |
| c01-01 | 44 | `80abbae3` | `3f7e36e1` | `3f84354a99d937bd25e02b754884a2179550f6c6efec41534aec12cdad34c12b` |
| c01-10 | 48 | `857ff527` | `b4cc627e` | `b47c7190a2b2d99cd2f507a3193536e0ff30581a911e5a25838682022e067f38` |
| c01-14 | 58 | `bad63086` | `c2fd1c27` | `c47b8877737550d353ee0a01237ce5fd6083de06b1bc6021ea4be796c63cb988` |

These cover movement plus push/counter, nonfatal zero-distance collision, fatal
collision, a moved attacker killed by its counter, and counter/collision followed
by support attack. Each checks actual impact positions and amounts, ordered
completion, final state, effect cleanup and ground-plane movement.

Explicit **projection boundary fixtures**, not claimed campaign playthroughs,
cover turn announcement/recruit before displacement, recovery on either side of
movement, two successive actor slides, teleport followed by combat, terrain cuts
chosen to be absent before movement, cancellation mid-slide, reduced motion,
visibility-set combinations and newly recruited visibility. The prior source
assertions that forced movement/collision must have no presentation and that a
later-dead attacker must skip its earlier wind-up are replaced with these
intentional corrected contracts, rather than silently weakened.

`AnimationChecks` is included in the required native aggregate. Its new partial
test file retains the `*Checks.cs` naming boundary so the existing ExportRelease
compile exclusion also excludes it. Final build, native, negative-control and
aggregate outcomes are recorded after verification. Headless node/resource
checks are not GPU visual or exported-game acceptance.
