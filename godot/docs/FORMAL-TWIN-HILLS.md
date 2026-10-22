# Formal Twin Hills: registered painted battlefield

The native `c01-01` uses `data/scenes/c01-01-painted-ground.json` and the unchanged
1536 × 1024 `assets/native/scenes/c01-01-painted.png` motherplate. This is a
registered whole painting with same-image foreground polygons, not independently
assembled buildings, trees or an interchangeable modular asset kit.

## Ownership and scale

`grayflag-campaign-levels.json` owns the 36 × 30 terrain, elevation, cliff edges,
structure sites and 19 starting actors (11 friendly, 8 enemy). `BoardProjection`
owns their shared affine projection, continuous slope surface and inverse pick.
There are no platform-specific coordinate systems or visual-only elevations.
The painted registration validates the source level's canonical digest before use.

The image registration is one source-pixel basis:

- origin: (13.9748278, 89.65361629)
- cell X: (36.37115073, 0.20583468)
- cell Y: (6.17017828, 23.75958941)
- world scale: 0.8798181899041609
- each height level: 12 world units, approximately 13.64 source pixels

The two low platforms reach height 2. Three lanes approach the north platform
from below; two lanes approach the south platform from the west. All pass
continuously through heights 0, 1 and 2. Other visible grassy
approaches remain open. Twenty-one finite rock edges block ground traversal;
these are not closed fortress perimeters. The southern front rock band is not an
extra ramp. Outer woodland and painted tree trunks are outside the playable area;
736 non-outside cells include 24 blocked building-body cells.

The four interactive entrances are (3,16), (18,13), (22,16), (34,13). Their original
fort/village functions and owners are preserved. Same-image polygons own their
foreground silhouettes and selection; no second building sprite is rendered.
All actors retain their AssetCatalog scale and anchors. Entry focus is (13,14),
with the existing 1.25 camera zoom and movable camera, not a forced full-map fit.
The original six scenery placements and 24 frame placements are explicitly gone.

## Intentional tactical differences

This is a reauthored level, not coordinate parity with the historical JavaScript
map. Grass platforms use hill rather than opaque mountain terrain. Mounted units
can now climb their visible ramps. Forest terrain was not painted onto open
meadow merely to reproduce the old cell counts. Heights still use the existing
climb/drop, movement cost and damage rules; no movement profile changed.

The northern scout and shield start six grid steps apart rather than eleven.
The rushed Torren starts at (15,7), genuinely on height 2, five steps from the
northern shield. His health remains `min(existing/default 98,72)`, not a heal.
Normal Torren starts at (10,15). The main frontline route is longer and the south
mounted reinforcement is slower: terrain-only path costs are 18 versus 15 and
32 versus 23 respectively. The latter is approximately five movement turns to
adjacency versus four under a fresh-budget segmentation, not a combat simulation.

All actor identities, non-coordinate attributes, four site roles, objectives,
choice flags, victory/defeat rules and story files are retained. The story's
Roderick “one rescue” wording still has no implemented one-shot rescue event;
this change does not add one. The old staging-card illustration remains separate
art and does not establish runtime geometry.

## Verification status

Validation on 2026-10-22:

- Final build: no warnings or errors. Whitespace and source metadata checks passed.
- Focused native registration check passed: canonical actor feet, all four
  entrances, no duplicate building sprites, two-hill movement and connectivity,
  injured/default rush health, elevated depth and inverse picking.
- Actual unmodified game: entered both choices through the campaign UI. The rush
  entry put Torren on the visible northern platform with 72/98 health.
- Actual steady game: moved the northern guard along the visible dirt ramp from
  height 0 through 1 to 2 across normal turns; moved the western camp guard around
  the house side and behind its roof. The roof hid the rear actor without hiding
  the door guard, and movement did not cross the building-body walls.
- F5/F9 restored the earlier turn and unspent movement in the native window. A
  later actual save also retained the guard on height 2 and the rear-house actor.

The first actual entry exposed an out-of-frame foreground polygon, which was
clipped to the image boundary before this acceptance. The first focused check
then failed because its health assertion requested an integer from a policy
value encoded as a double; the corrected numeric assertion passed on rerun.
Neither earlier failure is counted as a pass.

This is not a complete-campaign victory, balance, export, performance or full-suite
pass. Updated camera/material fixture coordinates compiled but their full scenes
were not rerun. The historical JavaScript policy oracle still expects the old
rush coordinate; pre-existing battle-screen and material-scene checks also retain
unrelated stale assumptions. They were not rewritten to manufacture an all-green
report. Revisit those acceptance fixtures when their respective scenes are next
maintained, without restoring obsolete scenery or changing the historical oracle.
The cloud desktop used a software renderer and dummy audio; those are not target
hardware performance or audio acceptance results.
