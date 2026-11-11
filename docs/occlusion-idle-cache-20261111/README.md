# Bounded idle occlusion layouts (2026-11-11)

## Problem and scope

C10's terrain-covered actors need whole-field safe cue placement. Profiling the
shipped `9b61db3c` implementation found repeated idle sprite poses repeatedly ran
the same expensive pixel/polygon search. A spatial bounds index was evaluated and
discarded: warm C10 p95 only changed from 37.055 to 33.998 ms, with mixed results on
other maps. The exact geometric search is retained.

Only settled-projection layouts are memoized, as immutable cue/diagnostic values,
not Godot nodes. Keys contain all registered idle actor identities and frames.
128 FIFO entries bound retention. RenderState (including fog, owner and movement
changes), Configure, successful ConfigureGround and tree exit clear history.
GlobalTransform changes clear it on the next refresh. Finite animation does not
reuse settled layouts; completion/cancellation reconstructs the projection.
Arbitrary external edits of private child-node geometry are not a supported API.

## Controlled CPU observation

Both variants ran on the same cloud Linux/Godot 4.6.3 .NET environment, headless,
without concurrent regression suites. Each loaded the canonical opening snapshot,
configured its registered scene and ground, disabled automatic presentation ticks,
and called AdvancePresentation(1.0/60) for 300 warmup + 600 measured steps, yielding
one ProcessFrame between steps to retire queued nodes. Stopwatch covered only
AdvancePresentation, not hashing or frame wait. Sorted sample index 570 is p95.
No gameplay snapshot mutation was allowed. All four scenes had 56 complete idle
pose combinations across the 900 ticks. Raw logs are adjacent.

| Scene | Baseline p95 / mean ms | Cached p95 / mean ms |
| --- | --- | --- |
| C01 | 1.564 / 0.241 | 1.364 / 0.217 |
| C06 | 1.014 / 0.149 | 0.773 / 0.119 |
| C07 | 2.035 / 0.256 | 0.909 / 0.151 |
| C10 | 37.055 / 4.016 | 1.938 / 1.743 |

These are controlled CPU observations, **not whole-game FPS or hardware targets**.
Cold/new poses still run the full search. C10 maximum was 53.236 ms baseline and
57.777 ms cached; this does not eliminate cold spikes. Empty cue layouts on the
other three opening snapshots are weaker correctness evidence than C10.

For each measured step, cue node name and local X/Y (round-trip invariant numeric
format), in child order, were concatenated as `step:name:x:y;` and SHA256 hashed.
Both C10 variants produced
`8388F40D2330DA355E322EB91CC901F86E0D01DFF71D4A0536F1BB96C92E651F`.
Other opening snapshots produced the same empty-layout hash in both variants.
Temporary benchmark instrumentation was removed before building the final tests.

## Correctness and interaction

PresentationChecks compares 193 C10 projections against an explicitly forced
fresh rebuild after selecting each oracle pose: repeated idle frames, nonuniform
parent scale/rotation/skew, fog hiding, owner replacement, accepted canonical
move-and-wait, and reconfiguration. It compares cue identity/order/position/shape,
owner colors, leader geometry, diagnostics, cue-center hits, terrain hits, and
unchanged canonical snapshots. Removing Frame from the key is a negative control:
it fails at repeated pose 3 (UnitCue_2 placement); the correct key is restored.

Real 960x640 C10 level-entry fixture: after prolonged idle, the blue covered-actor
cue selected Laiya correctly. Zoom-out updated positions; clicking the new cue
again selected Laiya. Game was then closed. This is an isolated chapter opening,
not a complete campaign playthrough. No private saves/screenshots are included.

Checkpoint: build (0 warnings/errors), formatting, indexed source metadata,
76 catalog boundary assertions and PresentationChecks (including all 193 forced
fresh comparisons) pass. Negative control exits 1 at the expected assertion.
Final focused regressions also pass under the strict native process/diagnostic
gate: DynamicState (750 assertions), PaintedSurface, Animation (8257 assertions,
including finite animation/cancellation/reduced-motion behavior), and BattleScreen.
Each uses isolated XDG_DATA_HOME; the wrapper timeout was 900 seconds. No forced
termination was required. The full sixteen-scene aggregate was not run, and no
remote CI result is claimed by this focused performance change.
