# Knight four-pose review

C01-UNIT-KNIGHT maps to c01.knight and built-in knight. Original source board and HD/runtime strips are preserved under references/.

## Source identity
Stern bearded adult with rounded segmented steel helmet and cheek flaps. Cream ivory riveted armor with red edging over dark chainmail bands, brown gloves/bracers and belt, very short legs, huge brown cuffed boots with small spurs. Screen-left glove carries short broad straight silver sword; screen-right arm carries a pointed oval olive-brown shield with muted metal rim and gold branching vertical mark. No horse appears in the original knight source or remake.

## Geometry
Fixed44/899 body scale from accepted idle helmet crown57 to mean soles956. Every native canvas is1586x992, requiring no padding. All whole canvases fit104x64 with origin8,4. No native master editing or independent silhouette fitting.

### idle
Accepted bearded foot knight in rounded steel helmet, broad cream/red riveted armor, chainmail bands, huge brown gloves and boots with spurs. Screen-left sword hand and screen-right dark pointed oval shield with gold branching mark retain original source identity.
Source foot anchor [813, 959]; exact two-sole midpoint [813.404, 956.0]; whole-canvas logical offset [0.0, 0.0]; native visible bounds [382, 57, 1204, 960].

### walk-a
Front screen-right boot toe-up/heel-contact with dark sole visible; rear screen-left boot flat. Same sword/shield hands, gold branching mark, armor, helmet and attached spurs.
Source foot anchor [795, 950]; exact two-sole midpoint [794.763, 947.5]; whole-canvas logical offset [0.880978865, 0.440489433]; native visible bounds [387, 56, 1226, 951].

### attack
Clear compact forward sword thrust, original brown gloved weapon hand extended, blade horizontal ahead of the retained upright shield. Braced short-legged stance, focused face, gold branching mark preserved. No horse, lance, effects or equipment-hand swap.
Source foot anchor [624, 943]; exact two-sole midpoint [624.291, 938.5]; whole-canvas logical offset [9.250278087, 0.783092325]; native visible bounds [234, 48, 1416, 944].

### walk-b
Opposite contacts: front screen-right boot flat and weight-bearing, rear screen-left heel raised/toe down. Same foot-knight identity, equipment hands, shield mark and matte armor.
Source foot anchor [831, 944]; exact two-sole midpoint [831.01, 941.5]; whole-canvas logical offset [-0.880978865, 0.734149055]; native visible bounds [384, 52, 1230, 945].

## Action contract
Existing idle/walk-a/attack/walk-b for exact canonical C01-UNIT-KNIGHT. Preserve original foot sword/shield identity and depict a compact sword thrust. Gameplay c01.knight remains mounted with lance/charge; that pre-existing semantic mismatch is documented, not changed or claimed fixed. No remap to distinct C01-UNIT-LANCE-CAVALRY.
The sword thrust retains the original physical weapon/shield hands and gold branching shield mark. The mounted/lance gameplay versus foot sword/shield art mismatch remains explicitly unresolved; source-contract-review.md preserves the evidence. This redraw does not remap the separate cavalry topic or claim to fix gameplay semantics.

## Provenance and QA
All four selected masters are first successful native outputs. Idle is also retained under attempt-01. Exact prompts, request metadata, original source references, raw PNGs, measured sole bands and black/white QA are preserved. Every master/raw/native hash agrees. Native perimeter alpha<=1 with no silhouette crossing; alpha1 specks retained unchanged. No runtime/repository edits.
