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

`BoardView.PresentTransitionAsync(viewportFeedback, transition, afterVisibleUnitIds)` returns true
on completion, false on cancellation. `CancelPresentation()` resolves pending
waiters and, while mounted, settles immediately at `After`. Exit cancels without
redrawing disposed nodes; reattachment permits a fresh binding. Cancellation,
replacement, errors and repeated refreshes cannot advance the battle a second time.

`BattleScreen` owns a persistent full-size, noninteractive viewport-feedback
`Control`, a sibling of the camera-transformed `World` inside the clipped map
viewport. Its layer sits above map cursor/range overlays and below the HUD;
read-only hover can still draw a cursor while playback is busy. The screen passes
that host explicitly to playback. Each playback owns one
world-effects root and one viewport-effects root, plus the only receipt clock.
Cancellation clears and settles its own roots before resolving waiters, so a
synchronous replacement cannot be removed or overwritten by old cleanup. The
shared host and unrelated feedback are never cleared by playback.

Turn announcements use anchored Controls in that viewport root, not field-size
coordinates or camera transforms. Their full-width 34-pixel band is vertically
centered in the map viewport, with the existing gradient, 24-pixel text baseline
and 15-pixel font. Entrance travel is a normalized 30% viewport-width slide;
resizing during entrance, hold or exit preserves its position and timing. World
impacts, movement and other effects retain their original world geometry. The
220/420/220 ms sequence remains part of the same playback: `EventPresented` is
still the sole journal publication point, after the announcement completes.

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
frame. The shipped finite attack clips retain the declared attack pose, or `carry-or-work` for mission sheets.
These are roles in a four-frame sheet, not four directional views or attack frames.

## Finite actor clips

`selected-assets.json.actorClips` owns named finite sequences of `{ frame,
durationMs }`, with an optional `appearanceId` for a separate action sheet.
`art-bindings.json` explicitly maps each unit/actor appearance's `actionClips`
to those definitions. `AssetCatalog` compiles each effective binding once into
an immutable `ActorClip`: one resolved sheet, frame indices and cumulative times.
An alternate sheet must share the binding's canonical topic and footprint. Its
physical crop, density, scale, contact and filter remain owned by the existing
asset record, not by another animation geometry table.

`FrameAt` samples finite elapsed milliseconds and holds the last frame after the
duration. Empty sequences, unknown roles/clip IDs, nonpositive/nonfinite durations,
overflow and wrong-topic/missing alternate sheets are rejected. The catalog
detaches the sequence and action mapping from caller-owned mutable input.
Missing actions are explicit errors, never generic-art or fabricated-action
fallbacks. Idle-only geometry fixtures may declare no finite actions.

`BoardView` selects the `attack` clip for the existing strike event. The sum of
its authored frame durations owns that strike's length; the same
`PresentationTimeline` drives both frame sampling and the existing seven-pixel
lunge. Frames change only when their sampled index changes. Catalog frame
application updates the entire sheet geometry while preserving horizontal
facing, then the board synchronizes shadows and standing depth. Normal completion
restores the bound idle appearance before impact; cancellation/error retains the
existing committed-After settlement instead of writing to a retired actor.

The two current profiles, `static.attack` and `static.work`, each retain one
trusted pose for exactly 150 ms. This first slice introduces no new sprite pixels
or accepted multi-frame character artwork. Multi-frame/cross-sheet checks use
trusted sheets as explicitly synthetic infrastructure fixtures. It does not add
looping idle, hit, death, celebration, or a separate magic/caster action contract.
Those remain unfinished; FX identity is not an action classifier. In particular,
`castResolved` must not repeat the following attack's wind-up, and area victims
must not each start another caster animation.

Future ambient idle must be nonblocking BoardView-owned visual state tied to the
current projection, never an endless receipt-clock waiter. It must yield frame
ownership to finite actions and be discarded on render/rebind/exit. No ambient
clock, global animation controller or second completion authority is added here.

Supported native presenters (including the explicit recovery and displacement refinements):

| Event | Consumed fields | Presentation |
| --- | --- | --- |
| attack, counter, supportAttack | attacker, defender, weapon, damage, killed | Finite actor clip (currently 150 ms), then impact |
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

The current export has 39 actor sheets (29 combat, 3 mission and 7 hero
appearances), containing 156 actor frames. All roles, physical crops, density,
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

`AnimationViewportChecks` extends that required scene with actual C03 end-turn
receipts from native viewport E input at 960×640 and 1180×812, opposite camera
pan limits, camera-transform invariance, and resizing during all three banner
phases. A native busy-time hover also checks the live cursor and confirms that
the announcement layer is above the map overlay/range planes and below the HUD. It also checks ordered once-only journal delivery, cancellation in each
phase, reentrant replacement, failure cleanup, rebind, unmount/reattach and
reduced motion. These are native headless geometry and lifecycle guards, not
GUI acceptance or a claim of pixel-identical desktop output. Control anchoring
and input behavior follow the [Godot 4.6 Control reference](https://docs.godotengine.org/en/4.6/classes/class_control.html).
