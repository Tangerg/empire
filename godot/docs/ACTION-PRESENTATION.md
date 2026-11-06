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
with linear ground-plane travel and no whole-actor/shadow hop. The explicit `legacy.move` clip retains the declared
`walk-a`/`walk-b` support phases alternating per travelled cell, independent of AI
pace; a one-cell move intentionally needs only one support phase. This changes
presentation only, not canonical path costs, rules, receipts or event ordering. Shipped idle retains the declared single idle
frame. The shipped finite attack clips retain the declared attack pose, or `carry-or-work` for mission sheets.
These are roles in a four-frame sheet, not four directional views or attack frames.

## Finite actor clips

`selected-assets.json.actorClips` owns named finite sequences of `{ frame,
span }`, an explicit `unit` (`milliseconds` or `cells`), and an optional
`appearanceId` for a separate action sheet.
`art-bindings.json` explicitly maps each unit/actor appearance's `actionClips`
to those definitions. `AssetCatalog` compiles each effective binding once into
an immutable `ActorClip`: one resolved sheet, sampling unit, frame indices and
cumulative interval ends. `ResolveUnitClip` requires the consumer's expected
unit and rejects a mismatch instead of converting or guessing.
An alternate sheet must share the binding's canonical topic and footprint. Its
physical crop, density, scale, contact and filter remain owned by the existing
asset record, not by another animation geometry table.

`FrameAt` samples a finite nonnegative offset in the declared unit and holds the
last frame after `Length`. Empty sequences, unknown units/roles/clip IDs, nonpositive/nonfinite spans,
overflow and wrong-topic/missing alternate sheets are rejected. The catalog
detaches the sequence and action mapping from caller-owned mutable input.
Missing actions are explicit errors, never generic-art or fabricated-action
fallbacks. Catalog geometry can be inspected without an action selection, but rendered actors must explicitly declare an `idle` clip.

`BoardView` resolves the weapon's explicitly selected actor action for each
existing primary strike. The sum of authored frame spans owns that actor's action
length; the same `PresentationTimeline` drives frame sampling, motion and the
released impact. Frames change only when their sampled index changes. Catalog frame
application updates the entire sheet geometry while preserving horizontal
facing, then the board synchronizes shadows and standing depth. At its own clip end the actor
restores the bound idle appearance, even if the impact is still playing; cancellation/error retains the
existing committed-After settlement instead of writing to a retired actor.

The two finite attack profiles, `static.attack` and `static.work`, each retain one
trusted pose for exactly 150 ms, with an explicit release at 75 ms. This first slice introduces no new sprite pixels
or accepted multi-frame character artwork. Multi-frame/cross-sheet checks use
trusted sheets as explicitly synthetic infrastructure fixtures. It does not add
new animated artwork, hit reactions, death, celebration, or a complete area/cast activation contract.
Those remain unfinished; FX identity is not an action classifier. In particular,
`castResolved` must not repeat the following attack's wind-up, and area victims
must not each start another caster animation.

## Explicit weapon release and recovery

`art-bindings.json.CANDIDATE_01_WEAPON_ACTION` explicitly selects an actor action
ID and motion (`stationary` or `lunge`) per weapon. All 38 weapons used by the
39 bound unit types are declared. The policy is immutable after admission;
missing policies/actions and unknown motion values fail explicitly. FX remains
independent: a weapon without a themed FX binding intentionally uses the existing
generic impact. FX identity, damage type, range and actor class never classify an
action or movement policy at runtime.

The selected `ActorClip` alone owns `releaseMs`; a releasing consumer requires
it. The marker is optional for non-releasing clips, but when present it requires
milliseconds and a finite offset in `[0, Length]`. It is not duplicated in the
weapon record. Frame spans, release offset and action length all come from that
single admitted clip. Idle and movement clips retain no release marker.

One composite receipt tween samples elapsed time for the actor and impact.
The impact begins once at the release, sampling its actual age `elapsed -
releaseMs`, including a tick that jumps across release or the whole action.
Recovery and impact run together; the presenter finishes at the maximum of actor
length and release plus impact duration. The existing 40 ms fatal beat follows
both. Shared `ImpactVisual` creation/sampling/cleanup is also used by standalone
area impacts, regeneration and source-hidden healing; it owns no clock. An impossible numeric
impact interval fails explicitly rather than silently becoming instantaneous.

Stationary actors may face the target but keep their position and logical
contact. Lunge motion approaches seven units at release, then returns to the
anchor by clip end using sine approach/cosine recovery. The existing trusted
150 ms clips release at 75 ms: their original body trajectory is preserved, but
impact now intentionally starts at the forward peak instead of waiting for the
body to return. Frame/shadow/contact application and terrain cuts retain their
existing owners. The actor rejoins idle at its own end, and an impact is removed
at its own end even if the other part continues. Cancellation and exceptions
settle `After`; no cleanup writes back to a retired actor. Reduced motion settles
the same composition synchronously once.

This complete primary-strike slice covers `attack`, `counter`, `supportAttack`,
`partingShot` and `attackStructure`. It preserves the parting-shot location,
current contacts and later-dead ghosts. An absent structure still permits the
action but creates no invented impact. Area victim events remain impact-only,
and `castResolved` remains a marker, so neither repeats a primary action.

This does **not** yet represent every area/cast activation. An empty aimed cell
can emit only area hits or no hits; structure-primary attacks can emit unit area
hits before their primary structure event. A later complete slice needs an
explicit combat-owned resolution/group start fact, not a guessed grouping by
attacker/weapon/first victim. Healing has its own explicit source ability binding
below; regeneration has no caster. New casting art, charge gestures, hit reactions,
death and celebration remain separate work.
No unaccepted candidate image is shipped by this contract change.

## Healing source and recipient

`DefaultAbilities.Heal` remains the sole healing rule owner. Its committed `heal`
event supplies `source`, `target` and the actual `amount`; the presenter neither
queries healing rules nor invents a weapon or another event. The explicit
`CANDIDATE_01_ABILITY_ACTION.heal` policy selects the existing `attack` pose and
`stationary` motion. This deliberately reuses trusted art rather than falling
back to a pose or claiming a newly authored healing animation. The four shipped
healer types (`cleric`, `c01.mirelle`, `c01.gravekeeper`, `c01.druid`) resolve that
action through their normal type/actor art bindings.

Ability and weapon declarations share immutable `ActorAction` admission, but
retain separate lookup domains. `ActorClip` still exclusively owns release.
`AnimateActorAction` is the same composition for damage and healing: source
recovery and the recipient's existing 500 ms healing effect sample one receipt
clock. The shipped static pose releases at 75 ms, restores idle at 150 ms, and
finishes recipient feedback at 575 ms. The recipient keeps its emitted amount,
current drawn contact, original effect strip, number rise and fade. No extra
`attack` or `heal` receipt is produced.

The existing recipient gate is preserved: it must remain in `After`, have been
visible before the receipt and have a drawn contact. Only an independently
visible, drawn source may start an actor action. A hidden or absent source does
not trigger action/art lookup or expose its position; its visible recipient
retains immediate target-only healing feedback. Hidden/missing recipients do
not expose an otherwise visible source action. `regen` remains source-free,
target-only feedback. Canonical heal targeting excludes self; synthetic
source-equals-target presenter checks protect lifecycle robustness, not a new
self-heal rule.

The unchanged formal C01-12 healing witness replays its 94-order prefix before
unit 4 heals unit 6 for 27 HP. Initial/before/after state and ordered event hashes
are preserved, with one publication per event. Controllable-clock checks cover
release, overshoot, source/recipient identity, recovery/impact retirement,
visibility and cancellation. They do not replace dynamic visual art acceptance.

## Distance-sampled movement clips

Every moving unit/actor explicitly selects `move` in its existing action map.
The current `legacy.move` uses cells with `walk-a` and `walk-b` spans of one each,
retaining the previous pictures and cadence. These are old support poses, not
newly authored or visually accepted walking animation.

The existing single path tween owns progress. `cells` means continuous progress
through the committed path's cell-to-cell segments: one full segment is one
cell, regardless of terrain movement cost or camera projection. The renderer
samples `FrameAt(travelledCells % Length)` from that same progress. Arbitrary
positive sub-cell spans and longer sequences need no extra clock; turns and
large frame steps preserve the phase. Player/AI pacing still uses 160/110 ms per
path segment, so equal travelled distance yields the same pose at either speed.
The committed path and game rules remain unchanged.

Before playback, path preparation samples all clip contacts through the existing
`ActorClipDepths`/`StandingVisual` geometry owner and prepares the full depth sweep
for every path contact and pose, including canonical base geometry and all other
idle actors. A changed frame is applied before placing/sorting the actor, with
initial depth updated before awaiting a tick. Facing changes synchronize shadows
even when the sampled frame does not change. Normal completion returns to idle;
error, cancellation, rebind, exit, visibility and reduced motion retain the
existing receipt contract. Forced slides/teleports do not request movement clips
or turn displacement into walking. No presentation phase is persisted.

The manifest interval field was renamed from `durationMs` to `span` and every
existing timed definition now explicitly declares `milliseconds`. This is an
atomic content/API change, not a save migration or compatibility fallback.

## Persistent idle playback

Every rendered unit/actor binding explicitly selects `idle`. The shipped
`static.idle` holds the existing trusted idle pose for a 1000 ms cycle; it is not
new animated artwork. Multi-frame idle is exercised only by clearly synthetic
fixtures over already trusted images.

`BoardView.AdvancePresentation` is the only advancement entry point: first it
advances eligible idle phases modulo their clip durations, then the finite
receipt clock. Idle membership, phase and cached geometry belong to the current
render projection, not the battle or save. Idle has no task, waiter, event
publication or input gate. Only changed poses apply frames, synchronize shadows,
re-sort and refresh active occlusion cues; an unchanged pose replaces nothing.

Finite movement, teleport, strike, spawn and death remove their actor's idle
registration and restore the canonical base pose before taking exclusive visual
ownership. Other actors keep idling. A successfully completed surviving action
returns to its declared first idle pose without consuming that tick's overshoot.
Receipt completion/cancellation still settles the committed snapshot exactly
once. Depth preparation always includes all remaining idle-frame contacts,
because terrain partition preparation replaces rather than appends its cuts.
The depths are sampled through the existing frame/contact owner at registration,
never rebuilt on every idle tick.

Successful `RenderState` replaces registrations atomically and starts phases at
zero. Rebind and exit discard them. Bare reattachment cannot restart retired or
partly played actors: a fresh render/bind is required. Hidden boards and hidden
ancestors freeze idle with no catch-up; they do not stop finite receipt completion.
Turning reduced motion on immediately restores eligible actors to their first
idle pose and resets their phase. Existing finite receipts retain their original
motion setting and ownership; the setting applies to the next receipt as before.

Occlusion cues use the current silhouette and geometry. A changed idle tick
refreshes them once outside finite playback, preserving the existing fog filter
and current pointer/depth owner. Colors and non-cue diagnostics remain detached
projection data. No second animation node clock, global controller or completion
authority is added.

Supported native presenters (including the explicit recovery and displacement refinements):

| Event | Consumed fields | Presentation |
| --- | --- | --- |
| attack, counter, supportAttack | attacker, defender, weapon, damage, killed | Weapon-selected actor action; impact at authored release, recovery overlaps |
| partingShot | same plus at | Weapon-selected action; release impact at explicit departure cell |
| areaAttack | defender, weapon, damage, killed | Impact without another wind-up |
| attackStructure | attacker, structure, weapon, damage, destroyed | Weapon-selected action and released structure impact |
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

A declared lunge follows the normalized projected direction with a 7-pixel release-aligned approach/recovery; stationary policies do not translate the actor. Hit effects last 420 ms; their number rises 14 pixels and
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
