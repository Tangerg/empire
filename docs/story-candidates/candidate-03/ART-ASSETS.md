# 管理《布衣定鼎》的美术与素材

本入口管理《布衣定鼎》的美术基准、404 个资源题材、当前运行时库存、后续生产、文件规格和验收。你可以只阅读本剧本文件夹完成策划、美术生产和程序接入。

## 按任务阅读

本套文档按工作任务拆分：

1. [确认美术方向](./art-assets/01-art-direction.md)
2. [确定资源范围](./art-assets/02-asset-scope.md)
3. [查询当前库存](./art-assets/03-runtime-inventory.md)
4. [安排后续生产](./art-assets/04-production-plan.md)
5. [按运行时规格交付](./art-assets/05-runtime-contract.md)
6. [验收并晋级素材](./art-assets/06-qa-and-promotion.md)
7. [查询全部 404 个题材](./art-assets/07-topic-catalog.md)

## 已定调资源样例

[中国古代风资源样例 V1](./art-assets/samples/reference-v1/README.md) 已按真实游戏规格导出刀盾卒四帧、河堤闸桥三态、开仓粮站三态、水田河渠地形和装备 / 军务图标。该包是独立试产，不覆盖当前运行时库存。

## 当前状态

《布衣定鼎》的内容预算为 404 个题材。当前运行时库有 123 项输出，其中 96 项绑定精确题材 ID。40 个战斗单位已完成生产，所有输出仍需真实关卡验收。

| 指标 | 当前值 |
| --- | ---: |
| 题材目标 | 404 |
| 运行时输出 | 123 |
| 精确题材 | 96 |
| 战斗单位 | 40 / 40 |
| 实机通过 | 0 |
| 运行时候选 | 123 |

## 使用本地文件

游戏接入按以下顺序读取本剧本文件：

1. `assets/manifest-runtime-v2.json`
2. `assets/manifest-runtime-v2-b02.json`
3. `assets/manifest-runtime-v2-b03.json`
4. `assets/manifest-complete.json`，只用于查询 404 个题材 ID

不要把 `assets/expanded/`、`assets/draft-v1/`、`assets/masters/` 或旧 HD 剧情样包当成当前运行时资源。
