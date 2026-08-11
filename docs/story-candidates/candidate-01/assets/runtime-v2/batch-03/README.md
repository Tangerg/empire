# 《断冠之誓》runtime-v2 Batch 03

战斗单位收口批次：在主批 4 个语义映射和 B02 8 个精确题材之外，交付剩余 28 个 `combat-unit`，累计覆盖 `40/40`。本批保持 `runtimeReady=false`，不覆盖主批或 B02。

## 入口

- 题材锁：`TOPIC-LOCK.json`
- 提示词：`PROMPTS.md`
- 锁题脚本：`scripts/lock_topics.py`
- 构建与 QA：`scripts/build_batch_03.py`
- 1× 预览：`previews/c01-v2-b03-combat-preview-1x.png`
- Manifest：`../../manifest-runtime-v2-b03.json`
- QA：`../../qa-runtime-v2-b03.json`

## 规格

- `16` 个普通单位：`32×48` 单帧。
- `6` 个大型/骑乘单位：`64×64` 单帧。
- `6` 个攻城/飞行/巨像单位：`96×64` 单帧。
- 每个单位均为四帧横排，底中心锚点；大型单位显式声明 `footprint` 与 `zOrder`。
- `7` 张 ImageGen 母板，每张四个兵种、每兵种四帧；母图、透明中间图和处理脚本全部保留。

## 重建

```bash
python3 docs/story-candidates/candidate-01/assets/runtime-v2/batch-03/scripts/lock_topics.py
python3 docs/story-candidates/candidate-01/assets/runtime-v2/batch-03/scripts/build_batch_03.py
python3 docs/story-candidates/pixel-master-tools/validate_runtime_v2_b03.py
```

