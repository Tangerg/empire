# Native action presentation

The battle remains an atomic, deterministic C# state transition. Presentation
plays a detached receipt after that transition has committed. It never dispatches
an action, computes damage, queries legality, advances actor turns, reconstructs
movement, or runs TypeScript.

## Ownership and application seam

`BattlePresentationTransition` carries the resulting revision, `Before`, the
complete canonical `Action`, ordered `Events`, committed `After`, the AI pace
flag, and canonical pre-action visible unit IDs. Its constructor clones every
`JsonElement`, copies events into a read-only collection and freezes visible IDs.
It cannot retain a caller's mutable array, hash set or disposed JSON document.

`BattleSnapshot.Transition` is optional. The session emits it only for accepted
commits. The screen consumes each new revision once, skips history on initial
bind/load, and prevents input while a presentation is outstanding. The application
also waits before advancing the AI or opening the result modal.

`BoardView.PresentTransitionAsync(transition, afterVisibleUnitIds)` returns true
on completion, false on cancellation. `CancelPresentation()` resolves pending
waiters and, while mounted, settles immediately at `After`. Exit cancels without
redrawing disposed nodes; reattachment permits a fresh binding. Cancellation,
replacement, errors and repeated refreshes cannot advance the battle a second time.

`PresentationRepaintRequested` marks recruit/turn announcement HUD refresh
points. The screen refreshes committed header/resources there without settling
actors ahead of the remaining events. `AnimationsEnabled = false` implements reduced-motion
settling without asynchronous delays.

## Source contracts retained

Source owners:

- `packages/game-ui/src/ui/game.ts`: march/dispatch/settle and ordered event playback.
- `packages/game-ui/src/ui/event-presentation.ts`: default event presenters and target lookups.
- `packages/game-ui/src/ui/board.ts`: motion geometry, durations and temporary effects.
- `packages/game-ui/src/art/frame-animation.ts`: finite cancellable timeline and strip policy.
- `packages/game-ui/src/art/runtime-raster.ts`: actor frame clips.
- `packages/story-candidate-01/src/presentation/candidate-01-runtime.ts`: numeric FX strips and anchors.
- `packages/story-candidate-01/src/presentation/candidate-01-bindings.ts`: weapon topics.
- `packages/story-candidate-01/src/presentation/index.ts`: explicit heal topic.

The accepted command's supplied path plays once, before event presenters; the
`move` event does not repeat it. Native visual refinement (2026-10-27) deliberately departs from the historical
85/65 ms source movement: each cell now takes 160 ms for the player or 110 ms
for AI. One continuous path tween preserves frame overshoot across cell boundaries,
with linear ground-plane travel and no whole-actor/shadow hop. The declared
`walk-a`/`walk-b` support phases alternate per travelled cell, independent of AI
pace; a one-cell move intentionally needs only one support phase. This changes
presentation only, not canonical path costs, rules, receipts or event ordering. Idle is the declared single idle
frame. Attack is the declared attack frame, or `carry-or-work` for mission sheets.
These are roles in a four-frame sheet, not four directional views or attack frames.

Supported native presenters (including the explicit recovery and displacement refinements):

| Event | Consumed fields | Presentation |
| --- | --- | --- |
| attack, counter, supportAttack | attacker, defender, weapon, damage, killed | 150 ms wind-up, then impact |
| partingShot | same plus at | Wind-up, impact at explicit departure cell |
| areaAttack | defender, weapon, damage, killed | Impact without another wind-up |
| attackStructure | attacker, structure, weapon, damage, destroyed | Wind-up and structure impact |
| areaAttackStructure | structure, weapon, damage, destroyed | Structure impact without wind-up |
| forcedMove | unit, mode, from, to, path | Ordered ground slide; teleport changes contact without interpolating a route |
| collisionDamage | unit, amount, killed | One generic impact for the emitted actual collision damage |
| regen | unit, amount | Existing healing effect at the current visible contact |
| death | unit | 220 ms fade and 35% shrink about cell center, then remove ghost |
| heal | target, amount | Heal strip and number rising 16 pixels over 500 ms |
| recruit | unit, at | Add only the new actor, then 240 ms fade/scale from 0.6 to 1 |
| capture | captured | Refresh site ownership when completed |
| turnStart | player, turn | Refresh HUD, 220 ms entrance, 420 ms hold, 220 ms exit |
| gameOver | none | 220 ms beat before the application result modal |

Wind-up follows the normalized projected direction with a 7-pixel sine lunge. Hit effects last 420 ms; their number rises 14 pixels and
fades quadratically. Fatal hits add a 40 ms beat before the next presenter.
Effects use cell center; actors retain the canonical cell-foot contact. Moving
actors participate in the existing standing-depth ordering.

## Ordered contact ownership

The native refinement in [Displacement feedback](DISPLACEMENT-FEEDBACK-20261103.md)
corrects historical final-snapshot lookups. Each existing actor's `logical_contact`
is its one presentation-position owner. It starts in `Before`, follows the
committed command path once, then follows ordered `forcedMove` paths. Unit
impacts and strike anchors read that current contact, including a later-dead
attacker's still-present ghost. A future displacement or death cannot retroactively
move or suppress an earlier attack.

Push/pull slides use the existing cancelable timeline at 120 ms per emitted path
segment, with the idle pose rather than a walk cycle. Teleport moves directly to
its emitted destination and preserves facing. No pathfinding, health mutation or
additional game event is performed. Collision impacts use the actual emitted
amount and the existing generic burst; the following death owns its one fade.
Current actor contacts drive both standing order and terrain depth partitions,
including contacts retained from earlier movement and teleport destinations.

Turn announcements refresh committed HUD facts but leave actors in timeline
order. Recruitment adds only its new actor at the emitted `at`, through the same
constructor used by full projection. It does not settle other actors early.
Final completion or cancellation remains the sole full `After` settlement.
Structure impacts retain the existing committed-structure lookup: normal
zero-HP destruction retains the object, while an actually absent structure gets
no fabricated impact.

## Original assets, not generated replacements

The current export has 34 actor sheets (24 combat, 3 mission and 7 hero
appearances), containing 136 actor frames. All roles, physical crops, density,
authored anchors and palettes remain unchanged.

The nine needed original FX topics are exported:

- `C01-FX-01`: slash; `02`: pierce; `03`: arrow hit; `04`: blunt impact.
- `C01-FX-05`: ordinary flame; `06`: oath fire; `07`: siege debris; `08`: undead-memory impact.
- `C01-FX-17`: healing.

Their asset IDs are `C01-FFV1-FX-01` through `08`, plus `C01-FFV1-FX-17`.
They use `res://assets/runtime-hd/fx/fx-NN.png`: a 256×64 physical strip,
four 32×32 logical frames at density 2, anchor `[16,16]`, 12 fps, nonlooping,
normal blend. The original source has all 24 FX sheets; the other 15 are not
required by these presenters and remain unexported. Only source topics 09–12 loop.

FX deliberately have no semantic `frameOrder`. The reader permits numeric strip
indices for category `fx` only, while keeping named actor/structure validation.
It retains fps/loop/blend metadata, rejects missing/invalid FX pivots and unsupported
blends, and does not convert an invalid binding into another picture. Unbound
weapons receive the source generic burst/number without themed art. Heal art is
injected through `CANDIDATE_01_HEAL_FX`, exported from the source presentation AST.

## Explicit boundaries

- Source default presenters do not animate castBegan/resolved/cancelled,
  statusTick, unitWithdrawn, unitEmbarked,
  unitDisembarked, unitRouted, unitSurrendered, unitSpawned, unitRevived,
  structureDamaged/repaired/destroyed or arbitrary scenario signals. They settle
  without invented effects. A resolved cast's subsequent ordinary combat events
  already produce its real strike; animating castResolved again would duplicate it.
- Text feedback is independent of those animation boundaries. Committed
  `unitRouted` describes a before-visible departure; `unitSpawned` describes a
  finally visible arrival using its event-time owner and creation identity. No
  spawn coordinates are disclosed. The existing ordered journal and toast own
  delivery and consecutive-message folding; neither event introduces new FX.
- Before-visible unit IDs gate ghosts and unit impact locations. Post-action
  visibility is supplied by the application for the settled/spawned projection.
  The renderer never computes fog. Structure attacks by hidden attackers are not
  animated, rather than revealing their locations through effects. New recovery,
  displacement and collision feedback conservatively require visibility at both
  boundaries. New recruits absent from explicit before-visibility therefore do
  not disclose later displacement/collision feedback before final settlement.
- Unlike the source's pre-dispatch march, a refused native command has no visual
  march: only a committed receipt is presented. The renderer cannot display an
  uncommitted rule transition; ordered displacement and collision presentation
  deliberately improve the earlier source behavior.
- This does not port camera follow/panning, sound, new status/cast/departure FX,
  arbitrary presenter plugins, or missing authored scenery. Godot's current UI
  font is retained; source CSS font weight/letter spacing is not claimed pixel-identical.
- Headless checks establish source-backed timing, resource and lifecycle contracts.
  They are not desktop observation, a pixel-perfect visual comparison, or a live
  TypeScript differential test of every frame.

## Verification

`AnimationChecks` is now a required scene in `bash tools/godot/verify-native.sh`.
Its original resource/timing/lifecycle checks are retained, with the two formerly
unhandled displacement events replaced by real campaign receipt regressions.
The dedicated `bash tools/godot/animation-checks/run.sh` remains useful for a
focused run. The new report distinguishes actual command witnesses, explicit
projection-boundary fixtures and final verification outcomes. Headless checks
are not GPU visual or export acceptance.
