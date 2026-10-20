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

## Explicit remaining gaps

Nine of sixteen levels have no authored scenery JSON in the recovered source, so the application labels that absence rather than borrowing another level's scene. Source-authored lighting/shadows still require work. Source camera follow/panning is not yet reproduced in this checkpoint. Story beat-by-beat speaker/scene presentation, full campaign autosave/startup menu and terminal-ending navigation remain required, not completed by battle-rule parity. The recovered source has no audio playback implementation, so silence is not an omitted original feature. All default built-in action, effect and condition families have now been independently audited; arbitrary custom plugin registration and extension API parity are not claimed (see BUILTIN-COVERAGE.md). Unsupported saved feature states are rejected. Other story themes, the editor and packaged desktop distributions remain outside this checkpoint. Original TypeScript is retained as a development oracle, never a runtime rules bridge.
