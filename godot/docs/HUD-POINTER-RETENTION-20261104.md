# HUD pointer retention, 2026-11-04

## Reachable workload and cause

Baseline: `642a46bb7baf0cd9048f683c5baabfea38f4911c`.

The witness starts the real application with isolated saves, completes the opening
story and choice, enters C01-01, and selects its real actor using native Space
input. It finds 16 legal ground cells through the existing movement query, board
projection/picking and native GUI hover owner. Native mouse events visit those
cells while the actor stays selected and the battle digest and revision remain
unchanged.

The measured boundary is synchronous native input handling, with two process
frames between events. This is a headless CPU measurement, not GPU timing, a
rendered visual comparison, physical mouse latency or a device FPS claim.

Initial profiling separated map picking, authoritative queries, overlay generation
and HUD application. Applying precomputed HUD projections retained the dominant
cost. Removing old controls and texture references cost about a millisecond each;
recreating text across visible sections and the hidden dossier dominated instead.

Causal controls, each with the same 16 projection shapes:

- Keeping strong references to the three HUD chrome textures did not improve it.
- Constructing sections off-tree before mounting them did not improve it.
- Original text took 11,774.701 ms, equal-character-count ASCII took 347.919 ms,
  and an experimental explicit CJK SystemFont took 3,914.824 ms. Those text/font
  substitutions are measurement controls only; neither is in production.

The shipped UI relies on the existing font fallback setup. Recreating unchanged
Chinese text on every changed hover repeatedly incurs that text-processing cost.
No new font, global resource cache, domain-query cache or platform-specific font
path was introduced.

## Ownership and implementation

`GrayFlagSession.ProjectHud` continues to own the immutable presentation facts.
`TacticalHud` compares its previous projection with the new one and owns native
widget lifetime; there is no second writable copy of battle state.

Within the same battle stamp and busy state, unchanged sections remain mounted.
The unit section also depends on its portrait, including the existing tray and
medallion. The dossier's Details container remains mounted, changed sections are
replaced, and native MoveChild restores the projection's ordering without
unmounting unchanged controls.

Compound forecast/target blocks, dossier entry and bottom instructions still
rebuild because their layout and behavior depend on facts outside a single
section. Their existing generation, stale-input and subscription guards remain.
ConfigureArt, unmounting, stamp changes and busy changes invalidate retention.

A retained disclosure checks the live HUD/control lifetime and a current
projection instead of its creation generation. An unmounted or re-mounted HUD
without a fresh Apply, and detached old controls, remain inert.

The existing dossier focus regression caught a necessary integration change:
restoring the reading position in the same deferred queue was too early when
content before a retained focused button grew. Scroll restoration and visibility
now wait for the next native process frame, guarded by the current generation and
node lifetime. This follows ScrollContainer's documented layout timing. A
manually selected reading offset is still preserved when focus was already off
screen before the refresh.

## Measurements and regression evidence

Same phase-separated 16-cell workload, milliseconds of synchronous work:

| Boundary | Baseline | Initial fixed result |
| --- | ---: | ---: |
| Native changed-cell input | 8,087.302 | 1,959.550 |
| Precomputed HUD Apply, sample 1 | 12,389.739 | 1,907.035 |
| Precomputed HUD Apply, sample 2 | 10,047.799 | 1,716.982 |
| Precomputed HUD Apply, sample 3 | 11,232.495 | 1,738.122 |

Native input CPU time fell about 76% in that paired phase; isolated HUD Apply
fell about 83–85%. The permanent real-flow check then measured 1,853.687,
1,967.145 and 1,727.245 ms across three 16-cell sweeps. These are environment
measurements, not timing thresholds in the test suite. Per-hover domain
projection and overlay work still runs through its original authoritative path.

The permanent CampaignNavigationPointerChecks guard verifies stable native
section/portrait/action instances, current terrain text against the authoritative
projection, objective expansion, native pointer opening and keyboard disclosure
activation, close-to-opener focus, and an unchanged canonical battle digest.
Running this guard with the exact baseline TacticalHud fails on its first
measured hover; the retained implementation passes all 222 assertions in the
isolated two-group runner. The complete campaign runner is restored in source.

The added HUD lifecycle fixture checks insertion order, changed hidden action
eligibility, busy state, section removal, retained disclosure state, remount
without Apply, and detached stale callbacks. Existing portrait, hint, forecast,
native button, focus, multi-row layout and teardown checks remain enabled.

The pre-layout-frame version failed the unchanged DossierFocusChecks assertion
for focus visibility after earlier content grows. The next-process-frame version
passes its 73 assertions, including both window sizes, deliberate reading offsets,
real-session adjustments, closure and unloading.

Removing only the current-projection disclosure guard from the fixed implementation
also fails, specifically at the remount-without-Apply assertion (native exit 1).
The mutation was restored from the committed checkpoint before the full gate.

## Completed verification

Code checkpoint: `d3a8a5d27fca7b232d78cbe141d15debbfd62f7f`.

All 39 stages completed in one uninterrupted run from 04:14:56 through 04:51:20
UTC on 2026-11-04. Every stage recorded exit 0. The cohort includes all commands
in `tools/godot/verify-native.sh` plus ExportRelease compilation. The documented
process budgets were 900 seconds per native stage and 1,200 seconds for the
sixteen-level replay suite. No checks, assertions or teardown scans were skipped.

- Source selection negative controls, active-source whitespace, metadata and the
  four authored-map static contracts passed. Metadata: 165 unique script UIDs;
  4,190 intentionally unimported historical audit files remain outside delivery.
- Debug and ExportRelease builds passed with zero warnings and zero errors.
- All 13 core contracts passed, including 16 independent shipped levels with
  3,425 legal native actions and exact save/replay round trips. This is not a
  claim that one carried-state campaign was won from start to finish.
- All 25 native scene processes exited 0, with no ERROR, leak, segmentation-fault
  or unhandled-exception diagnostic in their logs.
- Full campaign navigation passed 8,783 assertions with zero failed groups;
  HUD 469, dossier focus 73, animation 844, result flow 371, campaign UI 205,
  lifecycle 106, lighting contracts 1,791 and scenery/resource contracts 8,753
  also passed. Existing native camera and battle integrations passed.
- The final full-run pointer samples were 1,767.145, 1,779.571 and 1,724.366 ms
  per 16 native changed-cell events. All measured sweeps retained the unchanged
  controls and updated the changed terrain text.
- The preceding idle-polling guard still measured exactly 153,600 managed bytes
  per 600 callbacks in all twelve opening/played/undo/load samples.
- Gate-script and compiled-binary checksums matched at completion, and the worktree
  was clean at the tested code checkpoint. The verification update is docs-only.

The original fonts, artwork, map, gameplay rules and save format are unchanged.
GPU rendering, manual visual acceptance and a packaged desktop acceptance run
remain separate from these headless checks.

## Godot references

- [CPU optimization and repeated measurements](https://docs.godotengine.org/en/stable/tutorials/performance/cpu_optimization.html)
- [Scene ownership and explicit dependencies](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html)
- [Font configuration and fallback](https://docs.godotengine.org/en/stable/tutorials/ui/gui_using_fonts.html)
- [ScrollContainer layout timing](https://docs.godotengine.org/en/stable/classes/class_scrollcontainer.html#class-scrollcontainer-method-ensure-control-visible)
