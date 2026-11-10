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
frame. Most shipped finite attack clips retain the declared attack pose, or `carry-or-work` for mission sheets. Torren now has a separate six-pose thrust, documented in `../../docs/art-torren-thrust-20261110/README.md`.
These are roles in a four-frame sheet, not four directional views or attack frames.

## Finite actor clips

`selected-assets.json.actorClips` owns named finite sequences of `{ frame,
span }`, an explicit `unit` (`milliseconds` or `cells`), and an optional
`appearanceId` for a separate action sheet. Death actions additionally declare
their explicit `death` presentation policy, described below.
`art-bindings.json` explicitly maps each unit/actor appearance's `actionClips`
to those definitions. `AssetCatalog` compiles each effective binding once into
an immutable `ActorClip`: one resolved sheet, sampling unit, frame indices and
cumulative interval ends, plus any declared release or death policy. `ResolveUnitClip` requires the consumer's expected
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

The two legacy finite attack profiles, `static.attack` and `static.work`, each retain one
trusted pose for exactly 150 ms, with an explicit release at 75 ms. The initial infrastructure slice introduced no new sprite pixels
or accepted multi-frame character artwork. The later `torren.thrust` action selects a separate six-pose sheet for `campaign-torren` only: 360 ms total, releasing at the full-extension frame boundary at 160 ms. Its base appearance and movement remain unchanged; the separately reviewed breathing idle and recoil are described below. The admitted clip
`showAccent` option defaults to true; this authored thrust sets it false because
the old crescent obscured its arm and blade in real gameplay. This controls only
accent creation, never impact, timing, motion or state transitions. Multi-frame/cross-sheet checks use
trusted sheets as explicitly synthetic infrastructure fixtures. Recipient reactions
and celebration are specified below; their new artwork remains unfinished. Main
attack activation follows. FX identity is not an action classifier. In particular,
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

For reactive strikes and healing, one receipt timeline samples elapsed time for the actor and impact.
The impact begins once at the release boundary, including a tick that jumps
across release or the whole action. Source recovery and recipient presentation
are awaited together on the same receipt timeline.
Recovery and impact run together; the presenter finishes after source recovery
and the complete released recipient presentation (including its hit clip). The existing 40 ms fatal beat follows
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

## Main attack activation and independent impacts

`BattleCombat.ExecutePlan` owns one `attackActivated` / `attackCompleted` pair
per successfully consumed main attack. The activation records the attacker,
weapon, executed origin and aimed cell directly from `CombatPlan`; it never
selects a first victim. The completion follows primary unit/structure effects
and their death events, before attacker rewards, counter and support strikes.
Delayed declarations/cancellations emit no activation. A due, valid delayed
attack still activates when its aimed cell has become empty. `castResolved`
remains a separate lifecycle marker.

`BoardView` starts the weapon-selected action at activation and yields at that
clip's release. Main `attack`, `areaAttack`, `attackStructure` and
`areaAttackStructure` events independently present only their existing recipient
impact. Recovery continues on the same receipt timeline; completion waits for
it before a later action can take over. The main victim's fatal beat follows
its impact, not the source's recovery. A source death or forced movement inside
the scope first waits for recovery so no later sample writes to a retired or
relocated actor. Counter, support and parting shots remain independent composite
actions. No victim count, weapon identity comparison or delayed-cast marker
creates an implicit scope.

`PresentationTimeline` advances pending completion boundaries chronologically.
A coarse frame that crosses release starts the impact at the exact boundary and
samples the remaining elapsed time, rather than adding frame-dependent latency.
The receipt owns each active actor's finite depth requirements as geometry
projections, not a second timer. The existing board partition preparation unions
those requirements with idle/standing contacts. One actor returning to idle must
not discard another actor's remaining displacement path. Ending the path, death,
settlement, cancellation and rebinding retire the corresponding projections.
Logical contacts, shadows and standing-depth geometry retain their existing owners.
Hidden sources create no action or source timing; visible recipients retain their
own impact. A visible source can act with no visible recipient. Reduced motion,
cancellation, rebinding and tree exit use the existing settle/cleanup path.
Malformed explicit scopes fail rather than guessing; a standalone victim event
is still a valid target-only projection and does not invent a source action.

These two domain facts intentionally increment `scenario.eventCounts`, which is
part of the full-state digest. Action replay schema **4** identifies the changed
receipt/state contract and explicitly rejects schema 3 replay documents. Battle
snapshot schema **3** and application schema **4** are unchanged: existing admitted
saves retain their stored counts and can continue under the new rules. There is
no field stripping, digest exclusion or replay migration in production.

The formal C01-09 witness uses its unchanged 58-order prefix and structure-primary
satchel charge, including the unit collateral that precedes the structure hit.
Synthetic lawful fixtures additionally exercise delayed empty targets, counters,
support and cancellation. This contract does not supply newly animated artwork,
charge gestures, hit-reaction, death or celebration artwork. No unaccepted candidate
image is shipped.

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

## Recipient hit and terminal death clips

`BattleDamage.Resolve` remains the only owner of applied damage and unit death.
The presenter consumes its committed reports: positive `damage` in attack, area,
counter, support and parting events, and positive `amount` in collision/status
reports start the recipient's explicit `hit` action. Healing, zero damage and
structure impacts do not select a hit pose. Neither a `killed` flag, an HP value,
a corpse marker nor absence from the final snapshot starts death. Only the
ordered `death` event starts the explicit `death` clip. Transport passengers have
no drawn pre-transition actor and receive no invented death presentation.

All maps except Torren's hit/death explicitly retain `static.hit` (150 ms) and
`static.death` (220 ms). Both use the existing trusted idle image.
The static hit is an authored hold within the existing 420 ms impact; static death retains
the previous normalized fade and 35% shrink through its explicit
`death: { "mode": "during-clip" }` policy. These are declared placeholders,
not newly completed reaction artwork. The frame spans own the entire duration of
this mode, so it forbids a separate `fadeMs` value.

Torren alone selects `torren.recoil`: four independently painted hit poses with
40/110/90/80 ms intervals (320 ms total). The reaction returns to his declared
breathing idle at its endpoint, before the unchanged 420 ms impact completes.
The existing receipt clock, grounded frame anchors and facing own playback; no
new actor timer, root motion or combat rule is introduced. See the
[Torren recoil review](../../docs/art-torren-recoil-20261110/README.md) for source,
extraction and real-render acceptance.

Torren alone now selects `torren.fall`: six independently painted collapse poses
over 620 ms, then `death: { "mode": "after-clip", "fadeMs": 160 }`. The still
prone pose holds for its last 180 ms interval before fading. See the
[Torren fall review](../../docs/art-torren-fall-20261110/README.md). In this mode
all authored frames play before opacity changes. The last frame then holds during
the explicit terminal fade, preserving the starting tint, root position and scale;
there is no shrink or idle restoration. Frame spans remain the sole owner of
collapse duration, and `fadeMs` owns only the subsequent fade. Both stages use the
existing receipt clock, including accumulated overshoot across their boundary.

Every bound death action must declare one of these policies; other actions must
not carry it. Death clips require milliseconds and forbid attack release metadata.
Unknown modes, missing/nonpositive/nonfinite terminal fades and a total duration
that overflows or cannot advance are rejected during catalog admission. The
catalog detaches the policy along with the frame sequence from mutable input.
Neither asset identity nor frame count selects a policy.

Both modes share the existing single removal path, which retires standing geometry
and the node once; a duplicate death cannot find or remove it again. Cancellation,
rebind and exit during either stage settle the committed state without publishing
an unfinished event. Hidden deaths are filtered before art lookup, reduced motion
settles synchronously, and results remain behind completion of the entire receipt,
including the terminal fade. No separate death registry or completion flag is
introduced. The general policy remains separate from Torren's accepted artwork. Synthetic frame checks
prove sampling and lifecycle contracts, not real-time visual acceptance.

`PrepareActorPose` shares immutable clip resolution, finite depth preparation,
frame/contact geometry, shadows and facing preservation with outgoing actions.
It does not require attack release metadata, infer a motion policy or create a
clock. Positive hits suspend only the recipient's idle membership. The hit and
impact sample the receipt timeline together, finish independently, and hold the
event until the longer one finishes. A surviving reaction restores idle only at
its own endpoint. A reactive source's release starts its recipient reaction;
source recovery, reaction and impact must all finish before the event publishes.
A fault or cancellation in either branch terminates promptly rather than waiting
for an unrelated long recovery.
If the same drawing is still the outgoing source, recovery ends before it can
acquire a hit pose. This is pose serialization, not a second gameplay decision.

Area reports remain in their domain order. The renderer does not invent a
simultaneous group or infer a source from a recipient. Existing pre-action
visibility gates strikes and death; collision and the new status damage use the
conservative before-and-after gate for continuing-unit effects. A prior-visible
terminal death remains independent of whether its damage effect was visible. Hidden recipients are filtered
before action-art lookup. Status damage uses the committed amount and existing
generic impact, without guessing a status-themed FX or a fatal pause. Cancellation,
rebind, exit and errors use the existing committed-After settlement; cancelled
waiters cannot return old actors to idle. Reduced motion settles synchronously.
The screen/application still await the complete receipt before AI or results;
no separate result-completion flag or death registry is introduced.

## Winning-team celebration clips

Only the ordered `gameOver` event starts the explicit `celebrate` action.
`BattleConclusion` remains the owner of winning-team semantics; presentation parses
its committed event value rather than evaluating objectives or treating the local
player as the winner. Neutral conclusions select no celebration art. The eligible
cohort is drawn actors still in committed `After.units` with positive HP, whose
After owner belongs to the winning team, and who were and remain visible. A
surviving ally is still on the winning team even if its owner previously failed a
critical objective; no second player-victory policy is introduced. Transported,
withdrawn, routed, dead and newly visible actors are not materialized from stale
Before drawings to celebrate. Filtering happens before clip lookup.

Torren alone selects `torren.salute`: six painted victory-salute poses over
900 ms (100/120/160/240/140/140), then returns to his breathing idle. The other
39 unit/actor maps retain `static.celebrate`: one trusted idle picture for 220 ms,
not completed celebration artwork. See the [Torren salute review](../../docs/art-torren-salute-20261110/README.md)
for source and native acceptance. Multi-frame clips use the same `PrepareActorPose` and geometry owner as
other finite actions. All eligible actors start together without moving their
logical contacts or changing facing. One group tween on the existing receipt
clock lasts the greater of 220 ms and the longest declared clip; each actor
returns to idle at its own endpoint. Durations are never summed per actor or
appended to a second result timer.

The screen and application retain their existing full-receipt result gate.
`gameOver` publishes once only after the group completes; repeated refresh and
loading a terminal save do not start historical actions. Cancellation, rebind,
exit and errors settle through the same After cleanup, and reduced motion
completes synchronously. No gameplay, event, save, reward or result-state owner is
added. Native tests with distinct trusted fixture poses validate timing and
lifecycle; they are not visual acceptance of new artwork.

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
new animated artwork. Torren now explicitly selects `torren.breathe`: four new
painted pictures sampled as 0→1→2→3→2→1 over 1800 ms. Its alternate sheet keeps
fixed support anchors and uses the existing idle owner, including finite-action
suspension, resume and reduced motion. All other actors retain `static.idle`.
See [the Torren idle review](../../docs/art-torren-idle-20261110/README.md) for
source extraction, loop limits and real-render evidence. Synthetic multi-frame
fixtures remain separate from these production-art checks.

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
| attackActivated | attacker, weapon, at | One weapon-selected source action; yield at clip release |
| attackCompleted | attacker, weapon | Finish that source recovery before later actions |
| counter, supportAttack | attacker, defender, weapon, damage, killed | Independent weapon-selected action and released impact |
| attack, areaAttack | defender, weapon, damage, killed | Recipient impact; never infer another source action |
| partingShot | same plus at | Weapon-selected action; release impact at explicit departure cell |
| attackStructure, areaAttackStructure | attacker, structure, weapon, damage, destroyed | Recipient structure impact without another wind-up |
| forcedMove | unit, mode, from, to, path | Ordered ground slide; teleport changes contact without interpolating a route |
| collisionDamage | unit, amount, killed | Positive recipient hit clip with generic impact; retain both-boundary visibility |
| statusTick | unit, amount | Positive recipient hit clip with generic impact; both-boundary visibility |
| regen | unit, amount | Existing healing effect at the current visible contact |
| death | unit | Bound terminal clip with normalized fade/shrink, then remove ghost once |
| heal | source, target, amount | Ability-selected source action; released heal strip and 500 ms recipient number |
| recruit | unit, at | Add only the new actor, then 240 ms fade/scale from 0.6 to 1 |
| capture | captured | Refresh site ownership when completed |
| turnStart | player, turn | Refresh HUD, 220 ms entrance, 420 ms hold, 220 ms exit |
| gameOver | visible surviving winning-team actors | concurrent explicit celebrate clips, overlapping the 220 ms result beat; neutral outcomes retain only the beat |

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

## Facing during playback

Canonical facing belongs to `UnitDisposition` and the composed tactical grid.
`BoardProjection.FacingVector` projects that grid direction; the sprite mirrors
only for negative projected X. Zero horizontal projection consistently uses the
unmirrored pose, both during playback and after settlement. No direction-name
list or raw target-X comparison substitutes for `ITacticalGrid.Toward`.

Ordered `facingChanged` receipts update the existing visible actor at their event
boundary. Command paths still run before their receipts; each path segment asks
the composed grid for its facing, including the initial displayed movement frame.
Push/pull use the same path rule; teleport preserves the prior facing.

Outgoing action poses aim through the grid using current logical contacts. Their
continuous projected lunge vector remains separate from tactical facing. Reactions
and due delayed casts need not change canonical facing, so their temporary mirror
returns to the captured pre-action mirror at normal clip completion, before idle
resumes. The source can recover while its released impact is still playing.
Cancellation, errors, rebinding and exit keep their existing settlement ownership;
a retired action never writes back a captured mirror after cancellation.

Mirroring stays in the existing foot-relative scale transform, inherited by the
composited figure/shadow. It does not change the authored crop, anchor, logical
contact or world rules. In particular, changing `Sprite2D.FlipH` would flip only
texture geometry and is not interchangeable with this transform contract.
`AnimationFacingChecks` covers the zero-X C04 failure shape, both mirrors,
square4 diagonal quantization, composed hex directions, ordered turns, recovery,
forced movement, later hits, cancellation, rebinding, hidden actors and reduced
motion. The original-art C04 native witness and acceptance limits are recorded in
[Facing consistency verification](FACING-CONSISTENCY-20261106.md).

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
  unitWithdrawn, unitEmbarked,
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

### Clip-bound action accents

`ActorActionAccent` adds a compact, tapered sweep to lunge actions and an outlined
focus diamond to stationary actions (including healing). These are generic action
accents, not weapon classifications or new sprite-sheet animation. The lunge
ribbon continuously sweeps from anticipation through target-aligned release into
follow-through. Two thinner tapered trails appear only after release, behind the
leading edge. Their warm fill and ink edge retain separation from the character
silhouette at gameplay zoom; see the [C01 real-render comparison](MELEE-READABILITY-20261110.md). Rotation and trails are sampled analytically from that same clip
clock; no previous-frame history, particles or extra timeline is retained. The
stationary focus remains upright and unchanged. See the
[continuous sweep verification](COMBAT-SWEEP-20261110.md) for native coverage and
explicitly missing pixel acceptance. They inherit
the actor's standing depth and position, and sample the same clip clock as its
pose. The declared release still owns the first hit. No new wait, projectile
travel, camera shake, battle rule, random stream or duplicate cast event is added.

Impacts use a short angular flash rather than the previous filled white circle.
The flash clears within 118 ms; the existing effect strip and damage/healing
number retain their own duration and values. Neither accent uses additive bloom,
random particles or full-screen overlays. Clip completion, cancellation and
disabled animations remove the accents through the existing projection lifetime.

This is presentation polish of the trusted existing poses, not newly drawn actor
animation. The release checks cover anticipation, release, recovery, cancellation,
and disabled animations for both action motions. Real-display review of melee,
ranged, healing, area attacks and overlapping terrain remains a separate visual
acceptance gate; headless checks cannot establish visual quality.

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
