# 《群星熄灭之前》题材槽位原型与 HD 素材 QA

## 404 题材槽位原型

本节只证明题材、文件和图集单元的原型结构通过检查，不证明运行时质量或游戏接入完成。运行时候选、精确覆盖与验收门槛分别见 [Runtime V2 素材画廊](../../RUNTIME-V2-ASSET-GALLERY.html)、[Runtime V2 覆盖状态](../../RUNTIME-V2-STATUS.md) 和 [游戏运行时素材契约](../../GAME-RUNTIME-ASSET-CONTRACT.md)。

- 检查日期：2026-08-11
- 清单：`manifest-complete.json`
- 自动报告：`qa-complete.json`
- 题材：**404/404**；`existing` 22、`expanded` 382
- 分类：80 剧情静态、40 战斗母型、24 任务单位、12 阵营基准、32 地形、24 交互结构、32 战场物件、48 装备、48 技能、24 状态、24 FX、16 HUD
- 原型交付：33 个 delivery、66 个 PNG/SVG 文件引用；其中本次新增 17 组 atlas、34 个原型文件
- 单元映射：390 个 atlas cell 全部在画布内且非空；其余 14 个既有单图题材使用整图映射
- 尺寸与文件：33 组 PNG/SVG 均存在，PNG 实际尺寸与清单一致，SVG `viewBox` 一致
- 透明度：全部 delivery 使用二值 alpha；透明 atlas 保留透明画布，场景/风格画面保持不透明
- 结果：**C02 本地构建检查与共享 `validate_complete_library.py` 均为 0 error**

### 视觉多样性结果

| 类别 | 独立 cell | 透明轮廓 | 结果 |
| --- | ---: | ---: | --- |
| 战斗单位 | 40/40 | 40/40 | 通过 |
| 任务单位 | 24/24 | 24/24 | 通过 |
| 阵营基准 | 12/12 | 12/12 | 通过 |
| 地形 | 32/32 | 不适用（不透明 Tile） | 通过 |
| 交互结构 | 24/24 | 24/24 | 通过 |
| 战场物件 | 32/32 | 32/32 | 通过 |
| 装备 | 48/48 | 48/48 | 通过 |
| 技能 | 48/48 | 48/48 | 通过 |
| 状态 | 24/24 | 24/24 | 通过 |
| FX | 24/24 | 24/24 | 通过 |
| HUD | 16/16 | 16/16 | 通过 |
| 有 cell 的剧情静态 | 66/66 | 38/38 | 通过 |

视觉检查重点：单位使用盾、枪、术式工具、机器人、载具、共生体和任务工具改变轮廓，不以换色代替兵种；地形分别使用沙丘/水路、冰裂/热区、浮台/洋流、根网/菌床、轨道/真空、废墟/记忆、舱室/管道、星脉/旋转重力；交互结构包含控台、罐站、升降机、舱门、炮台、避难所、档案柜和广播桅杆等轮廓族；FX 按命中、爆炸、气候、治疗、控制、维修、失联和占领分形。

### ImageGen 与确定性构建

- 高概念主视觉母版使用内置 ImageGen 生成，保存为 `expansion/masters/c02-style-keyart-master-imagegen.png`。
- 最终提示核心：以既有远灯号起飞图为风格/船型参考，绘制修补的民用货船驶向开始熄灭的白金人造太阳；前景强调拥挤生活舱、水管、工具和居民；限制为无文字、无水印、非光滑军舰、非通用蓝全息和非霓虹赛博朋克。
- 原型 key art 不是直接使用母版：构建器将其裁切为 16:9、降采样到 `256×144`、限制调色板后写入风格原型 atlas。
- 其余像素单位、Tile、结构、物件和图标由 `tools/build_complete_library.py` 确定性绘制，同一输入与脚本可重建一致的 PNG/SVG 配对。

## 既有独立 HD V1 素材

- 检查日期：2026-08-11
- 检查范围：`assets/` 下的正式 PNG/SVG；排除 `draft-v1/`、`masters/`、`style/`
- 正式资产：22 组 PNG/SVG，共 44 个文件
- 总结：全部通过，无阻塞问题

## 自动检查规则

- Pillow 读取每张 PNG，检查实际尺寸、不透明颜色数、唯一 RGBA 数与 alpha 通道。
- 色数上限按不透明颜色统计：人物/单位 48 色、建筑/道具 64 色、场景 96 色；透明像素不占美术调色板颜色。
- 人物、单位、建筑和道具必须使用二值透明（仅 `0/255`），且四角透明；四张场景必须完全不透明。
- 单位图集按四个 `32×48` 帧检查，每帧均须包含非透明像素。
- 道具图集按四个 `48×48` 格检查，每格均须包含非透明像素。
- 检查不透明像素中是否残留色键洋红（`R > 200`、`B > 200`、`G < 80`）。
- 核对同名 PNG/SVG 配对以及 SVG `viewBox` 与 PNG 尺寸。
- 解析每个 SVG 的像素矩形，重建完整 RGBA 像素缓冲并与 PNG 逐像素比较。
- 使用 `xmllint --noout` 验证每个正式 SVG 的 XML 结构。

“RGBA 总数”包含一个透明色，因此透明建筑出现 65 个 RGBA 时，实际仍是 64 个不透明美术颜色，符合 `--colors 64` 规格。

## PNG、颜色与逐像素配对结果

| 正式资产 | 实际尺寸 | 不透明色 / RGBA 总数 | 透明/切片检查 | SVG 逐像素 | 结果 |
| --- | ---: | ---: | --- | --- | --- |
| `architecture/farlight-cargo-ship-hd` | 128×128 | 62 / 63 | 二值 alpha；四角透明 | 一致 | 通过 |
| `architecture/kairon-ring-node-hd` | 128×128 | 64 / 65 | 二值 alpha；四角透明 | 一致 | 通过 |
| `architecture/soler-archive-monastery-hd` | 128×128 | 64 / 65 | 二值 alpha；四角透明 | 一致 | 通过 |
| `architecture/zero-rain-tower-hd` | 128×128 | 62 / 63 | 二值 alpha；四角透明 | 一致 | 通过 |
| `characters/helo-portrait-hd` | 96×112 | 45 / 46 | 二值 alpha；四角透明 | 一致 | 通过 |
| `characters/iya-portrait-hd` | 96×112 | 47 / 48 | 二值 alpha；四角透明 | 一致 | 通过 |
| `characters/mira-portrait-hd` | 96×112 | 46 / 47 | 二值 alpha；四角透明 | 一致 | 通过 |
| `characters/naim-portrait-hd` | 96×112 | 46 / 47 | 二值 alpha；四角透明 | 一致 | 通过 |
| `characters/roan-portrait-hd` | 96×112 | 46 / 47 | 二值 alpha；四角透明 | 一致 | 通过 |
| `characters/talos-7-portrait-hd` | 96×112 | 47 / 48 | 二值 alpha；四角透明 | 一致 | 通过 |
| `props/campaign-props-sheet-02-hd` | 192×48 | 63 / 64 | 二值 alpha；四角透明；4 格非空 | 一致 | 通过 |
| `props/story-props-sheet-hd` | 192×48 | 60 / 61 | 二值 alpha；四角透明；4 格非空 | 一致 | 通过 |
| `scenes/farlight-departure-hd` | 256×144 | 96 / 96 | 完全不透明 | 一致 | 通过 |
| `scenes/folding-table-covenant-hd` | 256×144 | 96 / 96 | 完全不透明 | 一致 | 通过 |
| `scenes/season-without-rain-hd` | 256×144 | 96 / 96 | 完全不透明 | 一致 | 通过 |
| `scenes/seven-minute-rain-hd` | 256×144 | 96 / 96 | 完全不透明 | 一致 | 通过 |
| `units/helo-walk-sheet-hd` | 128×48 | 44 / 45 | 二值 alpha；四角透明；4 帧非空 | 一致 | 通过 |
| `units/iya-walk-sheet-hd` | 128×48 | 45 / 46 | 二值 alpha；四角透明；4 帧非空 | 一致 | 通过 |
| `units/mira-walk-sheet-hd` | 128×48 | 41 / 42 | 二值 alpha；四角透明；4 帧非空 | 一致 | 通过 |
| `units/naim-walk-sheet-hd` | 128×48 | 40 / 41 | 二值 alpha；四角透明；4 帧非空 | 一致 | 通过 |
| `units/roan-walk-sheet-hd` | 128×48 | 42 / 43 | 二值 alpha；四角透明；4 帧非空 | 一致 | 通过 |
| `units/talos-7-walk-sheet-hd` | 128×48 | 41 / 42 | 二值 alpha；四角透明；4 帧非空 | 一致 | 通过 |

配对检查结果为 PNG 22/22、SVG 22/22；没有孤立文件。所有 SVG `viewBox` 均与对应 PNG 尺寸一致，22 组 SVG 重建像素与 PNG 完全一致，所有不透明像素中的色键洋红检出数均为 0。

## SVG XML 结果

以下 22 个正式 SVG 均通过 `xmllint --noout`：

- `architecture/farlight-cargo-ship-hd.svg`
- `architecture/kairon-ring-node-hd.svg`
- `architecture/soler-archive-monastery-hd.svg`
- `architecture/zero-rain-tower-hd.svg`
- `characters/helo-portrait-hd.svg`
- `characters/iya-portrait-hd.svg`
- `characters/mira-portrait-hd.svg`
- `characters/naim-portrait-hd.svg`
- `characters/roan-portrait-hd.svg`
- `characters/talos-7-portrait-hd.svg`
- `props/campaign-props-sheet-02-hd.svg`
- `props/story-props-sheet-hd.svg`
- `scenes/farlight-departure-hd.svg`
- `scenes/folding-table-covenant-hd.svg`
- `scenes/season-without-rain-hd.svg`
- `scenes/seven-minute-rain-hd.svg`
- `units/helo-walk-sheet-hd.svg`
- `units/iya-walk-sheet-hd.svg`
- `units/mira-walk-sheet-hd.svg`
- `units/naim-walk-sheet-hd.svg`
- `units/roan-walk-sheet-hd.svg`
- `units/talos-7-walk-sheet-hd.svg`

## 生成与目视检查记录

- 9 组新增母版均使用内置 `imagegen` 单张顺序生成，纯 `#ff00ff` 色键；未使用 CLI。
- 赫洛与伊娅的头像、四帧单位分别检查了人物身份、服装一致性、四格数量和缩图轮廓。
- 两座建筑分别检查了索勒冷/暖层级、凯隆环形权限层级以及 128×128 缩图可读性。
- 两张场景分别检查了有限舱位/留下者和五界材料/独立接口/私人门控的叙事可读性。
- 第二组道具检查了精确四格顺序、两张撤离票数量和无可读文字。
- 凯隆节点首次调用遇到 1 次临时 401，使用同一内置流程重试 1 次后成功；其余调用无重试。

## 已知边界

本次检查覆盖文件完整性、静态像素规格、透明度、调色板上限、图集切片、色键残留、SVG XML 合法性与 PNG/SVG 逐像素一致；尚未在具体游戏引擎中验证纹理导入参数、实际动画节拍和缩放相机。接入时必须关闭纹理过滤与有损压缩，并使用整数倍率缩放。
