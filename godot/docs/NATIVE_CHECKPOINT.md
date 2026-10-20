# Native campaign checkpoint, 2026-10-20

This is a working migration checkpoint, not full visual or feature completion.

## Freshly rerun

- Main core: 14 suites, including 396 full-battle actions with every ordered event and intermediate state digest checked.
- All 16 original campaign levels: 2,983 independently chosen C# AI actions, each action, event list and state digest equal to the original TypeScript. These are independent levels, not every roster-transformed branch.
- Persistence: 96 checks, including all 16 opening states, complete saved battle/replay compatibility and malformed/unsupported-state rejection.
- Scenario/composite: 124 source differential cases. A test-only objective fixture initially omitted its runtime `id`; fixing the fixture to the real state shape resolved that false event mismatch. No production rule was altered to accept malformed state.
- Application: prologue/choice-to-battle orchestration, restart restores initial state, midbattle save/load followed by 127 identical actions and identical campaign result; invalid load leaves current campaign unchanged.
- Godot.NET build: zero warnings/errors. Headless scene import/start and real-session BattleScreen checks pass.

## Actual cloud desktop checks

The original Chinese prologue, mission briefing and choice screens rendered legibly. Selecting steady advance entered the actual native battle. A saved first-turn battle progressed through the real opposing AI and returned to human turn 2. Clicking Load restored turn 1 and original positions. Restart displayed a confirmation and returned to the initial battle. Header controls initially collapsed because wrapping buttons had no minimum horizontal size; the final build fixes this and the visible controls were retested.

The native viewport was 1180×812. The cloud machine has no usable audio device and falls back to Godot's dummy driver; no audio playback was validated.

## Explicit remaining gaps

Water and some terrain presentation adapters remain incomplete. Nine of sixteen levels have no authored scenery JSON in the recovered source, so the application labels that absence rather than borrowing another level's scene. Structure/composite/scenario state visuals and lighting/shadows require more work. Deployment/transport and other original engine action families outside the tested paths are not fully ported; unsupported saved feature states are rejected. Other story themes, the editor and packaged desktop distributions remain outside this checkpoint. Original TypeScript is retained as a development oracle, never a runtime rules bridge.
