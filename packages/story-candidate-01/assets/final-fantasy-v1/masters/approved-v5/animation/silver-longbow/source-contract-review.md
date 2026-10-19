# C01-UNIT-SILVER-LONGBOW source and action contract

Canonical combat-unit C01-UNIT-SILVER-LONGBOW, asset C01-FFV1-UNIT-SILVER-LONGBOW. Original128x48 runtime and256x96 HD strips; four32x48 logical cells ordered idle / walk-a / attack / walk-b, anchor[16,47], footprint1x1. Preserve gameplay semantics; larger presentation canvas is separate.

Full original source is masters/combat-units/silverwood-longbow-guard-source.png,1672x941. Complete board and original strips copied unchanged and hashed. Reference crops are native, unresampled: idle[0,0,418,941], attack[836,0,1254,941], face/gear[75,250,354,618], bow[265,194,357,688]. Full board and HD strip inspected before reconstruction.

## Original identity and actual consumers

candidate-01-bindings.ts:29 maps c01.silver-longbow only. units.ts:87 gives 银林长弓, infantry/ranged/silverwood, unarmored,vision5. c01-15 银林长梦 has four enemy actors (levels.ts:754,757-758,761): controlled-longbow-1[14,5],controlled-longbow-2[14,9],north-longbow[8,2],south-longbow[8,13]. Coordinates come from matching seed order to deployment slots; place()827-830 uses slots[index]. No builtin consumer found.

Source is a tan adult with pointed ears, short dark hair, clean-shaven face and small left-ear hoop. Gray hood is DOWN; green laced tunic, brown layered leather shoulders, crossing bracer straps, tan fur sleeve/boot trim, brown boots, left-hip woven pouch with blue tie and back quiver. Pointed-ear appearance is directly visible; no new ancestry inferred from the role name. No silver hair, hood-up redesign, additional dagger or shield.

Original screen-right hand holds a pale wooden LONG bow with brown wrapped central grip and tip bindings; screen-left hand is free. One taut string connects both complete tips. Pale quiver upper ends are FEATHERS with shaft tails, not arrowheads pointing toward the guard. Actual metal points stay hidden down in the quiver. No distinct religious/faction emblem; do not add one.

## Sole weapon and attack physics

c01.longbow (weapons.ts:44-54) is pierce,minRange2,maxRange3,direct line-of-sight,1.4bonus against flying. Its equipment is C01-EQUIP-SILVER-LONGBOW (bindings106) and separate projectile effect C01-FX-03 (150). The original bow matches this rule.

Original attack is near-idle, with free hand merely at waist; no credible drawn arrow. Rebuild one clear shooting posture: original bow arm extends screen-right holding the same longbow, original free arm draws toward cheek. One arrowhead at forward/right end, pale rear fletching next to the draw hand, one straight shaft resting above the bow grip, nock/string meeting at drawn hand, V-string connecting actual two bow tips. Do not hold an extra knife or reverse arrowhead/tail. Bow limb length/handle proportions remain consistent across poses; bending/perspective can change tip separation, not arbitrary scale. Keep projectile/trail/impact FX separate. No rule or fifth-frame changes.

Original walking poses favor the same rightward contact; reconstruct two clearly opposite support phases. Both idle and walks keep arrows in quiver, no unexplained floating arrow.

## Native QA and proposed scale

First generated idle is preserved as rejected attempt01:24edge pixels with alpha>1(max3), low boot/bow margin and partly occluded lower bow tip. Imagegen-only attempt02 corrects framing and yields native1585x992 with all perimeter alpha0, complete bow tips and boots. No programmatic alpha cleanup, painting, recolor, crop or resampling. Exact prompts and all native/raw/master attempts retained.

Selected idle hair crown120 to both boot soles956 gives836nativepx; proposed44/836(1/19),foot[800,956]. Longbow approximate vertical span925nativepx,1.106xbody. Uniform locked scale and whole-canvas translation will register all poses. Await coordinator idle review before remaining frames; any1-2px native canvas discrepancy must be explicit transparent padding only.
