# 《布衣定鼎》Runtime V2 Batch 02 ImageGen Prompts

模式：Codex 内置 ImageGen。透明素材采用纯色键母图，再由 imagegen 技能随附的 `remove_chroma_key.py` 去背。生成原图保留于 `$CODEX_HOME/generated_images/019ff114-299c-7261-b63d-4189128e99f5/`，选定母图复制到本目录 `masters/`。下列参考图只承担同项目风格、颗粒和图集布局参考，不是编辑目标。

共同约束：低奇幻历史东亚题材、HD 像素画、泥土褐/湿靛蓝/暗铁/旧木/粗麻；无现代器物、魔法、UI、水印或可读文字；透明类使用均匀色键背景且单元互不串格。

## 1. 战斗步兵图集

输出：`masters/combat-infantry-master.png`。

> Exact 4 columns by 4 rows sprite atlas on flat #00ff00 chroma green. Four frames per row: idle A, step A, idle B, step B, fixed baseline. Row 1 modao shock infantry with broad two-handed saber; row 2 heavy crossbow corps with stocky hand crossbow; row 3 divine-arm crossbow specialist with foot stirrup and winding lever, mechanically distinct; row 4 hooded border scout with short spear, signal pennant and map satchel. Strongly different silhouettes, no cell bleed, labels, floor or shadow.

参考：`../masters/combat-units-master.png`。

## 2. 骑兵、舰船与楼船兵图集

输出：`masters/combat-special-master.png`。

> Exact 4x4 atlas on flat #00ff00. Row 1 light cavalry scout on lean unarmored horse with short bow; row 2 armored cavalry on stocky barded horse with lamellar rider and lance; row 3 compact mengchong river warship with armored timber hull, oars, ram and four movement/wake frames; row 4 tower-ship marine with hooked boarding pole, rope and wet-deck coat. Four ordered frames, same row scale/anchor, no overlap, text or shadows.

参考：`../masters/combat-units-master.png`。

## 3. 任务单位 A

输出：`masters/mission-civilians-a-master.png`。

> Exact 4x4 mission-unit atlas on flat #00ff00. Row 1 famine refugee carrying tied bundle and empty bowl; row 2 farmer with straw hat, hoe and seed pouch; row 3 sowing worker with seed basket and scattering hand; row 4 young dike apprentice with measuring pole, wicker basket and tamping tool. Four fixed-baseline walk frames, dignified civilians, distinct tools and silhouettes.

## 4. 任务单位 B

输出：`masters/mission-civilians-b-master.png`。

> Exact 4x4 mission-unit atlas on flat #00ff00. Row 1 Water League deckhand with boathook and rope; row 2 salt worker with shoulder yoke and two salt baskets; row 3 granary porter with grain sack and tally paddle; row 4 grain-cart driver with reins, short whip and axle-tool pouch, without the cart. Four ordered frames, no combat weapons or cell bleed.

## 5. 地形图集

输出：`masters/terrain-b02-master.png`。

> Exact 4x2 full-bleed HD pixel terrain swatches. Top: cracked famine field; receding flood mud; reclaimed farming furrows; stone-foot seepage ditch. Bottom: engineered diversion flood channel; dense reed bank; lashed pontoon boards and bridge edge; city wall walk with gray bricks and patrol track. Distinct material vocabulary, no people, buildings, labels or borders; sufficient local texture for seamless variants and N/E/S/W masks.

参考：`../masters/terrain-master.png`。

## 6. 四建筑三状态

输出：`masters/structures-b02-master.png`。

> Exact 3 columns by 4 rows on flat #00ff00. Columns are normal, damaged, captured with identical row camera/scale/anchor. Rows: central command banner platform with drum/map table; fortified city-gate control and portcullis winch; river navigation signal beacon tower; field grain supply depot with sacks/scales/carts. Strong distinct silhouettes, complete structures, no floor shadow, text or overlap.

参考：`../masters/structures-master.png`。

## 7. 地图物件初版

保留：`masters/props-b02-master-source.png`。

> Exact 4x2 prop atlas on flat #00ff00. Top: grain-crate cover; wooden shield-wall barricade; saltbag cover; overturned grain cart. Bottom: black-powder barrel group; sealed fire-oil jars; loose dike earth with broken stakes and seepage; bundled unlit fire arrows in rack. One complete object group per cell, strong silhouettes, no labels or UI.

## 8. 地图物件去文字修订

输出：`masters/props-b02-master.png`。

> Precise edit of the referenced prop atlas: remove every Chinese character, warning label, glyph or readable mark from the grain sack, powder barrel and oil jars. Replace only those markings with plain coarse cloth, aged wood/iron hoops and plain brown ceramic. Preserve all eight objects, layout, positions, lighting, chroma green and pixel style; add no new symbol or object.

## 9. 装备图标

输出：`masters/equipment-b02-master.png`。

> Exact 4x2 equipment atlas on flat #00ff00. Top: modao; heavy crossbow and bolt case; divine-arm crossbow with stirrup/winding lever; light-cavalry bow/reins/saddlebag. Bottom: armored-cavalry lance and horse chamfron; border-scout hood clasp/pennant/blank route roll; mengchong ram/oar/hull shield; tower-ship marine hook/rope/deck cleats. Silhouette-first, readable at 32x32, no frames or text.

## 10. 技能图标

输出：`masters/skills-b02-master.png`。

> Exact 4x2 skill atlas on flat #00ff00. Top: armor break as broad blade cracking lamellar plate; aimed shot through wooden sight ring; suppression as three bolts pinning a low shield; cavalry flank as horse head sweeping around shield. Bottom: pursuit as boot/spear/footprints; boarding hook on ship rail; field treatment herb pouch/bandage/splint; banner inspire with plain banner, drum and spear tips. Restrained non-magical motion accents, no UI frame or text.

## 11. 状态与 HUD

输出：`masters/status-hud-b02-master.png`。

> Exact 4x2 icon atlas on flat #00ff00. Top statuses: wounded snapped arrow/bandage; burning cloth hem; soaked cloth and drops; routed broken pennant/bootprints. Bottom HUD: ally upright pale banner/up chevron; enemy crossed dark spearheads/down chevron; neutral shield/staff; recruitable open hand/plain pennant. Shape differences rather than color alone, no modern pictograms or text.

## 12. FX 连续帧

输出：`masters/fx-b02-master.png`。

> Exact 4x4 animation atlas on flat #ff00ff. Each row is one four-frame continuous effect. Row 1 musket pellet hit from tiny impact to flash, smoke and fade; row 2 demolition blast from fuse flash to earth/wood burst, debris and falling smoke; row 3 heavy rain from sparse streaks to peak puddle splashes and receding rings; row 4 snowstorm from first flakes to dense flurry, curling gust and fade. Fixed anchors, non-magical historical effects.

## 13. 天未亮的押粮车

输出：`masters/scene-pre-dawn-grain-cart-master.png`。

> 16:9 HD pixel narrative scene before dawn on a wet county road: official guarded grain cart under white lanterns, hungry villagers in ditch shadows, clerk checking a plain tally, horse breath in cold mist. Tense ration enforcement, diagonal cart/guards silhouette. Reserve bottom 22% as dark low-detail dialogue-safe foreground; no evidence or faces there.

## 14. 决口后的界碑争议

输出：`masters/scene-breach-boundary-dispute-master.png`。

> 16:9 daylight scene after dike breach: two village delegations argue beside a half-submerged uninscribed boundary stone while river engineers measure the changed channel with poles and rope. Eroded bank, raised field edge, flooded furrows and muddy current show the evidence. Calm county engineer mediates. Reserve bottom 22% as dark low-detail foreground.

## 15. 芦苇滩白灯赠粮

输出：`masters/scene-reed-bank-white-lamp-master.png`。

> 16:9 moonless reed-bank landing: Water League skiff arrives silently; boatwoman offers two modest grain sacks to refugees beneath a plain white mutual-aid lantern, child with empty bowl, militia at respectful distance. Intimate mutual aid, reeds and black water. Reserve bottom 22% as dark water/shore dialogue-safe strip.

## 16. 第一面军旗与军法宣读

输出：`masters/scene-first-banner-law-master.png`。

> 16:9 dawn militia camp: first large undyed plain banner raised beside a drum while young commander reads military law from an unmarked bamboo roll. Farmers, river workers and volunteers form a disciplined semicircle; confiscated loot and a bound unharmed offender show the stakes. Sober founding moment. Reserve bottom 22% as dark low-detail ground.
