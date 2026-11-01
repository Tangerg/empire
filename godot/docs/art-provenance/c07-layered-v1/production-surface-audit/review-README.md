# C07 最小生产接入复核（2026-11-01）

只读代码审查；未运行 GUI、真实游戏、性能实验或修改生产。下述新增字段/内部类型为建议，不冒充现有接口。不要把四个外部 wrapper 原样移入生产。

## 现有可复用入口和唯一所有者

- `godot/Presentation/BoardSceneRendering.cs:Load` 已把 `RegisteredGroundDocument` 的 plane 与 ground factory 一起解析，并拒绝同时 assembled/painted。保留这一入口，不在 BattleScreen/CityPreview 加环境变量式生产开关。
- `AssembledGroundDocument.cs:Factory` 加载 catalog、原图与完整 object texture；`AssembledGroundView.cs:Create/Render` 校验注册、规范 map、site、pointer、contact 并向 board 返回 `TerrainDepthEntry`。这是最小扩展点，不另造一套 C07 地面/拾取 owner。
- `BoardGroundView.cs` 的 `GroundSite` 是每个站点唯一绑定，不是每个绘制片绑定。`BoardView.cs:RenderState` 根据 Sites 建旗并抑制重复 MapStructure；每个新表面不能自行追加同一 site。
- `BoardView.cs:ConfigureGround` 已负责 detached factory、authored subtree 转移/失败恢复；`OrderStanding`、`PickScene` 负责真实深度及拾取。新绘制片仅提供 contact/visual/selection，不自行固定 Z、不绕过该排序。
- `StandingVisual.cs` 的 Covers 是完整材质遮挡，ContainsPointer 是选择轮廓，IntersectsPointer 是允许保守的 cue 避让。不得把缩窄 pointer 当作材质透明；alpha sampler 与 GPU Linear/Mipmap 边缘不是完全等价。

## 最小数据与运行时收敛

1. 保留现有 whole-sprite / DepthStrips 路径、其完整画布分区与透明独立资源校验。为已选复杂资源增加一种显式、可选的 authored-surface 描述（建议挂在 SceneObjectAsset 的独立 geometry path；与 DepthStrips 互斥），而非改变 DepthStrips 的语义去偷偷丢弃像素。
2. 描述只拥有资源局部 physical-pixel polygon、UV、pointer、material coverage 模式和 support。实例继续由 SceneObjectPlacement 的 contact/scale/site 决定位置与规范身份。静态夹具中的世界 source 坐标、硬编码 `/16 * TileSize`、前缀 NW、所有绝对区间必须在迁移时按登记坐标显式转换，不得作为通用生产公式保留。
3. 只编译一份不可变的已裁剪三角/UV/支撑片结果：Polygon2D draw、StandingVisual alpha 映射、bounds、depth entries 都消费该结果。三角化/排除只做一次；不让测试、draw、pick 各自重建不同事实。按 member/slice 保留稳定身份以便检查重复 primitive，不能仅按 entry 计重。
4. 将 SectionedSpriteGround 的未变 RGBA 区域表示为 identity UV surface；替换区先明确扣除，再加入已审核的新表面。排除必须完整消费；当前不支持内部孔就明确拒绝反向内环，不能把洞单独实填。
5. TexturedPropGround 的 opaque-material 合同和 AlphaMappedSurfaceGround 的 native-alpha 合同仍分开声明/校验，但共享三角编译和投影。不能为了共用代码让 opaque 路径自动接受透明洞，也不能用全 polygon coverage 遮盖 native-alpha 的透明处。限定支持的 tint alpha，或让 coverage 明确遵守它，避免透明绘制仍阻挡。
6. support 物理列只能从所属 UV/member 的真实原图 alpha 经同一映射取得；弧拱/空列延续必须显式标 structural/virtual，并引用已证明端点。拒绝全图 low(x)、隐式外推及假称虚拟脚为物理脚。支撑 span、单调 X、有限数、非零段、全部实际切片 contact 均需校验。

## 哪些不移入生产

- AtomicFenceGround：硬编码 rejected 源图、x946..992、支撑公式、woodUv clamp 都是一次性夹具。只迁移已选木材 UV 与构件数据；用同一编译路径绘制，不保留独立 fence 类型。
- TexturedPropGround / AlphaMappedSurfaceGround / SectionedSpriteGround：可复用其语义，但不移入多层包装、重复 Render 累加和重复三角化实现。
- CityPreview/各 Probe 的演员替身、静态站位、环境变量、JSON 日志、source 原图对比、负控变体、固定 1468 计数均属测试。生产加载器不应拥有静态演员或测试结论。
- 离线 Python/PIL 几何构建留在 provenance/tooling；运行时不运行生成器。输入/产物 hash 和 support 物理/虚拟表可保存以审计，不引入两个可独立演进的几何事实源。

## 最小迁移序列与关口

A. 冻结选定的素材、几何、注册和 provenance；修清 support 来源及重复 primitive 检查。保留当前测试可读，不继续散件换皮。
B. 先加入可选 typed surface 数据、严格验证和唯一编译/visual 路径，C01-C06 默认行为不变。验证旧 catalog 无新字段仍一致；新旧格式明确互斥。
C. 在正式 scene 尚未切换时，用独立 C07 场景通过生产 BoardSceneRendering.Load 装配整个选择集。每个 site 只一次、旗和原 map structure 不重复；render/pick 不再调用夹具 helper。
D. 集中一次验收：mesh 内部/边界、同 entry 重复 primitive、base 未扣除重复、UV 只改 draw、左右新组件分别移坏 support；真实 Torren 前后、右门柱前后、透明窗/门洞/道路连续区、pointer 与 cue、窗口缩放。RGBA 原图整体全等只用于未变区域，不能套到刻意重建的 UV 部分。
E. 生产路径验收通过才切 `c01-07.json` 指向已注册 assembled 文档，做入口/退出/重入/读档显示的受影响回归。实际通关另按授权执行，静态结果不冒充实玩。每一步保存可恢复提交。

## 性能：有界研究，不阻塞为无限重构

已读 `godot/docs/C05-SOFTWARE-RENDERING-REVIEW-20261101.md`：同机 llvmpipe 当前 strips 293 calls / 26942 patches / 147 partitions；unsplit 193 / 15518 / 67，但破坏真实后栏遮挡，不能用作产品优化。FPS 顺序变化明显，文档明确未性能通过。

复杂度不只 Polygon2D 数：额外 contact 会增加 BoardProjection 深度分区，触发 PaintedSurface.Rebuild 的地面 patches；因此先统计整个 C07 的 entries、primitive、distinct depth partitions、ground patches、实际 draw calls、纹理、构造耗时和稳态 frame/process/render 时间，而非用 strip 数猜 GPU calls。

最小实验：同一独立静态完整 C07 画面/真实演员组合与相机，固定 viewport、renderer、缩放；各变体热身10秒、连续20秒采样，反向次序复测；测量期间无 F12/build/import。先对照基线与一个候选，仅测试具有相同深度键/同材质/同混合状态且保持稳定绘制顺序的片内三角合并；先减少每 slice 的 Polygon2D 数，再考虑更宽条带。缓存三角编译与不随状态改变的几何，避免每次 Render 重算。

不能把不同 contact 的片合成一个 Z 以省 calls；若按同深度分组，会改变随 projection/elevation 的排序，需重新证明。MultiMesh/全物体烘焙并不能自动保留逐片 actor 穿插，非本轮最小方案。合并需检查半透明同层叠色、接缝、脚/影/栏前后、爬坡高低差/悬崖fallback，保留 C05 回归。未测用户 GPU，不能承诺 FPS、顶尖性能或把云端慢全部归罪 strips。
