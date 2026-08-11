# 《布衣定鼎》Runtime V2 Batch 03 母图提示词

本批使用 built-in ImageGen 生成 7 张原创单位母图；每张均为 `4 列 × 4 行`，四列依次为站立 A、踏步 A、站立 B、踏步 B，四行题材顺序与 `TOPICS.md` 和 `process_batch_03.py` 一致。透明内容统一在纯 `#00ff00` 色键底上生成，再由官方 `remove_chroma_key.py` 和确定性脚本处理。参考图为本项目已通过风格审阅的 C03 Runtime V2 / Batch 02 单位母图；参考仅用于低奇幻历史 HD 像素风、材质颗粒与动作尺度，不复刻其人物。

## 01 工程与攻城

母图：`masters/units-b03-01-engineering-master.png`

> Use case: historical-scene. Asset type: HD pixel-art combat-unit atlas for C03. Exact 4 columns by 4 rows on perfectly flat vivid #00ff00 chroma green, no grid, labels, text, floor, shadows, border, crop or overlap. Four ordered fixed-baseline movement frames per row. Row 1 compact river fire ship with low armored bow, bundled fire arrows, covered oil jars and two disciplined deck crew, no active flame. Row 2 bridge corps carrying long timber beam, rope coil and mallet, coordinated heavy steps. Row 3 ladder corps carrying a tall wooden assault ladder with hooked top, padded armor and sidearm. Row 4 traction catapult crew moving a compact wooden stone-thrower with rope bundle and stone basket. Strong unique silhouettes and movement, historical East Asian low-fantasy HD pixels, readable wood/iron/cloth materials, no magic, modern objects, readable emblems or watermark.

## 02 火器与军医

母图：`masters/units-b03-02-gunpowder-support-master.png`

> Use case: historical-scene. Asset type: HD pixel-art combat-unit atlas for C03. Exact 4 columns by 4 rows on perfectly flat vivid #00ff00 chroma green, no grid, labels, text, floor, shadows, border, crop or overlap. Four ordered fixed-baseline movement frames per row. Row 1 early matchlock musketeer with long dark iron firearm, slow match and fork rest, padded coat. Row 2 fire-arrow corps archer with recurved bow, distinctive bundled fire arrows and covered ignition pot, not actively burning. Row 3 thunder-bomb corps grenadier carrying a round ceramic bomb in tongs and a padded wicker blast shield. Row 4 field medic in plain robe with medicine satchel, cloth rolls, gourd and small folding splint, no fantasy staff. Strong weapon and profession silhouettes, historical East Asian low-fantasy HD pixels, no text, magic, modern firearm, logo or watermark.

## 03 号令与侦察

母图：`masters/units-b03-03-command-intel-master.png`

> Use case: historical-scene. Asset type: HD pixel-art combat-unit atlas for C03. Exact 4 columns by 4 rows on perfectly flat vivid #00ff00 chroma green, no grid, labels, text, floor, shadows, border, crop or overlap. Four ordered fixed-baseline movement frames per row. Row 1 drummer-standard bearer with waist drum, two sticks and narrow military pennant without readable emblem. Row 2 quartermaster with grain tally board, keys, sealed ration sack and practical padded coat. Row 3 strategist with folded fan, map scroll case and restrained scholar-officer robe, no magic. Row 4 scout-office field agent in travel cloak carrying compact spyglass substitute made from bamboo sighting tube, knot cord and report tube. Distinct command, supply, counsel and intelligence silhouettes; historical East Asian low-fantasy HD pixels, no readable text, modern objects, logos or watermark.

## 04 劝降与守备

母图：`masters/units-b03-04-garrison-master.png`

> Use case: historical-scene. Asset type: HD pixel-art combat-unit atlas for C03. Exact 4 columns by 4 rows on perfectly flat vivid #00ff00 chroma green, no grid, labels, text, floor, shadows, border, crop or overlap. Four ordered fixed-baseline movement frames per row. Row 1 unarmed envoy with tall plain staff, folded credentials and open-palmed diplomatic gesture. Row 2 village militia in mixed farm clothes with bamboo spear, sickle at belt and improvised round wicker shield. Row 3 city-gate shield guard with very tall rectangular pavise and short saber. Row 4 commander's retinue in fitted lamellar armor with long polearm and small back pennon without readable emblem. Strong social and equipment silhouettes, historical East Asian low-fantasy HD pixels, no magic, readable text, modern objects or watermark.

## 05 破阵与水陆防卫

母图：`masters/units-b03-05-assault-defense-master.png`

> Use case: historical-scene. Asset type: HD pixel-art combat-unit atlas for C03. Exact 4 columns by 4 rows on perfectly flat vivid #00ff00 chroma green, no grid, labels, text, floor, shadows, border, crop or overlap. Four ordered fixed-baseline movement frames per row. Row 1 warhammer formation-breaker in heavy shoulder armor wielding a long two-handed iron hammer. Row 2 fortification guard with construction apron, hooked polearm, mallet and bundled stakes. Row 3 covert hull saboteur in soaked dark river clothes carrying hand auger, short adze and rope, face visible and not an assassin. Row 4 canal escort marine with boarding spear, buckler and coiled mooring rope. Strong weapon and occupational silhouettes, historical East Asian low-fantasy HD pixels, no readable emblems, magic, modern objects or watermark.

## 06 边地与禁军

母图：`masters/units-b03-06-border-elite-master.png`

> Use case: historical-scene. Asset type: HD pixel-art combat-unit atlas for C03. Exact 4 columns by 4 rows on perfectly flat vivid #00ff00 chroma green, no grid, labels, text, floor, shadows, border, crop or overlap. Four ordered fixed-baseline movement frames per row. Row 1 salt-store guard with broad pole cleaver, storehouse key ring and salt-stained cloth armor. Row 2 snowfield horse archer on a compact shaggy steppe horse, fur mantle, recurved bow and quiver, broad mounted silhouette. Row 3 frontier-market guard with long spear, small round shield and visible trade-weight pouch, mixed frontier clothing. Row 4 Ning imperial guard in dark disciplined lamellar armor with tall ceremonial-but-functional spear and narrow shoulder pennon without readable emblem. Strong regional and rank silhouettes, historical East Asian low-fantasy HD pixels, no magic, text, modern objects or watermark.

## 07 密察、地道、火油防备与整编

母图：`masters/units-b03-07-covert-reintegration-master.png`

> Use case: historical-scene. Asset type: HD pixel-art combat-unit atlas for C03. Exact 4 columns by 4 rows on perfectly flat vivid #00ff00 chroma green, no grid, labels, text, floor, shadows, border, crop or overlap. Four ordered fixed-baseline movement frames per row. Row 1 secret-inspector arrest agent in dark fitted robe and low cap, short baton, cord restraints and concealed document tube, controlled quick steps, not an assassin. Row 2 tunnel corps miner with hood, short pick, shovel, timber brace and dirt-stained padded clothes, stooped underground gait. Row 3 fire-oil defense soldier wearing wet felt cape and face cloth, long jar hook, sand bucket and lid shield, non-flaming defensive equipment. Row 4 reorganized surrendered soldier with deliberately mixed old armor pieces, plain uncolored cloth armband, grounded spear and lowered cautious posture, disciplined but visibly transitional. Strong unique silhouettes and social semantics, historical East Asian pixels, no readable emblems, magic, modern objects or watermark.

