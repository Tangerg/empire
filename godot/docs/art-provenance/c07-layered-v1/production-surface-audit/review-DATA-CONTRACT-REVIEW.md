# 最小 surface 数据合同复核（2026-11-01 18:31 UTC）

只读建议；不修改生产或他人夹具。已复核 AtomicFenceGround 的先 clip 后逐顶点 clamp UV，以及 AssembledGroundView/BoardView 的现有节点所有权。

## 建议选择：首版用显式已审核片作为作者资源

`SurfaceGeometryPath` 与 `DepthStrips` 互斥合理。对本轮已选 C07 候选，建议 surface 文件直接记录已审核的片/三角与每片 contact，而不是在运行时重新解释 support curve 和 sliceWidth。

原因不是追求通用性，而是当前原子栏存在真实的非交换运算：它先把 member 按世界 source x 单位条带裁剪，再对新顶点的沿轴/横轴坐标做 Clamp 得到 UV。先给整 member 四角 UV、再三角化/分片插值，并不等于上述结果。换成 asset-local 后重新对齐整数 slice 网格也会移动旧接缝。即使外观近似，已通过的像素证据不能自动继承。

最小可行权威数据：每片稳定 id、local contactPixels、supportKind(physical/structural)、该片所属 primitives（稳定 id、local vertices、UV、tint、coverage）。同片多三角共享一个深度条目；site/pointer 仍来自 asset/placement。若非仿射四边形，保留原有三角索引/对角线，不能在导出时旋转顶点或改顺序导致另一条对角线。不要同时让原 support curve 和每片 contact 都可覆盖对方。

曲线、物理列见证、虚拟延续端点、生成器版本/输入hash可保存在 provenance，离线生成显式片。运行时只读一个权威产物；更新源必须重生成、重验并更新hash。无需另引同时支持两种几何作者格式的插件层。

如果坚持运行时曲线方案，最低代价也必须让原子栏输入保持原切片后的独立 primitives + 对应切片contact，不再重新按统一 sliceWidth 二次切片。这实际已经需要显式片，因此首版统一显式片更小、更不易双重所有权。

## 最少必要校验

- 文档 schema、未知字段拒绝、asset路径/资源尺寸一致；SurfaceGeometryPath 与 DepthStrips 明确互斥，旧资产默认不变。
- 所有 id 非空且在约定范围唯一；数量有限，至少一个非退化 primitive。坐标/UV/contact/scale/tint 必须 finite；polygon 简单、三角有面积、索引有效、UV 数量匹配。纹理边界的端点坐标可用，但有效内部采样不得依赖越界 wrap/clamp；显式设定 sampler repeat 行为，不继承不明父级。
- 顶点坐标是 asset-local，UV 是真实纹理像素。只在 Assembled 放置时应用 anchor、instance scale、contact 与 registration，恰好一次；不要保留 C07 `/16`、世界 source 原点或整关绝对坐标假设。
- 每片 contact X 属于其声明支撑跨度；physical 需有所属 primitive/UV 原像素见证，structural 需有引用/说明且不能冒称物理脚。两种不能用同一宽松默认绕过越界。接触证据可以在离线回归验证，但生产必须拒绝缺失/非法基本数据。
- primitive 严格内部不可意外重复：同片也计数，不只数 TerrainDepthEntry。共享边不是重复面积。一个 asset 含多个表面可能有有意遮叠，不能盲目要求全场任意 primitive 零交叠；本轮单材质重建替换域应声明/测试唯一 ownership，不能用可选“允许重复”开关追绿。
- opaque/native-alpha 显式枚举且各守原合同。opaque UV 区域透明洞必须拒绝；原helper的粗重心采样不能冒充整纹素证明。native-alpha 保留透明孔、整材质遮挡与独立 pointer。首版把 tint 限制为 alpha=1 可避免新增透明调制语义；若支持 A<1，Covers/遮挡阈值必须明确同步，不可忽略。
- pointerclip 从最终片几何与现有 PointerPolygonPixels 派生，不再创一套可独立编辑的裁剪polygon事实；site/selection由placement唯一注册。碎片数不能导致旗/站点复制。

## 生命周期和缓存边界

- 先纯数据校验/编译，再创建 Godot Nodes；编译异常应带 asset/member/片 id，不留下半棵树。AssembledGroundView.Create 现有 try/catch Free(view) 覆盖节点创建阶段；不得在该保护区之前创建隐藏 sampler 后抛错泄漏。
- 编译数组保持内部只读，draw与visual共用同一三角UV/contact数据；对外返回可修改数组时拷贝。渲染节点需要的本地偏移只是投影，不能反向改写权威几何。
- 一个 sampler 按 asset、按 ground view 创建并归该view所有，放进正常节点树但隐藏；不能把同一Node跨view reparent。多个实例共享同asset纹理/编译几何及sampler查询即可，传入纯UV，不把某个实例的Node变换写进sampler。
- 不手动Free ResourceLoader共享Texture2D；临时 GetImage() 用using释放。构造完成后 Render 只更新projection/contact/depth entries，不重复triangulate/clip/GetImage。
- 沿用 BoardView.ConfigureGround 的 detached factory、authored subtree转移/异常恢复；不新增全局 sampler 单例或永久静态Node缓存。重复进入/切回、构造失败后重进应无孤儿Node和重复site。

## 最小回归

1. 原子栏迁移前后：每片真实三角顶点/UV/contact经坐标变换逐项等价；包含clamp转折和原片边界，不只截图近似。多缩放GPU边缘单独确认。
2. 当前候选mesh内部及接缝、UV-only mutant、同片duplicate primitive、未扣base duplicate、左右组件各自support mutant。物理/结构支撑表与原alpha见证一致。
3. 单asset多实例、front/back镜像或缩放（仅产品已有能力）、target/pass-through、透明孔、site/旗唯一性、authored subtree失败恢复及重复挂载。
4. 旧whole-sprite/DepthStrips路径和C05后栏/脚影合同保持不变。IsPixelOpaque与GPU Linear/mipmap不宣称逐像素同算法；测试分别报告CPU材质合同与实际栅格外观。

首版不要同时做运行时曲线引擎、自动条带宽度优化、通用孔洞三角化、multi-texture/multi-pass、多层蒙版或全局几何cache。先用单一显式片实现已选候选，生产运行时数量/性能另按有界计划测量。
