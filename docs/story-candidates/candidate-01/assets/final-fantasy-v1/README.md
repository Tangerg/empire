# 剧本一：卡通西幻完整游戏素材包

本目录是剧本一当前唯一的最终美术生产包。它不是写实概念设定集，也不继承旧 `runtime-v2` 的像素风格。

## 定调

- 题材：原创、古典、质朴的西方奇幻世界；以史诗旅程感、诸族联盟和边境战争为气质参考，不复制任何现有作品的角色、徽记或建筑。
- 造型：大头、短身、夸张手脚、清楚的职业剪影。
- 表现：粗深色描边、哑光低至中等饱和度、两段赛璐璐阴影、少量材质纹理。
- 镜头：单位为游戏侧前视；地形、建筑和剧情关卡图采用高位斜俯视。
- 禁止：写实人体比例、照片质感、厚重油画、光滑 3D、无意义的高饱和发光，以及对已有商业 IP 的直接模仿。

正式定调图：[`../../art-assets/reference/art-direction-map.png`](../../art-assets/reference/art-direction-map.png)

## 交付规模

| 类别 | 数量 | 游戏形态 |
|---|---:|---|
| 剧情静态图 | 80 | 角色卡、地点/事件图、关键道具 |
| 战斗单位 | 40 | 每单位 4 帧横条，32/64 像素级帧宽 |
| 任务单位 | 24 | 每单位 4 帧横条，32×48 单帧 |
| 阵营基准 | 12 | 128×128 装备/材质板 |
| 地形 | 32 | 4 变体或 N/E/S/W 16-mask |
| 交互建筑 | 24 | 正常、受损、占领三态 |
| 战场物件 | 32 | 透明背景可放置物件 |
| 装备 | 48 | 透明背景 48×48 图标 |
| 技能 | 48 | 透明背景 48×48 图标 |
| 状态 | 24 | 透明背景 32×32 图标 |
| 特效 | 24 | 每项 4 帧横条 |
| HUD | 16 | 透明背景 32×32 图标 |
| **总计** | **404** | **全部有唯一 topicId 和 PNG** |

## 游戏接入

- 正式清单：[`manifest-final-fantasy-v1.json`](manifest-final-fantasy-v1.json)
- 运行时文件：[`runtime/`](runtime/)
- 战场 2× 高清文件：[`runtime-hd/`](runtime-hd/)
- 战场高清清单：[`manifest-tactical-runtime-hd.json`](manifest-tactical-runtime-hd.json)
- QA 结果：[`qa-final-fantasy-v1.json`](qa-final-fantasy-v1.json)
- 可复建工具：[`tools/build_final_fantasy_v1.py`](tools/build_final_fantasy_v1.py)
- 原始母图：[`masters/`](masters/)
- 自动去背中间稿：[`intermediate/alpha/`](intermediate/alpha/)

### 通用地图环境建造包

404 项正式清单覆盖“内容题材”，不等于已经拥有完整地图拼装语法。通用环境建造包 V1.1 位于 [`environment-builder-v1/`](environment-builder-v1/README.md)，包含 36 套 1×/2× 图集与 1188 个可复用部件，覆盖地表变体、八邻域过渡、四变体道路/水体、路肩、0/1/2 层高差、坡道、断崖、森林、桥墙工事、营地地基、大型地标、贴花和农村生活件。

该包专门用于替换程序绘制道路、假山轮廓和重复连续底图；它引用现有 `GameMap.elevation`，不改变战斗规则，也不抵扣 404 个题材。接入真实关卡完成截图验收前保持 `runtimeReady=false`。

`manifest-final-fantasy-v1.json` 是游戏接入时的唯一索引。每项记录都包含 `topicId`、类别、实际 PNG 路径、尺寸和运行时规格；单位/建筑/特效额外记录帧尺寸与顺序，地形记录 `variants` 或 `connectionMasks`。

战场表现层会按同一个 `topicId` 自动优先解析 `runtime-hd/`。该包从 1200～1700px 的批准母版重新采样，覆盖 40 个战斗单位、24 个任务单位、32 个地形、24 个建筑、32 个战场物件、24 个特效与 1 张第一关连续地表，共 177 项。它只提高像素密度，逻辑帧宽、锚点、占地与战斗坐标保持不变，因此不会把高清规格耦合进战斗内核。

## 预览

- [`previews/units-1x.png`](previews/units-1x.png)：40 战斗单位 + 24 任务单位
- [`previews/map-assets-1x.png`](previews/map-assets-1x.png)：阵营、地形、建筑、战场物件
- [`previews/icons-fx-1x.png`](previews/icons-fx-1x.png)：装备、技能、状态、特效、HUD
- [`previews/narrative-1x.png`](previews/narrative-1x.png)：80 个剧情静态题材

## 角色与世界一致性约束

- 莱娅不是隐藏王族或天选之女；三个年龄阶段通过职责、磨损和可拆权力标记区分。
- 罗德里克是可靠的老兵导师，不使用反派化黑甲。
- 凯恩的帝国军与改革军都保留标准化装备语言，他不是“邪恶黑骑士”。
- 米蕾尔依靠姓名册、木片和记忆灯工作；归名亡者不能被简化成骷髅军。
- 银林是有育儿、修理与争议的社会；山炉有劳动者与档案；荒原诸部不是野蛮人模板。
- 伊芙拉和古龙不佩戴永久鞍具或缰具；断裂骑具只作为被丢弃的剧情道具。
- 无旗者是普通人的集体记忆，不是王、骷髅怪或单一英雄。
- 终局保留多面旗帜、多种符号与公开分歧，不把联盟画成单一帝国。

## QA

运行：

```bash
python3 docs/story-candidates/candidate-01/assets/final-fantasy-v1/tools/build_final_fantasy_v1.py
python3 docs/story-candidates/candidate-01/assets/final-fantasy-v1/tools/build_tactical_runtime_hd.py
```

构建器会从母图重新切图并覆盖 `runtime/`、manifest、预览和 QA 文件。当前验收要求：404/404 覆盖、PNG 实际尺寸等于清单声明、无重复 topicId、四帧条不是静态复制、最终不透明洋红像素为 0。
