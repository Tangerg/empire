# 《断冠之誓》正式 HD 素材 QA 报告

- 检查日期：2026-08-11
- 检查范围：`characters/`、`units/`、`architecture/`、`scenes/`、`props/` 中的 `*-hd.png` 与 `*-hd.svg`
- 明确排除：`draft-v1/`、`masters/`、`style/`
- 工具：Pillow 12.3.0、xmllint（libxml 2.9.13）
- 结论：**通过**。22 项正式资产均具备 PNG/SVG 配对，尺寸、调色板、二值透明、四帧/四格占用、洋红色键清理、XML 结构与 PNG/SVG 逐像素一致性均符合当前交付约定。

## 本轮生成与目视复核

- 生成路径：内置 ImageGen，严格单张顺序调用；未使用 CLI。
- 本轮逻辑资产：9 组；生成调用 9 次；首次通过 9 次；重试 0 次；401 失败 0 次。
- 所有透明母版使用纯 `#ff00ff` 色键并保存在 `masters/`，生成目录内原文件保留未删除。
- 布兰：头像与四帧单位均确认人物身份、深绿修补衣、短弩、修靴工具和焦痕护具连续；四帧无裁切或重叠。
- 塔莎：头像与四帧单位均确认黄褐军需轮廓、账本、筹绳、靴牌、工程工具与短弩连续；无贵族或海盗化设计。
- 三桥河谷：确认三条分流与三座不同桥型均可数，中桥白布、修复木和泥泞车辙清楚。
- 银林树城：确认银皮母树、生活步道、空育儿亭和人类边缘屋同框，没有白衣精灵宫殿或金色尖塔。
- 白河夜渡：确认粮车、工具车、伤员车三种选择与布兰浅滩引路均在目标画面保留。
- 七塔王城终局远景：确认正好七座主要誓塔在 `256×144` 仍可清楚计数，城市功能与多旗联军同框。
- 第二组道具：确认四格顺序正确，控制誓文只有抽象断环结构，无可读文字或现成纹章。

## 验收规则

1. 人物头像必须为 `96×112`，RGBA 颜色总数不超过 49（48 个前景色 + 透明），四角透明。
2. 单位图集必须为 `128×48`，可均分为 4 个 `32×48` 帧，RGBA 颜色总数不超过 49，四角透明。
3. 建筑必须为 `128×128`，RGBA 颜色总数不超过 65（64 个前景色 + 透明），四角透明。
4. 场景必须为 `256×144`，不透明，颜色总数不超过 96。
5. 道具图集必须为 `192×48`，可均分为 4 个 `48×48` 图标，RGBA 颜色总数不超过 65，四角透明。
6. 每项 PNG 必须存在同名 SVG；SVG 的 `width`、`height`、`viewBox` 必须与 PNG 一致，并通过 `xmllint --noout`。
7. 单位图集的 4 个 `32×48` 帧与道具图集的 4 个 `48×48` 格必须分别包含非透明像素。
8. 透明素材的不透明像素中不得残留近似色键像素：`R > 180`、`B > 180`、`G < 100` 且 `|R-B| < 80`。
9. 将 SVG 的每个像素矩形重新栅格化后，必须与配对 PNG 的 RGBA 字节逐像素一致。

## PNG 检查结果

“RGBA 色数”包含透明色；“四角 α”按左上、右上、左下、右下排列。

| 文件 | 尺寸 | RGBA 色数 | Alpha 级数 | 四角 α | 结果 |
| --- | ---: | ---: | ---: | --- | --- |
| `characters/laiya-18-portrait-hd.png` | 96×112 | 49 | 2 | 0, 0, 0, 0 | 通过 |
| `characters/roderick-portrait-hd.png` | 96×112 | 49 | 2 | 0, 0, 0, 0 | 通过 |
| `characters/kain-portrait-hd.png` | 96×112 | 49 | 2 | 0, 0, 0, 0 | 通过 |
| `characters/mirelle-portrait-hd.png` | 96×112 | 48 | 2 | 0, 0, 0, 0 | 通过 |
| `characters/bran-portrait-hd.png` | 96×112 | 49 | 2 | 0, 0, 0, 0 | 通过 |
| `characters/tasha-portrait-hd.png` | 96×112 | 49 | 2 | 0, 0, 0, 0 | 通过 |
| `units/laiya-18-walk-sheet-hd.png` | 128×48 | 40 | 2 | 0, 0, 0, 0 | 通过 |
| `units/roderick-walk-sheet-hd.png` | 128×48 | 47 | 2 | 0, 0, 0, 0 | 通过 |
| `units/kain-walk-sheet-hd.png` | 128×48 | 47 | 2 | 0, 0, 0, 0 | 通过 |
| `units/mirelle-walk-sheet-hd.png` | 128×48 | 42 | 2 | 0, 0, 0, 0 | 通过 |
| `units/bran-walk-sheet-hd.png` | 128×48 | 45 | 2 | 0, 0, 0, 0 | 通过 |
| `units/tasha-walk-sheet-hd.png` | 128×48 | 45 | 2 | 0, 0, 0, 0 | 通过 |
| `architecture/redstone-oath-tower-hd.png` | 128×128 | 65 | 2 | 0, 0, 0, 0 | 通过 |
| `architecture/gray-banner-field-camp-hd.png` | 128×128 | 65 | 2 | 0, 0, 0, 0 | 通过 |
| `architecture/three-bridges-river-valley-hd.png` | 128×128 | 65 | 2 | 0, 0, 0, 0 | 通过 |
| `architecture/silverwood-tree-city-hd.png` | 128×128 | 61 | 2 | 0, 0, 0, 0 | 通过 |
| `scenes/gray-flag-over-burned-village-hd.png` | 256×144 | 96 | 1 | 255, 255, 255, 255 | 通过 |
| `scenes/twin-hills-first-command-hd.png` | 256×144 | 96 | 1 | 255, 255, 255, 255 | 通过 |
| `scenes/white-river-night-crossing-hd.png` | 256×144 | 96 | 1 | 255, 255, 255, 255 | 通过 |
| `scenes/seven-towers-at-dusk-hd.png` | 256×144 | 96 | 1 | 255, 255, 255, 255 | 通过 |
| `props/story-props-sheet-hd.png` | 192×48 | 62 | 2 | 0, 0, 0, 0 | 通过 |
| `props/campaign-props-sheet-02-hd.png` | 192×48 | 64 | 2 | 0, 0, 0, 0 | 通过 |

透明素材只使用 α=0 与 α=255 两级，没有半透明边缘；四张场景只使用 α=255，符合烘焙成品场景约定。

## 分片、色键与像素一致性

| 检查 | 结果 |
| --- | --- |
| 6 张单位图集，共 24 个 `32×48` 帧 | 24/24 含非透明像素，通过 |
| 2 张道具图集，共 8 个 `48×48` 格 | 8/8 含非透明像素，通过 |
| 透明素材近似洋红色键残留 | 0 像素，通过 |
| SVG 像素矩形重建后与 PNG 比较 | 22/22 RGBA 逐像素一致，通过 |

逐像素一致性不是只比较外观或尺寸：检查脚本从 SVG 的每个 `<rect>` 读取 `x/y/width/height/fill/opacity`，在透明画布上重建 RGBA 图，再用 Pillow `ImageChops.difference()` 与 PNG 比较；22 项差异包围盒均为空。

## PNG/SVG 配对与 XML 检查

- 正式资产：22 项
- PNG 文件：22 个
- SVG 文件：22 个
- 缺失配对：0
- `xmllint --noout` 失败：0
- SVG 尺寸或 `viewBox` 与 PNG 不一致：0

全部 SVG 均为实际像素矩形，不含 base64 PNG 嵌入；根节点具备 `shape-rendering="crispEdges"` 与 `image-rendering:pixelated`。

## 接入注意事项

- 当前正式 SVG 是扁平像素矩形集合，不含可单独开关的 `background`、`foreground` 或 `hd2d-fx` 图层。场景光效已经烘焙。
- `props/story-props-sheet-hd.*` 必须按固定的 48 像素宽切片，顺序为灰旗、自由誓石、王冠碎片、巨龙盟约扣。
- `props/campaign-props-sheet-02-hd.*` 必须按固定的 48 像素宽切片，顺序为莱娅左手护腕、逃亡硬饼、控制誓文残片、三族盟约扣件。
- `draft-v1/` 内资产没有纳入本次检查，也不应进入正式构建或资产索引。

## 复查命令

PNG 的尺寸、颜色数与 Alpha 可使用 Pillow 的 `Image.open(...).convert("RGBA")`、`get_flattened_data()` 和 `getpixel()` 复查。SVG 使用以下命令验证：

```bash
find docs/story-candidates/candidate-01/assets -type f -name '*-hd.svg' \
  ! -path '*/draft-v1/*' ! -path '*/masters/*' ! -path '*/style/*' \
  -print0 | xargs -0 -n1 xmllint --noout
```
