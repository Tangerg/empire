# 《断冠之誓》游戏运行时 V2 首批

这是供真实战棋关卡接入的首批运行时素材，不是 404 题材完整库。“双子丘陵”已实机使用其中 15 项：4 个单位表、8 类地形和 3 个地图建筑。其余 15 项仍是运行时候选。资产级状态以 `gameIntegration.runtimeReadyAssetIds` 为准；规格与验收门槛见 [本剧本运行时规格](../../art-assets/05-runtime-contract.md) 和 [本剧本验收规则](../../art-assets/06-qa-and-promotion.md)。

## 预览

- 1× 总览：[`previews/runtime-v2-overview-1x.png`](./previews/runtime-v2-overview-1x.png)
- 2× 总览：[`previews/runtime-v2-overview-2x.png`](./previews/runtime-v2-overview-2x.png)
- 单位 1× / 2×：[`previews/units-1x.png`](./previews/units-1x.png) / [`previews/units-2x.png`](./previews/units-2x.png)
- 随机地形 1× / 2×：[`previews/terrain-random-12x8-1x.png`](./previews/terrain-random-12x8-1x.png) / [`previews/terrain-random-12x8-2x.png`](./previews/terrain-random-12x8-2x.png)
- 道路/水面连接单元 1× / 2×：[`previews/terrain-connections-1x.png`](./previews/terrain-connections-1x.png) / [`previews/terrain-connections-2x.png`](./previews/terrain-connections-2x.png)
- 村庄/兵营/城堡三态 1× / 2×：[`previews/map-structures-1x.png`](./previews/map-structures-1x.png) / [`previews/map-structures-2x.png`](./previews/map-structures-2x.png)

## 实机用途

- BoardView 实机证据：[`qa/candidate-01-first-level-in-game-final.png`](./qa/candidate-01-first-level-in-game-final.png)
- 实测加载：6 个运行时单位、176 个运行时地图格、8 个运行时建筑；单位与地形回退均为 0，缺失图片为 0。

| 资源 | 运行时绑定 | 用途 |
| --- | --- | --- |
| 四个 `128×48` 单位表 | `soldier`、`archer`、`knight`、`cleric` | 直接替换 BoardView 当前程序单位；四帧为站立 A、踏步 A、站立 B、踏步 B，锚点 `(16,47)` |
| 八类 `32×32` 地形 | `plain/road/bridge/forest/hill/mountain/water/wall` | 替换程序地形；六类使用 4 个坐标 hash 变体，道路和水面使用 0–15 的 N/E/S/W 邻接 mask |
| 两个建筑状态表 | `redstone-oath-tower`、`gray-banner-supply-depot` | 运行时候选，尚未计入实机 ready；每个按 `128×128` 竖向切出 `normal/damaged/captured` 三态 |
| 三个 1×1 地图建筑 | `village`、`barracks`、`castle` | 直接替换旧亮绿回退图；每态 `32×64`、锚点 `(16,63)`，本体保持中性，占领态增加灰蓝旗面 |
| 八个图标 | 4 装备 + 4 技能 | 运行时候选，尚未计入实机 ready；目标显示尺寸为 24–32 px |
| 四组 FX | 4 帧、`128×32` | 运行时候选，尚未计入实机 ready；fps、混合模式和循环属性见 manifest |
| 剧情场景 | `gray-banner-dawn-council` | 运行时候选，尚未计入实机 ready；尺寸为 `256×144` |

机器入口：[`../manifest-runtime-v2.json`](../manifest-runtime-v2.json)。QA：[`../qa-runtime-v2.json`](../qa-runtime-v2.json)。母图生成提示词：[`PROMPTS.md`](./PROMPTS.md)。

## 复现处理

从仓库根目录执行：

```bash
python3 docs/story-candidates/candidate-01/assets/runtime-v2/tools/process_runtime_v2.py --refresh-chroma
```

`--refresh-chroma` 会调用 ImageGen 技能自带的 `remove_chroma_key.py` 重建透明母图，然后重新输出 PNG、SVG 包装、manifest、QA 和 1×/2× 预览。脚本只写入 `candidate-01/assets/runtime-v2/`、`manifest-runtime-v2.json` 和 `qa-runtime-v2.json`。

第二轮根据实机截图调整地形：对 ImageGen 材料保留低频结构、压低 1 px 高频噪声，并提升中间调；边缘仍使用同一 32 px 像素连接基准，不改变 56 个 tile 单元及 mask 顺序。
