# C06 remaining baked foreground trees — failing evidence

This is an unresolved **static fixture** audit, not gameplay or a completed fix.
The 96-object unbound candidate retains two tall bare trees in its clean ground.
Their roots are near source (93,166) and (70,185), respectively. Exact bark interior
witnesses overlap actual Torren idle alpha at legal forest cells (2,4) and (1,4),
while both roots lie in front of the actor contact y156. The ground is fixed at
Z=-4096 and neither tree has an independent object owner. Actor pixels therefore
cover these foreground trunks incorrectly. Exact source/clean/manifest hashes,
frame coordinates and alpha samples are in the adjoining JSON.

Native initial-state captures `tree-depth-2-4-old0.png` and
`tree-depth-1-4-old0.png` reproduce this candidate defect; corresponding `old1`
images show the painted baseline. These explicitly labelled diagnostic poses do
not issue engine commands, read the paused real save or advance enemy AI.

A repair must remove only the proven baked trees, restore their separate rooted
foreground objects, and retain transparent gaps and legal cell picking. Do not
replace all top-border background forest or invent roots for hidden trees.

## Bounded repair and new static verification

The unbound 98-object preview now uses two independent transparent trees and the
v5 ground with their baked counterparts erased. Root bases remain (93,166) and
(70,185); forward wood roots have their own physically measured depth anchors.
The front tree's final source scale is .063 (the early .060 trial is not final).
No rules, map cells, existing bridge objects or production campaign binding changed.

Fresh native initial-state fixtures cover six legal cells. At rear-tree back pose
(2,4), 152 shared body/tree pixels correctly do not pick the actor; at front pose
(2,5), all30 shared pixels pick the actor. The nearer tree gives76 hidden shared
pixels at (1,5) and15 actor-picking shared pixels at (1,6). All six poses retain
227–379 directly selectable body pixels. Forcing each tested tree back to Z=-4096
makes all five frozen sampled overlap points pick the actor again: the negative
controls reproduce the baked-background defect, rather than avoiding overlap.
`native-depth-result.json` is actual output; `native-probe.cs.txt` preserves its
renderer-only harness. It creates fresh state and issues no battle commands.

Same-window native screenshots are `tree98-final-depth-{1-4,2-4,1-6,2-5}.png`.
The preserved earlier `tree-depth-*-old0.png` images are failing 96-object previews;
`old1` is the historical painted baseline. Screens are diagnostic, not real play.

Affected shared probe:1134 legal centers,180 entrances,180 fixed body samples,
20 site/approach actor poses, zero rendering issues, original initial state digest
unchanged. Existing house-alpha and SW-cliff regressions and C06 canonical source
checks also pass. These are not full-campaign or normal-AI acceptance.

The ground edit changes whole-raster texture values. Three bridges showed no new
obvious edge displacement in +/-4pixel gradient comparison (best0,0), not exact
pixel equality. Previous bridge support failures and other wall/forest acceptance
debts remain open. Production still uses the prior scene; actual C06 is pausedT1.
