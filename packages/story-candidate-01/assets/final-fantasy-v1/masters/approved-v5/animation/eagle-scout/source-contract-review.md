# Eagle-scout source contract

The original native1254×1254 board is combat-atlas-ballista-cavalry-battlemage-eagle.png, fourth row. Identity must follow that row rather than the English label. The native artwork depicts a grounded quadrupedal, eagle-headed, fully feathered fantasy mount with folded wings and bird talons; three feet are visible in all four poses and the fourth is occluded. The remake makes four connected feet readable and does not turn the creature into a natural two-legged eagle or a lion-bodied gryphon.

The rider retains short dark hair, tan skin, pointed ears, olive cloth, brown/brass armor, brown boots, back quiver, brown shortbow in the forward screen-right hand and blue-gray reins in the near screen-left hand. Brown saddle, blue-gray breast harness, brass rings and diamond chest pendant are identity-bearing.

Canonical sheet256×48, four64×48frames, anchor[32,47], footprint[2,2]. Frame order idle/walk-a/attack/walk-b. HD sheet512×96 at density2. Legacy visible idle48×46. There is no actual runtime unit binding. Design documentation calls it flying reconnaissance, but this is neither a consumer nor an implemented attack rule. The grounded source posture is preserved.

Proposed new scale52/944: native hair crown y17 to mean near hind/front claw soles y961. Whole mounted body uses one scale, compared against wolf52, stag54, cavalry56 and legacy46 silhouette. No independent rider, mount, feather, bow or per-pose fit. Source body and ground support determine registration, never the rider boot. Native1586×992full canvas is retained, including alpha1 boundary samples.

All art is generated via built-in imagegen. Master and raw are immutable byte-identical copies of the generated PNG. QA backgrounds, source reference crops, scale comparison and registration measurements are diagnostic only. No source cleanup, recolor or programmatic art painting.
