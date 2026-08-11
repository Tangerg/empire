# 《断冠之誓》HD 像素素材包

本包把剧本的“自由与强制和平”转译为一套原创 16-bit SRPG 视觉语言：人物与建筑保持硬像素边缘，剧情场景使用像素前景、中景、远景和烘焙后的 HD-2D 式光效。风格只借鉴经典 16-bit 战棋游戏共有的轮廓、配色与三分之四视角特征，不复制既有角色、建筑或纹章。

## 视觉 DNA

- 核心形状：断裂竖线、灰色无纹旗、七段式高塔、可拆分的扣环。
- 核心材质：湿羊毛、旧钢、烟熏石、焦木；魔法只以小面积琥珀誓火出现。
- 色彩关系：冷蓝灰承担制度与战争，灰旗保持中性，琥珀只给誓言/烽塔，暗酒红用于强制秩序。
- 人物轮廓：装备强调军职与责任，不以华丽王族服饰区分地位；莱娅全程不戴王冠。
- 场景镜头：把旗、城墙、烽塔放得比人物更重，让“制度”始终成为视觉上的第二主角。

完整色值见 [style/tokens.json](./style/tokens.json)，可视色板见 [style/palette.svg](./style/palette.svg)。被否决的低细节首稿已整体移入 `draft-v1/`，不会与正式素材混用。

当前正式覆盖、历史缺口和分批补产顺序见 [PRODUCTION-GAP.md](./PRODUCTION-GAP.md)。下述扩展包只补齐 382 个题材槽位原型，不等于 382 项运行时美术已经完成。可预览的运行时候选、精确覆盖与验收门槛分别见 [Runtime V2 素材画廊](../../RUNTIME-V2-ASSET-GALLERY.html)、[Runtime V2 覆盖状态](../../RUNTIME-V2-STATUS.md) 和 [游戏运行时素材契约](../../GAME-RUNTIME-ASSET-CONTRACT.md)。

## 404 题材槽位原型包

原型包按统一口径登记 404 个可追踪题材：沿用既有正式题材 22 个，新增 382 个原型槽位。每个题材均有稳定 ID、唯一 delivery 归属和可裁切的 `cell` 坐标；新增部分集中在 `complete/`，不会覆盖上述 HD 质量标杆。

| 类别 | 题材数 | 扩展图集 |
| --- | ---: | --- |
| 叙事静态 | 80 | 5 张新增图集 + 既有 16 份正式交付 |
| 战斗单位 | 40 | `complete/units/combat-unit-package-atlas-v1.*` |
| 任务单位 | 24 | `complete/mission-units/mission-unit-package-atlas-v1.*` |
| 阵营套件 | 12 | `complete/factions/faction-kit-atlas-v1.*` |
| 地形 | 32 | `complete/terrain/terrain-group-atlas-v1.*` |
| 交互建筑 | 24 | `complete/structures/interactive-structure-atlas-v1.*` |
| 战场物件 | 32 | `complete/battle-props/battle-prop-atlas-v1.*` |
| 装备 | 48 | `complete/equipment/equipment-atlas-v1.*` |
| 技能 | 48 | `complete/skills/skill-icon-atlas-v1.*` |
| 状态 | 24 | `complete/status/status-icon-atlas-v1.*` |
| FX | 24 | `complete/fx/fx-atlas-v1.*` |
| HUD | 16 | `complete/ui/hud-atlas-v1.*` |

题材原型入口为 [`manifest-complete.json`](./manifest-complete.json)，结构校验结果见 [`qa-complete.json`](./qa-complete.json) 和 [`qa-complete.md`](./qa-complete.md)。原型包包含 16 张 PNG 图集及其 16 份独立 SVG；清单合计引用 32 份 delivery、64 个 PNG/SVG 文件。六位英雄的既有四帧单位保留为补充素材，不占 40 个战斗单位母型名额。

本次扩展沿用既有硬边像素与矢量扫描线体系：战斗/任务单位通过武器、盾、坐骑、法器、职业工具和体型建立可辨识轮廓；地形按草泥、河桥、石城、林地、炉城、荒原、墓地和誓光母型拆分；场景覆盖议事、城防、渡河、林地、营地、火场与墓地构图；装备按武器、防具、军旗、工具和消耗品组织轮廓族。

## 正式质量标杆

| 资产 | PNG | SVG | 规格 |
| --- | --- | --- | --- |
| 莱娅 18 岁头像 | `characters/laiya-18-portrait-hd.png` | `characters/laiya-18-portrait-hd.svg` | 96×112，48 个前景色 + 透明 |
| 罗德里克头像 | `characters/roderick-portrait-hd.png` | `characters/roderick-portrait-hd.svg` | 96×112，48 个前景色 + 透明 |
| 凯恩头像 | `characters/kain-portrait-hd.png` | `characters/kain-portrait-hd.svg` | 96×112，48 个前景色 + 透明 |
| 米蕾尔头像 | `characters/mirelle-portrait-hd.png` | `characters/mirelle-portrait-hd.svg` | 96×112，47 个前景色 + 透明 |
| 布兰头像 | `characters/bran-portrait-hd.png` | `characters/bran-portrait-hd.svg` | 96×112，48 个前景色 + 透明 |
| 塔莎头像 | `characters/tasha-portrait-hd.png` | `characters/tasha-portrait-hd.svg` | 96×112，48 个前景色 + 透明 |
| 莱娅四帧单位 | `units/laiya-18-walk-sheet-hd.png` | `units/laiya-18-walk-sheet-hd.svg` | 128×48，每帧 32×48，39 个前景色 + 透明 |
| 罗德里克四帧单位 | `units/roderick-walk-sheet-hd.png` | `units/roderick-walk-sheet-hd.svg` | 128×48，每帧 32×48，46 个前景色 + 透明 |
| 凯恩四帧单位 | `units/kain-walk-sheet-hd.png` | `units/kain-walk-sheet-hd.svg` | 128×48，每帧 32×48，46 个前景色 + 透明 |
| 米蕾尔四帧单位 | `units/mirelle-walk-sheet-hd.png` | `units/mirelle-walk-sheet-hd.svg` | 128×48，每帧 32×48，41 个前景色 + 透明 |
| 布兰四帧单位 | `units/bran-walk-sheet-hd.png` | `units/bran-walk-sheet-hd.svg` | 128×48，每帧 32×48，44 个前景色 + 透明 |
| 塔莎四帧单位 | `units/tasha-walk-sheet-hd.png` | `units/tasha-walk-sheet-hd.svg` | 128×48，每帧 32×48，44 个前景色 + 透明 |
| 赤石誓约烽塔 | `architecture/redstone-oath-tower-hd.png` | `architecture/redstone-oath-tower-hd.svg` | 128×128，64 个前景色 + 透明 |
| 灰旗野战营 | `architecture/gray-banner-field-camp-hd.png` | `architecture/gray-banner-field-camp-hd.svg` | 128×128，64 个前景色 + 透明 |
| 三桥河谷 | `architecture/three-bridges-river-valley-hd.png` | `architecture/three-bridges-river-valley-hd.svg` | 128×128，64 个前景色 + 透明 |
| 银林树城 | `architecture/silverwood-tree-city-hd.png` | `architecture/silverwood-tree-city-hd.svg` | 128×128，60 个前景色 + 透明 |
| 焚村后的第一面灰旗 | `scenes/gray-flag-over-burned-village-hd.png` | `scenes/gray-flag-over-burned-village-hd.svg` | 256×144，不透明，96 色 |
| 双子丘陵初次指挥 | `scenes/twin-hills-first-command-hd.png` | `scenes/twin-hills-first-command-hd.svg` | 256×144，不透明，96 色 |
| 白河夜渡 | `scenes/white-river-night-crossing-hd.png` | `scenes/white-river-night-crossing-hd.svg` | 256×144，不透明，96 色 |
| 七塔王城终局远景 | `scenes/seven-towers-at-dusk-hd.png` | `scenes/seven-towers-at-dusk-hd.svg` | 256×144，不透明，96 色 |
| 四件剧情道具图集 | `props/story-props-sheet-hd.png` | `props/story-props-sheet-hd.svg` | 192×48，每件 48×48，61 个前景色 + 透明 |
| 第二组战役道具图集 | `props/campaign-props-sheet-02-hd.png` | `props/campaign-props-sheet-02-hd.svg` | 192×48，每件 48×48，63 个前景色 + 透明 |

SVG 不是对 PNG 的简单封装，而是按扫描线合并后的像素矩形，可独立渲染和整数倍缩放。生成母版保存在 `masters/`。浏览总览见 [`gallery-hd.html`](./gallery-hd.html)，机器清单见 [`manifest-hd.json`](./manifest-hd.json)，验证记录见 [`qa-report.md`](./qa-report.md)。

## 归档的 V1 覆盖规划（非正式资产）

下表记录 `draft-v1/` 曾覆盖的剧本范围，仅用于后续正式重制排期；它不代表当前正式交付数量，也不能加入游戏构建。

| 类别 | 数量 | 内容 |
| --- | ---: | --- |
| 人物头像 | 8 | 莱娅（18 岁）、罗德里克、凯恩、米蕾尔、布兰、塔莎、塞维恩、奥德伦 |
| 战斗单位 | 8 | 与头像一一对应的四帧横向图集 |
| 建筑 | 4 | 赤石誓约烽塔、灰旗野战营、银林树城、阿斯塔里亚七塔王城 |
| 剧情场景 | 3 | 双子丘陵、焚村后的第一面灰旗、七塔王城终局 |
| 道具图标 | 4 | 灰旗、自由誓石、王冠碎片、巨龙盟约扣 |

正式目录规格：

- `characters/`：`96×112` 半身头像，透明背景。
- `units/`：`128×48` 四帧横排图集，每帧 `32×48`，透明背景。
- `architecture/`：`128×128` 单体建筑，透明背景，可按两格建筑或剧情插图使用。
- `scenes/`：`256×144`、16:9、不透明剧情画面。
- `props/`：`192×48` 四件横排图集，每件 `48×48`，透明背景；具体切片顺序以 `manifest-hd.json` 的 `itemOrder` 为准。

## 角色阶段设计

| 角色 | 阶段 | 必须保留 | 可变化 |
| --- | --- | --- | --- |
| 莱娅 | 18 岁见习旗官 | 蓝灰短披风、无个人纹章、轻甲 | 第四章加入战痕与灰旗扣；终章换多族盟约扣、去掉个人王权暗示 |
| 罗德里克 | 导师/追兵/有限和解 | 老骑士重肩甲、克制的金色誓纹 | 甲片破损、披风颜色和剑鞘；不做邪恶黑甲突变 |
| 凯恩 | 敌将/盟友/独立盟军 | 酒红军团色、方盾、严整轮廓 | 流亡后减少帝国礼仪件，但保留帝国身份 |
| 米蕾尔 | 守墓人/异端/最后誓约 | 灰紫兜帽、手提誓灯 | 灯火从圣辉白转为亡者琥珀；不做传统纯白圣女 |
| 布兰 | 猎人/刺客指挥官 | 短弩、深绿林地布料 | 焚村后加入烧焦皮护腕；和解不改变伤痕 |
| 塔莎 | 佣兵首领/军需负责人 | 实用短弩、账袋、黄褐衣 | 军队正规化后增加灰旗肩带，不换贵族礼服 |
| 塞维恩 | 摄政王/最后暴君 | 黑紫官服、低调金边、旧剑 | 王冠力量只改变光源和背景，不把脸怪物化 |
| 奥德伦 | 教廷领袖/记忆反噬 | 圣辉兜帽、金色灯具 | 后期让多个错位誓火像素覆盖轮廓 |

## 七章资产矩阵

| 优先级 | 章节/系统 | 需要继续制作的素材 |
| --- | --- | --- |
| P0 | 第一章垂直切片 | 莱娅/罗德里克/凯恩/米蕾尔/布兰、双子丘陵、三桥河谷、焚村灰旗场景已完成；补双子丘陵战斗地块变体、赤石塔受损态、灰旗举起过场 |
| P0 | 第二章灰旗流亡 | 塔莎、白河夜渡已完成；补无主之城、教廷运输笼、伊芙拉幼年单位与头像 |
| P1 | 第三章古老诸族 | 银林树城已完成；补银林长生者、山炉氏族、荒原诸部各 3 个基础兵种，以及苍林/炉城/荒原营地战斗地块 |
| P1 | 第四/五章联盟与分裂 | 莱娅 22 岁头像与单位、青年伊芙拉、跪下之城/无王之城同图双状态、被控制盟友色替换规则 |
| P1 | 第六章七塔战争 | 七座烽塔独立顶冠与受损态、各阵营指挥官旗、成年伊芙拉大型单位 |
| P2 | 第七章无王之座 | 七塔王城终局远景已完成；补莱娅 27 岁不戴冠头像、王冠完整/断裂/自由誓石三套道具动画、四种结局场景差分 |
| P2 | 长期复用 | 村庄“繁荣/战争/重建/纪念地”四态，墓碑“回声/归名/安息”三态，季节与九年老化差分 |

## 接入约定

- SVG 已设置 `shape-rendering="crispEdges"` 与 `image-rendering: pixelated`，缩放时仍建议使用 2、3、4 等整数倍。
- 单位图集从左到右为：站立 A、踏步 A、站立 B、踏步 B；每帧裁切宽 `32`、高 `48`。
- 当前场景 PNG 与 SVG 都是合成后的成品画面，HD-2D 光效已烘焙；若游戏需要运行时关闭光效，应从母版另行导出分层文件，不能假定成品 SVG 内存在图层分组。
- `campaign-props-sheet-02-hd.*` 从左到右为莱娅左手护腕、逃亡硬饼、控制誓文残片、三族盟约扣件；同样按 `48×48` 固定切片。
- 阵营换色只替换 `uniform`/`accent` 对应色，不应替换皮肤、誓火和材质高光。
- 所有旗面默认无文字；阵营识别依赖颜色、剪影和旗尾形状，避免在小尺寸中使用不可读纹章。

重新生成被归档的 V1 草稿（不会覆盖 HD 正式文件）：

```bash
node docs/story-candidates/generate-pixel-assets.mjs
```
