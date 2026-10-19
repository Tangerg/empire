# C01-UNIT-RUNE-ARTIFICER source and action contract

The canonical topic is combat-unit C01-UNIT-RUNE-ARTIFICER, asset C01-FFV1-UNIT-RUNE-ARTIFICER. Original runtime strip128x48, HD256x96, four cells ordered idle / walk-a / attack / walk-b, original32x48 frame, anchor[16,47], footprint1x1. These gameplay/semantic dimensions remain unchanged; enlarged presentation masters are separate.

Full high-resolution source is masters/combat-units/mountain-forge-rune-artificer-source.png,1672x941, rather than a row in one of the later1254x1254 multi-role atlases. Its four horizontal cells are preserved intact with hashes. Idle source cell[0,0,418,941], tool detail[28,375,387,641], face detail[95,245,306,445] are unresampled reference crops. The HD four poses and whole board were visually inspected.

## Actual identity and consumers

candidate-01-bindings.ts:27 binds only c01.rune-artificer to this character. units.ts:85 identifies the campaign forge artificer with heavy armor and infantry/engineer/arcane/forge tags. c01-14, 山炉余烬, includes enemy rune-rebel-2 at deployment index3 position[15,7], and south-artificer at index7 position[15,12] (levels.ts:740-746). No builtin consumer was found. This is distinct from c01.rune-shield even though both share an equipment topic.

The actual character is a clean-shaven tan young adult with black side braid, dark work cap and single flipped-up brass-rimmed lavender-gray lens. Slate shoulder/arm armor, four-rivet left shoulder patch, gray collar, brick-red apron, charcoal skirt, brown gloves/belt/boots, front silver chisel and orange/black geometric belt plaque. Original screen-left hand carries the compact rectangular steel hammer; screen-right hand raises the open brass arched rune-tool with three blue leaf pendants. No shield, beard, staff or alternate species is inferred from role name or equipment icon.

The arched tool has a real open aperture showing source magenta background; it must be transparent. The hat lens instead has solid pale glass. Preserve the original angular marks as simplified industrial marks without invented religious meaning, alphabet or new crest. Generated idle simplifies the small original hammer angular-loop inset into an angular diamond; the belt plaque remains a separate orange/black geometric shape. This is a stylized simplification, not pixel tracing.

## Weapons and four semantic slots

rune-hammer is blunt melee (weapons.ts:92); forge-burst is2-range magic with armor_down2turns and cooldown2 (93-103). Both map to C01-EQUIP-RUNE-ARTIFICER (bindings112-113), with separate C01-FX-04 and C01-FX-07 (156-157). Source hammer/rune-tool are compatible with these rules. Preserve original hands across all poses.

The legacy attack is almost idle and the two legacy walk contacts are not convincingly opposite. Reconstruct a visible raised-hammer action and opposite walking contacts. The existing single action animation is used by both weapons; this limitation remains explicit. No fifth pose, rule changes, baked glow, separate projectile or impact burst is added.

## Native preservation

Approved chunky blue-swordsman/concept-v2 style is the scale/proportion authority. Idle is genuine native RGBA1586x992; body crown55 to mean boot soles969 gives914px, proposal44/914 and common foot[802,971]. Full native canvas and alpha are preserved byte-for-byte. Raw/master/exact prompt/request/QA and source hashes are retained; remaining poses await idle review before generation. Any later size discrepancy must be explicit transparent right/bottom padding only, not resampling or independently fitting poses.
