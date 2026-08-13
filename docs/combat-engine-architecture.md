# Empire Tactics 通用 SRPG 战斗引擎架构

> 状态：第五阶段微内核与通用资源账户已落地（2026-08-11）  
> 适用范围：候选剧本 1、2、3 以及未来未知题材  
> 核心定位：小型、无界面、确定性、数据驱动的 SRPG 战斗引擎  
> 相关文档：[战斗系统设计研究](./combat-system-design.md)

## 1. 引擎定义

本项目的战斗核心不是某个故事的战斗模块，而是一个可以独立运行、测试、回放和模拟的小型 SRPG 引擎。

引擎接收：

- 一份内容包：单位、武器、能力、状态、地形、结构定义；
- 一份关卡：地图、部署、阵营、目标、规则和触发器；
- 一连串玩家或 AI 行动。

引擎输出：

- 新的确定性游戏状态；
- 可供界面播放的语义事件；
- 可供 UI、AI 和测试共同使用的行动合法性与结果预测；
- 与故事文本无关的目标进度、胜负和场景变量。

引擎不知道：

- 当前故事发生在伊瑟兰、弥光星系还是淮右；
- 一个结构在剧情里叫烽塔、天候塔还是粮仓；
- 某个伤害表现为火球、等离子炮还是火铳齐射；
- 对话文本、人物立绘、镜头、音乐和章节顺序；
- 一个选择在叙事上代表自由、归零还是开国约法。

## 2. 核心架构原则

### 2.1 机制取并集，单局复杂度取子集

三个剧本提出的机制应尽量归纳为通用原语并进入引擎能力并集：

- 奇幻剧本需要指挥、飞行、亡灵、烽塔、复活与多族单位；
- 星际剧本需要节点、机械、接管、真空、潮汐、动态地形与多部位目标；
- 历史剧本需要军团、士气、粮道、城门、水军、火攻、投降与撤离。

但任何单关只启用其中少数机制。引擎能力丰富不等于玩家同时管理所有机制。

规则预算保持：

- 普通单位一件基础武器，最多一件特殊武器；
- 精英单位最多两个明确能力；
- 英雄最多一个个人资源；
- 阵营最多一个主动战术资源；
- 单位通常不超过三个可见状态；
- 地形格通常只表达移动、保护和一个特殊标签；
- 单关最多一个主要环境机制和一个次要变化机制。

### 2.2 核心只表达事实，不表达剧情含义

例如“雨牧者死亡后黑雨扩散”应拆为：

- 条件：结构或单位 rain_shepherd 被摧毁；
- 效果：区域 A 的地形覆盖为 conductive_water；
- 效果：区域 A 内机械单位获得 susceptible_control 状态；
- 效果：目标从 defeatTarget 切换为 protectAndCleanse；
- 事件：scenarioSignal，键值 rain_shepherd_destroyed。

剧情层收到最后一项信号后决定播放什么对话。核心不保存台词。

### 2.3 UI 是引擎的消费者，不是规则的共同作者

界面只能：

- 查询合法行动；
- 请求结果预测；
- 提交行动；
- 播放事件；
- 渲染状态、地形和目标。

界面不能：

- 自行扣血或添加状态；
- 通过动画回调决定是否反击；
- 在按钮隐藏时把一个本来合法的行动变成非法；
- 读取剧情变量后偷偷修改战斗数值；
- 根据视觉位置重新计算射程。

### 2.4 故事层通过适配器影响下一关，不直接修改本局

跨关选择先由战役层转换成明确的关卡输入：

- 初始资金；
- 可用单位与英雄；
- 增援表；
- 初始结构状态；
- 场景变量；
- 可启用触发器；
- AI 性格；
- 额外目标。

本局开始后，所有变化都通过合法行动或关卡触发器发生，以保证回放和 AI 模拟一致。

### 2.5 规则确定、随机可注入

默认核心模式没有基础命中、闪避和随机暴击。任何随机性必须：

- 来自关卡显式种子；
- 进入 GameState；
- 通过引擎统一随机源消费；
- 在预测中显示结果范围或概率；
- 可以在回放中完全复现。

首个正式版本继续采用完全确定性伤害。

## 3. 三剧本需求并集矩阵

| 通用能力 | 奇幻 | 星际 | 历史 | 引擎原语 |
| --- | --- | --- | --- | --- |
| 地面近战与远程 | 剑士、弓手 | 动力剑、步枪 | 刀盾、弓弩 | Unit + Weapon |
| 重装与攻坚 | 巨魔、骑士 | 机甲、机器人 | 甲骑、陌刀 | Armor + MovementProfile |
| 飞行与特殊通行 | 巨龙 | 无人机、悬浮 | 船只、两栖 | MovementProfile |
| 治疗与修复 | 牧师 | 医师、维修师 | 军医、河工 | Ability + TargetFilter |
| 魔法与术式 | 誓火 | 星脉技术 | 火器、军令 | Weapon/Ability 标签与内容文案 |
| 军团关系 | 誓约军旗 | 行动队与无人机 | 将领与部曲 | CommanderLink |
| 战术爆发 | 誓约能力 | 协议调用 | 鼓舞、突进 | CommandPoint + Tactic |
| 英雄爆发 | 龙盟、誓言 | 共鸣、超载 | 将略、死战 | Momentum + SignatureAbility |
| 建筑 | 城门、烽塔 | 节点、炮台、舱门 | 城门、粮仓、堤坝 | Structure |
| 地图变化 | 魔潮、火焰 | 潮汐、真空、重力 | 洪水、火攻、浮桥 | TerrainOverlay + Trigger |
| 控制与异常 | 中毒、亡灵 | 接管、冻结 | 动摇、溃退 | Status |
| 召唤与增援 | 亡灵、盟军 | 无人机、地方军 | 预备队、民兵 | SpawnEffect |
| 复活与回收 | 墓碑召回 | 备份机体 | 救回伤兵 | Corpse/Marker + Spawn/Restore |
| 占领与生产 | 城镇、城堡 | 部署节点、机库 | 城池、营寨 | Site + RecruitPolicy |
| 补给 | 魔力节点 | 能源节点 | 粮道、漕船 | Scenario meter 或 Site modifier |
| 多阶段 Boss | 王冠、古龙 | 巨型维护体 | 城寨、旗舰 | Composite structures + Objectives |
| 投降与阵营变化 | 敌将加入 | 临时停火 | 招降、倒戈 | ChangeOwner/Team + AI policy |
| 护送与撤离 | 难民、使者 | 车队、飞船 | 百姓、粮车 | Unit objective + Zone |

结论：三个剧本没有要求三套战斗规则。绝大多数差异可以归约为定义、标签、触发条件和效果组合。

## 4. 引擎分层

### 4.1 内容定义层

只保存不可变定义：

- DamageChannel：伤害通道；
- ArmorProfile：护甲与抗性；
- MovementProfile：移动方式；
- WeaponDef：武器；
- AbilityDef：主动能力；
- StatusDef：状态；
- UnitDef：普通单位模板；
- HeroDef 与 ClassDef：英雄和职业；
- TerrainDef：基础地形；
- OverlayDef：临时地形覆盖；
- StructureDef：结构；
- TacticDef：指挥战术；
- AIProfile：AI 性格。

定义通过注册表按字符串 ID 访问。关卡和存档只保存 ID，不复制完整定义。

### 4.2 对局状态层

只保存本局会变化的数据：

- 单位位置、生命、所属、行动状态；
- 武器冷却与剩余次数；
- 正式状态实例；
- 指挥归属与反应姿态；
- 结构生命、所属、启用状态；
- 地形覆盖与持续时间；
- 阵营资金、指挥点和可用招募池；
- 回合、当前阵营、阶段和胜负；
- 目标状态；
- 触发器是否已触发；
- 场景变量；
- 确定性随机种子，如未来启用。

### 4.3 规则服务层

无状态或只读查询：

- movement：寻路、可停留格、强制移动；
- targeting：射程、视线、区域和目标筛选；
- forecast：伤害、反击、援护、状态和结构结果；
- actions：合法行动枚举与执行；
- turn：回合阶段与状态生命周期；
- objectives：目标求值与进度；
- scenario：触发条件和效果执行；
- ai：使用同一合法行动与预测接口评分；
- serialization：关卡、存档和回放迁移。

### 4.4 适配器层

不属于核心规则：

- Web/SVG UI；
- 地图编辑器；
- 剧情演出系统；
- 战役存档和章节选择；
- 内容包加载；
- 本地化；
- 音效、动画和镜头；
- 网络同步或服务器适配。

## 5. 通用领域模型

### 5.1 ID 和标签

所有内容使用稳定字符串 ID。显示名称可以随语言和剧本改变，ID 不改变。

示例：

- damage.anti_armor 可以显示为魔法、等离子或震天雷；
- movement.air 可以显示为飞行、悬浮或空中；
- structure.command_node 可以显示为烽塔、星脉节点或中军旗台。

标签用于内容筛选，不应替代正式状态：

- 合适：infantry、mechanical、undead、naval、building；
- 不合适：poisoned、guarding、two_turns_left。

### 5.2 移动方式

MovementProfile 不再是固定联合类型的唯一枚举，而是注册表定义：

- foot：普通步行；
- mounted：骑乘与轮式；
- heavy：重装、攻城器械、机甲；
- air：飞行或悬浮；
- naval：只能进入水域和港口；
- amphibious：水陆均可但成本不同；
- phase：特殊关卡使用，忽略部分阻挡。

地形保存按移动 Profile ID 的成本表。缺少条目表示不可进入。

同一底层移动方式可以拥有题材化名称。星际悬浮单位不必等同高空飞行，可由标签决定是否受到对空武器加成。

### 5.3 伤害通道和专属克制

伤害关系已经抽象为内容包可注册的 Damage Model：

- 温和的伤害通道与护甲基础关系；
- 武器对明确标签的 TargetBonus；
- 状态、地形、指挥与位置修正；
- 统一、可解释的修正管线。

`DamageType` 与 `ArmorClass` 都是开放字符串 ID，核心只调用 `DamageMatchupRegistry.effectiveness()`，不知道具体矩阵。一个内容包必须同时声明它使用的伤害类型、护甲类型和完整配对关系，安装时会验证缺失与重复项。

题材包可以选择共享功能语义，也可以注册自己的通道：

- cutting：对轻装和无甲；
- piercing：远程、对空或穿透；
- impact：破盾、破阵、结构；
- antiArmor：魔法、能量、火器等题材化反重甲手段；
- true：极少数环境伤害，绕过常规护甲但仍有明确上限。

界面显示名来自 `DamageTypeDef` / `ArmorClassDef`，不硬编码在核心或 HUD。当前奇幻包仍使用斩击、穿刺、钝击、魔法，以保持既有平衡；星际包可以添加动能、能量和护盾，历史包可以添加破阵、火器和甲骑，而不修改伤害管线。

### 5.4 武器

WeaponDef 至少包含：

- ID 与内容键；
- 威力与伤害通道；
- 最小和最大射程；
- 移动后能否使用；
- 视线类型：无、直射、抛射；
- 目标过滤器；
- 范围模板；
- 冷却或有限次数；
- 气势门槛；
- 对标签的专属修正；
- 可施加状态；
- 是否允许反击与援护。

普通单位的基础武器无消耗。弹药不作为全局资源，只是特定武器的有限次数。

### 5.5 单位

UnitDef 负责单位固有棋盘身份：

- 基础生命、移动和视野；
- 移动方式和护甲；
- 武器列表；
- 能力列表；
- 招募成本；
- 标签；
- 是否可以占领；
- 默认反应姿态；
- AI 角色提示。

UnitState 负责当前状态：

- 当前生命和位置；
- 本回合行动状态；
- 武器冷却与次数；
- 状态实例；
- 指挥官链接；
- 反应姿态；
- 当关军衔；
- 场景标记。

正式机制不得长期放在自由格式 meta 中。

### 5.6 状态

StatusDef 使用少量通用修正原语：

- 属性修正；
- 移动限制；
- 能力禁止；
- 持续伤害或治疗；
- 阵营控制；
- 反应姿态强制；
- 不能占领；
- 受到某标签武器额外影响；
- 回合开始、行动后、受击后的触发效果。

首批跨题材状态：

| 内部含义 | 奇幻显示 | 星际显示 | 历史显示 |
| --- | --- | --- | --- |
| damageOverTime | 中毒、燃烧 | 腐蚀、过载 | 火伤、疫病 |
| armorDown | 破甲 | 护盾崩解 | 破阵 |
| suppressed | 沉默、动摇 | 信号干扰 | 震慑、溃势 |
| guarded | 护卫 | 拦截 | 掩护 |
| controlled | 魅惑、亡灵支配 | 黑客接管 | 倒戈只用于脚本 |
| rooted | 缠绕 | 冻结锁定 | 陷马、泥泞 |

状态生命周期必须明确到回合阶段，不能由 UI 计时。

### 5.7 指挥官、军团与普通士气

每名指挥官默认统领 3～5 支部队，提供一个局部光环和少量战术能力。

普通军团不增加永久士气数值条。其战术士气由以下机制表达：

- 是否在指挥范围；
- 动摇、鼓舞、受压等短状态；
- 指挥官阵亡或离场触发；
- 特定任务变量，如全军撤退阶段。

英雄气势与普通士气是不同概念：

- 气势属于少量命名英雄，用于招牌能力；
- 士气通过状态和指挥关系表达；
- 二者不能使用同一条数值同时承担个人爆发与全军溃退。

### 5.8 反应与援护

第一阶段反应姿态：

- counter：正常反击；
- guard：放弃反击，降低一次受伤；
- support：优先援护相邻友军；
- conserve：只使用无消耗武器反击。

每单位每轮最多援护一次，不允许援护链。预测必须展示援护者、伤害承担和可能死亡结果。

### 5.9 结构与站点

Terrain 是格子固有属性，Structure 是有生命和状态的战场对象，Site 是可占领、产出或部署的战略位置。

结构可表达：

- 城门、塔楼、炮台；
- 烽塔、星脉节点；
- 粮仓、堤坝、桥梁；
- 舰船引擎、舱门、Boss 部位；
- 临时路障和工事。

StructureState 至少保存：

- ID、定义 ID、坐标与占格；
- 所属阵营；
- 当前与最大耐久；
- 启用、瘫痪、摧毁状态；
- 状态实例；
- 场景标签。

结构可以阻挡移动和视线，也可以为所在区域提供效果。普通村庄仍可只是地形站点，不必全部实体化。

### 5.10 地形覆盖与环境

基础地形保持稳定，临时变化使用 TerrainOverlay：

- 洪水、潮汐、水位；
- 火场、毒雾、黑雨；
- 真空、低重力、强风；
- 冰裂、藤蔓、崩塌；
- 魔法领域、信号干扰。

覆盖层可以修改：

- 移动成本和可通行性；
- 防御、视野和治疗；
- 回合伤害或状态；
- 武器与单位标签修正；
- 持续时间。

重力旋转、整张地图切面变化等极端效果仍由关卡触发器修改基础地形和单位坐标，不进入每格常驻计算。

### 5.11 区域

关卡可命名一组坐标为 Zone：

- 撤离区；
- 护送终点；
- 火炮危险区；
- 城内与城外；
- 潮汐影响区；
- Boss 身体区域。

目标、条件与效果引用 Zone ID，不在多处复制坐标列表。

## 6. 目标系统

### 6.1 目标不是硬编码的故事分支

Objective 使用通用类型与参数：

- eliminate：消灭满足过滤器的目标；
- defeat：击败指定单位或结构；
- capture：占领指定站点；
- control：同时控制多个站点或区域；
- survive：坚持到指定回合或阶段；
- protect：让目标保持存活；
- escort：让目标进入区域；
- evacuate：指定单位或数量离开；
- destroy：摧毁结构或装置；
- interact：完成修复、净化、开门等交互；
- prevent：在时间内阻止某条件发生；
- score：达到招降数、救援数等计数。

### 6.2 目标组合

支持：

- all：全部完成；
- any：任一完成；
- sequence：按阶段完成；
- optional：额外评价，不影响胜利；
- failOn：条件满足立即失败。

目标拥有稳定 ID、显示键、公开或隐藏状态和进度查询。剧情可以改变显示文本，但不能改变核心求值结果。

### 6.3 动态目标

关卡触发器可以：

- 激活新目标；
- 完成或取消旧目标；
- 将目标从隐藏变为公开；
- 修改目标过滤器引用的场景变量；
- 添加额外目标。

每次变化产生 ObjectiveChanged 事件，由 UI 显示，由剧情层决定是否播放对话。

## 7. 场景触发器与效果 DSL

### 7.1 条件原语

首批条件应包含：

- turn：回合或阵营阶段；
- variable：场景变量比较；
- objective：目标状态；
- unit：指定单位、标签或阵营的数量、位置、生命；
- structure：结构耐久、所属和状态；
- control：区域或站点控制；
- enteredZone：单位进入区域；
- eventCount：某类事件发生次数；
- all、any、not：逻辑组合。

### 7.2 效果原语

- setVariable、addVariable；
- spawnUnit、removeUnit、changeOwner；
- addStatus、removeStatus；
- damage、heal；
- addOverlay、removeOverlay、replaceTerrain；
- damageStructure、repairStructure、changeStructureOwner；
- grantFunds、grantCommandPoints；
- activateObjective、cancelObjective、completeObjective；
- setAIProfile、setDiplomacy；
- emitSignal。

效果只表达战斗事实。emitSignal 把语义键交给剧情和 UI 适配器。

### 7.3 触发时机

固定检查点：

1. 对局创建完成；
2. 回合开始状态结算后；
3. 行动执行完成、死亡和状态结算后；
4. 回合结束状态结算后；
5. 目标状态变化后；
6. 游戏结束前。

触发器执行到稳定状态为止，但每个触发器默认只触发一次。允许重复者必须声明冷却或每回合上限，防止无限循环。

### 7.4 事件顺序

一次攻击的核心事件顺序唯一固定：

1. actionDeclared；
2. move；
3. weaponUsed；
4. supportGuard；
5. damage；
6. death；
7. counter；
8. supportAttack；
9. statusApplied 与状态结算；
10. objectiveChanged；
11. scenarioSignal；
12. gameOver。

UI 可以合并或跳过动画，不能改变事件顺序。

## 8. 资源模型

### 8.1 通用资源账户

资源不是 `GameState` 外部的全局组件仓库，而是实体状态的一部分：

- `PlayerState.resources` 保存阵营账户，例如 `funds`、`command_points`；
- `Unit.resources` 保存单位账户，例如英雄 `momentum`；
- `UnitWeaponState.resources` 保存武器账户，例如 `weapon_uses`；
- `rankProgress` 是单位成长属性，不伪装成可支付资源；
- 跨关经验属于未来战役层，不由本局资源系统持有。

`BattleResourceSystem` 只负责账户访问、容量、支付、补充、原子可负担校验与 AI 估值。每种资源用独立 `ResourceAdapter` 定义名称、所属实体、整数规则和存储位置；账户真值始终留在 Player、Unit 或 WeaponState 上。

### 8.2 题材资源的归约

| 题材概念 | 默认实现 |
| --- | --- |
| 粮秣 | funds、站点控制、场景变量或断粮状态 |
| 能源 | 站点控制、结构启用和场景变量 |
| 魔力 | 特定能力冷却或次数，不新增普通单位法力条 |
| 弹药 | 特定武器的 `weapon_uses` 账户，不成为所有单位的资源 |
| 氧气 | 环境倒计时或区域状态 |
| 民心 | 跨关变量，决定部署与援军 |
| 全军士气 | 指挥关系和短状态，不新增全局数值条 |

场景变量可以有数字，但只有当玩家每回合需要围绕它作决定时才进入常驻 UI。

## 9. 题材化与内容包

### 9.1 共享功能 ID，内容包提供显示语义

引擎可以保存内部功能标签，内容包决定名称、图标、特效和声音：

| 功能 | 奇幻 | 星际 | 历史 |
| --- | --- | --- | --- |
| antiArmor | 魔法 | 等离子 | 火器、重锤 |
| supportHeal | 治疗术 | 医疗纳米 | 军医救护 |
| commandNode | 烽塔 | 星脉节点 | 中军旗台 |
| heavyGuard | 巨魔守卫 | 重装机甲 | 陌刀亲军 |
| airMobility | 巨龙 | 无人机 | 不启用或改为特殊侦察 |

题材化不能只靠改名字。如果一个单位视觉上是船，它必须使用与水域相符的移动定义；如果是直射步枪，三格以上应遵守视线。

### 9.2 内容包隔离

每套剧本注册自己的：

- 单位和英雄；
- 显示名称与描述；
- 武器和能力组合；
- 地形与结构皮肤；
- AI 风格；
- 关卡和剧情信号映射。

不同内容包可以复用同一功能模板，但存档必须记录内容包 ID 和版本。

代码层的 `ContentPack` 是纯数据清单，`ContentPackInstaller` 在提交前统一检查：

- 包 ID、版本和依赖；
- 所有注册表 ID 是否重复；
- 单位到移动类型、护甲和武器的引用；
- 武器到伤害类型与状态的引用；
- 地形生产列表、覆盖层状态和战术效果引用；
- 地图字符与默认地形；
- 同一安装批次实际使用到的伤害类型 × 护甲类型是否拥有克制项。

多个独立题材包联装时，未显式声明的跨包伤害/护甲组合使用中性倍率 1.0；已声明组合仍优先使用精确数据。这样既能严格发现单包缺表，也不会因为“等离子”第一次攻击“甲骑”而在运行期崩溃。

同一 ID/版本重复安装是幂等的，版本漂移会失败；任何验证错误都发生在注册表写入前，不会留下半安装内容。核心模块本身只声明空注册表，默认题材仅由应用组合根安装。

## 10. AI 约束

AI 不读取剧情文本，只读取：

- 合法行动；
- 预测结果；
- 目标权重；
- 单位、结构和区域价值；
- AIProfile 的进攻、防守、控制、经济和风险倾向。

新增机制必须同时回答：

- AI 如何枚举它；
- AI 如何估值它；
- 如何限制动作空间；
- 如何在测试中证明 AI 不会卡死。

触发器可通过公开的危险区域和目标引导 AI，不允许剧情脚本直接替 AI 移动普通单位。

## 11. 序列化与版本

### 11.1 三类数据分离

- ContentPack：不可变内容定义与版本；
- LevelData：关卡地图、部署、规则、目标与触发器；
- GameSave：运行中状态与已触发记录。

### 11.2 版本策略

- 每种格式拥有独立 schema；
- 旧关卡由纯迁移函数升级；
- 新增单位定义不应导致 LevelData schema 升级；
- 只有关卡自身结构变化才升级关卡 schema；
- 存档记录引擎版本、内容包版本和关卡版本；
- 回放记录初始状态哈希和 Action 序列。

### 11.3 不再使用自由格式袋承载正式机制

Unit.meta 与 LevelData.extra 只允许原型和未知扩展。以下内容必须进入正式字段：

- 状态；
- 武器次数与冷却；
- 指挥归属；
- 英雄气势；
- 结构；
- 目标状态；
- 触发器；
- 场景变量。

## 12. 测试策略

### 12.1 核心不变量

1. 同一状态、规则和行动必然产生同一事件与结果；
2. forecast 与 applyAction 使用同一结算管线；
3. 非法行动不修改状态；
4. 克隆模拟不污染原状态；
5. 已死亡单位不能反击、援护或触发行动后能力；
6. 同一状态在同一检查点只结算一次；
7. 每单位每轮援护不超过上限；
8. 触发器不会无限递归；
9. UI 缺席时完整对局仍能运行；
10. AI 只能返回引擎判定合法的行动。

### 12.2 跨题材契约测试

至少保留三张纯规则测试场景：

#### 奇幻契约场景

- 骑士、法师、弓手、飞行单位；
- 指挥光环与援护；
- 可损坏烽塔；
- 亡灵状态和净化；
- 动态目标从攻城切换为保护。

#### 星际契约场景

- 枪手、机器人、无人机；
- 节点接管与临时控制；
- 真空覆盖和舱门结构；
- 多部位 Boss；
- 撤离区域。

#### 历史契约场景

- 步军、弓弩、骑兵、水军；
- 粮仓与桥梁；
- 动摇、鼓舞和溃退；
- 洪水或火场地形覆盖；
- 护送粮车与招降目标。

三张场景不需要美术和剧情，只运行 headless 测试。

### 12.3 回归层次

- 单元测试：结算原语；
- 性质测试：不变量与边界；
- 契约测试：跨模块组合；
- 平衡快照：固定对局交换结果；
- AI 对打：无非法动作、死锁和无限回合；
- 旧关卡迁移：现有三关结果可审查。

## 13. 实施路线

### 阶段 A：稳定当前核心

- 修正攻击、伤害、死亡事件顺序；
- 修正 surviveTurns 语义；
- 保存当时 66 个测试作为迁移基线；
- 为公共查询和结算补充类型边界。

### 阶段 B：内容原语

- 引入 WeaponDef 和武器注册表；
- 引入 MovementProfile 注册表，增加 naval 与 amphibious；
- 引入可解释 CombatModifier；
- UnitDef 一次性迁移为武器 ID 列表，不保留首武器派生字段；
- UI 与 AI 从同一武器查询读取。

### 阶段 C：状态、反应和援护

- 正式 StatusState；
- 防御、反击、援护和节制姿态；
- 中毒、破甲、动摇、护卫纵向切片；
- 统一行动和回合生命周期。

### 阶段 D：指挥官与资源

- CommanderState 与 UnitCommandLink；
- 指挥点和通用战术；
- 英雄气势；
- 当关军衔；
- AI 指挥范围与战术估值。

### 阶段 E：结构、区域和目标

- StructureDef/State；
- Zone；
- 组合式 Objective；
- 护送、保护、撤离、摧毁和交互；
- 建筑耐久与攻城。

### 阶段 F：场景触发器与动态地形

- Condition/Effect DSL；
- TerrainOverlay；
- 动态目标；
- 增援、阵营变化和场景信号；
- 编辑器验证与只读可视化。

### 阶段 G：战役与内容包

- Hero/Class；
- 跨关奖励与关系变量适配器；
- ContentPack ID、版本和本地化；
- 存档和回放迁移；
- 三题材契约场景完整通过。

## 14. 架构验收标准

达到以下条件才能称为通用战斗引擎：

- 核心包不导入 DOM、SVG、剧情或具体关卡文本；
- 同一引擎实例可以加载三种内容包；
- 三类契约场景不修改核心代码即可运行；
- 新增地形、武器、状态和结构主要通过注册表完成；
- 新增普通关卡不需要在 actions、combat、victory 中增加剧情分支；
- UI、AI 和测试共享合法行动与预测；
- 一场战斗可仅凭初始状态和 Action 序列重放；
- 单关只启用必要机制，普通单位仍保持简单；
- 旧关卡通过迁移继续运行；
- 所有引擎变化都有纯规则测试。

## 15. 明确不做

- 不制作完整商业级通用游戏引擎编辑器；
- 不支持任意脚本语言直接执行代码；
- 不切换为单位独立时间轴或即时战斗；
- 不引入连续三维物理、自由角度旋转和任意形状碰撞；离散海拔与四方向朝向属于战棋规则；
- 不让每个题材拥有独立伤害结算器；
- 不为粮秣、氧气、魔力、弹药分别增加全局资源条；
- 不让 UI 参与规则结算；
- 不为每个故事事件在核心里增加 if/else；
- 不保证任意用户数据都能安全运行，关卡 DSL 必须有限且可验证；
- 不以“取并集”为理由让所有关卡同时展示所有机制。

## 16. 最终产品形态

理想结构是：

```text
@empire/core
  纯类型、规则、状态、行动、预测、AI、目标和触发器

@empire/content-fantasy
  《断冠之誓》的单位、能力、地形皮肤和关卡

@empire/content-stars
  《群星熄灭之前》的单位、节点、环境和关卡

@empire/content-history
  《布衣定鼎》的军队、工程、水战和关卡

@empire/campaign
  章节、人物关系、跨关变量和奖励

@empire/web
  SVG 界面、编辑器、演出、音效和输入
```

当前仓库不需要立即拆成多个 npm 包，但源码依赖方向必须从现在开始遵守这个边界。

> 战斗引擎负责让规则成立；内容包负责让规则具有题材身份；关卡负责组合问题；剧情负责解释为什么这些问题值得玩家关心。

## 17. 当前实现基线

本节记录已经进入代码和测试的能力，避免“设计文档写了，但引擎实际上不会”的错觉。这里的状态以仓库测试为准，不以 UI 是否已经制作完整演出为准。

### 17.1 已落地的内核模块

| 能力 | 实现位置 | 当前保证 |
| --- | --- | --- |
| 领域模型 | `domain/` | 可序列化快照与充血实体分离；单位、玩家、结构、目标不变量集中，跨对象修改由 BattleAggregate 协调 |
| 组合根 | `engine.ts`、`session.ts` | Facade、规则依赖注入、实例隔离、事务回滚、撤销、查询缓存和事件订阅 |
| 行动 | `action-system.ts`、`actions.ts` | 开放 Action 类型、策略注册、公共后处理和回合生命周期；扩展异常不留下半完成状态 |
| 内容装配 | `content-pack.ts`、`src/content/` | 原子、幂等、版本化安装；默认题材定义与核心目录物理隔离，跨引用和伤害矩阵完整性前置校验 |
| 武器 | `data/weapons.ts`、`combat.ts` | 核心只保留空注册表与规则；多武器、精确选择、射程、移动后使用、次数、冷却、直接视线、目标标签加成、反击择优 |
| 伤害模型与管线 | `data/damage.ts`、`combat-modifiers.ts` | 开放伤害/护甲 ID、内容注册克制矩阵、Provider 阶段顺序、统一减伤上限与完整解释轨迹 |
| 战场与移动 | `domain/battlefield.ts`、`movement.ts` | BattlefieldCell 聚合基础地形、动态地貌与结构；注册式移动类型、Dijkstra、结构阻挡和覆盖层成本 |
| 状态 | `statuses.ts` | 正式序列化状态、叠加规则、回合生命周期、持续伤害与数值修正 |
| 反应 | `combat.ts`、`actions.ts` | counter、guard、support、conserve；援护真实转移伤害，预测与结算一致 |
| 指挥 | `commanders.ts`、`data/tactics.ts` | 局部光环、单位链接、指挥点、范围战术、阵亡动摇、AI 与玩家入口 |
| 结构 | `structures.ts`、`data/structures.ts` | 耐久、修复、摧毁、阻挡、正式武器攻击、攻城标签加成 |
| 环境覆盖 | `overlays.ts`、`data/overlays.ts` | 不改底图地叠加洪水、火场、真空、低重力和干扰；影响移动、防御、视野、治疗与状态 |
| 任务目标 | `objective-model.ts`、`objective-system.ts`、`victory.ts` | 开放目标处理器、稳定 ID、运行状态、动态显隐；护送、保护、占区、摧毁、计数、交互和组合目标 |
| 场景 DSL | `scenario.ts` | 开放条件/效果处理器、区域、变量、阵营变化、环境覆盖、结构、目标状态和语义信号 |
| AI | `ai.ts` | 与玩家共享合法行动、武器预测、攻城评分、指挥战术；不读取剧情脚本 |
| 校验 | `mapio.ts` | 单位、指挥官、结构、区域、覆盖、状态、目标和触发器引用在加载/编辑阶段报错 |
| UI 适配 | `ui/game.ts`、`ui/hud.ts` | 多武器、援护预测、指挥点/战术、反应姿态只消费查询并提交 Action |

### 17.2 Headless API 边界

界面、AI、服务器模拟和测试都应走相同入口：

```ts
const session = new GameSession(level);

// 查询，不改变状态
const field = session.moveField(unit);
const commands = session.commandsAt(unit, destination);
const preview = session.forecast(attacker, defender, destination, weaponId);

// 提交唯一的状态改变来源
const events = session.dispatch({
  kind: 'command',
  unit: attacker.id,
  path,
  command: { ability: 'attack', weapon: weaponId, target },
});
```

调用方不能自己扣血、移动单位、消耗弹药或判断目标完成。它只能查询、提交 Action、渲染 GameEvent。

### 17.3 关卡数据示例

下面这组数据既可以被解释为誓约军团，也可以被解释为行动队网络或将领部曲；核心没有题材判断：

```ts
{
  units: [
    { key: 'leader', unit: 'soldier', owner: 1, x: 1, y: 2 },
    { unit: 'ballista', owner: 1, x: 2, y: 2, commander: 'formation-a' }
  ],
  commanders: [{
    id: 'formation-a',
    unitKey: 'leader',
    radius: 2,
    aura: { attackMultiplier: 1.1, defenseDelta: 0.05 },
    turnGrants: [{ resource: 'command_points', amount: 1 }],
    tactics: ['rally', 'steady']
  }],
  structures: [
    { id: 'main-barrier', type: 'gate', owner: 2, x: 6, y: 2 }
  ],
  scenario: {
    zones: [{ id: 'hazard-zone', cells: [{ x: 4, y: 2 }, { x: 5, y: 2 }] }],
    overlays: [{ id: 'hazard', type: 'flooded', zone: 'hazard-zone' }],
    triggers: [{
      id: 'barrier-down',
      timing: 'afterAction',
      condition: { type: 'structure', id: 'main-barrier', state: 'destroyed' },
      effects: [{ type: 'emitSignal', signal: 'barrier.destroyed' }]
    }]
  },
  victory: [{
    id: 'operation',
    type: 'all',
    objectives: [
      { type: 'destroy', structures: ['main-barrier'] },
      { type: 'escort', selector: { owner: 1 }, zone: 'hazard-zone', count: 1 }
    ]
  }]
}
```

故事适配器可以把 `barrier.destroyed` 映射成城门倒塌、舱门解压或营寨失守的对白；战斗核心只报告同一个确定事实。

### 17.4 三题材契约场景

`cross-theme-contracts.test.ts` 固化了三条不会随剧本选择而消失的能力链：

1. 奇幻：指挥光环 → 攻城武器 → 城门摧毁 → 目标完成；
2. 星际：真空覆盖 → 指挥节点摧毁 → 场景变量与信号 → 组合目标完成；
3. 历史：洪水改变移动 → 护送抵达 → 据点控制 → 组合胜利。

这些测试没有调用 DOM、对白系统或题材专属结算器，因此未来替换单位名、美术和章节不会改变战斗真值。

### 17.5 兼容策略

- 项目仍处开发期，因此资源模型采用一次性迁移，关卡格式升为 schema 2，不保留 `funds`、`commandPoints`、`momentum`、`usesRemaining` 等双轨旧字段。
- 仓库内置关卡、编辑器、UI、AI 和测试同时迁到统一账户；外部草稿必须先转换后载入，避免长期维护兼容分支。
- 单位的威力、伤害类型和射程展示直接汇总实际武器列表；`UnitDef` 不再保存首武器派生字段，支付条件与武器次数只读取 `WeaponDef.resourceRequirements/resourceCosts` 及武器账户。
- 原有 `routEnemies`、`captureHQ`、`holdAllVillages`、`surviveTurns` 仍可直接使用；复杂关卡再选择组合目标。
- `meta` 仍保留给原型数据，但状态、武器、指挥关系、反应、成长和目标状态已经迁出，不应再把正式机制塞回自由字段。

### 17.6 当前实现边界

当前版本已经具备正式生产普通关卡所需的小型 SRPG 战斗内核：

- `single`、`cross1`、`line`、`ring1` 均由 `CombatPlan` 统一解析单位和结构，预测与提交消费同一份计划；
- 援护防御、援护攻击、反击和武器命中效果已进入固定结算顺序；
- 状态行为、命中效果、任务 AI 顾问和军衔阈值都有可克隆、可注入的策略边界；
- 三级当关军衔会影响战斗，英雄气势已覆盖获取、门槛、消耗、冷却和招牌范围武器。
- 战斗状态正式拥有尸体标记、复活快照和战前部署阶段；增援、召唤、撤退、关系变化及动态空间修改均通过开放 Scenario Effect 进入事件流；
- 击退、拉拽和传送共用权威强制位移服务，武器命中效果与场景脚本不会各自解释碰撞、地形和死亡；
- 自定义能力必须配套 `AbilityAiEvaluator` 才会进入 AI 候选，不再以能力 ID 分支或通用常数猜测价值。

仍明确不在当前核心的是：本地化表、战役存档、跨关英雄成长、存档版本迁移，以及六边格、无缝大地图等另一种拓扑。正式高度、悬崖、朝向、夹击、复杂掩体和战斗内职业树已经在第 22 节落地；跨关职业与永久掌握状态仍由未来战役存档负责播种和回收。

## 18. 第二阶段架构硬化：充血模型与开放规则

第一版解决了“机制够不够用”，第二阶段解决“机制继续增加时，核心会不会重新长成巨型 switch 和散落的状态修改”。这一阶段不改变关卡 JSON、存档快照或 UI 的基本接口，重点是建立长期可维护的变化边界。

### 18.1 状态快照与领域对象分离

`GameState`、`Unit`、`PlayerState` 和 `StructureState` 保持可序列化状态形态。这一点是刻意保留的，因为它们需要：

- JSON 序列化、存档和回放；
- 深拷贝、撤销和失败回滚；
- 在 Worker 或服务端传输；
- 被调试器和测试直接观察。

但可序列化不等于贫血模型，更不等于 ECS。状态与实体有稳定的一一对应关系，运行时通过充血领域对象表达行为：

| 领域对象 | 拥有的规则与不变量 |
| --- | --- |
| `UnitEntity` | 受伤/治疗边界、武器冷却、行动生命周期、阵营变化、反应消耗与当关成长 |
| `PlayerEntity` | 战败状态与目标运行时访问 |
| `StructureEntity` | 结构防御、伤害、摧毁与维修上限 |
| `ObjectiveRuntimeEntity` | 目标激活、完成、失败、取消与显隐生命周期 |
| `BattleAggregate` | 单位移动、死亡移除、占领进度清理，以及单位/玩家/结构的统一查找 |
| `BattleResourceSystem` | 对实体自有账户执行补充、支付、容量限制与跨主体资源交易 |

这是一种“可序列化状态 + 充血运行时实体 + 少量跨实体领域服务”的混合模型。它避免把序列化对象做成难以克隆的复杂类，同时让实体行为和数据保持可追踪关系。

规则是：跨对象一致性由 `BattleAggregate` 负责，单对象不变量由实体负责，查询服务不能绕过它们直接完成同类写操作。

明确禁止演化为 ECS：不建立全局 Component Store，不用实体数字 ID 在多个无关数组之间拼装一个单位，不把 `HealthSystem`、`PositionSystem`、`CooldownSystem` 作为彼此不知道业务语义的处理器。Player、Unit、Structure 仍然是可以直接理解、调试和维护的领域实体；只有真正跨实体或跨资源种类的规则才进入领域服务。

### 18.2 Action 不再是封闭分发器

`ActionKindMap` 是可通过 TypeScript declaration merging 扩展的开放类型映射；`ActionHandlerRegistry` 为每种行动保存一个内聚策略。`actions.ts` 只保留公共事务边界、回合周期和内置策略，不再通过中心 `switch` 识别所有行动。

```ts
declare module '@empire/core' {
  interface ActionKindMap {
    deployDrone: { kind: 'deployDrone'; carrier: number; at: Coord };
  }
}

const actions = new ActionHandlerRegistry()
  .register({
    kind: 'deployDrone',
    execute(context, action) {
      // 验证和状态改变属于这一条策略；失败会由 BattleEngine 回滚。
    },
  });
```

`BattleEngine.dispatchWithReceipt` 把每次行动视为一个事务。任何非法行动、领域不变量异常或扩展处理器异常都会恢复行动前快照，不允许出现“移动已经发生，但扣费或目标结算失败”的半完成状态。`GameSession` 复用成功 receipt 中的快照实现 Memento 撤销。

### 18.3 伤害是可解释的修正管线

`CombatModifierPipeline` 将伤害拆成三个明确阶段：

1. `power`：克制、标签加成、生命强度、状态和指挥光环；
2. `mitigation`：地形与单位减伤，统一在阶段末应用上限；
3. `final`：防御姿态等结算后修正。

每个 `CombatModifierProvider` 只贡献自己负责的修正，并带有稳定 ID、显示标签、来源、运算方式和明细。预测结果保留完整的有序修正轨迹，因此 UI、AI、平衡测试和战斗日志使用同一真值。

```ts
const providers = CombatModifierProviders.clone()
  .register({
    id: 'fantasy.holy-ground',
    priority: 550,
    provide: (context) => isHolyGround(context)
      ? [{
          id: 'terrain.holy-ground',
          label: '圣地祝福',
          source: 'extension',
          stage: 'power',
          operation: 'multiply',
          value: 1.15,
        }]
      : [],
  });
```

管线兼有 Strategy 与 Chain of Responsibility 的特点，但不允许 provider 直接修改单位。它只描述数值贡献，最终计算顺序由管线统一掌握，避免扩展加载顺序偷偷改变减伤语义。

### 18.4 场景 DSL 与目标都是开放代数

场景条件、场景效果和任务目标分别使用：

- `ScenarioConditionKindMap` + `ScenarioConditionHandlerRegistry`；
- `ScenarioEffectKindMap` + `ScenarioEffectHandlerRegistry`；
- `ObjectiveKindMap` + `ObjectiveHandlerRegistry`。

新增原子目标时，一个 Objective handler 同时拥有：

- `outcome`：成功、失败或进行中；
- `describe`：无剧情依赖的默认描述；
- `progress`：HUD 可读进度；
- `role`：主要、可选或关键失败；
- `children` 与 `refresh`：如果它是新的组合目标，声明子树和刷新顺序。

内置 `all`、`any`、`sequence`、`optional` 和 `failOn` 是 Composite 模式；它们可以包裹第三方原子目标。目标定义与 `ObjectiveRuntime` 分开，保证相同关卡定义可以创建多个互不干扰的对局。

### 18.5 BattleEngine 是组合根，不是 God Object

`BattleEngine` 是面向应用层的 Facade 与依赖注入边界。它组合：

- Action handlers；
- Combat modifier pipeline；
- Weapon hit-effect handlers 与 Status behaviors；
- Scenario condition handlers；
- Scenario effect handlers；
- Objective handlers 与 AI objective advisors；
- Rank progression policy。

它只协调查询和命令，不承载单位、战斗、AI 或剧情细节。一个题材包或模组可以创建隔离实例：

```ts
const engine = createBattleEngine({
  actionHandlers,
  combatModifiers,
  scenarioConditions,
  scenarioEffects,
  objectives,
});

const session = new GameSession(level, engine);
```

不同实例可以并行运行不同规则策略集，不需要修改全局默认策略注册表。`BattleEngine` 构造器只接受完整依赖图；局部覆盖必须通过 `createBattleEngine`，该工厂会克隆所有未覆盖的可变注册表，避免“看似隔离、实际共享默认单例”。默认 `GameSession` 同样为每场对局重新组合微内核。

规则策略注册表提供 `clone()` 并可注入 `BattleEngine`。`BattleRuleServices.content` 同时注入一个完整 `ContentCatalog`；默认工厂从已安装内容创建快照，自定义引擎也可直接传入隔离目录。状态创建、空间查询、战斗计划、状态、职业、任务和 AI 的运行链都显式传播这个目录。全局目录只作为应用组合期模板，不再是对局中的隐藏真值。

### 18.6 依赖方向

```text
Web UI / Editor / Server / Tests
              │
              ▼
         GameSession
   事务、撤销、缓存、订阅
              │
              ▼
         BattleEngine
      组合根、Facade、依赖注入
       ┌──────┼────────┐
       ▼      ▼        ▼
   Actions  Queries  Rule Registries
       │               │
       └──────┬────────┘
              ▼
 BattleAggregate + Rich Entities
              │
              ▼
   Serializable GameState / Definitions
```

允许上层依赖下层；禁止领域层反向导入 UI、剧情或编辑器。`packages/battle-engine/src` 的无 DOM 搜索和构建测试会持续约束这一点。

### 18.7 设计模式使用边界

本项目采用模式来隔离真实变化点，不为“看起来像引擎”而堆叠抽象：

| 模式 | 使用位置 | 解决的问题 |
| --- | --- | --- |
| Rich Domain Model | Unit/Player/Structure 实体 | 防止不变量散落和贫血模型 |
| Aggregate Root | BattleAggregate | 跨对象修改保持一致 |
| Strategy + Registry | Action、Scenario、Objective | 新增类型不改中心分发器 |
| Pipeline / Chain | CombatModifier | 修正可组合、可解释且顺序稳定 |
| Composite | 组合目标与组合条件 | 用数据表达复杂任务树 |
| Facade + DI | BattleEngine | 应用 API 稳定、规则集实例隔离 |
| Memento | BattleEngine + GameSession | 引擎失败回滚与会话撤销复用同一快照 |

明确避免：

- 不为每个兵种建立子类；兵种差异仍由数据和组合能力表达；
- 不暴露无业务含义的 `setHp`、`setFunds` 等 setter；
- 不让 handler 直接操作 DOM、播放动画或读取剧情文本；
- 不用 Service Locator 在规则深处寻找依赖；依赖从 `BattleEngine` 组合根传入；
- 不把所有逻辑搬进 `BattleEngine`；它只编排，不实现领域细节；
- 不把未经定义的长期机制塞进 `Unit.meta`；正式机制必须拥有类型、生命周期和测试。

### 18.8 架构契约

`extension-contracts.test.ts` 使用测试内 declaration merging 新增一整条规则链：自定义 Action 改变变量 → 自定义条件触发 → 自定义效果写入结果 → 自定义 Objective 完成 → 对局结束。同一个自定义 Objective 还通过独立 `AiObjectiveAdvisor` 获得任务路径，不会修改内置 AI、reducer、场景解释器或胜负模块。

这条测试连同三题材契约测试共同保证：开放性不是文档承诺，而是可以被持续回归的代码属性。

## 19. 第三阶段：交战计划、任务 AI 与战中成长

### 19.1 `CombatPlan` 是预测与提交的共同真值

攻击不再在 HUD、AI 和结算器里分别推导范围目标。`forecastCombatPlan` 一次性产生：

- 攻击者、武器、出手格、矄准格与受影响格；
- 主目标、防御援护承伤者、范围单位与范围结构；
- 每个目标的伤害修正链、血量前后、死亡与命中效果；
- 反击和最多一次援护攻击。

`executeCombatPlan` 只消费这份不可变计划，不在提交时重新选目标或重算 AI 评分。因此确定性回放、服务端校验和动画都可以消费同一个语义结果。

当前范围攻击默认不伤友军，这是清晰的规则选择，不是算法限制。未来若增加友伤模式，应作为武器或规则的显式字段进入计划，不在结算器里做隐式特例。

### 19.2 命中效果与状态行为开放

`WeaponHitEffectHandlerRegistry` 负责“武器命中后发生什么”，内置添加/移除状态；`StatusBehaviorRegistry` 负责“状态在生命周期节点做什么”。数据型周期伤害与代码型特殊行为可以并存，但所有扣血、死亡和事件仍经过领域对象。

`blockedAbilityTags` 让沉默、封印、电磁干扰等题材概念共用一个机制：状态禁用语义标签，而不是写死某个技能 ID。指令菜单查询与权威提交共用 `canUseAbility`，不会出现 UI 隐藏但直接构造 Action 可绕过的漏洞。

### 19.3 任务 AI 不依赖剧情文本

`AiObjectiveAdvisorRegistry` 把活动目标树转换为四类战术意图：

1. 带权目的地，可限定为特定单位或占领单位；
2. 高优先敌方单位；
3. 高优先任务结构；
4. 需要避险或护卫的友方单位。

`sequence` 只访问第一个未完成阶段；`all`/`any` 访问当前活动分支；可选和关键失败目标有不同权重。顾问结果再与伤害、危险、地形、编队光环和资源一起进入原有效用搜索。新 Objective 若没有 AI 顾问也不会崩溃，只是暂时不产生额外任务权重。

### 19.4 军衔与气势是两条独立成长线

普通单位使用当关三级军衔：

```text
新兵(0) → 老兵(1) → 精英(2)
默认阈值：120 / 320
```

伤害、击杀、治疗、占领和防御援护会增加 `rankProgress`。`RankProgressionPolicy` 决定阈值，可由 `BattleEngine` 注入；军衔对攻防的默认影响作为普通 CombatModifier，题材包可替换或扩展，不会污染基础伤害公式。

气势只属于显式拥有 `resources.momentum` 账户的英雄单位。普通攻击 +5，击杀 +10，存活受击 +3；招牌武器通过 `resourceRequirements` 声明门槛，通过 `resourceCosts` 声明提交时消耗。菜单、AI、预测和权威提交共用 `BattleResourceSystem` 校验，不存在旧的专用气势字段。招牌武器仍受射程、站位、范围、冷却、反击和状态规则约束，不是剧情脚本直接扣血。

### 19.5 三套剧情共用引擎的严格分层

三套剧情不各自继承或复制一套战斗类。它们共用相同的 `BattleEngine`、`GameState`、Action、`CombatPlan`、Objective 和 GameEvent 语义，差异只从两层数据输入：

| 层 | 负责内容 | 不允许的内容 |
| --- | --- | --- |
| 战斗机制层 `packages/battle-engine/src` | 网格、移动、武器、修正、反应、援护、状态、目标、AI、成长和事件 | 人物名、章节名、台词、剧情分支、某题材兵种 ID 特判 |
| 题材内容层 `src/content/<pack>` | Unit/Weapon/Damage/Terrain/Status/Structure 定义、关卡、语义标签、美术键、名称和音效键 | 自己扣血、自己算反击、绕过 Action 的战斗脚本 |
| 剧情/战役层 | 章节、对话、选择、关卡编排、跨关变量与 `scenarioSignal` 的演出映射 | 改写伤害公式或在对话回调中修改战斗真值 |

题材差异用语义标签表达，AI 只读 `support`、`ranged`、`flying`、`building` 等角色标签，不读 `cleric`、`dragon`、`mecha`、`cavalry_general` 等具体内容 ID。因此：

- 奇幻的“牧师”、星际的“纳米医疗机”和历史的“军医营”都可标记为 `support`；
- 龙息、等离子扫射和火攻齐射都可使用同一个 `line` 或 `cross1` 范围模板；
- 烽火台、信号节点和粮仓都是 Structure，通过 `destroy`/`protect` 目标与 Scenario DSL 组合；
- 魔法沉默、电磁干扰和军令阻断都通过能力标签封禁表达。

`LevelData.extra` 对战斗引擎保持不透明，可保存剧情资产键，但不能成为战斗分支条件。战斗中真正需要的条件必须显式进入 RuleSet、Objective、ScenarioCondition 或注册策略，并有对应测试。

## 20. 第四阶段：内容隔离、Damage Model 与战场空间模型

### 20.1 物理目录就是依赖边界

当前源码按职责拆分为：

```text
packages/battle-engine/src/                         题材无关领域、算法、空内容注册表
packages/content-common/src/               跨题材通用定义
packages/content-ancient-empires/src/      当前奇幻包的数值、定义和关卡
packages/game-ui/src/application/                  浏览器存储与应用工作流适配
apps/game/src/ + packages/editor/src/ + packages/game-ui/src/ui/ 展示与交互
```

`packages/battle-engine/src` 不导入 `content`、`application`、`art`、`ui`、`editor` 或 `game`。默认内容的唯一副作用入口是 `content/bootstrap-default.ts`，只由游戏、编辑器和测试组合根加载。直接导入核心不会悄悄注册剑士、巨龙、城堡或地图字符。

`architecture-boundaries.test.ts` 会扫描运行时代码的导入方向，并保证 `core/data` 不再出现内置定义批量注册。这个边界不依赖开发者记忆。

### 20.2 地图、地势、地形、地貌不是一个对象

领域术语固定如下：

| 概念 | 当前模型 | 生命周期 | 示例 |
| --- | --- | --- | --- |
| 地图拓扑 | `GameMap` | 对局基础数据 | 宽高、坐标索引、所属、占领进度 |
| 基础地形 | `TerrainDef` + tile ID | 通常稳定，触发器可替换 | 平原、森林、水域、道路、站点 |
| 动态地貌/环境 | `TerrainOverlayState/Def` | 可添加、移除、过期 | 洪水、火场、真空、低重力、干扰风暴 |
| 战场结构 | `StructureState/Def` | 有耐久、所属和状态 | 城门、舱门、粮仓、Boss 部位 |
| 单位占用 | `Unit` | 随行动持续变化 | 友军可穿越、敌军阻挡、落点占用 |
| 地势高度 | `GameMap.elevation` + `cliffs` | 对局基础空间层，触发器暂不修改 | 海拔、坡差、悬崖、视点高度 |
| 方向掩体 | `LevelDirectionalCover` | 关卡空间层 | 矮墙、壕沟、沙袋、舷侧护板 |

`Battlefield` / `BattlefieldCell` 是这些图层的只读领域门面。移动、直接视线、防御、治疗和视野不再分别到 `GameState` 各处手工拼装，而是查询同一个格子模型：

```text
GameMap tile ───────────┐
Terrain overlays ───────┼─> BattlefieldCell ─> movement / combat / vision / healing
Structure state ────────┘
```

这样做的关键不是少写几行代码，而是保证“火场中的森林上有一扇城门”在所有规则里得到同一解释。基础地形数据、临时地貌和可破坏结构各自高内聚，规则消费者不需要知道它们存在哪个数组。

### 20.3 高低差的正式实现

地图已经支持可计算的离散高低差。丘陵和山地的 `high` 仍只是内容标签；真正规则只读取海拔数组、移动配置和显式悬崖边。

实现遵守以下边界，不往地形 ID 里塞空间特例：

1. `LevelData` 增加可选、可迁移的 elevation 图层，加载后成为与 tiles 等长的数值数组；
2. `BattlefieldCell` 暴露 elevation，格间移动上下文提供 `from/to/delta`；
3. 移动类型配置统一决定最大爬升、上坡额外成本、飞行/悬浮例外与悬崖阻断；
4. 直射视线使用视点高度、目标高度和中间格遮挡顶面判定遮挡；
5. 高打低的命中、射程或伤害收益作为独立 `CombatModifierProvider`，进入现有解释链，而不是写进基础伤害公式；
6. 编辑器负责高度笔刷、等高可视化和合法性检查，美术只渲染结果，不参与规则判断。

这种拆法允许森林同时出现在低谷和山顶，也允许星际低重力、奇幻飞行和历史城墙复用一套空间数据，但通过不同配置解释。默认高地倍率目前为 1.10，并由 `RuleSet` 覆盖，不是基础伤害公式常数。

### 20.4 Damage Model 与伤害算法解耦

`DamageType`、`ArmorClass` 已由封闭 TypeScript 联合类型改为内容 ID；`DamageMatchupRegistry` 保存题材矩阵，`CombatModifierPipeline` 只消费最终倍率。职责分工是：

- 内容包决定有哪些伤害、护甲及其基础关系；
- 武器标签加成处理对空、攻城、刺杀等专属关系；
- Provider 管线组合生命、军衔、状态、指挥、地形、反应、高度、方位与掩体；
- HUD、AI、预测和结算读取同一 Damage Model 和同一修正结果。

这使“魔法克重甲”不再是核心定律。它只是当前奇幻内容包的一组数据。

### 20.5 组合期可变，运行期不可变

内容只在应用启动组合期安装。进入关卡后，定义目录按不可变数据使用：

- 关卡和状态只保存稳定 ID；
- 安装失败不会部分污染目录；
- 同版本重复安装幂等，版本冲突立即失败；
- 运行时效果必须通过 Action、Scenario Effect 或正式领域行为改变 `GameState`，不能热改 UnitDef、TerrainDef 或克制矩阵。

这条约束是确定性预测、回放、AI 模拟和未来存档版本校验能够成立的前提。

## 21. 第五阶段：克制的微内核与通用资源账户

### 21.1 微内核只负责组合，不负责玩法

`SrpgMicrokernel` 的职责被刻意限制为四项：

1. 接收带稳定 ID 与整数版本的 `EnginePlugin`；
2. 校验插件依赖、缺失依赖和依赖环；
3. 按确定性拓扑顺序安装插件；
4. 要求每个能力端口只有一个提供者，再构造 `BattleEngine`。

微内核不知道移动、伤害、地形、剧情或资源余额。`KernelPluginContext` 只在组合期使用；战斗运行时不会把它当作 Service Locator。完成组合后，具体依赖都显式进入 `BattleEngine` 与 `BattleRuleServices`。

```text
ContentPack ──> 定义目录（单位、武器、地形、关卡）
                         │
EnginePlugin ─> SrpgMicrokernel ─> BattleEngine ─> GameSession
   规则能力          只组合/校验          显式依赖       事务/撤销
```

内容包与引擎插件不是同一种扩展：内容包提供数据和题材语义；引擎插件提供会改变规则行为的代码。换剧情通常只换内容包和关卡，不需要复制或继承战斗引擎。

### 21.2 插件按自洽业务能力成块

默认引擎只有四个插件，不把每个 handler、registry 或资源种类拆成插件：

| 插件 | 一起交付的能力 | 保持内聚的理由 |
| --- | --- | --- |
| `engine.tactical-rules` | 能力目录、战术空间、伤害修正、命中效果、状态行为、战中成长 | 都参与行动合法性、一次战术交战及其后果 |
| `engine.mission-rules` | 行动分发、场景条件/效果、任务目标 | 都属于关卡行动循环和任务结算 |
| `engine.resource-economy` | 资源适配器与账户规则 | 统一支付语义，但不拥有实体余额 |
| `engine.ai-planning` | 目标顾问与效用规划 | 只读取前三类能力产生的合法空间和预测 |

一个插件可以提供多个细粒度能力端口。端口是依赖注入和替换边界，插件是发布、版本和生命周期边界；两者不应机械地一一对应。

以下情况才值得新建插件：

- 能力能作为完整模块独立启用、禁用或替换；
- 有清晰的输入、输出与生命周期；
- 能写出不依赖 UI 和故事文本的契约测试；
- 与现有四个模块没有更强的共同变化原因。

一个单独公式、一个事件 handler、一个资源 ID 或一个兵种效果通常不满足这些条件，应进入现有模块的注册表或内容包。

### 21.3 资源独立，但实体不被拆散

通用资源模型使用开放 ID 和独立适配器：

```text
funds ----------> PlayerState.resources
command_points -> PlayerState.resources
momentum -------> Unit.resources
weapon_uses ----> UnitWeaponState.resources
```

新增资源通常只需要：

1. 在正确实体上创建账户 `{ current, capacity }`；
2. 注册一个 `ResourceAdapter`，声明资源名称、主体类型、取整方式与 AI 权重；
3. 在武器、单位招募、地形产出、指挥官回合产出或场景效果里引用资源 ID。

这不是 ECS。适配器没有单独保存余额，也不能把 Unit 拆成位置、生命、资源等组件。删除 `BattleResourceSystem` 后，实体状态仍然完整可读；系统只是让跨题材支付规则不再复制四套实现。

支付具有事务语义：相同资源的多条费用会先聚合并整体检查，再修改账户；行动级异常由 `BattleEngine` 的权威分发边界快照回滚。预测和合法性检查只读账户，只有行动提交阶段允许扣费。

### 21.4 一次性迁移，不保留双轨真值

开发阶段直接删除以下旧字段：

- `PlayerState.funds / commandPoints / commandPointCap`；
- `Unit.momentum` 与关卡的 `momentumCap / momentumInitial`；
- `UnitWeaponState.usesRemaining` 与 `WeaponDef.uses`；
- `UnitDef.cost`、`TacticDef.cost`、`TerrainDef.income`；
- `RuleSet.baseIncome / incomeOverride`。

对应的新真值分别是资源账户、`recruitCosts`、`costs`、`ownerTurnGrants`、`baseResourceGrants` 和 `siteResourceOverrides`。事件流也只保留统一的 `resourceChanged`，其 subject 明确指出 Player、Unit 或 Weapon。UI 可以把默认资源显示成金币、旗帜或气势，但不能据此恢复专用领域字段。

### 21.5 非 ECS 架构护栏

后续评审使用以下判断：

- 一个单位是否仍能从单个 `Unit` 状态读懂？如果必须跨多个组件表拼接，设计不通过。
- 行为是否放在最了解不变量的实体、聚合或自洽领域服务中？如果只是按字段类型建立 System，设计不通过。
- 插件是否代表可以独立替换的业务能力？如果只是包装一个类或函数，留在模块内部。
- 资源系统是否只提供规则，没有偷偷成为第二份状态仓库？如果存在镜像余额，设计不通过。
- `BattleEngine` 是否仍然只做组合与协调？如果开始实现具体玩法，能力应退回所属模块。

`kernel-resources.test.ts` 固定验证默认插件数量和能力归属、缺失依赖、依赖环、重复能力提供者、资源适配器实例隔离，以及重复费用扣减的原子性。`architecture-boundaries.test.ts` 继续验证核心不反向依赖内容与展示层。

## 22. 第六阶段：三维空间、方位战术与职业树

### 22.1 正式高低差与悬崖

高度不再由 `hill`、`mountain` 或 `high` 标签猜测。`LevelData.elevation` 是与地形数组等长的整数图层，加载后进入 `GameMap.elevation`，并由 `BattlefieldCell.elevation` 统一暴露。

每个 `MovementProfileDef` 明确声明：

- 单步最大爬升与最大安全下落；
- 每级上坡追加的移动消耗；
- 是否能跨越显式悬崖边。

`LevelData.cliffs` 保存两个正交相邻格之间的无向阻断边。步行、骑乘、重装和水军会被悬崖阻断，飞行等声明 `ignoresCliffs` 的移动方式可以越过。即使没有显式悬崖，超过移动类型爬升/下落能力的高度差也会成为不可通过的断面。

直射视线使用攻击者视点高度、目标视点高度和 Bresenham 中间格构成的射线。中间地形或结构的 `obstructionHeight` 与该格海拔合成遮挡顶面；射线高于遮挡顶面时，高处单位可以看过低处森林或矮墙。高地伤害作为 `elevation.high-ground` 修正进入解释管线，倍率和触发高度由 `RuleSet` 配置。

### 22.2 朝向、背刺与夹击

`Unit.facing` 是正式序列化状态，取北、东、南、西四个方向。单位沿路径移动后朝向最后一步方向，攻击前朝向目标；玩家也可以提交 `face` Action 在尚未行动时调整朝向。

近战伤害根据攻击者相对防守者的位置产生：

- 正面：无额外倍率；
- 侧面：`sideAttackMultiplier`；
- 背面：`backAttackMultiplier`；
- 对向夹击：攻击者相反的邻格存在友军时，再叠加 `flankAttackMultiplier`。

这些判断只依赖坐标、阵营、武器语义和朝向，不读取角色或兵种 ID。预测、AI 选位、反击与正式提交仍消费同一条修正管线。

### 22.3 半掩体、全掩体和方向掩体

掩体有三个来源：

1. `TerrainDef.cover`：森林、山地等格子的全向基础掩体；
2. `StructureDef.cover`：城门、隔断和题材化掩体结构；
3. `LevelDirectionalCover`：目标格北、东、南、西某一边的半掩体或全掩体。

远程直射命中目标时，系统按来袭方向选择最强掩体。半掩体和全掩体分别增加 `halfCoverDefense` / `fullCoverDefense`，并显示为 `cover.half` / `cover.full` 修正。近战不吃掩体；曲射或带 `ignores-cover` 标签的武器绕过掩体。攻击者达到高地阈值时会把全掩体压低为半掩体、半掩体压低为无掩体，而不是完全无视空间优势。

### 22.4 职业树与自由转职

`CareerDef` 是内容包定义，不是兵种子类。它声明稳定职业 ID、显示名、对应战斗单位模板、层级、任一前置职业、最低军衔、当前职业最低熟练度、转职费用、掌握阈值和掌握后继承能力。

内容包安装期会检查职业引用、数值形状和职业图中的环。运行时 `UnitCareerState` 保存：

- 当前职业；
- 已解锁职业；
- 各职业独立熟练度；
- 掌握后获得的跨职业能力由 `learnedAbilities` 保存。

伤害、击杀、治疗、占领和援护产生军衔经验时，也会增加当前职业熟练度。第一次进入新分支需要满足路径、军衔、熟练度和资源条件；已经解锁的职业可以从任意当前职业自由切换。`changeCareer` Action 保留单位 ID、阵营、状态、资源、指挥关系和战斗历史，按生命比例换算新模板生命，复用共有武器状态并初始化新增武器。支付、模板转换和事件发射处于同一事务，任何错误都会由 `BattleEngine` 回滚。

当前 `ancient-empires` 内容包提供民兵向游侠、影刃、侍祭，再进阶骑士、战斗法师和攻城技师的示例树，以及怪物侧的巨魔战士到龙骑领主分支。其他剧情只需提供自己的职业数据和名称。

### 22.5 工具链与验收

- 游戏棋盘显示海拔、悬崖、方向掩体和单位朝向；HUD 显示空间修正、职业熟练度和可用转职。
- 编辑器提供海拔笔刷、相邻格悬崖边工具、四方向半/全掩体工具，并完整导入导出这些图层。
- AI 的移动场和威胁场天然遵守高度与悬崖；攻击预测会评估背刺、夹击、掩体和高地，站位评分也读取海拔与来袭方向掩体。
- `spatial-rules.test.ts` 与 `careers.test.ts` 固定验证移动、视线、高地、方位、夹击、掩体、职业解锁、自由切换和失败回滚。

## 23. 第七阶段：扩展契约闭环与事务边界

### 23.1 扩展点必须贯穿完整调用链

一个接口只有在玩家菜单、AI 规划、权威校验和正式结算读取同一实例时，才算真实扩展点。本阶段修正了此前扩展面不均衡的问题：伤害、状态、目标已经可以注入，但能力目录、移动与视野仍有固定实现被门面或 UI 直接调用。

现在 `engine.tactical-rules` 一次提供两项新的自洽能力：

| 端口 | 包含的职责 | 不包含的职责 |
| --- | --- | --- |
| `abilities` | 能力定义、目标枚举、可用性与执行行为的实例目录 | 单位是否拥有能力，仍由 `UnitDef` 和职业掌握状态决定 |
| `space` / `TacticalSpace` | 移动场、权威路径、威胁范围、攻击/治疗目标与战争迷雾解释 | 地图数据存储、伤害公式、UI 选中状态 |

`TacticalSpace` 把共同变化的空间解释放在一起，而不是拆成 `PathPlugin`、`VisionPlugin`、`TargetPlugin` 等细碎模块。例如规则包实现控制区、潜行或传送时，可以替换一个空间策略，并保证：

```text
HUD 指令目标 ─┐
玩家提交校验 ─┼─> 同一个 TacticalSpace 实例
AI 行动枚举 ──┤
战争迷雾显示 ─┘
```

基础 `grid.ts`、`Battlefield` 和 `GameMap` 没有因此接口化。当前引擎明确使用矩形四方向拓扑；如果未来真正实现六边格，应作为一次有测试和迁移方案的拓扑变更，而不是现在预埋一套无人使用的抽象层。

### 23.2 能力是代码规则，内容包是纯数据

`AbilityDef` 含有可执行函数，因此不进入可序列化 `ContentPack`。规则插件依赖 `engine.tactical-rules` 后，通过 `context.require('abilities')` 向该引擎实例的克隆目录注册能力。这样同时满足：

- 新能力无需修改核心分发器；
- 两个 `BattleEngine` 可以安装不同能力而互不污染；
- 单位、职业和关卡仍只保存稳定 Ability ID；
- 菜单、AI 和行动提交不会各自维护能力表。

全局 `Abilities` 只作为默认定义模板存在；默认微内核构造时会克隆它。运行期模组不能再向全局表热注册并期望已有引擎自动变化，必须在组合期通过插件形成明确实例。

### 23.3 领域事件也是开放代数

Action、Objective、Scenario Condition/Effect 和 Weapon Hit Effect 已使用可声明合并的 KindMap，但原 `GameEvent` 是封闭联合类型，导致扩展动作只能滥用 `scenarioSignal` 表达自己的结果。

现在事件由开放映射派生：

```ts
interface GameEventKindMap {
  move: { type: 'move'; unit: number; path: Coord[] };
  // 插件可通过 declaration merging 添加新条目
}

type GameEvent = GameEventKindMap[keyof GameEventKindMap];
```

扩展仍需定义稳定、可序列化、有领域含义的事件；这里没有引入字符串 Event Bus，也没有允许任意 payload。UI 不理解新事件时可以忽略，回放器或题材适配器则能按 `type` 做穷尽处理。

### 23.4 原子性覆盖所有行动

`BattleEngine.dispatchWithReceipt` 是可变战斗状态的权威事务边界。`GameSession` 只负责保存成功行动返回的 `before` 快照，用于玩家撤销；服务器、测试模拟器或无界面调用者直接使用 `BattleEngine.dispatch` 时，也获得相同的失败回滚保证。

现在每个 Action 都先建立回滚快照：

- 任意 Action 提交：引擎只建立一次聚合快照；
- 普通行动成功：Session 复用该快照进入玩家撤销栈；
- 结束回合成功：Session 清空撤销栈，仍不能跨回合撤销；
- 任意行动失败：引擎原位恢复聚合，不产生可观察的半成品状态。

“是否允许用户撤销”和“失败是否原子回滚”由此成为两条独立语义。扩展规则可以抛出明确错误，而不需要自己实现补偿事务。

### 23.5 本轮刻意没有增加的抽象

为了保持小型引擎的可维护性，本轮没有引入：

- 通用中间件链或任意生命周期 Hook；现有 Action、Scenario Trigger、Status Behavior 已覆盖明确时机，真实出现无法表达的新生命周期再增加；
- ECS、组件仓库或按字段分类的 System；单位与其生命、位置、职业、资源仍属于同一实体；
- 六边格/自由角度/连续物理的假接口；当前矩形离散战棋语义保持明确；
- 为每个查询建立单独插件；端口按共同变化原因成组，插件仍保持四个自洽模块。

`engine-extension-seams.test.ts` 通过纵向契约固定本阶段成果：隔离能力能产生自定义强类型事件，替换空间策略会同时影响菜单与权威提交，失败的结束回合扩展会完整回滚，默认会话之间的策略图互不污染。

## 24. 第八阶段：依赖图隔离与编辑器领域化

### 24.1 默认引擎是工厂，不是共享实例

默认引擎不再以进程级可变单例提供。每个 `GameSession` 调用 `createDefaultBattleEngine()`，获得独立的 Action、Ability、Status、Scenario、Objective、Resource、Combat Modifier 和 AI Advisor 注册表。插件对某场对局进行 `register` 或 `replace`，不会影响已经运行或之后创建的其他对局。

### 24.2 依赖排序只有一个实现

`SrpgMicrokernel` 和 `ContentPackInstaller` 共同使用 `orderByDependencies`。规划器统一保证：

- 依赖先于消费者；
- 无关节点保持声明顺序；
- 已安装的外部依赖可以显式满足；
- 缺失依赖报告消费者和依赖 ID；
- 依赖环报告完整路径，而不是只报其中一个节点。

微内核插件同时声明 `provides` 与 `requiresCapabilities`。依赖按能力契约解析到真实提供者，而不是绑定默认插件 ID；因此完整提供战术端口的第三方模块可以替换 `engine.tactical-rules`，AI 模块仍能被正确排序。显式 `requires` 仅保留给没有 capability 表达的发布/生命周期关系。

组合结果暴露只读 `KernelCapabilities`；插件安装时只拿到绑定自身 provider ID 的 `KernelPluginContext`。内核会拒绝缺失能力、竞争提供者、声明却未交付的能力和依赖环，符合接口隔离原则。

### 24.3 编辑器使用充血文档模型

`EditorDocument` 负责地图尺寸、地形与归属一致性、洪水填充、单位唯一占格、悬崖无向边、方向掩体、裁剪以及 LevelData 序列化。`EditorApp` 只处理 DOM、工具状态和交互编排，不再直接复制这些领域规则。

战斗棋盘与编辑器的海拔、悬崖、掩体标记以及渲染缓存键也统一到 `battlefield-layer` 适配器，避免两个界面随机制演进产生表现分叉。

## 25. 第九阶段：高级战斗原语与内容真隔离

### 25.1 单位生命周期属于战场聚合

`BattleAggregate.damageUnit` 在死亡时原子地保存完整战斗内单位快照并创建 `corpse` 标记，然后移除活动单位。场景层只通过正式效果操作生命周期：

- `spawnUnits`：增援或召唤，复用与初始部署/招募相同的单位构造规则；
- `withdrawUnits`：撤退或移除，可选择留下尸体；
- `reviveMarkers` / `removeMarkers`：复活或清理标记；
- `setPlayerTeam`：动态同盟、停火和倒戈。

尸体不是 UI 装饰，也不是剧情变量；它是可克隆的 `GameState.markers` 领域状态。复活恢复原单位 ID、职业、武器账户和战斗内身份，但按效果显式决定阵营、生命比例、状态清理与当回合行动权。

### 25.2 强制位移只有一个权威解释器

`forced-movement.ts` 统一执行 `push`、`pull` 和 `teleport`。直线位移忽略移动点，但仍检查边界、单位占格、结构、不可通行地形、爬升/下落与悬崖；提前受阻可以触发确定性碰撞伤害、死亡和尸体。武器的 `forcedMove` 命中效果与场景 `forceMove`/`teleportUnits` 共用该服务。

这种设计没有引入连续物理系统。强制位移仍是四方向离散战棋规则，接口与当前拓扑匹配。

### 25.3 动态空间修改保持分层

场景 DSL 现在能独立修改：

- 基础地形：`replaceTerrain`；
- 海拔：`setElevation` / `addElevation`；
- 边约束：`setCliffs`；
- 方向掩体：`setDirectionalCover`；
- 环境覆盖：`addOverlay` / `removeOverlay`。

所有变化写回既有 `GameMap`/`ScenarioState` 图层并发出强类型事件，因此移动场、视线、伤害修正、AI 与渲染下一次查询自然看到新真值，不存在剧情脚本维护第二张地图。

### 25.4 战前部署是战斗阶段，不是战役名单

`LevelData.deployment` 把玩家、场景区域和可部署单位 key 关联起来。对局以 `deployment` phase 开始，只接受：

- `deployUnit`：在授权区域内换位，验证占格和移动类型可站立性；
- `finishDeployment`：按玩家顺序确认，全部完成后发出 `battleStarted` 并进入第 1 回合。

选择哪些英雄或佣兵出战仍属于战役适配层；战斗核心只接收已经确定的单位集合并负责合法布阵。两者边界明确。

### 25.5 能力扩展同时交付 AI 估值

`AbilityAiEvaluatorRegistry` 以 Ability ID 关联估值策略。AI 对每个由正式菜单枚举出的“落点 × 能力 × 目标”建立统一上下文，再把状态、位置分、任务意图、战斗预测、资源和内容目录交给策略。内置攻击、治疗、占领和待机也走同一个注册表。

未知能力没有估值器时不会被 AI 猜测执行。能力插件需要同时注册合法性/执行定义与估值器，击退、召唤、复活、再动或地形制造可以用各自真实语义评分，而无需修改 `ai.ts` 的分支。

### 25.6 `ContentCatalog` 成为每引擎依赖

`BattleRuleServices` 与微内核能力图都正式提供 `content`。默认引擎克隆全局组合期模板；自定义引擎可传入独立目录。目录沿下列主链显式传播：

```text
BattleEngine
  ├─ createState / mapFromLevel
  ├─ TacticalSpace / Battlefield / Vision
  ├─ Action / Ability / CombatPlan / Status / Career
  ├─ Scenario / Objective / Victory
  └─ AI planning / AbilityAiEvaluator
```

同一进程中的两个引擎可以对相同 `soldier_sword` ID 使用不同威力而得到不同预测，且不会修改全局定义。`UnitDef.attack/damageType/minRange/maxRange/attackAfterMove` 已一次性删除；威胁、招募估值和界面汇总均读取实际武器组合，双轨真值不再存在。

### 25.7 仍在战斗核心之外

`GameSave`、跨关等级、永久伤亡、人物关系、路线选择和章节变量仍不属于 `packages/battle-engine/src`。第 27 节之后新增的 `packages/campaign-engine/src` 已以单向适配层实现这些边界中的通用部分；战斗核心没有为此导入战役包。Action 回放文件和战斗版本迁移仍是独立产品能力，不能与战役存档混成同一种格式。

## 26. 第十阶段：引擎级硬化与测量驱动优化

### 26.1 性能优化先建立基准

仓库新增 `npm run bench:core`，固定测量四条纯战斗热路径：完整战场格投影、单单位移动场、确定性战斗预测和 AI 单步规划。基准使用 24×24 地图、多单位、结构、环境覆盖、方向掩体和悬崖，避免只测空地图得到虚假的快结果。

本阶段测量到的改造前后均值如下；微秒级项目会受运行环境波动影响，因此结论只使用数量级明显的变化：

| 热路径 | 改造前均值 | 改造后均值 | 结论 |
| --- | ---: | ---: | --- |
| 全战场格投影 | 0.104 ms | 0.066 ms | 约 1.6 倍吞吐 |
| 单位移动场 | 0.243 ms | 0.036 ms | 约 6.7 倍吞吐 |
| 确定性战斗预测 | 0.0021 ms | 0.0024 ms | 保持同一微秒级，无有意义回退 |
| AI 单步规划 | 38.4 ms | 7.5 ms | 约 5.1 倍吞吐 |

没有因为“可能更快”引入持久缓存、并行线程或复杂寻路器。当前地图和移动预算仍适合简单桶队列；优化集中在测量证明反复发生的空间层查询与 AI 枚举。

### 26.2 `Battlefield` 是短生命周期的自适应空间投影

`GameState` 继续保存平坦数组和可序列化集合，不加入需要手工失效的索引。一次规则查询创建短生命周期 `Battlefield`，它按访问频率处理结构、环境覆盖、方向掩体和悬崖：

- 少量访问直接扫描，避免一次战斗预测为整张地图建立索引；
- 同一查询内第三次访问某空间层时建立格索引或边集合；
- `BattlefieldCell` 按格复用，移动、视线和 AI 共享同一投影；
- 查询结束后索引自然释放，场景动态修改不需要维护缓存失效协议。

这体现了数据结构优化而非算法炫技：序列化状态保持简单，密集查询获得近似 O(1) 访问，单点查询仍保留低常数成本。

### 26.3 修正管线与能力枚举消除重复工作

`CombatModifierProviderRegistry` 只在注册或替换策略后重新排序；稳定运行期复用冻结顺序。`CombatModifierPipeline` 在一次遍历中分别累计 power、mitigation 与 final 变换，并让同一次伤害计算的空间型 Provider 共享 `Battlefield`。

能力菜单不再先按默认武器计算一次攻击、丢弃结果、再逐武器重复计算。现在它按单位真实能力目录枚举，非攻击能力只生成一次目标集，多武器始终读取引擎实例的 `ContentCatalog`，无合法目标的命令不会进入 UI 或 AI 候选。AI 的所有站位共享一个战场投影，避免每个候选格重新扫描空间图层。

### 26.4 `BattleEngine` 是失败原子的权威门面

事务不再依赖 `GameSession` 是否存在。`BattleEngine.dispatchWithReceipt` 在执行前建立唯一快照：成功返回事件与 `before`，失败原位恢复聚合并重新抛出原错误。`dispatch` 使用同一实现，`GameSession` 复用 receipt 构建撤销栈，不再为回滚和撤销各克隆一次。

`BattleEngine` 构造时还会检查内容与策略图的跨边界契约：

- 单位和职业引用的 Ability 必须已注册；
- 武器命中效果必须存在对应 Handler；
- 创建关卡前执行结构/引用校验；
- 关卡使用的 Objective、Scenario Condition 和 Scenario Effect 必须由当前引擎提供。

错误在组合或开局阶段集中报告，而不是战斗到一半才因缺失策略崩溃。

### 26.5 核心依赖图保持无环

`ObjectiveOutcome` 从目标规则服务下沉为领域值类型，消除了领域实体反向依赖服务层形成的九模块环。`architecture-boundaries.test.ts` 现在会解析生产核心的本地 import 图并拒绝任何循环，同时继续检查核心不依赖内容、应用、UI、编辑器或美术适配器。

默认微内核插件通过 capability manifest 声明提供与依赖关系。能力需求会解析到实际提供者，第三方可替换整块战术模块而不必冒充默认插件 ID；内核会校验缺失能力、重复提供、声明未实现和依赖环。

### 26.6 全局便捷模式与实例模式明确分离

`DefaultBattleRuleServices` 只服务 `applyAction` 等低层便捷 API，并显式绑定实时全局目录，不再在模块加载时捕获可能为空的内容快照。`createBattleEngine` 与默认微内核仍为每个引擎克隆完整内容和策略图。

因此两条使用路径语义清楚：

- 小工具或兼容调用可以使用实时全局默认；
- 正式对局、服务器、模组沙箱和测试使用实例化 `BattleEngine`，获得内容隔离、能力校验和事务保证。

## 27. 第十一阶段：三题材共用的战役级战斗原语

这一阶段补齐的不是三个故事的专用脚本，而是它们共同需要、同时也能服务未来题材的事实模型。所有能力仍以 `GameState + Action/Condition/Effect + GameEvent` 表达。

### 27.1 周期环境与可重复触发器

`ScenarioCondition` 新增回合周期、当前玩家、单位数量、单位生命比例、战场标记数量、语义事件计数和复合目标状态。`ScenarioTrigger.repeat` 用起止回合、间隔和最大次数描述风暴、潮汐、毒雾、炮击窗口或季节性战场变化。

运行时以“回合 + 当前玩家 + timing”构成触发时刻，同一时刻最多触发一次；重复次数和最后时刻保存在 `ScenarioState.triggerRuntime`，因此克隆、撤销、AI 模拟和未来回放不会依赖闭包计时器。

### 27.2 区域交战政策

`ZoneEngagementRule` 把桥上停火、医院保护区、谈判区域或登舰走廊统一解释为区域内的敌对行动政策。攻击菜单与 `forecastCombatPlan` 都调用同一个权威检查，既不能从 UI 选择非法目标，也不能通过直接调用战斗计划绕开限制。规则可以随场景效果动态添加和移除。

### 27.3 士气、溃退与投降

单位拥有正式士气账户和韧性。伤害按最大生命比例造成士气冲击，单位战败会影响范围内盟军，指挥官战败会影响其指挥链。士气归零不是把单位删除得无影无踪，而是生成带完整单位快照的 `routed` 标记；投降生成 `surrendered` 标记。场景可以恢复这些标记，从而承载撤退重整、劝降、俘虏交换和战后名单回收。

士气由 `RuleSet.moraleEnabled` 显式开启。关闭时既有轻量关卡不会因为新增字段改变胜负节奏。

### 27.4 阵形与 AI 战术指令

`FormationDef` 是内容数据，单位只保存当前阵形 ID。阵形只有在满足相邻盟军约束时才生效，并通过既有移动预算与战斗修正管线贡献移动、攻击和防御变化，没有第二套伤害公式。

`UnitDirectiveState` 保存 `assault / guard / patrol / retreat` 意图、区域、巡逻点和游标。AI 把指令作为位置效用的一部分：守卫不无故追出防区，巡逻兵沿序列推进，撤退单位显著压低主动攻击收益并向撤离区移动。剧情只负责设置数据，不控制 AI 分支。

### 27.5 运输单位

载具容量和允许/禁止标签定义在 `UnitDef.transport`。`embark` 与 `disembark` 是正式 Action，乘员离板后仍以完整 `Unit` 快照保存在 `GameState.embarkedUnits`，保留 ID、职业、资源、状态和成长。

载具被击毁时，乘员清理由 `BattleAggregate.damageUnit` 维护，而不是依赖某个攻击表现层补丁；即使测试或服务器直接调用聚合，状态也不会留下孤儿乘员。正常溃退、投降和脚本撤离则把乘员转换为相应非致死标记。

### 27.6 复合与移动战场目标

`CompositeState` 把多个可独立受击的结构部件组合为城塞、巨舰、巨兽或大型机关，并定义瘫痪阈值。`neutralizeComposite` Objective、`composite` Condition 和 `moveComposite` Effect 共用同一个聚合状态解释。

复合移动先验证所有部件的目标位置、边界、单位与外部结构碰撞，再一次性提交并逐部件发出事件；失败不会留下移动了一半的目标。

### 27.7 补给、倒戈与多阶段战斗采用正交组合

没有新增“古代粮道”“星际能量”“魔法泉水”等题材专属系统。现有通用资源账户配合周期触发器、单位/区域/结构条件和 `changeUnitResource`，可以表达补给站、补给线、弹药恢复和能源潮汐。`setPlayerTeam`、`changeUnitOwner`、士气、投降和恢复标记组合后，可以表达劝降、化敌为友、临时同盟和再次参战。

这遵守模块分寸：只有拥有独立不变量和生命周期的概念才成为新模块；可由既有正交原语稳定组合的题材语义留在内容层。

## 28. 战斗与战役的单向边界

通用战役框架位于 `packages/campaign-engine/src`，完整设计见 [战役引擎架构](./campaign-engine-architecture.md)。依赖方向固定为：

```text
content/campaigns ──> campaign ──> core types
                             └──> CampaignBattleBridge ──> LevelData / GameState / GameEvent

core ──X──> campaign
```

`CampaignBattleBridge` 生成一次性的 `BattleRequest`，战斗结束后把 `GameState + GameEvent[]` 投影为 `BattleResult`。战役只能播种和回收战斗数据，不能在战斗过程中持有旁路真值。架构测试明确禁止 `packages/battle-engine/src` 导入 `campaign`。
