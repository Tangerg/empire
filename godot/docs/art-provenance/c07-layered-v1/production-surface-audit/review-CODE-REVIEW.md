# 首版未提交 surface owner 代码复核（2026-11-01 18:36 UTC）

只读检查当时工作树：SceneSurfaceDocument.cs、CompiledSceneSurface.cs、AssembledGroundDocument/View.cs。未运行 build/GUI/游戏，未改生产。行号以当时版本为准。

## 应先修的明确问题

### 1. opaque UV 的透明洞验证存在盲区

`CompiledSceneSurface.ValidateOpaque`（96–111）仅验证三个 UV 顶点和三角内的纹素中心。退化 UV 线、极窄 UV 三角可穿过透明纹素，却没有任何纹素中心在其中；顶点也可能全在不透明处。例：UV=(1,1),(5,1),(1,1)，纹素(1,1)/(5,1)不透明而(3,1)透明；几何三角本身非退化。验证接受后，draw 会取中间透明像素，而 opaque Covers 对整个几何返回 true。

这不是纯恶意输入：原子栏的 Clamp UV 有机会折叠一个维度。最小负控就是上述线段；另加窄三角穿过透明纹素、三个端点和所有内含中心均避开该纹素。应验证被 UV 覆盖的纹素（含线/点退化），或明确拒绝不支持映射并验证导出器没有依赖它。仍需分开声明 GPU 线性滤波边缘与 CPU 原生 alpha 合同，不用本修复承诺二者完全等价。

### 2. duplicate primitive 保护可被 metadata 绕过

Compile 72–75 的 key 同时包含 SupportId、Coverage、Tint、UV。相同几何三角换个 supportId（或改一个 UV）就被接受。故目前只防“完全相同声明重复”，不能称为实际重叠 primitive 守卫；同 slice、跨 slice 都要测。建议至少让完全相同几何重复与这些属性无关；正面积部分重叠及同区域不同对角剖分还需 ownership 审计覆盖。共享边不要误判成重复面积；不要引入追绿用的任意 allow-overlap 开关。

## 其他应明确的小边界

- `AssembledGroundView:153` 的 sampler 没有显式 TextureRepeat.Disabled，而 draw 已设 Disabled；建议明确一致，避免继承父级 repeat。当前合法 UV 内部尚无已证失败点。
- Surface 采用 GroundFrontPixels=null，但 DTO 属性仍是 required double[] 非 nullable（AssembledGroundDocument）。应表达 required double[]?（旧 sprite 仍由 runtime 强制），避免静态声明与有意 null 合同不符。
- support Kind 被验证为枚举后丢弃，实际只查 X 在 polygon AABB。physical Y 可以任意有限值。至少测试非法 physical contact / 明确作者校验边界；不能把枚举标签本身当真实原图脚点证明。support 物理/结构证据仍须外部审计。
- `Instantiate` 创建未归属 node 后 yield；View先创建/加shadow再 AddChild(node)。若该段抛错，view.Free 不会拥有这个尚未挂载的 node。最小防护是尽早归view所有或局部try/finally；无阴影的当前原子栏未证触发。不要为此改整个已有生命周期架构。
- JSON 显式 null 的 FrontPixels / PolygonPixels 内元素会进入 RasterResources.Pair 的 Length，报 NullReference 而不是带asset/member标识的验证错误。属于错误诊断完整性，不是静默成功。

## 已核没有发现的错误

- `SurfaceVisual.Covers` 使用 Node.ToLocal + anchor，与 draw 的 polygon-anchor、instance Scale 对应；UV映射没有多减anchor。
- pointerPolygon 已在 Assembled Create 减anchor；ContainsPointer 用 local坐标，IntersectsPointer 将primitive减anchor，对应一致。
- `ParentFrontOffset = Node.Transform*(Front-anchor)-Node.Position` 对当前 Node2D scale/translation 正确，沿用 BoardView.StandingDepth 的真实支撑排序，没有写死 Z。
- sampler 在 view 的 try/catch 范围内创建并 AddChild，一asset一view复用，不是跨view Node 单例；GetImage 用using，纯编译先于节点创建。
- site/selection 校验发生在 surface分支前，site每instance一次；分片只带selection，不额外造GroundSite/旗。whole sprite/DepthStrips主路径未被改成surface假实现。

## 最小收敛测试

先做 opaque线/窄三角洞、同几何不同support/UV重复两个负控；再做两个实例不同scale/anchor、target/pass-through、physical/structural非法contact、构造失败后重新挂载。首个原子栏迁移用实际已导出片顶点/UV/contact对照旧clamp输出，并检查primitive identities及全部网格边界；不以接口build通过代替材质证明。
