# 管理《断冠之誓》的美术与素材

本入口管理《断冠之誓》的美术基准、404 个内容题材、1188 个地图连接变体与拼装部件、当前运行时库存、后续生产、文件规格和验收。你可以只阅读本剧本文件夹完成策划、美术生产和程序接入。

## 按任务阅读

本套文档按工作任务拆分：

1. [确认美术方向](./art-assets/01-art-direction.md)
2. [确定资源范围](./art-assets/02-asset-scope.md)
3. [查询当前库存](./art-assets/03-runtime-inventory.md)
4. [安排后续生产](./art-assets/04-production-plan.md)
5. [按运行时规格交付](./art-assets/05-runtime-contract.md)
6. [验收并晋级素材](./art-assets/06-qa-and-promotion.md)
7. [查询全部 404 个题材](./art-assets/07-topic-catalog.md)
8. [使用通用地图环境建造包](./art-assets/08-environment-builder.md)

## 已定调资源样例

[卡通西幻完整素材包 V1](../../../packages/story-candidate-01/assets/final-fantasy-v1/README.md) 是当前唯一正式运行时来源。偏写实、旧像素与历史运行时素材已经按三个剧本打包到 Git 忽略的 `.local-asset-archive/legacy-assets.zip`，只用于云盘备份，不参与构建。

## 当前状态

《断冠之誓》的 404 个题材已全部交付、通过素材包机器 QA，并由 Manifest 进入游戏的语义素材目录。当前前三章实际使用了单位、任务单位、地形、建筑、掩体物件、武器/技能/状态图标、特效、人物卡和剧情场景等完整链路。

通用地图环境包 V1.1 另有 36 套 1×/2× 图集、1188 个可复用部件，已通过独立机器 QA；它仍在等待真实关卡替换和截图验收，因此当前保持 `runtimeReady=false`。

| 指标 | 当前值 |
| --- | ---: |
| 题材目标 | 404 |
| 运行时输出 | 404 |
| 唯一题材 ID | 404 |
| 战斗单位 | 40 / 40 |
| 素材包机器 QA | 404 / 404 |
| 游戏接入 | Manifest + 语义绑定已启用 |
| 地图环境图集 | 36 套（1×/2×） |
| 地图拼装部件与变体 | 1188 |
| 环境包机器 QA | 0 错误，待实机关卡接入 |

## 使用本地文件

游戏接入按以下顺序读取本剧本文件：

1. `packages/story-candidate-01/assets/final-fantasy-v1/manifest-final-fantasy-v1.json`
2. `packages/story-candidate-01/assets/final-fantasy-v1/runtime/`
3. `packages/story-candidate-01/assets/final-fantasy-v1/qa-final-fantasy-v1.json`
4. `packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/manifest-environment-builder-v1.json`
5. `assets/manifest-complete.json`，仅作为早期题材规划的历史对照

不要从 `assets/archive/`、`assets/complete/`、`assets/draft-v1/`、旧 HD 剧情样包或母图目录加载游戏资源。
