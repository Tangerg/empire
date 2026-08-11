# 404 题材槽位原型清单契约

本契约用于三个剧本的 `assets/manifest-complete.json`。它把“玩法题材”与“物理文件”分开：一个图集原型可以承载多个题材，但每个题材必须有唯一 ID，并且只能映射到一个 delivery。这里的 404 槽位与 `status: "formal"` 是旧题材 schema 口径，不代表素材达到运行时质量或已经接入游戏。当前运行时候选、精确覆盖与验收门槛分别见 [Runtime V2 素材画廊](./RUNTIME-V2-ASSET-GALLERY.html)、[Runtime V2 覆盖状态](./RUNTIME-V2-STATUS.md) 和 [游戏运行时素材契约](./GAME-RUNTIME-ASSET-CONTRACT.md)。

## 固定数量

| `category` | 每剧本题材数 |
| --- | ---: |
| `narrative-static` | 80 |
| `combat-unit` | 40 |
| `mission-unit` | 24 |
| `faction-kit` | 12 |
| `terrain` | 32 |
| `interactive-structure` | 24 |
| `battle-prop` | 32 |
| `equipment` | 48 |
| `skill` | 48 |
| `status` | 24 |
| `fx` | 24 |
| `hud` | 16 |
| **合计** | **404** |

每套必须有 22 个 `source: "existing"` 题材与 382 个 `source: "expanded"` 题材。实名英雄单位是额外角色变体，不占 40 个 `combat-unit` 槽位。

## 顶层结构

```json
{
  "schemaVersion": "2.0.0",
  "campaignId": "candidate-01",
  "targetTopics": 404,
  "categoryTargets": {},
  "topics": [],
  "deliveries": []
}
```

`categoryTargets` 必须逐项等于上表。`topics` 必须有 404 项；`deliveries` 数量不固定，允许按玩法和运行时效率合并成图集。

## 题材记录

```json
{
  "id": "oath-terrain-mud-road",
  "label": "泥泞军道",
  "category": "terrain",
  "status": "formal",
  "source": "expanded",
  "assetId": "oath-terrain-atlas-01",
  "cell": 3
}
```

- `id`：剧本内唯一、长期稳定，不使用数组序号充当语义。
- `label`：制作和策划可读名称。
- `status`：旧题材 schema 固定写 `formal`；该值只表示槽位已登记，不等于 `runtimeReady`。
- `source`：只允许 `existing` 或 `expanded`。
- `assetId`：必须命中一个 `deliveries[].id`。
- `cell`：图集单元索引。也可写 `{x,y,width,height}`；单题材整图可以省略。

## 物理交付记录

```json
{
  "id": "oath-terrain-atlas-01",
  "type": "terrain-atlas",
  "png": "expanded/terrain/oath-terrain-atlas-01.png",
  "svg": "expanded/terrain/oath-terrain-atlas-01.svg",
  "width": 256,
  "height": 32,
  "cellWidth": 32,
  "cellHeight": 32,
  "columns": 8,
  "topicIds": [
    "oath-terrain-grass",
    "oath-terrain-mud-road"
  ]
}
```

- PNG 与 SVG 必须同时存在、同尺寸、同构图，用于原型审阅、重着色与重新导出。游戏只接入通过运行时契约和实机验收的 Runtime V2 PNG。
- `topicIds` 必须覆盖该交付中的全部题材；一个题材不得被两个交付重复声明。
- 图集应提供 `cellWidth`、`cellHeight`、`columns`，并在题材记录上给出 `cell`，以便自动检查空单元。
- 透明素材必须保留透明画布；全画幅场景可以不透明。所有图像必须使用整数像素边界与最近邻缩放。

## 自动验收

从仓库根目录运行：

```bash
python3 docs/story-candidates/pixel-master-tools/validate_complete_library.py
```

验收器检查 1212 个题材的总量、分类、唯一 ID、来源数量、文件存在性、PNG/SVG 尺寸、SVG `viewBox`、透明空图和图集单元映射。同类题材不得有完全重复的标准化 cell；当一类有至少 8 个透明 cell 时，独立 alpha 轮廓数不得低于 50%，防止只换颜色却重复同一形状。
