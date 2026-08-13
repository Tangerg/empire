# 组织剧情战役状态

战役引擎在单场战斗之上组织节点、选择、持久名单和版本化存档。本页只说明通用技术契约，不描述任何具体剧本、人物或章节内容。

> 文档类型：Conceptual · 状态：基础框架已实现 · 代码真值：`packages/campaign-engine/src/`

## 战役层的职责

战役层回答跨关问题：

- 当前位于哪个流程节点
- 哪些选择可用，选择修改哪些持久事实
- 下一场战斗使用哪张 `LevelData`
- 哪些名单单位进入战斗，战后如何回收状态
- 存档依赖哪一版战役定义和内容包

战役层不解释伤害、移动、射程、人工智能（AI）、地形和战斗动画。它也不排版对话；节点中的 `presentation` 只是上层界面解释的不透明定位符。

## 运行模型

战役定义不可变，战役状态可序列化，`CampaignRuntime` 负责原子迁移。

```mermaid
flowchart LR
  Definition["CampaignDefinition"] --> Runtime["CampaignRuntime"]
  State["CampaignState"] --> Runtime
  Runtime --> Aggregate["CampaignAggregate"]
  Runtime --> Request["BattleRequest"]
  Request --> Battle["BattleEngine / GameSession"]
  Battle --> Result["BattleResult"]
  Result --> Runtime
  State --> Save["CampaignSave"]
```

| 对象 | 稳定职责 |
| --- | --- |
| `CampaignDefinition` | schema、定义版本、内容包版本、起点、节点图和初始名单 |
| `CampaignState` | 当前节点、旗标、变量、资源、关系、功能、名单和历史 |
| `CampaignAggregate` | 定义与状态不变量、节点查询、效果应用和战果投影 |
| `CampaignRuntime` | `advance`、`choose`、`beginBattle`、`completeBattle` 和事务回滚 |
| `CampaignBattleBridge` | 战役 DTO 与战斗 DTO 之间的防腐层 |
| `CampaignSaveMigrator` | 显式、逐版本向前的存档迁移 |

## 节点代数

`CampaignNode` 使用封闭的流程节点类型。内容通过数据组合节点，不向运行时注入剧本名称判断。

| 节点 | 用途 | 离开方式 |
| --- | --- | --- |
| `story` | 线性演出定位 | `advance()` |
| `hub` | 营地或整备定位 | `advance()` |
| `travel` | 旅途或地图移动定位 | `advance()` |
| `choice` | 条件化分支 | `choose(id)` |
| `battle` | 关卡请求与战果出口 | `beginBattle()`、`completeBattle()` |
| `ending` | 完成或失败终点 | `advance()` |

节点效果和选项效果都通过 `CampaignEffectRegistry` 解释。条件由 `CampaignConditionRegistry` 解释。两个代数都支持 TypeScript declaration merging 和策略注册，但默认运行时会克隆注册表，避免实例间污染。

内置条件覆盖：

- 旗标存在性
- 变量数值比较
- 战役资源比较
- 势力关系比较
- 名单单位状态
- `all`、`any`、`not` 组合

内置效果覆盖：

- 设置或累加变量
- 设置或清除旗标
- 增加战役资源
- 修改势力关系
- 开关功能
- 修改名单单位状态

## 战斗防腐层

`CampaignBattleBridge` 是唯一同时理解 `CampaignState` 与 `GameState` 的通用模块。战斗内核始终只接收普通 `LevelData`。

### 进入战斗

`prepare()` 执行以下步骤：

1. 解析战斗节点和关卡快照
2. 按稳定的 `levelUnitKey` 查找关卡单位
3. 移除不可出战的名单单位
4. 写入兵种、生命比例、士气比例、军衔、资源、职业、熟练度和能力
5. 生成唯一 `requestId` 和只读语义上下文
6. 返回 `BattleRequest`

桥接器不会直接改变战役状态。`CampaignRuntime.beginBattle()` 在同一事务中记录 pending request，失败时恢复整个状态。

### 离开战斗

`result()` 根据活跃单位、载具乘员和战场 marker 还原持久单位状态：

| 战场结果 | 名单状态 |
| --- | --- |
| 仍在场或仍在载具中 | `available` |
| 溃退 marker | `routed` |
| 投降 marker | `surrendered` |
| 主动撤离 marker | `missing` |
| 死亡 marker | `fallen` |

`BattleResult` 还包含胜负、回合数、场景信号和语义事件计数。`completeBattle()` 只接受与 pending request 完全匹配的结果，因此重复提交和串关结果都会失败并回滚。

## 存档与迁移

`CampaignSave` 当前为 schema 1，包含：

- 战役定义 ID 和版本
- 内容包 ID 与版本映射
- 保存时间
- 完整 `CampaignState`

`CampaignSaveMigrator` 只执行已注册的逐版本迁移。以下情况会拒绝载入：

- schema 非法或高于当前版本
- 缺失某一步迁移
- 迁移没有提高 schema
- 定义 ID 或版本不匹配
- 内容包版本不匹配
- 状态不满足定义约束

该格式不是战斗存档或战斗回放。战斗回放仍缺少正式的初始状态哈希、Action 序列、规则版本锁定和回放验证器。

## 事务和失败语义

`advance`、`choose`、`beginBattle` 和 `completeBattle` 都在 `CampaignRuntime.transaction()` 内执行。任何校验、效果或桥接错误都会恢复调用前快照。

应用层可以依赖以下不变量：

- 一个战役最多有一个 pending battle
- 战斗结果只能提交一次
- 当前节点始终存在于定义中
- 结束状态不能继续迁移
- 名单状态与定义版本始终匹配
- 失败的迁移不会留下半次旗标或资源修改

## 当前能力边界

以下能力已经实现：

- 通用节点状态机
- 条件、效果和组合逻辑
- 持久名单及战斗状态投影
- 关系、资源、旗标、变量和功能开关
- 战斗请求与战果防腐层
- 版本化存档和显式迁移
- 失败原子性和定义校验

以下能力尚未形成通用产品闭环：

- 战役定义可视化编辑器
- 装备、物品背包和商店领域
- 战前编队与持久装备界面
- 多存档槽、自动存档和云同步
- 战斗中断存档与 Action 回放
- 本地化资源管理和配音时间线

这些缺口应继续留在战役或应用层，不能塞入战斗核心。

## 扩展检查表

新增战役能力前确认：

1. 它是否跨越多个战斗？如果否，应优先留在战斗或表现层
2. 它能否写成故事中立的状态、条件和效果？
3. 新条件是否同时提供类型、处理器和测试？
4. 新效果失败时是否保持事务原子性？
5. 它是否改变存档 schema？如果是，是否提供显式迁移？
6. 它是否需要战斗数据？如果是，是否通过 `CampaignBattleBridge`？

## 相关文档

- [战斗与战役能力边界](./engine-capabilities.md)
- [战斗引擎架构](./combat-engine-architecture.md)
- [关卡数据格式](./level-format.md)
- [Monorepo 架构](./monorepo-architecture.md)
