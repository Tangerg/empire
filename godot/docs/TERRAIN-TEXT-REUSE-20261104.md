# Terrain inspection text and dossier input ownership

Date: 2026-11-04 UTC
Baseline: `db4b47180b265c29ffa187a414c3bb867f9c46b6`
Code checkpoint: `007c474595262849298536b6dc838b995b93124c`

## Reproduction and ownership

The unchanged C01-01 campaign was entered through New Campaign, story/choice, staging and Start Battle. Native Space selected a real actor. The existing pointer check collected 16 reachable ground cells, pushed real viewport mouse events, checked the rendered terrain labels against `GrayFlagSession.ProjectHud`, and verified the battle digest and revision stayed unchanged.

Manual phase timing on the baseline identified repeated tile-section construction as the dominant remaining HUD work: approximately 74% of measured selected-unit HUD phase time and 79% of idle phase time across three samples each. Timers were placed around native construction regions; diagnostic printing and instrumentation affect those measurements, so they identify a bottleneck rather than represent device frame rates.

A separate uninstrumented regression reproduced an input defect: after closing the dossier and receiving opener focus, moving the pointer over terrain destroyed that opener and lost keyboard focus. The original implementation failed the new real-session assertion (exit 1). This was not inferred solely from source inspection.

The session still owns all coordinates, terrain properties, movement costs and gameplay transitions. `TacticalHud` owns only their native rendering, disclosure state, focus and node lifetime. No terrain calculation, alternate state model or cross-session cache was added.

## Change

- Reuse a tile section only within its existing stamp/busy context when row count, order, keys and every nontext row field match. Both rows must remain read-only. Label and hint differences are allowed; selection, enabled state, icons, unit icons, gauges and intents retain their ordinary rebuilding path.
- Update existing labels, tooltips, hidden heading and disclosure text directly from the incoming projection. Unchanged strings are not reassigned. Native containers continue to own wrapping and minimum-size layout.
- Resolve disclosure titles from the current projection when toggled, rather than restoring an old captured terrain title.
- Keep the dossier opener mounted within the same context. Refresh its title and portrait-dependent width. Capture generation/stamp when pressed, and still reject a deferred open after any newer HUD projection, even with the same battle stamp.
- Preserve original stacking: move the retained opener after newly mounted portrait trays. The first candidate exposed this integration dependency through a real pointer failure; the hovered control was `PortraitTray`. The repaired path checks that the native pointer reaches the opener and that the click opens the dossier.
- Preserve invalidation on changed binding, revision, busy state, art configuration and unmount. Removed sections and detached controls cannot change current disclosure state or open a stale popup.

There are no changes to campaign/map data, town composition, fonts, textures, rules, save formats or save semantics. Compound forecast and instruction layout remain outside this local text-reuse change.

## Measurements

For 16 selected-unit mouse events per sample, the uninstrumented baseline recorded 1,995.791 / 2,944.916 / 1,954.421 ms of synchronous input processing. The successful repaired real-session run recorded 539.466 / 630.003 / 636.145 ms. Managed allocation was nearly unchanged: 16,572,744 versus 16,536,776 bytes per sample. This is primarily a native control/text work reduction, not a claim of a major managed-allocation reduction.

Successful repaired idle samples recorded 179.285 / 168.727 / 649.793 ms and 8,012,976 / 8,546,200 / 8,546,440 bytes. Host scheduling and cold work vary, so the report preserves individual observations. These are headless native input/CPU and layout measurements, not rendered visual acceptance, GPU measurements or device-FPS promises.

The final complete aggregate repeated the same real-session input measurement: selected-unit samples were 482.635 / 458.730 / 462.588 ms with 16,536,776 bytes each; idle samples were 220.300 / 227.070 / 203.636 ms with 8,013,216 / 8,546,200 / 9,122,136 bytes. All canonical label, focus, input and digest assertions passed.

## Regression and negative controls

The actual campaign flow checks current terrain labels on every selected and idle pointer cell, continued native disclosure activation, pointer access to the opener, close/hover/Space reopening without changing the actor, and unchanged battle state.

Focused native fixtures cover changed title/coordinates/hints, stable row controls and focus, long-text reflow sufficient for every line, preserved manual reading offset, independent row-shape/style/media/gauge/intent changes, section removal/reappearance, independent revision/binding/busy invalidation, art rebinding, remount without Apply, stale callbacks and current opener title/width. A held dossier pointer also survives a same-context terrain refresh before release.

The focused positive control passed 55 assertions. Six deliberate production mutations compiled successfully and each exited 1 at its intended guard:

1. Disable tile reuse: stable native terrain controls/focus guard.
2. Omit tooltip updates: current terrain facts guard.
3. Restore captured disclosure title: current-title-after-toggle guard.
4. Ignore gauge differences: changed row-shape guard.
5. Ignore busy invalidation: independent context invalidation guard.
6. Drop the deferred generation check: same-stamp superseded-open guard.

Mutation sources and reduced runners were restored before the clean checkpoint. One mutation-harness setup attempt failed its exact-source anchor before changing source or running a mutant; that setup failure is not counted as a rejected mutation. Early candidate failures and their logs were retained, not reported as passing verification.

## Full verification

The complete 39-stage repository gate ran continuously on the clean code checkpoint from **07:43:44 to 08:20:45 UTC**, with every stage recording exit 0. This was an uninterrupted completed run, not a combination of presumed successes. The commands are `tools/godot/verify-native.sh` plus ExportRelease compilation, run through a stage journal with isolated save data for every native scene, bounded process timeouts, checkpoint verification and script/binary hashes before and after.

- Source-format selection negative tests, full active-source formatting, source metadata and all four authored-map static validators passed. Metadata reports 167 unique script UIDs and the existing 4,190 intentionally unimported historical audit files.
- Debug game, core-tests and ExportRelease builds succeeded with zero warnings/errors.
- All 13 core contracts passed, including 16 independent shipped levels, 3,425 legal native actions and exact save/replay round trips. This is not a claim of one complete carried-state campaign victory.
- All 25 native scenes exited 0. Strict scans found no engine errors, leaks, unhandled exceptions or segmentation faults. Expected fixture/source-readiness warnings remain explicitly classified in the existing suites.
- Full HUD: 510 assertions. Dossier focus: 73. Full campaign navigation: 8,743 assertions, zero failed groups, including the new genuine close/hover/Space interaction and the existing legally played opening victory.
- Animation: 844 assertions. Battle result flow: 371. Lighting contracts: 1,791. Scenery/resource/lifecycle: 8,753. Existing geometry/resource checks are not labeled visual acceptance.

The code checkpoint was unchanged throughout the aggregate; the accompanying documentation commit only records these results. No GUI-rendered visual acceptance was performed in this environment.

## Godot guidance

The approach follows [measuring CPU bottlenecks before optimizing](https://docs.godotengine.org/en/stable/tutorials/performance/cpu_optimization.html), [explicit scene dependencies and focused ownership](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html), and the native [node child-order contract](https://docs.godotengine.org/en/stable/classes/class_node.html#class-node-method-move-child). It keeps the existing injected art and immutable presentation boundary rather than creating a general HUD diff framework.
