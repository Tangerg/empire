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
