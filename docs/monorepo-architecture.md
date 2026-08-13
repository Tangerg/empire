# Empire SRPG Monorepo 架构

> 状态：已落地  
> 目标：冻结既有关卡内容后，用稳定包边界支持战斗引擎、三套剧情战役和可单独分发的超级体验关卡。

## 1. 边界

| 工作区 | 自洽职责 | 不允许承担 |
| --- | --- | --- |
| `@empire/battle-engine` | 战斗状态、规则、Action、AI、空间、目标和场景 DSL | DOM、台词、人物名、素材路径、跨关流程 |
| `@empire/campaign-engine` | 章节节点、选择、持久名单、战斗桥和存档版本 | 伤害/移动计算、具体剧本文案 |
| `@empire/content-common` | 跨题材资源定义 | 题材角色与故事特判 |
| `@empire/content-ancient-empires` | 通用奇幻数值和经典演示关卡 | 战役流程、UI |
| `@empire/game-ui` | 棋盘、HUD、通用战役外壳和表现端口 | 直接导入任一剧本 |
| `@empire/editor` | 可暂时无效的关卡文档及编辑交互 | 运行中战斗状态 |
| `@empire/story-candidate-*` | 每个剧本自己的内容、流程、演出适配和现役卡通素材 | 修改通用引擎来迁就情节 |
| `@empire/experience-lab` | 组合生产机制的纵向体验关卡 | Lab 专属规则和复制引擎代码 |

## 2. 组合根

所有全局内容注册只发生在 `apps/*/src/main.ts` 或 `tooling/test/setup-content.ts`。导入一个引擎包本身不会安装题材定义。

通用 UI 通过两个端口获得题材能力：

- `ArtProvider`：单位、地形、结构、立绘、图标和特效；
- `StoryCampaignAdapter`：剧本流程、台词、选择、关系标签和题材战果政策。

因此剧本二、三可复用同一棋盘和战役界面，而不要求 `game-ui` 知道它们的存在。

## 3. 素材政策

当前正式素材只有包内三套卡通资产：

```text
packages/story-candidate-01/assets/final-fantasy-v1
packages/story-candidate-02/assets/final-scifi-v1
packages/story-candidate-03/assets/final-ancient-china-v1
```

偏写实、旧像素和历史运行时素材已从 Git 工作内容移出，按剧本分为三个目录并打入：

```text
.local-asset-archive/legacy-assets.zip
```

`.local-asset-archive/` 被 `.gitignore` 排除，只供本地与云盘备份。正式运行时不得从归档路径读取文件。

## 4. 超级体验关卡分发

`apps/experience-lab` 使用 `vite-plugin-singlefile`：

1. 只注册通用、经典奇幻和剧本一内容；
2. 只启动 `@empire/experience-lab` 的一个关卡；
3. 将代码、CSS 和用到的卡通 PNG 全部内联；
4. 输出 `dist/experience-lab/index.html`。

这个产物支持直接 `file://` 打开，适合发给种子玩家。它与开发者能力 Demo 不同：前者验证玩家体验，后者解释引擎机制。

## 5. 新功能归属判断

新增内容时依次判断：

1. 是否改变战斗合法性或结算？若跨题材成立，进入 `battle-engine`；
2. 是否只是兵种、武器、地形或数值？进入对应内容包；
3. 是否是章节、人物、选择或持久关系？进入对应剧本和 `campaign-engine` 适配；
4. 是否只是表现？实现 `ArtProvider` 或 `BattlePresentation`；
5. 是否只是验证既有能力？进入 `experience-lab`，禁止创造平行规则。

这样可以保持核心小而稳定，同时允许三个题材取能力并集，而不是被迫只保留交集。
