# 编写和校验 SRPG 关卡数据

本页参考 `LevelData` schema 2，说明关卡文件的字段、引用关系、正规化和校验行为。它适用于 TypeScript 关卡定义与编辑器导出的 JSON。

> 文档类型：Reference · 当前 schema：2 · 代码真值：`packages/battle-engine/src/types.ts`、`packages/battle-engine/src/level/`

## 最小关卡

最小可运行关卡需要地图、玩家、单位、规则和胜利条件。先安装关卡引用的内容包，再创建状态。

```typescript
import type { LevelData } from '@empire/battle-engine';

export const level: LevelData = {
  schema: 2,
  id: 'training-field',
  name: '训练场',
  width: 5,
  height: 4,
  terrain: ['.....', '.....', '.....', '.....'],
  owners: [],
  units: [
    { key: 'blue', x: 1, y: 2, unit: 'soldier', owner: 1 },
    { key: 'red', x: 3, y: 1, unit: 'soldier', owner: 2 },
  ],
  players: [
    {
      id: 1,
      name: '蓝军',
      team: 1,
      color: '#3f7fd8',
      controller: 'human',
      resources: {},
    },
    {
      id: 2,
      name: '红军',
      team: 2,
      color: '#d8483f',
      controller: 'ai',
      resources: {},
    },
  ],
  rules: {},
  victory: [{ type: 'routEnemies' }],
};
```

`engine.createState(level)` 会先调用 `validateLevel()`。错误会被包装为 `BattleLevelError`，非法关卡不会产生半个 `GameState`。

## 顶层字段

`LevelData` 使用固定顶层结构。未知扩展元数据只能进入 `extra`，战斗核心不会解释它。

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `schema` | 是 | 当前必须为 `2` |
| `id` | 是 | 稳定关卡 ID，也用于匹配表现策略 |
| `name` | 是 | 显示名称，不参与规则 |
| `author` | 否 | 作者信息 |
| `description` | 否 | 关卡说明，不参与规则 |
| `width`、`height` | 是 | 地图格尺寸 |
| `terrain` | 是 | 高度为 `height` 的地形字符行 |
| `elevation` | 否 | 行优先扁平整数数组，省略时全为 0 |
| `cliffs` | 否 | 显式不可跨越边 |
| `directionalCover` | 否 | 格边方向掩体 |
| `owners` | 是 | 可占领格的初始归属 |
| `units` | 是 | 初始单位和战斗局部状态 |
| `commanders` | 否 | 指挥官链接、光环、供给和战术 |
| `structures` | 否 | 结构实例 |
| `composites` | 否 | 多结构组成的复合目标 |
| `players` | 是 | 玩家、队伍、控制器、资源和目标 |
| `rules` | 是 | 覆盖默认规则的局部字段 |
| `victory` | 是 | 玩家未覆盖目标时使用的通用目标 |
| `scenario` | 否 | 区域、变量、覆盖层、触发器和交战政策 |
| `deployment` | 否 | 战前部署区与确认顺序 |
| `extra` | 否 | 上层不透明元数据 |

## 地形字符与地图层

`terrain` 不直接写地形 ID，而是写内容包注册的单字符编码。一个字符只能映射一个地形，一个地形也只能拥有一个编码。

`terrain.length` 必须等于 `height`，每行的 Unicode code point 数量必须等于 `width`。不要用 UTF-16 字符串下标自行解析多字节字符，应复用 `mapFromLevel()`。

序列化同样是精确契约：地图中每个地形 ID 都必须在当前引擎的 `TerrainEncoding` 中有字符。编码器不会把未知地形偷偷写成默认地形；内容包安装时也会拒绝一个没有字符的默认地形，避免保存成功却悄悄改图。

关卡空间由独立层组成：

| 层 | 表达内容 | 运行时解释 |
| --- | --- | --- |
| `terrain` | 基础地形 | 移动、防御、视野、生产、治疗和资源 |
| `elevation` | 地势高度 | 爬升、下落、视线、高打低和掩体压制 |
| `cliffs` | 地形边界 | 显式禁止相邻格跨越 |
| `directionalCover` | 格边设施 | 按来袭方向提供半掩体或全掩体 |
| `scenario.overlays` | 临时地貌 | 修改成本、防御、视野、治疗和回合状态 |
| `structures` | 战场实体 | 阻挡移动或视线，提供掩体并承受伤害 |

不要把高地编码成一个特殊地形，也不要用结构代替悬崖。各层可以独立动态修改和序列化。

### 海拔

`elevation` 为长度 `width × height` 的行优先数组。第 `(x, y)` 格索引为 `y × width + x`。

省略数组时，`normaliseLevel()` 会填充全 0。显式数组必须使用有限数值，生产关卡应写整数。

### 悬崖

悬崖连接两个正交相邻格：

```typescript
cliffs: [
  { from: { x: 2, y: 3 }, to: { x: 2, y: 4 } },
]
```

边的方向不影响身份。`A → B` 与 `B → A` 是同一条悬崖，不应重复定义。

### 方向掩体

方向表示攻击来袭的边。例如北侧全掩体：

```typescript
directionalCover: [
  { at: { x: 4, y: 3 }, sides: { north: 'full' } },
]
```

同一格可以声明多个方向。省略方向等于该边没有额外掩体。

## 玩家与队伍

`PlayerConfig.id` 是战斗内所有权，`team` 是胜负与敌我关系。同 team 玩家互为盟友，可以由人类或 AI 分别控制。

每个玩家需要：

- 唯一正数 `id`
- 显示 `name` 和 `color`
- `team`
- `human` 或 `ai` 控制器
- `resources` 账户映射
- 可选 AI `aggression`
- 可选独立 `objectives`

玩家资源账户使用以下形状：

```typescript
resources: {
  funds: { current: 500, capacity: null },
  command_points: { current: 2, capacity: 5 },
}
```

`current` 必须是非负有限数值，`capacity: null` 表示无上限。资源 ID 必须有对应 `ResourceAdapter`，否则引擎配置或执行会失败。

如果玩家定义了 `objectives`，引擎使用它们覆盖顶层 `victory`。这适合非对称胜负和多方战斗。

## 单位

`LevelUnit` 创建运行时 `Unit`。关卡中的 `key` 是稳定场景引用，运行时数字 ID 由状态创建器分配。

| 字段 | 用途 |
| --- | --- |
| `key` | 场景 selector、指挥官、部署和战役名单绑定 |
| `x`、`y` | 初始格坐标 |
| `unit` | 内容目录中的单位类型 ID |
| `owner` | 玩家 ID |
| `hp` | 可选初始生命 |
| `commander` | 链接的指挥官 ID |
| `rank`、`rankProgress` | 当关军衔状态 |
| `resources` | 单位资源账户种子 |
| `reaction` | 初始反应姿态 |
| `facing` | 初始四方向朝向 |
| `career` | 当前职业 |
| `unlockedCareers` | 已解锁职业 |
| `careerMastery` | 职业熟练度 |
| `learnedAbilities` | 已掌握能力 |
| `morale` | 初始士气 |
| `formation` | 初始阵形 |
| `directive` | AI 战术指令 |

需要被目标、触发器、指挥官、部署区或战役桥引用的单位必须设置唯一 `key`。

AI 指令支持：

- `assault`：主动推进和交战
- `guard`：围绕区域或关键位置防守
- `patrol`：按 waypoint 循环
- `retreat`：优先撤向指定区域或 waypoint

## 指挥官、结构和复合目标

指挥官通过 `unitKey` 绑定已经存在的单位：

```typescript
commanders: [{
  id: 'blue-command',
  unitKey: 'blue-leader',
  radius: 3,
  aura: { attackMultiplier: 1.05, defenseDelta: 0.04 },
  turnGrants: [{ resource: 'command_points', amount: 1 }],
  tactics: ['rally'],
}]
```

普通单位使用 `commander` 字段加入指挥关系。链接并不保证光环，运行时还会检查指挥官存活、所有权和距离。

结构实例引用 `StructureDef`：

```typescript
structures: [{
  id: 'east-gate',
  type: 'gate',
  owner: 2,
  x: 9,
  y: 4,
}]
```

复合目标只引用结构 ID，不复制结构状态：

```typescript
composites: [{
  id: 'walking-fortress',
  parts: ['left-leg', 'right-leg', 'core'],
  minimumNeutralized: 2,
  tags: ['boss'],
}]
```

## 规则覆盖

`rules` 只写需要覆盖的字段。`resolveRules()` 会与 `defaultRules()` 产生的新默认值深合并，不共享嵌套对象。

主要规则分组如下：

| 分组 | 字段 |
| --- | --- |
| 棋盘几何 | `grid`：`square4`（默认四方格）、`square8`（对角也算一步）、`hex`（六邻格） |
| 行动顺序 | `turnOrder`：`side`（阵营轮流）、`initiative`（个体行动序） |
| 占领 | `captureMode`、`captureThreshold` |
| 资源 | `baseResourceGrants`、`siteResourceOverrides` |
| 据点 | `healOnOwnedBuilding` |
| 回合 | `counterAttack`、`turnLimit`、`recruitsActImmediately` |
| 占位 | `friendlyPassThrough`、`enemiesBlockMovement`、`maxUnitsPerPlayer` |
| 可见性 | `fog` |
| 高地 | `highGroundThreshold`、`highGroundDamageMultiplier` |
| 方位 | `sideAttackMultiplier`、`backAttackMultiplier`、`flankAttackMultiplier` |
| 掩体 | `halfCoverDefense`、`fullCoverDefense` |
| 士气 | `moraleEnabled` 和各类士气损失字段 |

不要复制完整默认规则到每张关卡。只写关卡真正改变的值，减少默认值升级时的漂移。

**棋盘几何只改「谁挨着谁」，不改文件格式。** 三种铺法都用同一套矩形行列存储（六边格采用 odd-r 偏移），所以地形行、海拔数组、悬崖边、编辑器笔刷完全不变；改变的是距离、邻接、朝向集合与画面。`square8` 连美术都不用改——铺法与四方格相同，只有邻接不同。`hex` 会把方形地形贴图按格子形状裁切，并按格描边。

朝向随铺法而变：四方格四向、八方格八向、六边格六向（`hexEast`、`hexNortheast`…）。关卡里写 `facing` 时必须用当前铺法认识的名字，否则 `validateLevel()` 与引用校验会拒绝。

## 目标

顶层 `victory` 与玩家 `objectives` 使用相同的开放 `Objective` 代数。复杂目标应使用稳定 `id`，方便场景条件和效果引用。

组合目标示例：

```typescript
victory: [{
  id: 'main-mission',
  type: 'all',
  label: '守住领队并夺取桥头',
  objectives: [
    {
      id: 'protect-leader',
      type: 'protect',
      selector: { keys: ['blue-leader'] },
      minimumAlive: 1,
      untilTurn: 10,
    },
    {
      id: 'take-bridge',
      type: 'control',
      zone: 'bridgehead',
    },
  ],
}]
```

支持的目标类型和编辑器覆盖见[引擎能力目录](./engine-capabilities.md)。

## 场景

`scenario` 用结构化数据表达动态战场。区域是其主要寻址单位：

```typescript
scenario: {
  variables: { gate_opened: false },
  zones: [
    {
      id: 'bridgehead',
      cells: [{ x: 5, y: 3 }, { x: 6, y: 3 }],
      tags: ['objective'],
    },
  ],
  triggers: [],
}
```

### 触发器

触发器由 ID、时机、条件和效果组成。省略 `repeat` 时为一次性触发。

```typescript
triggers: [{
  id: 'turn-three-reinforcement',
  timing: 'turnStart',
  condition: { type: 'turnAtLeast', turn: 3 },
  effects: [{
    type: 'spawnUnits',
    reason: 'reinforcement',
    ready: true,
    units: [
      {
        key: 'red-wave',
        x: 9,
        y: 2,
        unit: 'soldier',
        owner: 2,
        facing: 'west',
      },
    ],
  }],
}]
```

循环触发器使用：

```typescript
repeat: {
  everyRounds: 2,
  startTurn: 3,
  endTurn: 9,
  maxFirings: 4,
}
```

重复触发器必须有正整数 `everyRounds`。建议至少设置 `endTurn` 或 `maxFirings`，避免关卡无限生成状态或单位。

### 覆盖层和交战政策

初始覆盖层引用已定义区域：

```typescript
overlays: [{
  id: 'bridge-fire',
  type: 'fire_field',
  zone: 'bridgehead',
  remainingRounds: 3,
}]
```

区域交战政策可以禁止攻击或全部敌对行动：

```typescript
engagementRules: [{
  id: 'hospital-truce',
  zone: 'hospital',
  mode: 'no-hostile-actions',
}]
```

Action 菜单、AI 和正式提交都会读取同一政策。

## 战前部署

部署区引用 `scenario.zones`。可以限制每个区域允许调整的单位 key：

```typescript
deployment: {
  order: [1, 2],
  zones: [
    {
      player: 1,
      zone: 'blue-deployment',
      unitKeys: ['blue-leader', 'blue-archer'],
    },
  ],
}
```

存在部署配置时，初始 `GameState.phase` 为 `deployment`。玩家依次提交 `deployUnit` 和 `finishDeployment`，完成后才开始第 1 回合。

## 正规化、校验和创建状态

加载外部 JSON 时使用以下顺序：

```typescript
import { createBattleEngine, normaliseLevel, validateLevel } from '@empire/battle-engine';

const engine = createBattleEngine({ content });
const level = normaliseLevel(JSON.parse(source));

// 校验要对着规则集问：内容 id 归目录，铺法与目标／场景／常驻命令归组装好的规则。
const errors = validateLevel(engine.rules, level).filter((issue) => issue.severity === 'error');
if (errors.length > 0) {
  throw new Error(errors.map((issue) => issue.message).join('\n'));
}

const state = engine.createState(level);
```

`engine.createState()` 自己也会跑同一次校验并在有 error 时抛 `BattleLevelError`；上面这段只是为了在建状态之前把问题列给使用者看。

`normaliseLevel()` 只补齐允许缺省的字段并拒绝旧 schema。它不会修复非法引用、越界坐标或冲突定义。

`validateLevel()` 检查的主要不变量包括：

- 地图尺寸和地形行
- 海拔数组长度与数值
- 悬崖和方向掩体坐标
- 玩家 ID、队伍、资源和控制器
- 单位、职业、资源、朝向和位置
- 单位重叠与不可通行地形
- 指挥官、结构、复合目标和引用 ID
- 区域、覆盖层、触发器、条件和效果引用
- 部署区和单位 key
- 目标类型与关联对象
- 玩家是否开局即无单位且无生产能力

`BattleEngine.createState()` 还会检查当前引擎是否安装了关卡需要的目标、场景和命中处理器。

## 版本策略

schema 2 不兼容旧的隐式资金字段。每个玩家必须显式提供 `resources`。`normaliseLevel()` 遇到旧字段时会失败，不做猜测迁移。

改变关卡格式时遵守：

1. 只有无法用可选字段表达的破坏性变化才提高 schema
2. 同步修改类型、正规化、校验、编辑器和所有内置关卡
3. 当前开发阶段直接升级所有内置关卡与开发存档，不保留旧 schema 读取路径
4. 更新关卡往返、内置关卡和应用挂载测试
5. 更新本页和[关卡编辑器](./editor-guide.md)

## 相关文档

- [引擎能力目录](./engine-capabilities.md)
- [战斗系统设计](./combat-system-design.md)
- [战斗引擎架构](./combat-engine-architecture.md)
- [关卡编辑器](./editor-guide.md)
