# Javelin hunter source review

Visually verified FOURTH row of original1254x1254 board. Preserved complete board, native unresampled row[0,918,1254,1254] and complete idle cell[0,918,314,1254]. Chose y918 rather than nominal y940 quarter boundary to preserve spear tips crossing the nominal grid. Original runtime128x48 and HD256x96 retained, individual32x48/64x96 pose cells extracted for inspection.

Dark-brown-skinned young man with short curly black hair and strong eyebrows, no beard. Slate blue-gray scarf, ochre diamond-quilted padded coat, brown diagonal strap, brown gloves/bracers, square brass belt buckle, dark trousers, beige cuff wraps and brown boots. Three spare wooden javelins with gray points protrude above back/left shoulder, plus one held brown-shafted javelin with gray leaf point. Round wooden plank shield on screen-left forearm, gray riveted rim, pale brass/tan central boss and brace bands.

Original idle and attack nearly repeat. Original walk-a and walk-b both advance same leg and omit shield while holding javelin with two hands. New four-pose sequence must preserve shield and3spares+1held javelin through every pose, reconstruct opposite walking contacts and one clear overhand javelin throw wind-up/drive before release. No independently baked projectile or FX.

Separate C01-EQUIP-JAVELIN-HUNTER icon shows three total javelins and a hide-covered oval shield. It is not the character identity reference. Preserve original unit wooden plank shield and three back spares plus one held, rather than silently remap character to distinct equipment icon.

Catalog-only topic; no c01.javelin-hunter unit or corresponding art/weapon binding. Existing built-in soldier_javelin belongs to soldier bound to C01-UNIT-SWORDSMAN and is not a consumer of this topic. No binding invented.
