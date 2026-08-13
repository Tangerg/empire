# 剧本一：通用西幻 SRPG 环境建造包 V1.1

这是《断冠之誓》整场战役可复用的地图素材系统，不是第一关专用背景图。它补足原 404 题材包中“能列出地形题材、但不足以稳定拼出丰富地图”的部分，并与现有单位、建筑和战场物件共享同一高斜俯视卡通西幻语言。

## 交付结论

| 项目 | 数量 |
| --- | ---: |
| 运行时图集 | 36 套 |
| 运行时 PNG | 72 张（36 张 1× + 36 张 2×） |
| 可拼装部件 | 1188 个 |
| 原创母图 | 13 张 |
| 去背中间图 | 9 张 |
| 目检/拼接预览 | 9 张 |
| 本包新增 PNG 合计 | 103 张 |

1188 个部件包含：32 基础地表、188 八邻域过渡、256 道路、256 路肩、256 水体、32 自动高差边缘、32 高差模块、32 森林、16 区域小景、16 桥梁/工事、16 营地/地基、16 大型地标、24 地面贴花和 16 农村生活件。

V1.1 不把 1188 个部件全部塞进第一关。第一关使用 [`scenes/c01-01.scene.json`](scenes/c01-01.scene.json) 的 10 套图集白名单，并明确禁用大型城堡、水体、寒地和墓园素材。

## 使用入口

- 运行时清单：[`manifest-environment-builder-v1.json`](manifest-environment-builder-v1.json)
- 机器验收：[`qa-environment-builder-v1.json`](qa-environment-builder-v1.json)
- 素材规格：[`ASSET-SPEC.md`](ASSET-SPEC.md)
- 母图与提示记录：[`PROMPTS.md`](PROMPTS.md)
- 可复建构建器：[`tools/build_environment_builder_v1.py`](tools/build_environment_builder_v1.py)
- 1×/2× 图集：[`runtime/atlas/`](runtime/atlas/)
- 逐单元语义与足迹：[`SEMANTIC-CATALOG.json`](SEMANTIC-CATALOG.json)
- 第一关表现层配置：[`scenes/c01-01.scene.json`](scenes/c01-01.scene.json)

游戏接入应读取 Manifest，而不是在代码里硬编码文件名。当前 `runtimeReady=false` 的含义是：素材包与机器 QA 已完成，但还没有替换真实关卡中的 SVG 道路/山体并完成关卡截图验收。

## 图层顺序

推荐从下到上：基础地表 → 47 格过渡 → 道路/水体 → 高差边缘 → 建筑地基 → 地面贴花 → 桥墙工事/大型地标 → 森林与区域小景 → 单位 → 前景遮挡。

道路、水体和高差边缘都按 N/E/S/W 位掩码选择；材质交界按八邻域 Blob47 选择。V1.1 的每个道路/水体掩码都有 4 个材质和轮廓变化，索引为 `mask * 4 + variant`；道路另有正式 `route-edge` 路肩、车辙和破损覆盖层。普通地表有四个可随机变体，边缘已经锁定；`regional_surface_variant` 以 4×3 区域为主、少量微扰为辅，避免固定循环造成棋盘重复。

## 高低差接入

Manifest 的 `elevationContract` 直接绑定既有 `GameMap.elevation` 整数层：0 为地面，1 为一级高地，2 为二级山脊；更高层按差值叠加岩壁。坡道只负责表现合法的可通行边，通行代价、最大攀爬/坠落、制高点命中、遮挡与视线仍由现有战斗系统决定。

严禁用一张带山的平面贴图假装高度，也严禁由美术图反推规则高度。

## 验收结果

构建器目前通过：1188/1188 部件、72/72 运行时 PNG、256 次基础地表跨变体边缘比较、道路/水体 16-mask × 4 变体连接口、152 个语义单元、第一关白名单、整关预览尺寸、土路/草地明度差、区域地表分布、透明洋红残留、单元重复、Manifest 尺寸与文件存在性检查；全部为 0 错误。

重建命令：

```bash
python3 docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/tools/build_environment_builder_v1.py
```

## 预览

- [`previews/twin-hills-v1.1-scene-1x.png`](previews/twin-hills-v1.1-scene-1x.png)：按 21×13 规则地图、两组 `elevation=2` 高地和第一关白名单搭建的 V1.1 场景语言证明
- [`previews/compatibility-map-temperate-1x.png`](previews/compatibility-map-temperate-1x.png)：与现有单位/建筑混搭
- [`previews/elevation-system-1x.png`](previews/elevation-system-1x.png)：0/1/2 层高差、坡道与断崖
- [`previews/biome-coverage-1x.png`](previews/biome-coverage-1x.png)：雪境、荒地、墓园、锻炉区域
- [`previews/environment-atlases-overview.png`](previews/environment-atlases-overview.png)：36 套图集总览
