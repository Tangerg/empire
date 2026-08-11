# 《断冠之誓》404 题材槽位原型 QA

校验对象：[`manifest-complete.json`](./manifest-complete.json)；机器可读结果：[`qa-complete.json`](./qa-complete.json)。本报告只证明题材、文件和图集单元的原型结构通过检查，不证明运行时质量或游戏接入完成。运行时候选、精确覆盖与验收门槛分别见 [Runtime V2 素材画廊](../../RUNTIME-V2-ASSET-GALLERY.html)、[Runtime V2 覆盖状态](../../RUNTIME-V2-STATUS.md) 和 [游戏运行时素材契约](../../GAME-RUNTIME-ASSET-CONTRACT.md)。

## 结果

- 404 / 404 题材槽位原型通过；22 个槽位引用既有正式题材，382 个槽位使用原型扩展。
- 12 个类别数量与统一目标完全一致：80 / 40 / 24 / 12 / 32 / 24 / 32 / 48 / 48 / 24 / 24 / 16。
- 32 份交付、64 个 PNG/SVG 文件全部存在；404 个题材 `cell` 坐标均合法且单元非空。
- 32 对 PNG/SVG 像素尺寸一致；新增图集采用整数像素边界和 `crispEdges`。
- 标准化单元精确重复：0 组；透明类 alpha 轮廓多样性全部达到至少 50% 的统一门槛。
- 错误 0，警告 0，最终状态 `passed: true`。

## 视觉多样性

| 类别 | 独立单元 | 透明单元 | 独立轮廓 |
| --- | ---: | ---: | ---: |
| 叙事静态 | 80 / 80 | 51 | 51 |
| 战斗单位 | 40 / 40 | 40 | 40 |
| 任务单位 | 24 / 24 | 24 | 24 |
| 交互建筑 | 24 / 24 | 24 | 19 |
| 战场物件 | 32 / 32 | 32 | 32 |
| 装备 | 48 / 48 | 48 | 47 |
| 技能 | 48 / 48 | 48 | 48 |
| 状态 | 24 / 24 | 24 | 24 |
| FX | 24 / 24 | 24 | 24 |
| HUD | 16 / 16 | 16 | 16 |
| 阵营套件 | 12 / 12 | 不透明 | 不适用 |
| 地形 | 32 / 32 | 不透明 | 不适用 |

除自动化重复与轮廓检查外，已针对四个高风险方向进行目检：战斗/任务单位的职业与武装辨识、地形母型差异、叙事场景构图差异，以及装备/建筑/战场物件的轮廓族差异。

## 复现

在仓库根目录执行：

```bash
python3 docs/story-candidates/candidate-01/assets/tools/generate_complete_assets.py
python3 docs/story-candidates/pixel-master-tools/validate_complete_library.py
```

生成脚本只写入 `candidate-01/assets/complete/`、`manifest-complete.json` 和 `qa-complete.json`，不会覆盖既有正式 HD 素材。
