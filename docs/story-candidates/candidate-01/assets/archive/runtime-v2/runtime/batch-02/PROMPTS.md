# C01 runtime-v2 Batch 02 — ImageGen prompts

生成模式：Codex built-in ImageGen，`stylized-concept`。透明资产统一使用平坦 `#ff00ff` 色键底；母图保存于本目录的 `masters/`，再用 imagegen skill 自带 `remove_chroma_key.py` 去色键并由 `tools/process_batch_02.py` 切片、缩放、有限色量化和锚点校正。剧情场景直接生成不透明 16:9 母图。

风格参考：`../previews/units-2x.png`、`../previews/terrain-random-12x8-2x.png`、`../previews/map-structures-2x.png`、`../../../props/story-props-sheet-hd.png`、`../../../scenes/gray-flag-over-burned-village-hd.png`。参考图只用于材质、光影、颗粒和剧本语汇，不复制构图。

所有透明提示词共享以下尾部约束：

> High-detail late-16-bit / 32-bit hand-clustered grim pixel art, crisp square pixels, worn material highlights, readable at final runtime size. Perfectly flat uniform #ff00ff chroma-key background, no texture, gradient, floor, reflection or cast shadow. No text, labels, UI, watermark, scenery or cropped equipment; never use #ff00ff inside the subject.

## 战斗单位：8 个独立四帧母图

每张均要求“exactly four equal-width isolated poses in one horizontal row; same identity; idle A, step A, idle B, step B; right-facing three-quarter direction; shared foot baseline”。

- `masters/units/swordsman-master.png`：灰旗剑士；磨损链甲与炭灰披肩，缺口武装剑、小圆盾，四个清晰步行动作。
- `masters/units/engineer-master.png`：战地工程师；皮围裙、锻锤、黄铜测规与短量杆，工具轮廓完整。
- `masters/units/banner-guard-master.png`：旗卫；长灰旗、窄剑、铁护肩，旗面始终在帧内。
- `masters/units/legion-shield-master.png`：军团盾卫；酒红巨盾、厚重铁盔与短兵，防守姿势明显。
- `masters/units/rune-artificer-master.png`：山炉符文工匠；方形锻造面甲、青铜肩甲、琥珀符文钳与工具杖。
- `masters/units/wolf-rider-master.png`：灰烬巨狼骑手；巨狼低重心、四足动作、钩枪与圆盾；目标为 `64×64` 单帧。
- `masters/units/gravekeeper-master.png`：活人守墓人；长炭灰葬衣、铁灯与宽墓铲，灯光温暖克制。
- `masters/units/skeleton-guard-master.png`：骸骨卫士；裸露骨骼、破旧灰甲、墓盾与腐蚀长枪，眼窝冷蓝誓光。

## 任务单位

- `masters/mission-units/border-farmer-master.png`：边境农户四帧；补丁赭衣、草兜帽、短耙与萝卜篮。
- `masters/mission-units/refugee-adult-master.png`：难民成人四帧；灰蓝披巾、卷毯/炊具大包、手杖与纪念袋。
- `masters/mission-units/refugee-child-master.png`：明显矮于成人的难民儿童四帧；过大锈色兜帽、小包与木马玩具，目标人物约占 `24×38`。
- `masters/mission-units/mission-board-05-master.png`：严格 4 列×5 行、共 20 人形；每行同一身份的 idle/step/work/step。行 1 撤离车夫（缰绳与轮扳手）；行 2 面包师（面包篮与炉铲）；行 3 采矿工（镐与安全灯）；行 4 炉城工匠（皮围裙、钳与锤）；行 5 桥梁劳工（木板、槌与绳圈）。

## 地形、建筑与小型母板

- `masters/terrain/terrain-board-master.png`：严格 4×2 正交俯视材质板。依次为焦土农田、河岸、王都街石、母树根区、炉城石地、墓地、黑色熔沟、冷青誓文受控区；中间调明亮、纹理密度适中、八类材质不复用母型。
- `masters/structures/structures-board-master.png`：严格 3×4；每行 normal/damaged/captured。行 1 灰旗旗点，行 2 共管粮仓，行 3 山炉工坊，行 4 野战医院；第三列使用独立暗红占领旗，受损态保持可辨识主体。
- `masters/atlases/props-board-master.png`：4×2；木箱堆、盾墙、风蚀岩、翻倒粮车、油火盆、攻城火药桶、失控誓石、亡者污染残片。
- `masters/atlases/equipment-board-master.png`：4×2；八个新增兵种的剑盾、锤规、旗枪、巨盾、符文钳晶、狼骑钩枪/缰具、墓铲/灯、骸骨枪盾。
- `masters/atlases/skill-board-master.png`：4×2；占领、护卫、反骑、侦察、背刺、破甲、冲锋、指挥延伸；每项使用独立剪影而非换色圆环。
- `masters/atlases/status-hud-board-master.png`：4×2；上排中毒、沉默、护卫、誓文受控；下排友军、敌军、中立、可招降。
- `masters/atlases/fx-board-master.png`：严格 4×4；每行从左至右四帧。穿刺闪、箭矢命中/碎裂、钝击冲击环、秋雨击叶；每帧必须明显演进且粒子不越界。

## 剧情场景：4 张独立 16:9 母图

场景共享约束：`high-detail grim pixel-art narrative cutscene; wide 16:9; clear foreground/midground/background; readable midtones; no text, UI, watermark, modern objects, gore or magenta; composition must differ from the other three scenes.`

- `masters/scenes/three-bridge-ceasefire-master.png`：冷雨黄昏的三桥停火；中心桥两位敌对盾队长共同扶灰色停战旗，左右桥军阵停步，暗河和烧毁村落形成纵深。
- `masters/scenes/ivra-breaks-reins-master.png`：银木林夜路撤离；伊芙拉用短刀割断惊马与翻倒补给车的缰绳，后方难民车队停滞，山脊出现敌影。
- `masters/scenes/forge-repair-master.png`：战后山炉夜修；符文工匠、平民铁匠与桥工用钳、链式吊机把琥珀齿轮环装回裂开的玄武岩基座。
- `masters/scenes/public-rations-master.png`：防御营地清晨公开分粮；面包师与灰披士兵共用铁勺计量粮食，难民/矿工/伤兵排队，军官展示公开账册，背景为粮仓与医院。

