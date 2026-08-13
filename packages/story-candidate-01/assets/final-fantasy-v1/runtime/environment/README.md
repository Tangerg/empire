# 第一关环境补充图集

`twin-hills-forest-atlas-v1.png` 是《断冠之誓》第一关“ 双子丘陵”的运行时环境补充图集，用于弥补主素材包中缺少独立林冠、灌木、断木和树桩的问题。

## 运行时契约

- 尺寸：`1024 × 1024`。
- 布局：`4 × 4` 等分网格，每格 `256 × 256`。
- 格子编号：从左到右、从上到下，为 `0..15`。
- 透明度：PNG Alpha；运行时不得重新执行色键处理。
- 用途：仅作为表现层素材，不参与移动、视线、掩体或胜负判定。
- 接入点：`src/art/candidate-01-map-scene.ts`。

| 格子 | 内容 |
| --- | --- |
| 0 | 高松 |
| 1 | 双松 |
| 2 | 三松树丛 |
| 3 | 松树幼株 |
| 4–7 | 四类林下灌木 |
| 8 | 枯松 |
| 9–11 | 三类成熟松树 |
| 12 | 低灌木 |
| 13 | 倒木 |
| 14 | 树桩 |
| 15 | 苔岩 |

## 生成与处理

使用 Codex 内置 ImageGen 生成，原图采用纯 `#ff00ff` 色键背景；随后通过系统 `remove_chroma_key.py` 完成柔边、去色溢和 Alpha 输出，再缩放到 `1024 × 1024`。

最终提示词：

> Create a clean 4 by 4 production atlas containing sixteen distinct medieval northern-forest environment sprites: dark conifers, tree clusters, saplings, shrubs, mossy bushes, fallen pine, stump and mossy rock. Use hand-painted high-detail pixel art for a top-down 3/4 tactical RPG, restrained overcast lighting, blue-green pine foliage, mossy sage highlights and dark bark. Every cell contains exactly one isolated asset with generous padding. Use a perfectly flat `#ff00ff` chroma-key background. No characters, buildings, UI, text, borders, shadows, fog, watermark or objects crossing cells.

