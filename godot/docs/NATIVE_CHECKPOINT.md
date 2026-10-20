# Native campaign checkpoint, 2026-10-20

This is a working migration checkpoint, not full visual or feature completion.

## Freshly rerun

- Main core: 14 suites, including 396 full-battle actions with every ordered event and intermediate state digest checked.
- All 16 original campaign levels: 2,983 independently chosen C# AI actions, each action, event list and state digest equal to the original TypeScript. These are independent levels, not every roster-transformed branch.
- Persistence: 96 checks, including all 16 opening states, complete saved battle/replay compatibility and malformed/unsupported-state rejection.
- Scenario/composite: 124 source differential cases. A test-only objective fixture initially omitted its runtime `id`; fixing the fixture to the real state shape resolved that false event mismatch. No production rule was altered to accept malformed state.
- Application: prologue/choice-to-battle orchestration, restart restores initial state, midbattle save/load followed by 127 identical actions and identical campaign result; invalid load leaves current campaign unchanged.
- Godot.NET build: zero warnings/errors. Headless scene import/start and real-session BattleScreen checks pass.

## Additional complete action families

All 12 original engine action kinds now have native dispatch handlers. Dedicated source comparisons pass: deployment 2,519 checks; career/formation progression 175 cases; recruitment 25 cases; transport 81 direct cases, 24 dispatch steps, four saved states with eight resumed steps and ten malformed-save guards. A deliberate boarding capture-reset defect is caught by the emission-time digest test. Deployment and embarked-unit persistence are enabled only with explicit relationship guards. The 16-level, 2,983-action AI regression still passes after this integration. These counts do not establish full extension-registry coverage.

## Actual cloud desktop checks

The original Chinese prologue, mission briefing and choice screens rendered legibly. Selecting steady advance entered the actual native battle. A saved first-turn battle progressed through the real opposing AI and returned to human turn 2. Clicking Load restored turn 1 and original positions. Restart displayed a confirmation and returned to the initial battle. Header controls initially collapsed because wrapping buttons had no minimum horizontal size; the final build fixes this and the visible controls were retested.

The native viewport was 1180×812. The cloud machine has no usable audio device and falls back to Godot's dummy driver; no audio playback was validated.

## Complete default scenario and text boundary

All 34 default scenario effect kinds and all 16 conditions are implemented through
canonical owners. 466 live-source cases compare state, ordered events and every
emission-time digest; 29 real-engine steps save and reload JSON text after every
action. This includes formerly failing lone-surrogate streams, IDs, variable keys,
values and signals. No skipped compatibility probes remain. Overlay-expiry and
transport capture-reset deliberate defects are detected by the tests.

The canonical `EcmaJson` boundary preserves JavaScript UTF-16 code units, including
lone surrogates in strings and property keys. 2,100 live-source vectors verify
parsing, cloning, RNG counters, save/load, immutable transfer and exact digests.
Use its Parse/Stringify/DeepEquals APIs, not System.Text.Json's lossy defaults.
See the UTF-16 runner README for the explicit lazy-object input boundary.

Catalog and level admission now validate the actual composed capability owners.
68 source catalog cases, 215 level behaviors, 100 persistence payload cases and
explicit native unsupported-capability guards pass. Native fail-closed additions
are distinguished from source-exact validation in COMPATIBILITY.md.

After this integration, all 16 battles again reproduce 2,983 independent actions;
main core 14, persistence 96, deployment 2,519 and native dynamic 100 checks pass.
The two real campaign choice routes reproduce 376 and 414 actions and 14 save/resume
checkpoints each, naturally ending in second-battle defeats, not invented wins.

The final built-in audit additionally exercises 27 action refusal cases, 96
resource queries and 62 resource announcements, including deliberately invalid
zero-amount and non-finite calls. Three targeted semantic mutants are detected.
The core checkpoint was reconstructed from the staged tree without the ongoing
animation work: its Godot build had zero warnings/errors, core 14/application/
persistence 96 checks passed, and native terrain 16/dynamic 100/BattleScreen
contracts passed. This is headless validation, not renewed desktop acceptance.

## Native presentation and query expansion

Source-backed water, river banks, three registered bridges, material families,
whole-ground clipping and 17 guarded c01-02 edge overlays are implemented. Terrain
contract tests pass for 16 canonical projections. Dynamic presentation passes 100
assertions including all nine live structures, markers, source mother-root fallback,
HP/done/capture/facing badges and owner banners. The viewport consumes canonical
fog visibility, public cast countdowns and composite status without owning rules.
72 fog and 12 cast projection source comparisons pass across the three core grids.

Actual c01-01 and c01-02 terrain previews were inspected. These previews do not
have gameplay controls and are not a new campaign interaction acceptance test.
The cloud desktop subsequently became unresponsive to bounded read-only checks;
new integrated UI interaction remains unverified after this renderer expansion.

## Committed-transition animation integration

The view receives immutable detached Before/Action/Events/After facts and never
recomputes rules. Source movement and ordered strike/impact/death/heal/recruit/
turn-banner timing now drive named actor frames and numeric effect strips.
The session emits no new transition for rejected actions; loaded sessions do not
replay history. AI advancement and result dialogs wait for view settlement.
Controller checks cover repeated clicks/refresh, cancellation, load/rebind,
superseding transition-free snapshots and unmounted stale callbacks.
The exact staged animation tree passed the complete `verify-native.sh` base
command: build (0 warnings/errors), core 14, persistence 96, application
continuation, terrain 16, dynamic 100, animation 177 and native BattleScreen
contracts. `run-native.sh --headless --quit-after 2` also built/imported/launched
the real story entry successfully. `--full` was not rerun as one aggregate command. Source
unhandled events remain settle-only rather than receiving invented death/FX.
See ACTION-PRESENTATION.md for source references, exact tested scope and timing contracts.

## Descriptive event log and postbattle summary

Source event descriptions now appear after their corresponding animation, in a
single bounded/coalesced log. Opening/restart/load/save behavior follows the
original controller. Victory summaries use canonical bridge signals/outcome and
combat strike counts, with turns, survivors and fallen names. No in-battle
scenario popup or audio behavior was invented. See BATTLE-FEEDBACK.md. The isolated
feedback tree passed the base aggregate verification (build 0 warnings/errors,
core 14, persistence 96, application continuation, terrain 16, dynamic 100,
animation 177 and UI contracts). Both real campaign routes again matched 376/414
independent actions and 14 saved resumptions each, ending in source-matching losses.
Those autonomous core outcomes are not the interactive-shell commitment policy:
only explicit victory settlement advances the native campaign. Defeat supports
retry/camp without committing failure. The camp-flow regression uses an actual
92-action defeat and verifies 149 state/persistence assertions. Staging matches
371 source comparisons and 145 native screen assertions, including all original
exported art with zero missing-art diagnostics. The source-style prebattle autosave
uses a separate file from manual midbattle saves; complete campaign auto-resume
and menu behavior remain pending.

## Native camera and compound lighting checkpoint

The feedback/camp parent checkpoint is `e6e18036`; its exact staged tree passed
142 result-flow, 149 camp-flow and 145 staging assertions in addition to the base
verification. The camp test's loss was reached through 92 legal actions. The
application uses separate automatic prebattle and manual midbattle save files;
source menu/startup and remaining autosave boundaries are still pending.

The subsequent camera integration preserves source scale, center-anchored zoom,
resize scroll retention, pointer capture/cancellation and visible AI path follow.
It passed 1,822 live-source helper assertions, 26 native transform assertions and
60 real-session integration assertions. Source keyboard/selection behavior and
whole-loop AI pacing remain separate inventory gaps.

Lighting composes the original contact/ring/clipped actor frame before filtering,
and clips registered crossing axes before filtering their union. Whole-actor
fades use a separate CanvasGroup owner. Bounded checks passed 1,537 lighting,
177 animation and 100 dynamic assertions plus every terrain projection. The
committed lighting fixture is generated by the source oracle script; `--full`
regenerates it from TypeScript rather than treating fixture presence as parity.
Cold composition was approximately 4.9 seconds in this cloud environment, with a
4 ms immediate cached projection. This is a performance acceptance concern, not
a claim of responsive desktop rendering. See the lighting runner README for
remaining route/action/cursor/marker/cover-prop/perimeter paths.

The first combined renderer run exposed a shutdown crash after successful
animation assertions. Four leaked textures were traced to unshared structure
contacts. Explicit node-owned texture disposal and idempotent board-cache disposal
now prevent resurrection and release managed wrappers before teardown. The
regression exercises real save/JSON-load/rebind/restart and queued scene changes;
omitting disposal fails deterministically. Eleven isolated processes subsequently
exited cleanly, including six animation runs and three lifetime runs (nine complete
session cycles). `verify-native.sh` also checks process exit and leak/error output,
not merely assertion markers, with independent temporary save directories.

No new desktop visual acceptance accompanies these headless results. The full
[feature inventory](GREYFLAG-FEATURE-INVENTORY.md) distinguishes implementation,
differential/native tests and actual GUI evidence.

## Narrative, menu and autosave integration

The camera/lighting parent is `27cf0d79`; its exact-index aggregate command exited
cleanly after the ownership repair, including 73 lifecycle assertions. This batch
adds the original single-beat narrative, source speaker portraits and 13 scene
images, a shared campaign shell, title Continue/New, guarded overwrite dialogs,
clean campaign autosaves and source ending behavior. F5/F9 remain a separate full
manual slot. Local story cursors do not advance canonical state until the final
ordinary beat. The original ending's beat-zero/finish behavior is deliberately
preserved rather than silently rewritten.

Focused source-backed evidence: 4,949 native narrative assertions, 299 storage/
menu .NET assertions, 82 isolated menu controls, 377 staging comparisons across
45 actual source renders, and 151 native staging assertions. Narrative label,
storage broad-catch and staging metadata mutants are rejected. Native integrated
navigation passes 169 assertions and result flow 150, including actual opening
beats/choice/staging/battle/camp/Continue, failed saves, invalid data, overwrite
cancellation and detached/remounted stale confirmation. The latter regression
caught and fixed a genuine autosave deletion after application unmount. Each
save-mutating suite refuses existing data before reading or writing and uses a
fresh temporary namespace. Terminal/settlement fixtures are explicitly labeled;
these tests do not claim a played victorious campaign route.

Committed gzip fixtures allow the base native check to run without TypeScript.
They are test-only outputs of the included live-source exporters; `--full`
regenerates source comparisons. The shared menu/art and aggregate exact-index
validation are integration gates, not implied by isolated fixture assertions.
No newer GUI acceptance has been obtained.

## Explicit remaining gaps

Nine of sixteen levels have no authored scenery JSON in the recovered source, so the application labels that absence rather than borrowing another level's scene. Remaining source lighting paths and cold-render performance still require work. Camera controls are integrated but desktop usability and remaining keyboard/selection behavior are not accepted. Narrative/menu/autosave behavior now has source-backed and native integration evidence, but actual desktop acceptance remains required. Tactical selection/keyboard/undo, destination commands, complete inspection/forecast/support HUD and exact AI pacing remain required; see TACTICAL-UI-MIGRATION.md. The recovered source has no audio playback implementation, so silence is not an omitted original feature. All default built-in action, effect and condition families have now been independently audited; arbitrary custom plugin registration and extension API parity are not claimed (see BUILTIN-COVERAGE.md). Unsupported saved feature states are rejected. Other story themes, the editor and packaged desktop distributions remain outside this checkpoint. Original TypeScript is retained as a development oracle, never a runtime rules bridge.

## Tactical canonical foundations, before widget integration

The narrative/menu parent `87da7282` passed the exact staged aggregate, including
4,949 narrative, 102 full-project menu/art, 151 staging, 169 navigation and 150
result-flow assertions, clean exits and four deliberately induced autosave warnings.
The new canonical foundation adds registration-owned handoff metadata, trusted
lineage-checked restore, session-owned undo history and detached read queries at
their original owners. Source session comparison covers 138 operations plus
terminal/deployment cases; four compiled mutants are rejected. Tactical query
comparison covers 740 actual source outputs; eight compiled mutants are rejected.
Recruitment-card affordability is intentionally distinct from full legality, and
objective/cast/progression/content facts have one configured owner. The engine
shares its singleton VictorySystem between lifecycle and inspection. Existing
core replay hashes and application continuation are unchanged. These APIs are
not yet a completed tactical HUD/selection/keyboard/AI-pacing implementation.

The canonical foundation above is committed as `2ea32bd0`. Its frozen index
passed the full base native runner with clean process exits, including both new
fixture consumers and all narrative/menu/navigation/lifetime suites. Recovery
delta restoration was byte-checked against all 780 tracked files. The next
controller/HUD/AI integration is not covered by that committed result.

## Tactical interface integration gate

The next frozen integration replaces the generic menu consumer with the original
destination-first selection flow and complete canonical HUD projections. Focused
source evidence is 430 controller assertions/eleven mutants, 2,398 adapter and
real-dispatch assertions/six mutants, 821 core query assertions/eleven mutants,
and 7,429 actual source GameController.hudView→Hud.render comparisons/fourteen projection mutants. The revised HUD
oracle executes actual GameController.hudView into actual Hud.render; constructor-only
board/camera/event wiring is intentionally omitted. The overlay oracle likewise
executes GameController.overlay, not merely selection.paint. Native HUD checks cover 62
assertions, source art SHA256, repeated Apply/rebind/teardown texture ownership
and three rejected native mutants. Actual BattleScreen controls and 63 camera
integration assertions pass, including source busy hover and leave behavior.

AI pacing compares seven executing source-loop scenarios and 26 clock
observations, the 2000-action guard and disposal, with five rejected mutants.
Twenty mounted Game assertions verify actual AI-selected commits, post-animation
Save/Load blocking, deadline timing and lifecycle cancellation. Result-flow150
and application127 still pass. Frozen gzip fixtures run without Node; live-source
and mutation checks are available through verify-native.sh --full. The final
exact-index aggregate is the next gate; focused passes alone are not that gate.

No new GUI acceptance is claimed. Remaining required work includes original
marker/filter/cover/perimeter rendering and cold-composition performance, the
original open extension kernel, real-window campaign acceptance and standalone
desktop export. Missing authored scenery in nine recovered levels is explicit,
not silently filled from another map. Original sources remain preserved.

The tactical integration above is committed as `041abb24`. Its corrected exact
index passed verify-native.sh with clean process exits, including full source
Game.overlay composition, actual Game.hudView→Hud.render fixture consumers and
all previous suites. Four additional HUD composition mutants brought the
recorded pure suite to fourteen total (the preceding staged text retained the
older ten); no production changes were required by that audit. Source replay
hashes remain 3942584e, f0fe6f08 and ecb676a7. Recovery restoration matched all
898 tracked files. New renderer/performance changes are separate, uncommitted
work and not covered by that result.


## Frozen renderer batch after 041abb24

Source ground decorations, per-cell connector filters, live cover and generic perimeter are implemented. Focused checks passed 470 decoration, 9,227 connector/marker and 8,254 scenery assertions with clean exits; eight renderer and three scenery negative controls were rejected. Nine missing authored scene documents remain missing, not replaced by invented authored content. The source-defined generic perimeter is separate. Exact raster optimization passed 195 assertions and matched fresh-process timing; first-use latency remains a performance gate. Combined immutable aggregate and repeated teardown validation passed at `16e05c4d`: six independent animation processes, three lifetime processes, real screen bindings and all source-lighting contracts exited cleanly. No new GUI or publication acceptance is claimed.


## Exact-output performance follow-on

The separate pending batch introduces format-checked bulk RGBA pixels, exact tap-order SIMD and an exact no-cutout interior-coverage path. Focused normal/scalar fallback reference tests passed before integration. Temporary byte snapshots trade higher cumulative managed allocation for lower first-use CPU time; this is not a peak-memory or 16 ms frame-budget claim. Immutable aggregate and repeated teardown are required again before committing. Native GUI acceptance and standalone packaging remain pending; no new screenshots or publication are claimed.

## Extension foundation integration (verified 2026-10-20 23:13 UTC)

After `5593b1b6`, shared registry mechanics, typed kernel primitives, open command abilities, and resource adapter/subject policies are integrated around existing rule owners. Focused source comparisons and deliberate mutants passed. The combined frozen native aggregate exited cleanly on immutable tree `c13dff11e4dbdb976dda0c2e16ffcb545200d49f`; all 16 independently replayed default battles matched 2,983 chosen actions, ordered events and state digests. Only this verification documentation changed after that code snapshot. Kernel primitives are not yet the engine composition root. Custom plugin persistence, spatial replacement, action lifecycle metadata, combat/lifecycle policies, and handler-owned admission remain required.

The preceding lighting optimization checkpoint passed both SIMD and hardware-disabled scalar reference comparisons and repeated clean teardown. Its benchmark improvement does not establish desktop responsiveness or visual acceptance.

Foundation evidence: 227 defined registry outcomes (mixed-NaN orders separately characterized), 121 kernel checks with 13 source/15 native mutants, 55 ability cases/307 assertions with 10 mutants, 284 resource policies +24 registry +22 canonical-consumer cases/1,895 assertions with 9 mutants. The immutable aggregate also passed persistence 96, application 127 actions, narrative 4,949, staging 151, navigation 169, source lighting 1,537, exact SIMD/scalar raster checks, session/tactical/HUD contracts and clean native exits. Source campaign validation and 27 registry cases passed in focused tests. No new GUI acceptance or publication is implied.
