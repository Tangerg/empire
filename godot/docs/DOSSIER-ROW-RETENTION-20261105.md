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

## Integration verification and retained failure evidence

Code checkpoint: `d2513e9afe8e7e426d3b5c997d986e331dbc7a5f`.
Focused integration passed whitespace, metadata, Debug build, strict editor
import, HUD 1,021 assertions and dossier focus 73 assertions.

The first complete-gate attempt ran from 03:57:02 to 04:27:28 UTC and **failed**.
MoraleFeedbackChecks passed its 74 behavior assertions and the native process
returned 0, but shutdown emitted `Disconnecting nonexistent signal 'changed' in
'<Object#0>'`. The strict diagnostic wrapper correctly stopped the aggregate with
exit 1. Its log and all 23 native-process logs up to that point remain preserved;
the passing behavior line is not treated as a passing stage.

A predeclared alternating cohort then ran four baseline and four exact-candidate
MoraleFeedbackChecks processes, with fresh isolated user data and offline builds.
All eight passed 74 assertions, native exit 0 and strict exit 0, with byte-identical
logs. The failure did not reproduce. Repository history and Godot's managed
shutdown implementation suggest a disposal-timing hypothesis, but these samples
do not establish the offending resource or whether this change contributed.
No production release logic, GC behavior, diagnostic filter or test assertion was
changed to hide the error. An initial isolated build was blocked before any
native run; the subsequent cohort used only the already installed SDK and
existing cached packages, with package sources cleared.

One bounded, unchanged-source canonical recheck was then run, with a stop-on-any-
failure rule rather than retrying until success. `tools/godot/verify-native.sh`
completed without interruption from **04:35:57 to 05:11:28 UTC** on 2026-11-05,
**35m31s, exit 0**. This later passing run does not erase the first failure or
prove its root cause fixed.

- All source-selection negative controls, whitespace, source metadata and four
  authored-map validators passed. Metadata: 178 unique script UIDs and 4,194
  intentionally unimported historical audit files.
- Both Debug builds passed with zero warnings/errors; all 15 core groups passed.
  Sixteen independent formal battles completed 3,425 legal commands, with exact
  baseline ordered action/event/receipt hashes, final digests and save/replay.
- All 30 native scenes and editor import passed their strict process/diagnostic
  boundary. All 31 complete logs were retained. Forty warning headers exactly
  match the preceding complete gate; no error/leak/crash diagnostic occurred in
  this recheck, including MoraleFeedbackChecks shutdown.
- HUD passed 1,021 assertions, dossier focus 73, morale feedback 74, animation
  967, campaign navigation 9,107 with zero failed groups, staging opening 160,
  lighting lifetime 106, lighting contracts 1,791 and scenery/resource contracts
  8,753. Navigation assertion totals include per-frame checks and are not a fixed
  coverage metric; all original groups remained enabled.
- ExportRelease configuration compilation subsequently passed with zero
  warnings/errors. The verified Debug assembly hashes remained unchanged after
  that build. This is not a packaged export acceptance run.

The intermittent first shutdown diagnostic remains an unresolved observation,
not a claimed production fix. If it recurs, its preserved evidence should guide
resource-lifetime investigation rather than suppression or blind retries.

Original artwork, maps, gameplay rules and save semantics are unchanged. Manual
GUI rendering, physical-device latency and a packaged desktop acceptance run
remain separate. Publication is ordinary fast-forward to the existing feature
branch; its push does not imply hosted CI because the workflow triggers on main,
pull requests or manual dispatch.

## Godot references

- [General optimization](https://docs.godotengine.org/en/stable/tutorials/performance/general_optimization.html): measure representative bottlenecks and compare candidate results.
- [Scene organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html): keep ownership and dependencies explicit.
- [Node](https://docs.godotengine.org/en/stable/classes/class_node.html): MoveChild preserves mounted-node lifetime; QueueFree defers disposal.
- [ScrollContainer](https://docs.godotengine.org/en/stable/classes/class_scrollcontainer.html): preserve the existing layout-aware scroll/focus handling.
