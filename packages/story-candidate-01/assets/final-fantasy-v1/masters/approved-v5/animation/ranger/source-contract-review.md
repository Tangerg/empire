# Ranger source contract

C01-UNIT-RANGER is the actual combat-unit art for protagonist c01.bran (candidate-01-bindings.ts line8). It is distinct from C01-EQUIP-RANGER, C01-CHAR-BRAN-01 and the separate archer/woodland-walker topics. Original board is the first row of combat-atlas-ranger-assassin-mage-knight.png,1254x1254. Native first-row crop [0,0,1254,330] preserves complete boots and stops before the assassin row; idle crop [0,0,314,330]. Runtime128x48, four32x48 frames; HD256x96, four64x96 frames. Order idle,walk-a,attack,walk-b; anchor[16,47], footprint[1,1].

## Identity
Clean-shaven adult human with short brown bangs, raised deep charcoal hood and neck cowl, muted blue-gray split-hem tunic, olive-gray shoulder caps, brown gloves/bracers/belt/boots and hip arrow quiver. Screen-left glove holds a short steel blade low; screen-right glove holds a short wooden bow upright. Preserve physical equipment hands, human identity and broad muted costume.

## Action contract and limitation
Bran has primary c01.ranger-bow (piercing, maxRange3, arc line of sight) plus c01.border-blade (slash/melee). The original strip has only one attack slot. Parent approved using a clear primary-bow shot in that slot: bow remains in the original bow hand and the short blade is temporarily sheathed at its original waist location to free the blade hand for drawing. Both weapons will still call this same existing animation slot; that visual limitation remains explicitly documented and no rules change is claimed. Idle and walks preserve the original carried short blade plus bow.

## Scale
Use approved chunky adult infantry proportions at approximately44logical body height. Idle must be reviewed first, then one locked source-body scale and complete native canvas are shared across all four poses. Do not shrink for bow room or independently fit silhouettes.
