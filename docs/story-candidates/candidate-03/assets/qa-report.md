# 《布衣定鼎》正式像素素材 QA 报告

- 检查日期：2026-08-11
- 检查范围：`characters/`、`units/`、`architecture/`、`scenes/`、`props/` 内文件名以 `-hd` 结尾的正式 PNG/SVG
- 排除范围：`draft-v1/`、`masters/`、`style/`
- 检查结果：**通过**

## 404 题材槽位原型 QA

本节对应 [manifest-complete.json](./manifest-complete.json) 与 [qa-complete.json](./qa-complete.json)，只证明题材、文件和图集单元的原型结构通过检查，不证明运行时质量或游戏接入完成。运行时候选、精确覆盖与验收门槛分别见 [Runtime V2 素材画廊](../../RUNTIME-V2-ASSET-GALLERY.html)、[Runtime V2 覆盖状态](../../RUNTIME-V2-STATUS.md) 和 [游戏运行时素材契约](../../GAME-RUNTIME-ASSET-CONTRACT.md)。下方旧表继续记录原 22 个单体 HD 资产的逐像素检查。

| 检查项 | 结果 |
| --- | --- |
| 题材覆盖 | **404/404**；12 类数量全部命中统一目标 |
| 来源口径 | `existing` 22，`expanded` 382；实名英雄单位不计战斗母型 |
| Delivery | 33 个 PNG/SVG 对，共 66 个被引用文件；其中新增 atlas 17 对、34 文件 |
| ID 与映射 | 404 个唯一 ID；390 个 atlas 单元非空且每题材只被一个 delivery 声明 |
| 两项返工 | 官粮封条、无籍灾民临时册均通过，旧道具图集未被覆盖 |
| 文件与规格 | PNG/SVG 全部存在；尺寸与 SVG `viewBox` 一致；透明交付均非空 |
| 视觉重复 | 各类别归一化后无完全重复 cell；所有透明类别的独立轮廓比例高于 50% 门槛 |
| 关键多样性 | 战斗单位 40/40 独立 cell 与轮廓；任务单位 24/24；地形 32/32；结构 24/24；技能 48/48 |
| 结果 | **通过**；`qa-complete.json` 错误数 0 |

## 汇总

| 检查项 | 结果 |
| --- | --- |
| 清单覆盖 | 22 个逻辑资产，44 个正式文件；每项均有同名 PNG/SVG 配对 |
| PNG 尺寸 | 全部符合 `manifest-hd.json` 与 README 的现行规格 |
| Alpha | 人物、单位、建筑、道具均为二值透明；场景完全不透明；无半透明杂边 |
| 帧切分 | 6 套单位图集的 4 个 `32×48` 帧均非空；2 套道具图集的 4 个 `48×48` 单元均非空 |
| SVG 结构 | 全部通过 `xmllint --noout`；`width`、`height`、`viewBox` 与 PNG 一致 |
| PNG/SVG 像素一致性 | 将 SVG 的逐行矩形重建为 RGBA 后逐像素比较，22/22 均为 0 差异像素 |
| 色键残留 | 按 `R>200, B>170, G<80, A>0` 扫描洋红色键，22/22 均为 0 像素 |
| 画廊引用 | `gallery-hd.html` 的 22 个 PNG 引用全部存在，且与清单 PNG 集合完全一致 |
| JSON | `manifest-hd.json` 可解析，路径、尺寸和类型字段齐全 |

## 逐项结果

颜色数包含透明色；“不透明/透明”是像素数量。SVG 矩形数用于确认文件不是嵌入位图的包装 SVG。

| ID | PNG 尺寸 | RGBA 色数 | 不透明/透明 | SVG 矩形 | 像素差异 | 结果 |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| `shen-li-22-portrait` | 96×112 | 49 | 6314/4438 | 5112 | 0 | 通过 |
| `lu-qinghe-portrait` | 96×112 | 49 | 5485/5267 | 4328 | 0 | 通过 |
| `han-yue-portrait` | 96×112 | 49 | 6116/4636 | 5085 | 0 | 通过 |
| `pei-zhao-portrait` | 96×112 | 49 | 6212/4540 | 5052 | 0 | 通过 |
| `jiang-zhaoye-portrait` | 96×112 | 49 | 5702/5050 | 4569 | 0 | 通过 |
| `aletan-portrait` | 96×112 | 49 | 6712/4040 | 5641 | 0 | 通过 |
| `shen-li-22-walk-sheet` | 128×48 | 45 | 2066/4078 | 1872 | 0 | 通过 |
| `lu-qinghe-walk-sheet` | 128×48 | 46 | 2065/4079 | 1823 | 0 | 通过 |
| `han-yue-walk-sheet` | 128×48 | 45 | 2222/3922 | 2005 | 0 | 通过 |
| `pei-zhao-walk-sheet` | 128×48 | 46 | 1835/4309 | 1640 | 0 | 通过 |
| `jiang-zhaoye-walk-sheet` | 128×48 | 45 | 1814/4330 | 1644 | 0 | 通过 |
| `aletan-walk-sheet` | 128×48 | 41 | 1914/4230 | 1750 | 0 | 通过 |
| `county-granary` | 128×128 | 65 | 9143/7241 | 8200 | 0 | 通过 |
| `huai-right-bank-dike` | 128×128 | 64 | 7506/8878 | 6387 | 0 | 通过 |
| `linchuan-government-hub` | 128×128 | 65 | 8476/7908 | 7751 | 0 | 通过 |
| `great-lake-mixed-fleet` | 128×128 | 63 | 5892/10492 | 5190 | 0 | 通过 |
| `opening-the-county-granary` | 256×144 | 96 | 36864/0 | 30945 | 0 | 通过 |
| `rain-night-crossing` | 256×144 | 96 | 36864/0 | 31091 | 0 | 通过 |
| `great-lake-precision-fire-attack` | 256×144 | 96 | 36864/0 | 30946 | 0 | 通过 |
| `one-bowl-of-new-grain` | 256×144 | 96 | 36864/0 | 29832 | 0 | 通过 |
| `story-props-sheet` | 192×48 | 61 | 4012/5204 | 3021 | 0 | 通过 |
| `campaign-props-sheet-02` | 192×48 | 61 | 4171/5045 | 3752 | 0 | 通过 |

## 接入注意

- 人物、单位、建筑和道具的透明边缘没有半透明抗锯齿；引擎缩放必须使用 nearest-neighbor，并优先采用整数倍。
- `county-granary` 与 `linchuan-government-hub` 的 65 个 RGBA 色均包含 64 个不透明色和 1 个透明色，符合建筑 64 色预算。
- 场景是全画幅不透明资源；若需要可关闭的雾、雨、灯火或水光，运行时应另外制作 FX 层，不能从当前合成图自动分离。
- 建筑 PNG 的透明轮廓只负责视觉构图，不应直接作为碰撞体或寻路足迹。
