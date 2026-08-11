# Runtime V2 覆盖状态

> 自动生成；请运行 `python3 pixel-master-tools/generate_runtime_v2_status.py` 更新。

口径：`outputCount` 是运行时文件输出；`exactTopicCount` 只统计存在于对应 404 清单、类别匹配且不重复的 `topicId`。没有 `topicId` 的 primary 资产只算输出，不算精确题材覆盖。若 manifest 提供 `gameIntegration.runtimeReadyAssetIds`，`runtimeReadySourceCount` 只统计列表内资产；否则才回退到整包 `runtimeReady`。其余输出计入 `candidateCount`。

## 断冠之誓（candidate-01）

目标 404；运行时输出 126；精确 topic 96；实机来源 15；候选 111。

| 类别 | 目标 | 输出 | 精确 topic | 实机来源 | 候选 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 叙事静态图 (`narrative-static`) | 80 | 5 | 4 | 0 | 5 |
| 战斗单位 (`combat-unit`) | 40 | 40 | 36 | 4 | 36 |
| 任务单位 (`mission-unit`) | 24 | 8 | 8 | 0 | 8 |
| 阵营套件 (`faction-kit`) | 12 | 0 | 0 | 0 | 0 |
| 地形 (`terrain`) | 32 | 16 | 8 | 8 | 8 |
| 交互设施 (`interactive-structure`) | 24 | 9 | 4 | 3 | 6 |
| 战场物件 (`battle-prop`) | 32 | 8 | 8 | 0 | 8 |
| 装备 (`equipment`) | 48 | 12 | 8 | 0 | 12 |
| 技能 (`skill`) | 48 | 12 | 8 | 0 | 12 |
| 状态 (`status`) | 24 | 4 | 4 | 0 | 4 |
| FX (`fx`) | 24 | 8 | 4 | 0 | 8 |
| HUD (`hud`) | 16 | 4 | 4 | 0 | 4 |

### 战斗单位 40 母型

| 阶段 | 输出 | 精确 topic | 说明 |
| --- | ---: | ---: | --- |
| Primary | 4 | 0 | 语义映射 4；无 topicId 不算精确覆盖 |
| Batch-02 | 8 | 8 | 精确 topicId |
| Batch-03 | 28 | 28 | 精确 topicId |
| 累计 | 40 | 36 | 生产覆盖 40/40；精确题材 36/40 |

来源 manifest：

- `primary` · partial · 30 outputs · 0 exact topics · 15 ready / 15 candidate · policy `gameIntegration.runtimeReadyAssetIds` · `candidate-01/assets/manifest-runtime-v2.json`
- `b02` · candidate · 68 outputs · 68 exact topics · 0 ready / 68 candidate · policy `manifest.runtimeReady` · `candidate-01/assets/manifest-runtime-v2-b02.json`
- `b03` · candidate · 28 outputs · 28 exact topics · 0 ready / 28 candidate · policy `manifest.runtimeReady` · `candidate-01/assets/manifest-runtime-v2-b03.json`

## 群星熄灭之前（candidate-02）

目标 404；运行时输出 123；精确 topic 96；实机来源 0；候选 123。

| 类别 | 目标 | 输出 | 精确 topic | 实机来源 | 候选 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 叙事静态图 (`narrative-static`) | 80 | 5 | 4 | 0 | 5 |
| 战斗单位 (`combat-unit`) | 40 | 40 | 36 | 0 | 40 |
| 任务单位 (`mission-unit`) | 24 | 8 | 8 | 0 | 8 |
| 阵营套件 (`faction-kit`) | 12 | 0 | 0 | 0 | 0 |
| 地形 (`terrain`) | 32 | 16 | 8 | 0 | 16 |
| 交互设施 (`interactive-structure`) | 24 | 6 | 4 | 0 | 6 |
| 战场物件 (`battle-prop`) | 32 | 8 | 8 | 0 | 8 |
| 装备 (`equipment`) | 48 | 12 | 8 | 0 | 12 |
| 技能 (`skill`) | 48 | 12 | 8 | 0 | 12 |
| 状态 (`status`) | 24 | 4 | 4 | 0 | 4 |
| FX (`fx`) | 24 | 8 | 4 | 0 | 8 |
| HUD (`hud`) | 16 | 4 | 4 | 0 | 4 |

### 战斗单位 40 母型

| 阶段 | 输出 | 精确 topic | 说明 |
| --- | ---: | ---: | --- |
| Primary | 4 | 0 | 语义映射 4；无 topicId 不算精确覆盖 |
| Batch-02 | 8 | 8 | 精确 topicId |
| Batch-03 | 28 | 28 | 精确 topicId |
| 累计 | 40 | 36 | 生产覆盖 40/40；精确题材 36/40 |

来源 manifest：

- `primary` · candidate · 27 outputs · 0 exact topics · 0 ready / 27 candidate · policy `manifest.runtimeReady` · `candidate-02/assets/manifest-runtime-v2.json`
- `b02` · candidate · 68 outputs · 68 exact topics · 0 ready / 68 candidate · policy `manifest.runtimeReady` · `candidate-02/assets/manifest-runtime-v2-b02.json`
- `b03` · candidate · 28 outputs · 28 exact topics · 0 ready / 28 candidate · policy `manifest.runtimeReady` · `candidate-02/assets/manifest-runtime-v2-b03.json`

## 布衣定鼎（candidate-03）

目标 404；运行时输出 123；精确 topic 96；实机来源 0；候选 123。

| 类别 | 目标 | 输出 | 精确 topic | 实机来源 | 候选 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 叙事静态图 (`narrative-static`) | 80 | 5 | 4 | 0 | 5 |
| 战斗单位 (`combat-unit`) | 40 | 40 | 36 | 0 | 40 |
| 任务单位 (`mission-unit`) | 24 | 8 | 8 | 0 | 8 |
| 阵营套件 (`faction-kit`) | 12 | 0 | 0 | 0 | 0 |
| 地形 (`terrain`) | 32 | 16 | 8 | 0 | 16 |
| 交互设施 (`interactive-structure`) | 24 | 6 | 4 | 0 | 6 |
| 战场物件 (`battle-prop`) | 32 | 8 | 8 | 0 | 8 |
| 装备 (`equipment`) | 48 | 12 | 8 | 0 | 12 |
| 技能 (`skill`) | 48 | 12 | 8 | 0 | 12 |
| 状态 (`status`) | 24 | 4 | 4 | 0 | 4 |
| FX (`fx`) | 24 | 8 | 4 | 0 | 8 |
| HUD (`hud`) | 16 | 4 | 4 | 0 | 4 |

### 战斗单位 40 母型

| 阶段 | 输出 | 精确 topic | 说明 |
| --- | ---: | ---: | --- |
| Primary | 4 | 0 | 语义映射 4；无 topicId 不算精确覆盖 |
| Batch-02 | 8 | 8 | 精确 topicId |
| Batch-03 | 28 | 28 | 精确 topicId |
| 累计 | 40 | 36 | 生产覆盖 40/40；精确题材 36/40 |

来源 manifest：

- `primary` · candidate · 27 outputs · 0 exact topics · 0 ready / 27 candidate · policy `manifest.runtimeReady` · `candidate-03/assets/manifest-runtime-v2.json`
- `b02` · candidate · 68 outputs · 68 exact topics · 0 ready / 68 candidate · policy `manifest.runtimeReady` · `candidate-03/assets/manifest-runtime-v2-b02.json`
- `b03` · candidate · 28 outputs · 28 exact topics · 0 ready / 28 candidate · policy `manifest.runtimeReady` · `candidate-03/assets/manifest-runtime-v2-b03.json`

## 校验

状态：**通过**；错误 0。
