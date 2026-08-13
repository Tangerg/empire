# C01 runtime-v2 Batch 03 — ImageGen prompts

模式：Codex built-in ImageGen，`stylized-concept`。七张母板均用 C01 已通过实机的 `units-2x.png`、B02 `units-2x.png`、结构预览或 `gray-flag-over-burned-village-hd.png` 作为纯风格参考。母图保存在 `masters/`，透明中间图由 imagegen skill 的 `remove_chroma_key.py` 生成到 `intermediate/`，最终运行时表由 `scripts/build_batch_03.py` 切片。

所有母板共享提示词骨架：

> Use case: stylized-concept. Asset type: C01 runtime-v2 batch-03 tactical RPG combat animation mother board. Strict 4-column by 4-row contact sheet, exactly sixteen isolated unit poses. Each row repeats one identity through idle A, movement A, idle B, movement B toward the right with a shared baseline. High-detail late-16-bit / 32-bit hand-clustered grim pixel art, crisp square pixels, worn C01 materials and restrained palette. Perfectly flat uniform #ff00ff chroma-key background; no floor, gradient, texture, reflection, cast shadow, grid lines, text, labels, UI or watermark. Do not use #ff00ff inside subjects; no cropped weapons, limbs, mounts, wings or wheels.

## A — `masters/combat-board-a-master.png`

- 行 1：长弓守卫；苔灰兜帽、层叠皮甲、极高浅木长弓与箭袋。
- 行 2：游侠；短反曲弓、叶片披风、猎刀，轮廓与长弓守卫明确不同。
- 行 3：刺客；炭黑贴身外套、半面罩、成对内弯短刃、低重心潜行。
- 行 4：法师；灰蓝旅甲法袍、弯铁杖与克制冷青法球。

四行均为普通 `32×48` 人形。

## B — `masters/combat-board-b-master.png`

- 行 1：无人弩车；木制双轮底盘、巨大横弩与重弩矢，`96×64`。
- 行 2：长枪骑兵；灰马、酒红甲骑手、超长平举骑枪与窄盾，`64×64`。
- 行 3：战斗法师；板甲/法袍、短剑与琥珀符文拳套，`32×48`。
- 行 4：鹰骑斥候；兜帽骑手乘巨型浅棕战鹰，翅膀由收拢到半张，`96×64`。

## C — `masters/combat-board-c-master.png`

- 行 1：林地行者；树皮板甲、根足、枯枝冠与重枝杖，非人形巨体，`64×64`。
- 行 2：德鲁伊；年长林地祭司、苔披风、活木杖、药包与小鹿角冠，`32×48`。
- 行 3：白鹿骑手；林地使者骑巨型白鹿、分叉鹿角与短叶枪，`64×64`。
- 行 4：符文盾卫；煤灰重甲、宽六角琥珀符文盾与短锤，`32×48`。

## D — `masters/combat-board-d-master.png`

- 行 1：战斧兵；酒红拼接板甲、双手宽刃战斧、低重心突进，`32×48`。
- 行 2：石魔像；玄武岩躯干、不对称巨石臂、琥珀裂纹，`64×64`。
- 行 3：萨满；毛皮骨饰、叉角面具、腰鼓与符物杖，`32×48`。
- 行 4：投矛猎手；边境皮衣、背组三短矛、投枪与椭圆皮盾，`32×48`。

## E — `masters/combat-board-e-master.png`

- 行 1：重装骑士；极宽酒红黑板甲、巨盔、塔盾与凸缘锤，`32×48`。
- 行 2：灵火精怪；冷蓝誓火包围破裂铁制葬面具，四种悬浮火形，`64×64`。
- 行 3：无人火炮车；四铁轮木车、短青铜轰炮与火药柜，`96×64`。
- 行 4：巨魔；灰绿驼背巨人、皮带、石槌与长臂重步，`64×64`。

## F — `masters/combat-board-f-master.png`

- 行 1：狂战士；裸臂边境老兵、破毛披肩、铁半盔与双重手斧。
- 行 2：圣殿骑士；浅钢甲、奶白罩袍、日芒几何风筝盾与长直剑。
- 行 3：审判官；深色甲袍、高檐铁帽、锁链誓书、细长行刑锤与小灯。
- 行 4：幽魂；冷蓝灰古甲、破旗枪与向下收束的雾状躯体。

四行均为 `32×48`。

## G — `masters/combat-board-g-master.png`

- 行 1：墓地巨像；墓碑石板、破钟护甲、根状石足、地窖门巨盾，`96×64`。
- 行 2：伊芙拉成长单位；成年灰旗女将、深色编发、断冠肩扣、指挥刀与折叠灰旗，`32×48`。
- 行 3：飞龙骑手；骑手乘双足灰烬飞龙、皮翼、长尾与短枪，`96×64`。
- 行 4：古龙；无骑手的古老灰黑四足龙、伤痕翼、青铜灰角冠与琥珀誓裂，`96×64`。

