# C13 archive collection locations

Base: `58114d10`. Real 960×640 chapter-entry play showed a city of similar
buildings. The escort exit offered a task locator, but the prerequisite three
archive locations had only a plain-text score row: clicking it did not locate any
collection site. The dialogue said to read three archives without explaining the
canonical arrival-and-complete-action trigger.

## Ownership and production changes

C13's score-archives objective declares the existing targetZones capability for
archive-a/b/c. Coordinates remain exclusively in canonical scenario zones. The
localized task says to arrive and complete an action to read an archive. No new
engine rule, HUD type dispatch, copied coordinates or progress counter is added.
All three authored archive locations remain shown until score completion; this
is not an unread-only checklist. The existing escort exit remains independent.

The new author declaration intentionally changes serialized C13 objective data
and therefore its full-field state digest. The full-level paired replay compares
all events and every other state field with the no-declaration baseline; production
digests do not omit the new field.

## Regression scope

The core guard checks the formal C13 declaration and exact three canonical cells,
plus state/save invariance. Parameterized C11/C13 legal visit tests start a unit
near one site in an explicitly detached fixture, then execute move-and-wait,
leave, and revisit over later turns. Count and trigger ledger remain one; events
and state match the no-locator baseline after removing only targetZones.

Removing the formal C13 targetZones declaration is the negative control. Native
CameraIntegration includes both archive and escort locators, keyboard input,
read-only navigation, repeat activation, focus, busy/rebind and hidden ancestors.
The original centroid-only camera then exposed a second real issue: the wide,
skewed three-site arrangement left one marked archive outside the safe viewport.
Objective focus now frames the authored triangle vertices actually drawn by the
overlay, not averaged cell centers or a duplicate grid projection. BattlefieldCamera
owns fitting: only reduce zoom when necessary, cancel pending wheel deltas, retain
legal zoom/scroll bounds, and center the bounds. Its return value reports limits
that cannot fit; no unconditional guarantee is made for arbitrary oversized sets.
A 12-pixel margin keeps outline strokes away from the HUD boundary. No unit
selection, command, fog fact or game state changes.

Strengthening the native check from centroid to every marker vertex failed
specifically at C13/score-archives under the old focus implementation (exit 1).
Pure camera coverage includes skewed sets, repeat framing, single targets, larger
viewports without zoom-in, pending wheel cancellation and minimum/scroll limits.
Final native/GUI results are recorded after completion. No screenshots, fixture
saves, full campaign victory or full native aggregate are claimed.

## Verified results

- Core: 17 groups, 0 failures, including all 16 shipped independent native AI
  save/replay levels. Fifteen trace hashes unchanged; C13 metadata-only difference
  is covered by paired replay. This is not a carried-state campaign victory.
- Native camera model: 52 assertions; screen integration: 414 assertions,
  including all target vertices at 960×640 for C02/C07/C11/C12/C13.
- Objective/outcome contract: 297 assertions; catalog boundary: 76 assertions.
- Strict native process/diagnostic gates, editor import, whitespace check,
  source metadata and diff check passed. Build: no warnings or errors.
- Both negative controls failed at the expected assertion: missing C13 targetZones,
  and old centroid-only focus failing C13's complete marker boundary.
- GUI at 960×640: all three archive outlines fully visible after locate, concise
  task text remains on one line; repeated locate is stable; escort's four exit
  cells are visible and switching back restores archive framing. Ready count 13,
  score 0/3 and escort 0/1 are unchanged. Window closed afterward.

GUI used the explicitly marked detached chapter-entry fixture, not continuous
campaign history. Collection/revisit is verified by real legal core actions in
a bounded placement fixture, not a manual full chapter playthrough. No full
native aggregate or remote CI was run. No screenshot or private save is included.
