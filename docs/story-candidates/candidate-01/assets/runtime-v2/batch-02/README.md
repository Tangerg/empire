# 《断冠之誓》runtime-v2 Batch 02

这是不覆盖首批已实机验收素材的增量包。它新增 `68` 个运行时资产，`runtimeReady=false`；只有接入真实关卡并通过实机截图验收后才可提升质量状态。

## 入口

- 运行时清单：`../../manifest-runtime-v2-b02.json`
- 独立 QA：`../../qa-runtime-v2-b02.json`
- 锁题表：`TOPIC-LOCK.json`
- ImageGen 提示词与模式：`PROMPTS.md`
- 可复现处理器：`tools/process_batch_02.py`
- 1× 总览：`previews/overview-1x.png`

## 本批覆盖

| 类别 | 资产数 | 运行时规格 |
| --- | ---: | --- |
| 战斗单位 | 8 | 7 个 `128×48`，狼骑 `256×64`；均四帧 |
| 任务单位 | 8 | `128×48`，四帧 |
| 地形 | 8 | 6 套四变体、2 套 16-mask，共 56 cells |
| 交互建筑 | 4 | `96×96` 单态，正常/受损/占领三态 |
| 地图物件 | 8 | `32×32` |
| 装备 / 技能 | 8 / 8 | `32×32` |
| 状态 / HUD | 4 / 4 | `24×24` |
| FX | 4 | `32×32` 单帧、四帧横排 |
| 剧情场景 | 4 | `256×144`，不透明 |

## 重建与验收

在仓库根目录运行：

```bash
python3 docs/story-candidates/candidate-01/assets/runtime-v2/batch-02/tools/process_batch_02.py
python3 docs/story-candidates/pixel-master-tools/validate_runtime_v2_batches.py
```

处理器只写 `runtime-v2/batch-02/`、`manifest-runtime-v2-b02.json` 与 `qa-runtime-v2-b02.json`。QA 逐项核对 `topicId`、类别、文件、尺寸、单位脚线、结构三态、56 个地形 cell/接缝/亮度、图标与 FX 重复、场景不透明度，并记录各预览实际绘制的完整资产 ID 集合。

