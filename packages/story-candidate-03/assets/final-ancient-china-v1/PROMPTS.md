# 母图生产提示词索引

所有母图均通过 Codex 内置图像生成能力制作；以下内容用于复现美术语义与图集布局。输出不得直接作为运行时文件，必须经 `tools/build_final_ancient_china_v1.py` 切片、去背、缩放与 QA。

## 全局风格前缀

> Original 2D ancient Chinese historical tactical strategy RPG asset, high-oblique cartoon, compact oversized-head characters, bold dark ink contour, matte restrained earth/olive/indigo/aged-gold palette, two-step cel shading, handmade painted texture, readable at small runtime scale. Grounded fictional late-imperial China focused on river war, grain logistics and civil administration. No Japanese samurai or ninja motifs, no torii, no wuxia magic, no modern technology, no photorealism, no glossy 3D, no pixel art, no text or watermark.

透明资源一律要求纯 `#FF00FF` 背景；场景与角色卡保留完整背景。

## 图集与顺序

- `combat/combat-board-01..10.png`：每张 4 行，每行一个战斗单位，4 列依次为待机、行走 A、攻击/工作、行走 B；顺序严格跟随完整 manifest 的 40 个 `combat-unit`。
- `mission/mission-board-01..06.png`：相同 4 帧结构；顺序严格跟随 24 个 `mission-unit`。
- `factions/faction-kits-board.png`：3 列×4 行，依次为白灯、旧朝、水盟、梁震大湖军、吴越、大朔/燕云、宁朝、地方乡勇、河工营、舟师、宫城禁卫、降兵整编。
- `terrain/terrain-board-01..04.png`：每张 4×2，顺序严格跟随 32 个地形题材。
- `structures/structures-board-01..04.png`：前两张 3×4，后两张 3×8；每行正常/重损/占领三态，顺序严格跟随 24 个交互建筑。
- `props/props-board-01.png`：8×4，顺序严格跟随 32 个战场物件。
- `equipment/equipment-board-01.png`：8×6，顺序严格跟随 48 个装备。
- `skills/skills-board-01.png`：8×6，顺序严格跟随 48 个技能。
- `status-hud/status-board-01.png`：6×4，顺序严格跟随 24 个状态。
- `status-hud/hud-board-01.png`：4×4，顺序严格跟随 16 个 HUD 标识。
- `fx/fx-board-01..03.png`：每张 4×8；每行一个语义的四帧连续效果，顺序严格跟随 24 个 FX。
- `narrative/characters/character-board-01..02.png`：每张 4×3，覆盖 23 个角色/阶段卡。
- `narrative/props/prop-board-01.png`：5×3，覆盖 15 个关键剧情物件。
- `narrative/scenes/scene-board-01..03.png`：每张 4×4，覆盖 42 个场景与地点题材。

## 硬性语义约束

- 兵种不能只做颜色替换；武器、携行物、姿态、编制规模和服装必须对应名称。
- 文职与任务单位必须体现工作：量粮、登记、抄录、医疗、摆渡、修堤、运输、救济等。
- 早期火器只使用古代火铳、火箭、震天雷和火船，不出现现代枪械结构。
- 角色阶段变化要反映年龄、身份和责任，不通过神力光环表达成长。
- 女性角色通过账房、医疗、舟师、情报、地方组织与制度工作参与剧情，不使用现代制服或仙侠服饰。
- 场景构图必须呈现玩法关系：道路、桥头、堤防、闸门、补给点、控制点、平民区与交战区可辨。
