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

## Explicit remaining gaps

Water and some terrain presentation adapters remain incomplete. Nine of sixteen levels have no authored scenery JSON in the recovered source, so the application labels that absence rather than borrowing another level's scene. Structure/composite/scenario state visuals and lighting/shadows require more work. All original dispatch kinds are implemented, but full extension-registry coverage still requires an independent audit; unsupported saved feature states are rejected. Other story themes, the editor and packaged desktop distributions remain outside this checkpoint. Original TypeScript is retained as a development oracle, never a runtime rules bridge.
