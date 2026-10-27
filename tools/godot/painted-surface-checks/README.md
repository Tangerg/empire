# Bounded painted-surface broad phase

Run `bash tools/godot/painted-surface-checks/run.sh [absolute-godot-project-directory]`.
The runner builds the project, checks the process exit and success marker, and rejects
errors/leaks. It does not open a native window or change saves or rules.

The frozen pre-optimization oracle clips every patch. The harness compares exact
polygon coordinates, piece indices/order, UVs, absolute depth, transformed pointer
polygons and alpha-aware picking against it. Cases cover dimetric and registered
raised projections; small, map-spanning, rotated, skewed, negatively scaled and tiny
patches; outside, touching and near-touching edges; transformed parents; depth-cut
rebuild/reset; and the unchanged unbounded ground/face path. Both the original and
optimized implementation pass 8,129 assertions on Godot 4.6.3.NET / .NET 8.0.427.

The production change calculates a local bounding rectangle for the decal, rejects
only wholly separated patch bounds, and retains the existing exact clipper for every
possible intersection (including touching edges). No geometry, ordering, rule,
refresh ownership or cross-frame cache changes.

## Measured C07 diagnostic (2026-10-27)

Isolated Debug/headless copy based on a7dfc03f, official toolchain above. Actual C07
T7 save loaded through GrayFlagSession.FromSave, actual authored scene and ground,
then canonical unit 2 move `(40,25) -> (41,25) -> (42,25)` with wait. Fresh-process
pairs used identical harness/instrumentation/input, changing only this broad phase.
Within each process: cold, three warm projections, the committed transition, then a
settled warm projection. Numbers below are wall-clock diagnostics, not FPS, GPU
benchmarks, hard thresholds, or an explanation of the earlier 1.8-second desktop gap.
Concurrent desktop activity can affect wall-clock values.

| Measurement | Original A / B | Optimized A / B |
| --- | ---: | ---: |
| Actual transition settlement RenderState (ms) | 307.063 / 355.634 | 117.159 / 97.003 |
| Subsequent settled-warm RenderState (ms) | 340.416 / 340.532 | 102.836 / 90.828 |
| Settlement dynamic surfaces (ms) | 268.371 / 299.985 | 49.656 / 41.669 |
| Settlement dynamic managed allocation (bytes) | 20,514,296 / 20,514,272 | 126,216 / 126,216 |

Each of two 48x24 marker decals previously clipped all 44,881 projected patches.
The broad phase reduces native clip calls to 55 and 88, respectively; emitted pieces
remain 39 and 66. This is the identified repeated work, not a shadow-cache miss.
The original full clipping algorithm remains the final geometry authority.

Small raw logs and temporary instrumentation/harness are retained outside the repo
in `empire-final-evidence/hud-integration-20261027/surface-profile-proof/`.
The disposable full profiling project was removed after verification.

Additional existing scene checks were attempted separately, not counted as passing:
`PaintedSurfaceChecks` fails its preferred-cue/cliff geometric-witness fixture;
`OathPadChecks` fails its historical walking-bob interpolation expectation. Both
failures reproduce with the original, unoptimized PaintedSurface in the isolated
baseline. Neither fixture is changed or suppressed by this patch.
