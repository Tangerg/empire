# Empire SRPG workspace

Empire 是一个 TypeScript monorepo，用于开发剧情战役型战略角色扮演游戏（SRPG）。仓库把单场战斗、跨关状态、内容定义、通用界面、编辑器和可分发体验关卡拆成独立工作区。

## 开始开发

安装依赖并启动主应用：

```bash
npm install
npm run dev
```

常用入口：

```bash
npm run demo
npm run experience
npm run dev --workspace @empire/editor-app
```

这些命令分别启动战斗引擎能力 Demo、单关体验应用和关卡编辑器。开发入口使用 Vite 本地服务器，不能直接通过 `file://` 打开源码 HTML。

## 运行检查

执行完整本地检查：

```bash
npm run typecheck
npm test
npm run build
```

构建四个应用：

```bash
npm run build:apps
```

构建可直接复制给测试者的单文件体验包：

```bash
npm run build:experience
```

产物位于 `dist/experience-lab/index.html`。该文件内联运行时代码、样式和引用素材，可以通过 `file://` 打开。

运行战斗热路径基准：

```bash
npm run bench:core
```

## 工作区

```text
apps/
  game/                    主游戏组合根
  editor/                  关卡编辑器组合根
  engine-demo/             引擎能力演示
  experience-lab/          单关体验发布入口

packages/
  battle-engine/           Headless SRPG 战斗内核
  test-content/            测试组合根：为各测试套件构建隔离内容目录
  campaign-engine/         跨关状态机、战斗桥和存档
  content-common/          跨题材内容定义
  content-ancient-empires/ 通用战术内容与演示关卡
  game-ui/                 棋盘、HUD、控制器和表现端口
  editor/                  编辑器文档与界面
  experience-lab/          体验关卡数据
  story-candidate-*/       相互隔离的题材内容包

tooling/test/              测试组合根
docs/                      技术设计、参考和操作文档
```

依赖方向固定为：

```text
apps / content / experience
               ↓
game-ui / editor / campaign-engine
               ↓
battle-engine
```

`battle-engine` 不依赖 DOM、界面、编辑器、战役或题材包。应用在组合根显式安装内容和注册表现。

## 技术能力

当前单场战斗内核覆盖：

- 加权移动、地形、海拔、悬崖、视线和战争迷雾
- 确定性预测、反击、援护、多武器、范围模板和攻城
- 朝向、侧击、背刺、夹击、半掩体和全掩体
- 状态、士气、溃退、投降、阵形、运输和职业树
- 占领、经济、生产、指挥官、战术和通用资源账户
- 组合目标、动态场景、增援、召唤、强制位移和战前部署
- 可插拔行动序：阵营回合（远古帝国 / AW）与个体行动序（皇家骑士团2 / FFT）
- 种子随机源、状态摘要与战斗回放，确定性可验证
- 共用正式合法性与战斗预测的人工智能（AI）
- Headless 运行、事务回滚、实例隔离和开放扩展注册表

编辑器已经覆盖基础地图、地形、单位、归属、海拔、悬崖、方向掩体、基础规则和 JSON 工作流。高级目标、场景触发器、结构、部署和题材场景图层仍通过代码或 JSON 配置。

## 技术文档

从[技术文档入口](./docs/README.md)按任务选择页面。核心文档包括：

- [引擎能力目录](./docs/engine-capabilities.md)
- [战斗系统设计](./docs/combat-system-design.md)
- [战斗引擎架构](./docs/combat-engine-architecture.md)
- [关卡数据格式](./docs/level-format.md)
- [关卡编辑器](./docs/editor-guide.md)
- [战场表现系统](./docs/presentation-system.md)
- [战役引擎架构](./docs/campaign-engine-architecture.md)
- [Monorepo 架构](./docs/monorepo-architecture.md)
- [质量与测试](./docs/quality-and-testing.md)

技术文档不包含人物、章节和小说内容。剧本资料保留在独立目录，不作为引擎契约。

## 扩展规则

新增能力时遵守：

1. 跨题材战斗合法性和结算进入 `battle-engine`
2. 兵种、武器、地形和数值进入内容包
3. 跨关名单、选择和关系进入 `campaign-engine` 或上层适配器
4. 素材、动画和场景进入 `game-ui` 表现端口或题材表现包
5. 组合根只安装依赖和挂载应用，不保存可复用领域逻辑
6. 不采用实体组件系统（ECS），单位、玩家和结构保持明确实体归属

每项扩展必须同时考虑类型、执行、预测、AI、事件、实例隔离和测试，不能只在界面或内容数据中增加一个名称。

## 依赖注入约定

引擎内没有任何函数可以隐式读取环境内容目录：

0. 内容目录按组合创建并注入；引擎内不存在环境目录，两个题材可复用同一套地形字符
1. 依赖参数一律必填，从不给全局默认值——漏传是编译错误
2. 单一 `content: ContentCatalog` 尾置；两个以上服务改为前置端口对象
3. 端口由消费方声明，`BattleRuleServices` 结构化满足全部端口
4. 表现层从会话拿规则集，不 import `battle-engine/data/*`
5. 只有应用组合根建 catalog 和 engine，库不做环境安装

## 行为归属约定

一条规则只能有一个归属，否则加入第二种玩法时它会分叉：

1. 「能不能行动」只由 `mayAct()` 回答，阶段归战斗、资格归行动序策略
2. 「能不能下这条指令」只由 `ActionExecutionContext.commandableUnit()` 回答
3. `state.phase` 和 `state.turn` 只由 `BattleLifecycle` 改写
4. 拒绝指令由发现问题的协作者抛 `IllegalActionError`，处理器不得捕获后改标签
5. 领域缺陷用 `DomainInvariantError`，永远不呈现为「这步不允许」

以上都由 `architecture-boundaries.test.ts` 中的架构适应度测试守卫。
