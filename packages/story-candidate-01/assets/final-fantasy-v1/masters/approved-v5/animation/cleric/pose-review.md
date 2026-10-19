# Cleric four-pose review

C01-UNIT-CLERIC is bound to the built-in engine cleric. It is not the chapter gravekeeper/Mirelle art. Original master board and HD strip are preserved under references/.

## Accepted identity
Gray-haired clean-shaven hooded adult; patched charcoal cape, blue-gray quilted tunic, cream sleeves, large bare hands and boots, belt scroll/pouch/two pale tags, short rigid spiked mace and candle lantern. The flame remains contained in the glass.

## Geometry
All selected native masters are1585x992. Body scale is locked at44/883 from the accepted idle hood crown61 to mean sole944. Masters/raw are untouched native outputs. Recommended104x64 full-canvas export at origin8,4 fits every registered canvas with positive margins. No canvas-size padding is needed.

### idle
Accepted broad squat idle; huge hooded adult head and hands, both broad boots planted. Low compact mace and upright candle lantern.
Source foot anchor: [784, 948]; exact two-sole midpoint: [783.536, 944.0]. Whole-canvas logical offset: [0.0, 0.0]. Visible native bounds: [273, 61, 1321, 949].

### walk-a
Front screen-right boot toe-up/heel-contact with dark underside visible; rear screen-left boot flat. Moderate torso rise from stride without independent rescaling.
Source foot anchor: [729, 948]; exact two-sole midpoint: [729.402, 941.5]. Whole-canvas logical offset: [2.740656852, 0.0]. Visible native bounds: [282, 26, 1348, 949].

### attack
Compact raised-mace windup with wide braced stance. Patched shoulder arm retains mace, unpatched far arm holds lantern. Entire steel head/spikes and lantern intact; face unobscured. First attack attempt swapped equipment hands and is retained as rejected; attempt02 corrects the arm assignment.
Source foot anchor: [710, 917]; exact two-sole midpoint: [710.228, 893.5]. Whole-canvas logical offset: [3.687429219, 1.544733862]. Visible native bounds: [182, 18, 1384, 918].

### walk-b
Opposite contacts: front screen-right boot flat and weight-bearing; rear screen-left heel raised/toe down. Both boots readable; same mace/lantern assignment.
Source foot anchor: [808, 932]; exact two-sole midpoint: [808.031, 927.0]. Whole-canvas logical offset: [-1.19592299, 0.797281993]. Visible native bounds: [288, 59, 1325, 933].

## Semantics
The existing four-frame contract remains idle/walk-a/attack/walk-b. Attack is a raised compact mace action credible for blunt melee. Heal currently shares the action animation contract; there is no support-only replacement or fifth frame.
The engine weapon id cleric_staff denotes blunt melee. Original drawn mace/lantern identity takes visual precedence; no invented staff, sword, shield or new icon was introduced.

## Provenance and QA
Exact prompts and native raw PNGs are retained for each pose and both attack attempts. Attack attempt01 switched the equipment hands; selected attempt02 restores mace to the patched-shoulder arm and lantern to the other arm. Black/white composites were visually reviewed; all masters pass perimeter alpha<=1 and zero silhouette alpha>1 at the edges. No manual painting, recolor, resampling, crop or alpha cleanup. No runtime/repository edits.
