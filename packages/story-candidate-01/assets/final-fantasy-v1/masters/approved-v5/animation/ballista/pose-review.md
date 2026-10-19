# Ballista four-pose review

C01-UNIT-BALLISTA is the actual character-plus-machine combat unit bound to c01.ballista and built-in ballista. The original board, first-row/idle crops and runtime-HD/runtime strips are preserved in references/.

## Scale and rules contract
Accepted idle complete-unit visible alpha>127 width 1509 source pixels mapped to 76 logical pixels. Not a humanoid 44-high fit.
Idle helmet-to-exposed-boot comparison is approximately 41.198 logical pixels in an operating stance. Parent reviewed beside 44-high standing infantry; no forced body-height scale.
Canonical wheel-ground anchor [922,983]. Common canvas 1586x992; three native 1585x992 frames need one transparent right column in export only. The old rules footprint remains 2x2. The old 64x48 frame is evidence of the source contract, not a crop limit for the new art. A 104x64 display frame with origin [8,4] contains every complete registered native canvas.

### idle
Accepted complete four-wheel wooden ballista and one large-headed imperial operator. Steel kettle helmet/cheek flaps, navy/red armor, gold cord, huge bare hands and brown boots; thick timber carriage, metal caps/brackets, disk wheels, rope string and loaded steel-tipped bolt aimed right.
Wheel-ground anchor [922, 983]; measured three-contact midpoint [921.826, 935.0]; logical whole-canvas offset [0.0, 0.0]; native visible bounds [57, 36, 1566, 984].

### walk-a
Operator leans forward pushing both original rear controls. Exposed rearward leg has heel raised/toe contact; opposite boot bears weight forward behind the rear wheel. Diagonal disk-wheel plank seams show rolling. Loaded bolt and complete chassis retained.
Wheel-ground anchor [932, 975]; measured three-contact midpoint [931.933, 928.333]; logical whole-canvas offset [-0.503644798, 0.402915838]; native visible bounds [31, 44, 1562, 976].

### attack
Unmistakable post-release firing moment: loaded bolt has left the guide, rope string is forward, bow assembly has relaxed and operator pulls the rear control up while bracing. Empty guide and visible released string distinguish attack from idle. Four wheels, complete permanent chassis and operator identity retained. No detached projectile or external effects are added.
Wheel-ground anchor [923, 984]; measured three-contact midpoint [923.356, 936.0]; logical whole-canvas offset [-0.05036448, -0.05036448]; native visible bounds [57, 33, 1544, 985].

### walk-b
Opposite pushing contact: the forward boot is toe-up/heel-contact beside the rear wheel, opposite boot trails with heel raised. Disk-wheel seam rotation differs from walk-a. Same operator, mechanical proportions, four wheels and loaded right-facing bolt.
Wheel-ground anchor [928, 980]; measured three-contact midpoint [927.727, 932.0]; logical whole-canvas offset [-0.302186879, 0.151093439]; native visible bounds [37, 43, 1564, 981].

## Action semantics
Exact C01-UNIT-BALLISTA combat-unit contract: idle, walk-a, attack, walk-b. The original source includes the operator; neither equipment-only art nor the separate deployment platform substitutes for the unit. Gameplay remains heavy siege/ranged, piercing range 2-4, no move-and-attack. Walk frames are opposite pushing phases; attack is the post-release shot. The consumed bolt is absent only in attack, while the whole permanent vehicle remains intact. The existing separate projectile contract is not changed or duplicated here.
Operator feet are animation evidence, not the registration pivot. Registration uses the same three observable wheel supports; the occluded fourth contact is never invented. A changed wheel-disk seam orientation signals rolling without adding spokes or changing diameter.

## Provenance and QA
All four selected masters are first successful native built-in image-generation outputs. Exact prompts, requests, raw PNGs, source references, wheel measurement ROIs and black/white QA are saved. Idle is additionally retained under attempt-01. Every native/master/raw hash and exact prompt hash was verified. No generated master was resized, cropped, recolored, repainted or alpha-cleaned; no runtime or repository write was made.
