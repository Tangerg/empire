# 远古帝国 · 通用 SRPG 战斗引擎

Ancient Empires 风格的回合制战棋与小型 SRPG 引擎，Web 技术栈实现：

- **纯 TypeScript 规则引擎**（`src/core`），不依赖 DOM，可测试、可移到 worker / 服务端
- **《断冠之誓》前三章可玩战役**：小说式演出、11 次抉择、16 场连续战斗、跨关名册/伤亡/资源与战果结算
- **双美术管线**：通用题材支持 SVG 与阵营换色，《断冠之誓》使用已交付的像素地形、角色动作、立绘和剧情场景素材
- **地图编辑器**（`editor.html`），关卡即编辑器文档，默认内容包中的地形与单位会自动进入调色板
- 224 个测试覆盖规则、AI、空间层、战役原语、职业树、微内核、战役桥接、存档边界、16 关平衡包络、关卡合法性与三个页面的运行时

详细设计：[《断冠之誓》可玩战役](./docs/story-candidates/candidate-01/PLAYABLE-CAMPAIGN.md) · [通用战斗引擎架构](./docs/combat-engine-architecture.md) · [剧情战役框架](./docs/campaign-engine-architecture.md) · [经典 SRPG 战斗系统研究](./docs/combat-system-design.md)

```bash
npm install
npm run dev        # http://localhost:5173  主游戏与《断冠之誓》前三章
npm run demo       # 自动打开引擎能力 Demo（推荐）
                   # http://localhost:5173/editor.html  编辑器
                   # http://localhost:5173/demo.html  引擎能力 Demo
npm test           # 全部测试
npm run bench:core # 战场投影、移动、预测和 AI 热路径基准
npm run build      # 产出 dist/
```

在 macOS 上也可以直接双击项目根目录的 `打开引擎Demo.command`。不要直接双击
`demo.html` 运行交互演示：浏览器会阻止 `file://` 页面加载 TypeScript 模块；该页面
现在会显示启动说明，不再产生 CORS 报错。

---

## 核心规则（对齐远古帝国）

| 系统 | 实现 |
| --- | --- |
| 网格 | 四方向连通，曼哈顿距离 |
| 移动 | Dijkstra 按地形进入消耗；移动型由注册表定义，内置步行 / 骑乘 / 重装 / 飞行 / 水军 / 两栖 |
| 穿越 | 可穿过友军，不能停在友军格；敌军完全阻挡 |
| 战斗 | **完全确定性**，HUD 里的预测就是结果 |
| 交战计划 | 单体、十字、直线和 3×3 范围共用同一份 `CombatPlan`，预测与结算不会分叉 |
| 反应 | 反击 / 防御 / 援护 / 节制；援护真实转移伤害，全部进入预测 |
| 状态 | 武器命中效果、周期行为、能力封禁都由开放注册表处理 |
| 指挥 | 局部军团光环、阵营指挥点、范围战术与指挥官阵亡动摇 |
| 成长 | 新兵 / 老兵 / 精英三级当关军衔；命名英雄可使用气势门槛与招牌武器 |
| 环境 | 洪水、火场、真空等覆盖层不改底图，同时影响移动、防御、视野和回合状态 |
| 高低差 | 独立海拔层、爬升/下落限制、上坡消耗、显式悬崖边、射线高度与高地伤害优势 |
| 方位 | 四方向朝向、移动/攻击自动转向、手动转向、侧击、背刺与对向夹击 |
| 掩体 | 地形/结构基础掩体与四边方向掩体；半/全掩体、高地压制、曲射与无视掩体标签 |
| 职业 | 数据化有向职业树、军衔/熟练度门槛、职业掌握、已解锁职业自由切换与事务回滚 |
| 战役级原语 | 士气/溃退/投降、阵形、运输、守卫/巡逻/撤退指令、停火区、周期环境与移动复合目标 |
| 占领 | 踏入即占领（`captureMode: 'instant'`），只有带 `capture` 能力的步兵可以占 |
| 经济 | 回合开始结算所属建筑收入；单位站在己方建筑上回血 |
| 生产 | 城堡 / 兵营出兵，新单位当回合默认不能行动 |
| 胜负 | 护送、保护、摧毁、占区、计数等原语可用 all / any / sequence / optional / failOn 组合 |

伤害公式（`src/core/combat.ts`）：

```
damage = 武器威力 × 属性克制 × 标签加成 × 生命/军衔/状态/指挥修正 × (1 - 减伤)
减伤 = 地形防御 + 单位护甲 + 军衔/状态/指挥修正，上限 60%
```

基础属性克制来自内容包注册的 Damage Model；当前奇幻包提供一张 4×4 表。标签、状态、指挥、地形和反应由可解释的修正管线叠加，每条贡献都会进入预测明细。星际或历史包可以注册自己的伤害类型、护甲类型与完整克制关系，不需要修改核心公式。

由此自然长出的战术：

- 弓箭手隔一格射击不吃近战反击，被贴身就会被反击
- 弩车最小射程 2、移动后不能开火 → 永远不会被近战反击，但必须被保护
- 骑士 6 移动但不能占领、怕魔法；巨龙无视地形但怕弓箭
- 山地 40% 减伤 + 骑乘/重装无法进入 → 高地是步兵和弓手的阵地

---

## 目录结构

```
src/
  core/                 题材无关规则引擎（无 DOM、无默认内容）
    types.ts            全部领域类型 + RuleSet 默认值
    registry.ts         通用注册表容器
    content-pack.ts     内容包契约、原子安装与引用校验
    content-builders.ts 无副作用的内容定义构造器
    grid.ts             坐标、邻居、环形范围、地形哈希
    data/               空注册表与动态查询入口
    data/damage.ts      开放伤害/护甲类型与克制矩阵
    mapio.ts            关卡序列化（字符地图）+ 合法性检查
    state.ts            GameState 构造 / 克隆 / 查询
    domain/             充血实体、Battle 聚合根与 Battlefield/Cell
    kernel.ts           极小插件宿主：依赖图、生命周期与能力装配
    plugins/            四个自洽能力插件（战术 / 任务 / 资源经济 / AI）
    resources.ts        实体自有账户的通用资源适配与事务规则
    engine.ts           BattleEngine Facade 与规则依赖注入
    action-system.ts    开放 Action 类型与策略注册表
    movement.ts         移动场、路径、威胁范围
    tactical-space.ts   移动/目标/视野一致性的可替换战术空间端口
    combat.ts           战斗结算与预测
    combat-plan.ts      多目标解析、范围攻击、反击与援护的统一计划
    combat-modifiers.ts 可解释伤害修正管线
    hit-effects.ts      开放的武器命中效果策略
    statuses.ts         状态定义、行为注册表与生命周期
    commanders.ts       指挥官、光环、指挥点和战术
    structures.ts       城门、节点、Boss 部位等结构
    overlays.ts         动态地形效果查询
    objective-model.ts  稳定目标 ID 与运行状态
    objective-system.ts 开放目标类型与策略注册表
    scenario.ts         开放条件/效果 DSL、周期触发与策略注册表
    morale.ts           士气变化、溃退与投降
    formations.ts       空间约束阵形
    transports.ts       登载、卸载与载具损失不变量
    composites.ts       多部件与移动战场目标
    engagement.ts       区域敌对行动政策
    progression.ts      当关军衔、可注入阈值与英雄气势
    abilities.ts        实例隔离的能力目录（attack/heal/capture/wait）
    actions.ts          内置行动策略、事务后处理与回合推进
    victory.ts          目标判定、收入、治疗
    vision.ts           战争迷雾
    session.ts          会话外壳：撤销、缓存、事件订阅
    ai-objectives.ts    开放的任务目标→AI 战术意图顾问
    ai.ts               效用评分 AI、任务执行与反应姿态选择
  content/
    common/             跨题材移动、状态、结构、环境和战术原语
    ancient-empires/    当前奇幻题材的伤害、兵种、武器、地形与关卡
      levels/*.json     当前内置关卡（= 编辑器文档）
    candidate-01/       《断冠之誓》单位/武器/地形、16 关、战役节点与剧情表现
    install-default.ts  默认内容组合函数（无副作用）
    bootstrap-default.ts 应用/测试唯一的默认安装副作用入口
  campaign/             通用章节状态机、战斗防腐层与版本化存档
  content/campaigns/    三套候选故事的七章结构契约
  application/          浏览器存储、试玩交接等应用适配器
  demo/                 微内核、实体资源与语义事件的可交互演示
  art/                  SVG 通用绘制 + 题材像素素材运行时适配
    candidate-01-runtime.ts  《断冠之誓》素材到内容 ID 的映射
  ui/                   游戏界面
    board.ts            SVG 棋盘 + 全部动画
    hud.ts              顶栏 / 侧栏 / 弹窗（纯展示）
    game.ts             控制器：选择状态机、输入、AI 驱动
  editor/               地图编辑器
    board.ts app.ts main.ts
```

数据流是单向的：`输入 → GameSession → BattleEngine → ActionHandler → GameEvent[] → 动画 → 重绘`。UI 只通过 `GameSession` 说话，引擎不知道“选中”这种概念。`SrpgMicrokernel` 只在启动时把四个自洽能力插件装配成 `BattleEngine`；运行时没有 Service Locator，也不采用 ECS。单位、玩家和结构仍是带明确状态归属的领域实体。能力目录与战术空间规则按引擎实例隔离，菜单、AI、战争迷雾和权威行动校验使用同一份策略。

---

## 怎么扩展（这部分是刻意留的口子）

### 加一个兵种

```ts
import { defineUnit, defineWeapon, installContentPacks, type ContentPack } from './core';

const weapon = defineWeapon({
  id: 'paladin_blade', name: '圣辉剑', power: 48, damageType: 'magic', tags: ['melee'],
});
const pack: ContentPack = {
  id: 'example.paladin', version: 1,
  dependencies: ['empire.common', 'empire.ancient-empires'],
  weapons: [weapon],
  units: [defineUnit({
    id: 'paladin', name: '圣骑士', weapons: [weapon.id],
    movementClass: 'mounted', armorClass: 'heavy',
    value: 500, recruitCosts: [{ resource: 'funds', amount: 500 }],
    maxHp: 130, defense: 0.2, movement: 5,
    abilities: ['attack', 'heal', 'wait'], tags: ['cavalry', 'support'],
    blurb: '能治疗的重骑兵。',
  }, new Map([[weapon.id, weapon]]))],
};

installContentPacks(pack);
```

编辑器调色板、征募面板、图鉴、AI 评分都会自动带上它。只差一张精灵图：在 `src/art/units.ts` 的 `sprites` 里加一条同名条目（没加会退化成剑士的图，不会崩）。

### 改平衡

平衡数值归内容包所有：单位和武器在 `src/content/ancient-empires/`，伤害克制在 `damage.ts`，地形在 `terrain.ts`。修改后由内容包契约测试检查所有引用与矩阵完整性；不要在战斗进行中热改全局定义。

### 加一种新玩法动作（技能）

能力就是“怎么枚举目标 + 怎么改状态”。代码规则属于引擎插件，不写进纯数据 `ContentPack`；扩展默认战术插件中的能力目录即可：

```ts
import { applyDamage } from './core/abilities';
import { ring } from './core/grid';
import { createDefaultMicrokernel, type EnginePlugin } from './core';

const FireMagicPlugin: EnginePlugin = {
  id: 'rules.fire-magic', version: 1,
  requires: ['engine.tactical-rules'],
  install: (context) => context.require('abilities').define({
    id: 'fireball', name: '火球', hint: '对 2 格内区域造成范围伤害。',
    selfTargeted: false, priority: 15, tags: ['magic'],
    targets: ({ state, at }) => ring(state.map, at, 1, 2),
    usable: () => true,
    execute: ({ state }, target, emit) => {
      for (const c of ring(state.map, target!, 0, 1)) {
        const victim = state.units.find((u) => u.x === c.x && u.y === c.y);
        if (!victim) continue;
        const death = applyDamage(state, victim, 30);
        if (death) emit(death);
      }
    },
  }),
};

const engine = createDefaultMicrokernel().use(FireMagicPlugin).buildBattleEngine();
```

然后把 `'fireball'` 写进某个兵种的 `abilities`，并把 `engine` 交给 `GameSession`。指令菜单、AI 枚举与权威合法性校验会读取同一份隔离目录；其他引擎实例不会被污染。需要长期保存的效果应注册为 `StatusBehavior` 或 `WeaponHitEffectHandler`，不要塞进 `Unit.meta`。

### 加一条规则开关

在 `RuleSet` 里加字段并给默认值，引擎里读 `state.rules.xxx`。关卡的 `rules` 会覆盖默认值，编辑器右栏加一个 `data-field="r.xxx"` 的输入框就能编辑。现成的开关：`captureMode` `counterAttack` `damageVariance` `fog` `turnLimit` `baseResourceGrants` `siteResourceOverrides` `enemiesBlockMovement` `friendlyPassThrough` `maxUnitsPerPlayer` `recruitsActImmediately`。

### 加一种胜利条件

通过 declaration merging 扩展 `ObjectiveKindMap`，再向独立的 `ObjectiveHandlerRegistry` 注册一个同时负责判定、描述和进度的策略。无需修改 `victory.ts`；需要隔离模组时，把该注册表注入新的 `BattleEngine`。完整范式见[架构文档第 18 节](./docs/combat-engine-architecture.md#18-第二阶段架构硬化充血模型与开放规则)。

### 预留但没用的口子

- `LevelData.extra`：战斗核心不解释的不透明应用元数据；正式跨关状态使用 `src/campaign`
- `Unit.meta`：只用于短期原型；经验、状态、气势等正式机制不能放回这里
- `rules.damageVariance`：预留的模式字段，当前确定性结算不读取它
- 多阵营：`PlayerConfig.team` 已经是独立字段，同队即为盟友（`areAllies`），编辑器支持添加到 8 名玩家

---

## 地图编辑器

`editor.html`。左栏调色板、中间画布、右栏属性与检查。

| 操作 | 说明 |
| --- | --- |
| 左键拖动 | 用当前工具绘制 |
| 右键 | 擦除（地形→平原 / 移除单位 / 归属→中立） |
| `B R F U O E I` | 笔刷 / 矩形 / 填充 / 放单位 / 归属 / 擦单位 / 吸取 |
| `1`–`9` | 快速选地形 |
| `Ctrl+Z` / `Ctrl+Shift+Z` | 撤销 / 重做 |
| `Ctrl+S` | 保存到「我的关卡」 |
| `Ctrl+滚轮` | 缩放 |

右栏的「检查」面板是实时的 lint：单位重叠、单位站在不可通行地形、玩家开局即败、归属指向不存在的玩家等都会立刻报错，**有错误时「试玩」会被拦下**。红色蒙版会直接画在有问题的单位上。

编辑器会自动把草稿写进 `localStorage`，刷新不丢。「导出」得到的 JSON 就是 `src/content/ancient-empires/levels/*.json` 的格式——放入对应题材包的关卡目录并在其 `levels/index.ts` 注册即可。

## 关卡格式

地形用字符网格存，方便 review diff，也能手改：

```
. 平原   - 道路   = 桥梁   T 森林   h 丘陵   ^ 山地
~ 水域   # 城墙   v 村庄   b 兵营   C 城堡
```

```json
{
  "schema": 2,
  "id": "twin-hills",
  "name": "双子丘陵",
  "width": 16, "height": 11,
  "terrain": ["..T..h.~~..T..^.", "..."],
  "owners": [{ "x": 1, "y": 1, "owner": 1 }],
  "units":  [{ "x": 2, "y": 1, "unit": "soldier", "owner": 1 }],
  "players": [
    { "id": 1, "name": "蓝军", "team": 1, "color": "#3f7fd8", "controller": "human",
      "resources": { "funds": { "current": 300, "capacity": null } } },
    { "id": 2, "name": "红军", "team": 2, "color": "#d8483f", "controller": "ai",
      "resources": { "funds": { "current": 300, "capacity": null } },
      "ai": { "aggression": 0.45 } }
  ],
  "rules": { "turnLimit": 14 },
  "victory": [{ "type": "routEnemies" }, { "type": "captureHQ" }]
}
```

`players[].objectives` 可以给单个玩家单独设目标（`03-siege.json` 就是攻方"攻占城堡"、守方"坚守 14 回合"）。

## AI

`src/core/ai.ts`：一层深度的效用搜索。枚举每个单位的 (落点 × 能力 × 目标)，用"造成伤害 × 目标价值 − 反击损失 + 任务价值 + 地形/编队加成 − 危险度"打分，全军里挑分最高的一个动作执行。`ai-objectives.ts` 会把护送、保护、指定歼灭、结构破坏、区域控制和顺序任务转换为战术意图；自定义目标可注册自己的 AI 顾问。`aggression`（0–1）调节推进欲望与冒险容忍度。

AI 目前不吃战争迷雾（有意为之，`vision.ts` 只服务于人类视角）。

## 测试

```bash
npm test                    # 全部
npx vitest run src/core     # 只跑规则引擎
```

- `core/__tests__` — 移动消耗与阻挡、伤害/克制/反击、占领、经济与回合、非法操作、胜负、撤销
- `core/__tests__/levels.test.ts` — 每张内置关卡：合法性、行列尺寸、序列化往返、AI 对打 12 回合不出非法动作
- `ui/` `editor/` `__tests__/` — jsdom 下真实挂载页面：棋盘绘制、选中与路径预览、征募弹窗、编辑器各工具、导出往返、草稿自动保存
