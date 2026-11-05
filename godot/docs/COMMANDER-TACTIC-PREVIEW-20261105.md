# Commander tactic disclosure

## Formal player evidence

At baseline `4470c8bcb`, a fresh native campaign followed New Campaign, the
original story, `rush-north-hill`, Start Battle, End Turn and normal AI completion.
On the second human turn, both Gray Rally and Hold the Line exposed only their
2-command-point cost. Arming Gray Rally and choosing the legal center (4,14),
three cells from the unchanged leader at (4,17), still showed only generic target
and cancel guidance. Neither the HUD nor the mounted dossier explained effects,
duration, radius or eligible allies. No tactic was committed during this audit.

The existing C01 recorded-rally regression was extended before production edits.
With baseline production assemblies it failed on the missing tactic preview,
strict native exit 1. This establishes a disclosure defect, not a change to tactic
legality or balance. A separate native attack-details audit passed mouse, quick
touch and split-frame touch entry without edits; that working flow was preserved.

## Ownership and implementation

- `SupportOrders.Tactics` exposes immutable authored range, radius and ordered
  effect payloads alongside its existing legal centers and resource costs.
- `PlanTactic` returns the admitted center and detached eligible allied IDs.
  Query and execution share one recipient selector using the resolved grid and
  canonical alliance relationship. Execution still selects recipients after its
  existing resource and tactic-use announcements.
- `GrayFlagSession.Hud` projects these facts through its current visibility
  boundary. The tactic choice explains its rules; the armed forecast shows effect,
  radius and cost; the existing scrollable dossier includes ordered effect details,
  center and named eligible allies. It explicitly says that eligibility does not
  guarantee a status change and an ineffective tactic still costs resources.
- `PlannedEffectText` composes shared status-payload interpretation with separate
  weapon-hit and tactic wording. Tactics do not inherit weapon-hit prerequisites.
- The existing selection owner retains a tactic preview when moving into the HUD.
  No widget owns a second target. Live target admission still gates confirmation;
  leave, cancel, busy and rebinding retain their normal boundaries.

There is no new map overlay or artwork, no save field or migration, and no change
to costs, legality, effect order, event timing or battle rules. Godot controls remain
views of injected session projections, and input continues through the existing
native GUI/unhandled-input flow. See the official
[scene organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html)
and [input event](https://docs.godotengine.org/en/stable/tutorials/inputs/inputevent.html)
guidance, and the repository's native capability ownership notes.

## Focused verification

- Both native application and core-test builds: zero warnings and errors.
- New core planning group: strict pass. Covers three grid strategies, a diagonal
  boundary that differs between square4 and square8, ally/enemy/neutral ownership,
  range limits, self normalization, unaffordable/used tactics, immutable data,
  no-op removal, recipient parity and post-announcement recipient selection.
- Final TacticalHudChecks: 1,079 assertions, strict native exit 0. It includes the
  real recorded C01 legal-order prefix, native tactic button/keyboard target input,
  map-to-HUD handoff, native details click, dossier close/focus return and Enter
  commit. Original action, post-state digest and ordered event hash remain exact.
- Added projection guards cover invalid live targets while a valid preview is
  retained, absent centers, busy/cancel/leave/rebind, enemy exclusion, and unchanged
  state/revision/events/action history.
- Native control rectangles at 960×640 and 1180×640 keep summary, details entry and
  confirmation guidance inside the lower forecast panel. This is not pixel-based
  visual acceptance; this environment has no graphical display.

- Independent fresh-campaign native acceptance: 35 checks, strict exit 0 and
  unchanged assembly hashes. Both tactics correctly disclose their differences at
  legal center (4,17), list Laia, open/close details and cancel without changing
  actor/resource facts. This probe does not commit a tactic.
- The objective/outcome formatter harness passes 146 assertions, including separate
  tactic wording, exact weapon-hit compatibility, missing-key admission and
  weapon-only effect rejection.

The first aggregate attempt (09:32:37–09:42:11 UTC) failed with exit 1 after all
16 core groups and 16 level replays passed: its formatter test project still
referenced the renamed source file. That project reference and all test type
references are now migrated; the focused harness passes. The failed attempt is
retained, not counted as a full pass.

## Complete verification

The corrected complete gate ran uninterrupted on `ed753c813` from 09:44:14 to
10:24:14 UTC (2,400 seconds), strict exit 0:

- Format-source selection, whitespace, source metadata and formal scene validators
  passed; both Debug builds had zero warnings and errors.
- All 16 core groups passed. The 16 independent formal level simulations executed
  3,425 legal orders with exact save/replay round trips. Every ordered
  action/event/receipt trace and final digest equals the preceding baseline. These
  are independent level runs, not a carried-roster campaign victory claim.
- Both contract harnesses, editor import and all 30 native scenes passed. All 31
  native logs were retained and checked for process/shutdown diagnostics. The 40
  existing warning headers exactly match the preceding complete gate.
- HUD passed 1,079 assertions within this aggregate. Campaign navigation,
  result flow, event feedback and lighting/resource lifetime also passed.
- ExportRelease configuration compiled with zero warnings and errors. This is a
  configuration build, not a packaged-platform or graphical acceptance claim.

The final documentation-only commit records these outcomes without changing the
verified source or assets. No user save, gameplay rule, map or artwork is changed.
The previously recorded isolated HUD and morale shutdown failures remain
unexplained; this change neither modifies resource teardown nor claims to fix them.
