# Deferred generic navigation fix

Status: not shipped. All battle-engine files in the spacious scene worktree were restored to HEAD f567b92958d0cb70c915105667a23657d035ba4e at 2026-10-20 09:31 UTC. The new AI test and the two AI documentation rows were removed. No engine commit exists.

## Proven defect
The stagedEnemy C02 .58 simulation stopped moving after turn 23 through the 900-action cap (turn 81). Friendly soldiers and two surviving refugees stayed on the west riverbank, while the last enemy shield stood at (19,14). The map was connected via crossings at rows 11 and 18. The shield had twelve legal stops and a ten-step route to the nearest ally. Original TileAppraisal geometric engagement and mission pulls preferred the current bank over initially moving away from the target. Board.distance is correct geometric range and is not the owner to change.

Evidence: /tmp/staged-stall-scores.json; /tmp/probe-spacious-stagedEnemy-0.58.json; /tmp/diagnose-staged-stall.ts.

## Broad route-distance experiment rejected
TacticalSpace.navigationDistances delegated to movement.computeNavigationDistances. Reverse BFS uses Board.neighbours, Battlefield.cell.admits and directed Battlefield.traversalCost. Counts navigable steps, ignores turn occupancy/ZoC, and does not replace legal per-turn moveField paths or costs.
TileAppraisal then used this distance in mission, capture, escort, enemy keep, home defence, and preferred-engagement-range pulls. That repaired the reproduced stall, but broadly changed utility gradients, even on useful turn-one openings. Numeric aggression and combat weights were unchanged; behavior was emphatically NOT preserved.
The initial all-foe engagement ring also incorrectly ignored a nearer threat if a farther foe was already at ideal range. This was corrected to nearest-reachable-foe policy before the final review sweep.

The corrected final broad implementation passed 49 engine files / 525 tests. Final full 60-profile comparison: 15 identical streams, 42 of 57 non-C02 streams changed, 11 non-C02 outcome changes. Existing seven action caps stayed seven; no new caps. Outcome changes: six wins to losses; one win to draw; three losses to wins; one draw to loss. Full exact reasons and turns: /tmp/ai-navigation-rejected-broad/final-sweep-comparison.json. This version must not ship as a narrow navigation repair.

## Narrow stationary-wait prototype incomplete
implementation.patch is the experimental narrow version, regressions.patch preserves the test. It restores all original tile-score values and only substitutes a route step for a selected in-place movement-only command when no other active command option exists. A proposed stop must preserve existing directive, defensive terrain, height, escort, cover, home-defence, exposure, recovery, and command-link values. The route must require a real detour and strictly reduce remaining traversable distance. Existing per-turn path owner remains authoritative.
Wait's AI evaluator declares movementOnly rather than branching on an ability ID. The prototype reads strongest mission destination / nearest foe from TileAppraisal to avoid a separate goal registry, but goal selection and hold semantics still need architectural review.

Crucial failed regression: first detour is correct, but on the next turn the unchanged one-ply geometric scorer walks back toward its original bank position. The multi-turn eventual-combat test fails. Do not weaken the test or call first-step escape a fix.
A complete narrow continuation requires a real route-intent lifecycle, or a more general navigation policy. There is no existing persisted AI route target in GameState or Unit. Adding one would require authoritative action commitment, target invalidation/preemption, cloning, digest, saves/replay and regression coverage. An ambient cache, changing Unit.directive, or mutating state from chooseAiAction would violate existing ownership contracts. No such state was added.

## Bounded next task
Only continue in an isolated engine branch/worktree. Specify route ownership and lifecycle first. Combat, healing/capture, explicit orders, survival/recovery and target invalidation must preempt travel. Preserve healthy-opening behavior; require the detour to complete, not merely begin. Re-run old 60-profile campaign floor and report any difficulty regressions before commit. The expanded scene has an independently successful authored baseline arrangement and should not depend on this engine experiment.
