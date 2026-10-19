# Legion-shield four-pose review

C01-UNIT-LEGION-SHIELD maps to c01.cain and c01.legion-shield. Original source board and HD/runtime strips are preserved under references/.

## Source identity
Stern clean-shaven adult, dark hair mostly covered by low angular steel helmet with projecting rim and broad cheek/neck flaps. Navy-blue-gray lamellar armor with red edging, gold shoulder cord/tassels, brown slatted bracer and leather belt. Huge bare screen-left hand carries short wooden spear with stepped triangular steel blade; screen-right arm holds tall cream chamfered rectangular shield with dark riveted steel border and exactly one centered vertical gold strip. Very short legs and huge brown cuffed boots.

## Geometry
Fixed44/909.5 body scale from accepted idle helmet crown60 to mean soles969.5. Common source1585x993; idle/walk-a need one transparent bottom row, attack/walk-b one transparent right column in export only. All whole canvases fit104x64 with origin8,4. No native master editing or independent silhouette fitting.

### idle
Accepted angular steel helmet, clean-shaven adult face, broad navy/red lamellar body, gold shoulder cord, huge bare spear hand and brown boots. Screen-left hand holds stepped triangular steel spear; screen-right arm holds cream chamfered shield with single gold vertical strip.
Source foot anchor [774, 971]; exact two-sole midpoint [773.65, 969.5]; whole-canvas logical offset [0.0, 0.0]; native visible bounds [260, 60, 1345, 972].

### walk-a
Front screen-right boot is toe-up with heel contact and dark sole visible; rear screen-left boot flat. Original spear hand, blade design, shield face and stripe remain unchanged.
Source foot anchor [751, 939]; exact two-sole midpoint [751.241, 938.0]; whole-canvas logical offset [1.112699285, 1.548103353]; native visible bounds [255, 56, 1350, 940].

### attack
Clear horizontal piercing spear thrust with extended original bare weapon hand and braced wide stance. Rigid spear shaft and stepped triangular blade project ahead of the retained upright shield. Same helmet, armor, shield stripe and physical equipment hands, no effects.
Source foot anchor [537, 954]; exact two-sole midpoint [536.613, 953.5]; whole-canvas logical offset [11.465640462, 0.822429907]; native visible bounds [94, 60, 1531, 955].

### walk-b
Opposite contacts: front screen-right boot is flat and weight-bearing; rear screen-left heel raised with toe down. Same head/body scale, equipment hands, spear blade and shield stripe.
Source foot anchor [798, 963]; exact two-sole midpoint [797.908, 954.5]; whole-canvas logical offset [-1.161077515, 0.387025838]; native visible bounds [258, 59, 1345, 964].

## Action contract
Existing idle/walk-a/attack/walk-b. c01.legion-spear is piercing melee with formation and anti-cavalry tags; Cain also has knight-charge. The shared attack slot visibly thrusts the spear while retaining the shield, with no extra action or mounted form.
The spear thrust retains the original physical weapon/shield hands, accepted blade shape and single shield stripe.

## Provenance and QA
All four selected masters are first successful native outputs. Idle is also retained under attempt-01. Exact prompts, request metadata, original source references, raw PNGs, measured sole bands and black/white QA are preserved. Every master/raw/native hash agrees. Native perimeter alpha<=1 with no silhouette crossing; alpha1 specks retained unchanged. No runtime/repository edits.
