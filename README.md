# 远古帝国 · 战术复刻

Ancient Empires 风格的回合制战棋，Web 技术栈实现：

- **纯 TypeScript 规则引擎**（`src/core`），不依赖 DOM，可测试、可移到 worker / 服务端
- **全部美术由 SVG 手绘**：11 种地形、9 个兵种精灵、9 张兵种立绘，阵营颜色实时换色
- **地图编辑器**（`editor.html`），关卡即编辑器文档，内置 3 张关卡都可以在编辑器里打开继续改
- 64+ 个测试覆盖规则、AI、关卡合法性与两个页面的运行时

```bash
npm install
npm run dev        # http://localhost:5173  游戏
                   # http://localhost:5173/editor.html  编辑器
npm test           # 全部测试
npm run build      # 产出 dist/
```

---

## 核心规则（对齐远古帝国）

| 系统 | 实现 |
| --- | --- |
| 网格 | 四方向连通，曼哈顿距离 |
| 移动 | Dijkstra 按地形进入消耗；四种移动型：步行 / 骑乘 / 重装 / 飞行 |
| 穿越 | 可穿过友军，不能停在友军格；敌军完全阻挡 |
| 战斗 | **完全确定性**，HUD 里的预测就是结果 |
| 反击 | 由射程覆盖推导：防守方能打到攻击者所在格才会反击 |
| 占领 | 踏入即占领（`captureMode: 'instant'`），只有带 `capture` 能力的步兵可以占 |
| 经济 | 回合开始结算所属建筑收入；单位站在己方建筑上回血 |
| 生产 | 城堡 / 兵营出兵，新单位当回合默认不能行动 |
| 胜负 | 歼灭敌军 / 攻占城堡 / 控制全部据点 / 坚守回合，可按玩家分别设置 |

伤害公式（`src/core/combat.ts`）：

```
damage = 攻击力 × 属性克制 × (0.5 + 0.5 × 剩余生命比) × (1 - 减伤)
减伤 = 地形防御 + 单位护甲，上限 60%
```

属性克制是一张 4×4 表（`src/core/data/units.ts` 的 `EFFECTIVENESS`）：斩击欺负无甲、穿刺克飞行、钝击破重甲、魔法无视重甲。想改平衡只动这一张表。

由此自然长出的战术：

- 弓箭手隔一格射击不吃近战反击，被贴身就会被反击
- 弩车最小射程 2、移动后不能开火 → 永远不会被近战反击，但必须被保护
- 骑士 6 移动但不能占领、怕魔法；巨龙无视地形但怕弓箭
- 山地 40% 减伤 + 骑乘/重装无法进入 → 高地是步兵和弓手的阵地

---

## 目录结构

```
src/
  core/                 规则引擎（无 DOM）
    types.ts            全部领域类型 + RuleSet 默认值
    registry.ts         内容注册表（地形/兵种/能力都挂在这里）
    grid.ts             坐标、邻居、环形范围、地形哈希
    data/terrain.ts     11 种地形定义
    data/units.ts       9 个兵种 + 属性克制表
    mapio.ts            关卡序列化（字符地图）+ 合法性检查
    state.ts            GameState 构造 / 克隆 / 查询
    movement.ts         移动场、路径、威胁范围
    combat.ts           伤害与战斗预测
    abilities.ts        能力系统（attack/heal/capture/wait）
    actions.ts          唯一的 reducer：applyAction + 回合推进
    victory.ts          目标判定、收入、治疗
    vision.ts           战争迷雾
    session.ts          会话外壳：撤销、缓存、事件订阅
    ai.ts               效用评分 AI
  art/                  SVG 绘制
    palette.ts terrain.ts units.ts portraits.ts icons.ts svg.ts
  ui/                   游戏界面
    board.ts            SVG 棋盘 + 全部动画
    hud.ts              顶栏 / 侧栏 / 弹窗（纯展示）
    game.ts             控制器：选择状态机、输入、AI 驱动
  editor/               地图编辑器
    board.ts app.ts main.ts
  levels/*.json         关卡（= 编辑器文档）
```

数据流是单向的：`输入 → Action → applyAction(state) → GameEvent[] → 动画 → 重绘`。UI 只通过 `GameSession` 说话，引擎不知道"选中"这种概念。

---

## 怎么扩展（这部分是刻意留的口子）

### 加一个兵种

```ts
// 任意启动前执行的文件里
import { UnitTypes } from './core/data/units';

UnitTypes.define({
  id: 'paladin', name: '圣骑士', cost: 500,
  maxHp: 130, attack: 50, defense: 0.2,
  movement: 5, movementClass: 'mounted',
  damageType: 'blunt', armorClass: 'heavy',
  minRange: 1, maxRange: 1, attackAfterMove: true,
  vision: 3, abilities: ['attack', 'heal', 'wait'],
  tags: ['cavalry'], blurb: '能治疗的重骑兵。',
});
```

编辑器调色板、征募面板、图鉴、AI 评分都会自动带上它。只差一张精灵图：在 `src/art/units.ts` 的 `sprites` 里加一条同名条目（没加会退化成剑士的图，不会崩）。

### 改平衡

`UnitTypes.override('knight', { cost: 300 })` / `Terrains.override('forest', { defense: 0.25 })` / 改 `EFFECTIVENESS` 表。

### 加一种新玩法动作（技能）

能力就是 "怎么枚举目标 + 怎么改状态"，加一个不需要动引擎：

```ts
import { Abilities, applyDamage } from './core/abilities';
import { ring } from './core/grid';

Abilities.define({
  id: 'fireball', name: '火球', hint: '对 2 格内区域造成范围伤害。',
  selfTargeted: false, priority: 15,
  targets: ({ state, at }) => ring(state.map, at, 1, 2),
  usable: ({ unit }) => Number(unit.meta.mana ?? 0) > 0,
  execute: ({ state, unit }, target, emit) => {
    unit.meta.mana = Number(unit.meta.mana) - 1;
    for (const c of ring(state.map, target!, 0, 1)) {
      const victim = state.units.find((u) => u.x === c.x && u.y === c.y);
      if (victim) applyDamage(state, victim, 30, emit);
    }
  },
});
```

然后把 `'fireball'` 写进某个兵种的 `abilities`。指令菜单、AI 枚举、合法性校验都会认它（`Unit.meta` 就是给这种自定义状态留的自由字段）。

### 加一条规则开关

在 `RuleSet` 里加字段并给默认值，引擎里读 `state.rules.xxx`。关卡的 `rules` 会覆盖默认值，编辑器右栏加一个 `data-field="r.xxx"` 的输入框就能编辑。现成的开关：`captureMode` `counterAttack` `damageVariance` `fog` `turnLimit` `baseIncome` `incomeOverride` `enemiesBlockMovement` `friendlyPassThrough` `maxUnitsPerPlayer` `recruitsActImmediately`。

### 加一种胜利条件

`Objective` 联合类型加一个成员 → `victory.ts` 的 `objectiveMet` / `objectiveProgress` / `OBJECTIVE_LABEL` 各加一支 → 编辑器 `OBJECTIVE_TYPES` 加一项。

### 预留但没用的口子

- `LevelData.extra`：剧情、触发器、对话之类以后要塞的东西
- `Unit.meta`：经验、状态、法力
- `rules.damageVariance`：想做随机伤害时把它调大即于生效
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

编辑器会自动把草稿写进 `localStorage`，刷新不丢。「导出」得到的 JSON 就是 `src/levels/*.json` 的格式——放进那个目录并在 `src/levels/index.ts` 里 import 一下，它就成了内置关卡。

## 关卡格式

地形用字符网格存，方便 review diff，也能手改：

```
. 平原   - 道路   = 桥梁   T 森林   h 丘陵   ^ 山地
~ 水域   # 城墙   v 村庄   b 兵营   C 城堡
```

```json
{
  "schema": 1,
  "id": "twin-hills",
  "name": "双子丘陵",
  "width": 16, "height": 11,
  "terrain": ["..T..h.~~..T..^.", "..."],
  "owners": [{ "x": 1, "y": 1, "owner": 1 }],
  "units":  [{ "x": 2, "y": 1, "unit": "soldier", "owner": 1 }],
  "players": [
    { "id": 1, "name": "蓝军", "team": 1, "color": "#3f7fd8", "controller": "human", "funds": 300 },
    { "id": 2, "name": "红军", "team": 2, "color": "#d8483f", "controller": "ai", "funds": 300,
      "ai": { "aggression": 0.45 } }
  ],
  "rules": { "turnLimit": 14 },
  "victory": [{ "type": "routEnemies" }, { "type": "captureHQ" }]
}
```

`players[].objectives` 可以给单个玩家单独设目标（`03-siege.json` 就是攻方"攻占城堡"、守方"坚守 14 回合"）。

## AI

`src/core/ai.ts`：一层深度的效用搜索。枚举每个单位的 (落点 × 能力 × 目标)，用"造成伤害 × 目标价值 − 反击损失 + 占领收益 + 地形加成 − 危险度"打分，全军里挑分最高的一个动作执行，一次一步（所以 UI 能逐步演出）。`aggression`（0–1）调节推进欲望与冒险容忍度。征募按"性价比 × 对当前敌军阵容的克制系数"选兵，并避免造成单一兵种。

AI 目前不吃战争迷雾（有意为之，`vision.ts` 只服务于人类视角）。

## 测试

```bash
npm test                    # 全部
npx vitest run src/core     # 只跑规则引擎
```

- `core/__tests__` — 移动消耗与阻挡、伤害/克制/反击、占领、经济与回合、非法操作、胜负、撤销
- `core/__tests__/levels.test.ts` — 每张内置关卡：合法性、行列尺寸、序列化往返、AI 对打 12 回合不出非法动作
- `ui/` `editor/` `__tests__/` — jsdom 下真实挂载页面：棋盘绘制、选中与路径预览、征募弹窗、编辑器各工具、导出往返、草稿自动保存
