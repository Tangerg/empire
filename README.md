# Empire SRPG Workspace

剧情战役型 SRPG 的 TypeScript monorepo。核心战斗、战役状态机、编辑器、剧本内容和可分发体验关卡已经物理隔离；规则引擎不依赖 DOM，也不知道任何人物名或章节。

当前视觉基线是三套 `final-*` 卡通素材包：

- 剧本一：西幻卡通 `final-fantasy-v1`；
- 剧本二：星际卡通 `final-scifi-v1`；
- 剧本三：东方历史卡通 `final-ancient-china-v1`。

偏写实、旧像素和历史运行时素材不参与构建，统一保存在 Git 忽略的 `.local-asset-archive/legacy-assets.zip`。

## 常用命令

```bash
npm install
npm run dev               # 主游戏与《断冠之誓》战役
npm run demo              # 战斗微内核能力 Demo
npm run experience        # 超级体验关卡开发模式
npm run build:experience  # 单文件产物 dist/experience-lab/index.html
npm run build             # 类型检查并构建四个应用
npm test                  # 整仓测试
npm run bench:core        # 战斗热路径基准
```

macOS 可双击根目录的 `打开引擎Demo.command`。只有构建后的超级体验关卡可直接通过 `file://` 打开；开发入口仍由 Vite 提供本地服务器。

## Monorepo

```text
apps/
  game/                    主游戏与战役入口
  editor/                  关卡编辑器入口
  engine-demo/             战斗引擎能力演示
  experience-lab/          面向种子玩家的单关体验入口

packages/
  battle-engine/           Headless SRPG 战斗内核
  campaign-engine/         通用章节状态机、战斗桥和版本化存档
  content-common/          跨题材状态、移动、结构、阵形和环境
  content-ancient-empires/ 通用奇幻数值与经典演示关卡
  game-ui/                 题材无关 SVG 棋盘、HUD 和战役外壳
  editor/                  关卡文档聚合与编辑界面
  story-candidate-01/      《断冠之誓》内容、16 关、剧情和卡通素材
  story-candidate-02/      《群星熄灭之前》七章契约与卡通素材
  story-candidate-03/      《布衣定鼎》七章契约与卡通素材
  experience-lab/          超级体验关卡数据，只组合现有机制

tooling/test/              整仓测试组合根
docs/                      设计、架构和三套剧本文档
```

依赖方向固定为：

```text
apps / stories / experience-lab
              ↓
game-ui / editor / campaign-engine / content packs
              ↓
battle-engine
```

`battle-engine` 禁止导入 UI、编辑器、战役或剧本包；`game-ui` 通过 `ArtProvider` 和 `StoryCampaignAdapter` 接收题材表现，不反向依赖任何剧本。

## 已实现的战斗能力

- 四方向 Dijkstra、地形移动、占领、经济、生产和治疗；
- 确定性预测/结算、反击、援护、范围模板、多武器、弹药与冷却；
- 海拔、悬崖、直射视线、高打低、方向、侧击、背刺与夹击；
- 半/全掩体、方向掩体、结构阻挡、动态覆盖层；
- 状态、士气、溃退、投降、阵形、运输和复合目标；
- 指挥官光环、指挥点、范围战术、英雄气势；
- 当关军衔、职业树、熟练度与自由转职；
- 护送、保护、歼灭、摧毁、占区、互动、组合和阶段目标；
- 增援、召唤、撤退、复活、强制位移、传送和场景触发 DSL；
- 共用合法行动与战斗预测的 AI；
- 战前部署、地图编辑、关卡校验和无 DOM 运行。

完整机制见[战斗系统设计](./docs/combat-system-design.md)，边界和扩展方式见[战斗引擎架构](./docs/combat-engine-architecture.md)，跨关部分见[战役引擎架构](./docs/campaign-engine-architecture.md)。

## 超级体验关卡

`@empire/experience-lab` 是冻结旧关卡之后用于验证 MDA 的纵向切片。当前关卡 `灰旗试炼 · 三线合围` 包含：

- 29×17 的三线战场；
- 21 个初始单位与双方动态增援；
- 九人玩家联队、友军观察团和敌方指挥体系；
- 高地、悬崖、方向掩体、结构、部署区和环境覆盖；
- 保护、占区、击退统帅的组合目标；
- 指挥、士气、火场、场景信号和题材卡通素材。

`npm run build:experience` 会把运行时代码、CSS 与所需素材全部内联到一个 HTML 中。该文件可以直接复制给最早的种子玩家，不依赖服务器、`node_modules` 或旁侧资源。

## 扩展原则

- 新机制进入 `battle-engine` 前，应证明至少能跨题材复用；
- 兵种、武器、地形和数值属于内容包，不写入 Action/AI 分支；
- 剧情变量、持久伤亡与关系属于 `campaign-engine` 上层适配器；
- 素材通过剧本包的 `ArtProvider` 绑定稳定领域 ID；
- 超级体验关卡只组合生产能力，不建立 Lab 专属规则；
- 不采用 ECS；单位、玩家、结构和战役状态保持明确的实体归属与聚合边界。

## 测试

```bash
npm run typecheck
npm test
npx vitest run packages/battle-engine
npx vitest run packages/experience-lab
```

架构测试会检查战斗内核环依赖和 package 边界；内容测试会检查定义引用、伤害矩阵、地图尺寸、关卡合法性和 AI 推演；页面测试会真实挂载游戏、编辑器、Demo、战役 UI 与体验入口。
