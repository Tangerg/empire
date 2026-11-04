# Idle recruitment guidance query, 2026-11-04

## Reachable workload and cause

Baseline: `6ab68f94d161599857d958b86e8af3331a5833ae`.

The real C01-01 pointer witness now also closes its dossier, returns native
button focus to the map, cancels the unit selection with the next Escape, and
moves across the same 16 reachable ground cells while idle. This follows the
existing two-step focus/back behavior; it does not change keyboard semantics.

The baseline idle sweep allocates 76,475,232 managed bytes. Every ProjectHud call
constructs a second detached tactical snapshot and scans all interactive cells to
answer one unchanged question: can the player open any recruitment panel? Prior
phase-separated profiling measured 227,934,312 bytes for 48 idle HUD projections.
The new real-input allocation guard rejects the unchanged baseline at exit 1.

The first three fixed idle sweeps allocate 8,579,024, 8,583,848 and 8,583,848 bytes,
about 88.8% less than baseline. The selected-unit sweeps remain 16,572,744 bytes.
The complete isolated native workflow passes 278 assertions and exits 0.

Baseline synchronous idle input took 1,717.232 ms for the sweep; initial fixed
samples took 1,346.157, 1,317.094 and 1,305.479 ms. These are environment samples,
not a latency guarantee or device FPS measurement. Managed allocation is the
regression budget: an average below 2 MiB per visited cell. This is native
headless input/control testing, not rendered visual acceptance.

## Ownership and boundaries

Battle remains the sole writer. Successful Commit and Undo advance the session
revision, and load creates a new session. A private session-owned tuple retains
only the recruitment-availability boolean for its revision. The cold path still
asks GetTacticalQueries.InteractiveCells.Any(CanOpenRecruitment); it introduces
no recruitment formula and caches neither a mutable adapter nor HUD data.

Changing hover, selection, busy state or view binding does not change this domain
fact. Each HUD projection still has its current stamp and text. The cold query's
revision checks finish before the cache is published, and the HUD's final revision
check now follows the complete projection, including the recruitment query.

The preserved question is whether the panel can open, not whether an offer can
be purchased. Zero money or a full army does not by itself hide recruitment
cards; actual purchase validity remains the authoritative command's decision. The canonical query still filters outside cells and tests occupancy
against the full state, including invisible units. No map, art, recruitment rule,
player save or save meaning is changed.

## Verification status at implementation checkpoint

The native allocation baseline and fixed real-input results above are complete.
The isolated recruitment contract passes for all 16 shipped levels and explicit
detached castle fixtures. It covers current/neutral/enemy ownership, zero money,
army limits, refused purchases, a real recruit and undo, movement away from an
occupied site and undo, hidden occupancy, session/load/binding isolation, and
human/AI/deployment/terminal guidance. Query reads preserve canonical state and
ordered logs; stale and reentrant projections must fail explicitly.

Five deliberate contract mutations compile and fail their intended runtime
assertions: keeping a result across revisions, sharing it globally, substituting
affordability for panel admission, ignoring invisible occupants, and removing the
HUD's final revision check. A sixth mutation skips idle pointer HUD refresh; the
native test rejects stale terrain/coordinate labels rather than mistaking a
frozen interface for a performance improvement. Both selected and idle pointer
loops compare the actual native labels with the canonical per-cell projection,
outside the allocation sampling boundary.

The strengthened final targeted run passes 327 native workflow assertions and
the full new recruitment contract, both at exit 0. Its three idle sweeps allocate
8,579,504, 8,583,848 and 8,584,088 bytes; all selected-unit sweeps still allocate
16,572,744 bytes. Final targeted Debug compilation has zero warnings and errors.
Both complete native scene entrypoints and the original hover implementation
are restored before the implementation checkpoint.

## Completed full verification

Code checkpoint: `dae84481576a929853c96e168b248fc0a250fd81`.

All 39 stages completed in one uninterrupted run from 06:24:22 to 07:06:00 UTC
on 2026-11-04. Every stage and the overall runner exited 0. The run includes all
commands in `tools/godot/verify-native.sh` plus ExportRelease compilation, with
the supported 900-second per-native-stage and 1,200-second replay-suite budgets.
The complete scene entrypoints, source tree, runner and tested binaries remained
unchanged throughout; no assertion groups or teardown scans were omitted.

- Source-selection negative controls, active-source formatting, 167 unique script
  UIDs and all four authored-map static contracts passed. The 4,190 historical
  audit files remain intentionally unimported and outside delivery.
- Debug and ExportRelease compilation passed with zero warnings and errors.
- All 13 core contracts passed, including 16 independent shipped levels, 3,425
  legal native actions and exact save/replay round trips. This is not a claim of
  a complete carried-state campaign victory.
- All 25 native scenes passed. The full battle scene includes both the movement
  field and recruitment contracts. HUD, dossier focus, result flow, campaign UI
  and lighting lifetime passed their 469, 73, 371, 205 and 106 assertions;
  complete campaign navigation passed 8,826 assertions and every group.
  Animation, resource, formal-map and scenery suites also passed.
- Strict native teardown scans found no ERROR, leaked-resource, segmentation-fault
  or unhandled-exception diagnostics. Existing negative-fixture WARNING output
  remains expected; this is not a claim of zero runtime warnings.

In the complete navigation run, idle sweeps allocated 8,579,264, 8,584,088 and
8,583,848 bytes, again about 88.8% below baseline. Selected-unit sweeps allocated
16,572,744, 16,572,744 and 17,150,400 bytes and passed their existing budget.
Idle CPU samples were 3,709.947, 2,234.209 and 1,857.374 ms, illustrating why the
verified allocation improvement must not be presented as a wall-clock or FPS
guarantee. Rendered visual and device acceptance remain separate.

## References

- [Godot CPU optimization](https://docs.godotengine.org/en/stable/tutorials/performance/cpu_optimization.html): measure representative work and verify the change.
- [Godot scene organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html): focused ownership and explicit dependencies.
- Repository `AGENTS.md` and `PROJECT_RULES.md`: one owner per fact, canonical queries, measured optimization and meaningful negative controls.
