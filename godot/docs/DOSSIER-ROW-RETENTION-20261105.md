# Unit dossier row retention, 2026-11-05

## Reachable workload and measured cause

Baseline: `a50bc800b932aa27c0f80487a446d89777d07962`.

Two real actionable actors from each shipped C01-01 and C01-07 opening session
are selected through `BattleScreen.SelectHit`, followed by the actual native
unit-dossier opening and closing controls. The screen uses its authored scene,
ground and dynamic-state setup. This exercises the normal selection boundary,
not physical pointer picking or a carried campaign playthrough.

Separate stage measurements attributed 58–66% of HUD Apply time to rebuilding
hidden `unit-details`. Rule projection took about 2–4 ms, while synchronous actor
switches took hundreds of milliseconds in this environment. Actual deferred
popup opening took about 0.69–1.74 ms; timing only the opening request would miss
that deferred work. The two actors have 19/16 rows in C01 and 21/18 in C07, with
ten unchanged text rows in common, so the existing same-shape terrain refresh
does not cover this workload.

An initial alternative that constructed dossier sections off-tree was rejected:
its four warmed selection medians increased 0.79–6.97%, allocations did not
improve, and hidden geometry differed. It is not part of this change. Existing
Chinese text, fonts, textures and layout are preserved.

## Ownership and implementation

`GrayFlagSession.ProjectHud` remains the sole owner of detached presentation
facts. `TacticalHud` owns native widget lifetime and compares its previous
immutable projection with the next one. No per-unit cache or independently
mutable row model is introduced.

Only changed, read-only `unit-details` sections with unique row keys can reuse
rows, within the existing same-Stamp, same-Busy and configured-art lifetime.
The previous projection describes the mounted HBox and optional sibling
ProgressBar for each row. A temporary mapping lives only during Apply.

- Equal complete row records, except Label/Hint, retain their mounted fragments.
  Only changed text or hints are assigned.
- Added, removed or otherwise changed rows use the existing renderer and texture
  release path. Icon, portrait, selected style and gauge changes cannot retain
  stale appearance.
- Native MoveChild restores the new row order without unmounting retained rows.
  Gauge siblings move with their row; no wrapper container or spacing is added.
- Heading and disclosure text update while expansion state, current-projection
  disclosure callbacks, focus and deferred scroll restoration remain intact.
- Duplicate keys or action-bearing rows are ineligible for this optimization and
  continue through the original complete renderer. Native structural corruption
  is an explicit error before mutation or advancing the current projection.
- Stamp/Busy changes, art reconfiguration and unmounting still invalidate reuse.
  Action buttons and their revision-bound callbacks are not retained by this
  new path.

## Final-source controlled comparison

Production TacticalHud.cs SHA256:
`7e2d995f4c2e60b8e92ed3fe2c749d81dd5765855d7d5dcff331fb973d70b9a1`.

Godot 4.6.3 .NET and .NET 8 Debug, with tiered JIT consistently disabled. ABBA
used four fresh isolated processes: baseline normal level order, candidate
normal order, candidate reversed, baseline reversed. Each actor/level has 13
repetitions per process; first invocation is reported separately, sample 1 is
warmup, and every sample 2–12 is retained. Each variant therefore contributes
22 warmed samples per path, without outlier trimming. Builds/native tests did
not run concurrently with measurement.

Synchronous selection elapsed milliseconds, baseline → retained rows:

| Path | Median | p95 | Maximum | Median reduction |
| --- | ---: | ---: | ---: | ---: |
| C01 actor 1 | 356.43 → 197.44 | 407.68 → 355.23 | 411.22 → 382.56 | 44.6% |
| C01 actor 2 | 269.94 → 123.74 | 475.28 → 241.81 | 1615.71 → 318.83 | 54.2% |
| C07 actor 1 | 373.88 → 189.40 | 503.00 → 209.37 | 968.63 → 245.19 | 49.3% |
| C07 actor 2 | 289.07 → 119.48 | 357.72 → 139.17 | 370.76 → 160.75 | 58.7% |

All eight separate actor/level/round medians improved, by 31.7–62.5%. Coarse
process-CPU medians also decreased. Current-thread managed allocation medians
rose slightly: 625,032 → 626,136; 460,232 → 464,280; 725,080 → 729,216; and
537,968 → 543,072 bytes respectively, an increase of 0.18–0.95%. Native
allocation was not measured. This is a CPU-work reduction, not a memory or FPS
claim; absolute timings and long tails are environment-dependent.

The comparison observer runs outside timed callbacks and is identical in both
variants. Opening-request timing excludes deferred execution and queue wait;
closing timing excludes deferred focus restoration. Neither represents
end-to-end physical input latency or GPU rendering.

## Native equivalence and regression guards

Acceptance criteria were fixed before timing. All 408 corresponding rich native
snapshots matched the immutable projection, node kinds/order, rendered text and
hints, resolved styles/colors/fonts, atlas identity/crop, gauges, button state,
focus/scroll and visible geometry. These include 96 repeated expanded-reading
snapshots: open the dossier, expand unit details and stances, focus/scroll to the
last stance, deliberately scroll back to the top, then close to the current
opener. Canonical session JSON and event counts remained unchanged.

The evidence separately retains 1,028 non-visible rectangle differences. Retained
hidden controls can carry prior layout while newly built controls have not laid
out yet. Those differences were declared separately before timing, not discarded
or presented as visible-equivalence evidence. Four native processes exited 0
without error/leak/crash diagnostics; both comparison builds had zero warnings
and errors. This remains native structural/layout validation, not manual rendered
visual acceptance.

The permanent existing HUD runner includes 317 new focused assertions covering
19↔16 additions/removals, text changes, title/disclosure, retained identity,
reordering two gauge-bearing fragments, low/normal gauge styling, positive icon
and portrait textures with release checks, duplicate keys, Intent/Stamp/Busy/art
boundaries and explicit structural corruption. Real C01/C07 screen fixtures
switch between the two actors and back, comparing current projection/native
fragments and unchanged battle state.

Three independent negative controls built successfully and exited 1 at the
intended assertion: disabling row reuse; ignoring an existing gauge's changed
value; and omitting the retained gauge sibling's MoveChild. Restored positive
code passed all 317 assertions without warnings/errors/leaks. Mutants and the
temporary focused-only runner were removed before integration. Existing full HUD,
dossier focus, input, lifetime, save/replay and campaign checks remain enabled.

## Integration verification

Focused integration passed: whitespace verification, metadata (178 unique script
UIDs), Debug build and strict editor import; HUD 1,021 assertions and dossier
focus 73 assertions. Every process exited 0. The eight existing HUD warning
headers and their project origins match the preceding complete gate; there are
no error/leak/crash diagnostics. The complete repository gate is pending at this
code checkpoint. A focused PASS is not a complete-gate result.

Original artwork, maps, gameplay rules and save semantics are unchanged. Manual
GUI rendering and a packaged desktop acceptance run remain separate.

## Godot references

- [General optimization](https://docs.godotengine.org/en/stable/tutorials/performance/general_optimization.html): measure representative bottlenecks and compare candidate results.
- [Scene organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html): keep ownership and dependencies explicit.
- [Node](https://docs.godotengine.org/en/stable/classes/class_node.html): MoveChild preserves mounted-node lifetime; QueueFree defers disposal.
- [ScrollContainer](https://docs.godotengine.org/en/stable/classes/class_scrollcontainer.html): preserve the existing layout-aware scroll/focus handling.
