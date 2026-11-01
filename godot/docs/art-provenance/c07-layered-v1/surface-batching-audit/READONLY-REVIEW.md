# 同 support 三角合批只读复核（2026-11-01 18:57 UTC）

范围：cfd5f370 正式 CompiledSceneSurface/Assembled 路径、已有 ProjectedGround 合批写法及 Godot 4.6.3 Polygon2D 源码。未运行 GUI、性能测量或改代码。

## 判定

按同一 support Node、texture、blend 状态，将原顺序三角合为一个 Polygon2D 是合适的有界第一步。保持 Compiled Primitive/Covers/FrontOffset/site 不变，不跨 support 合并、不改变接触分区。当前282个Polygon至最多45个只是节点/mesh上界改善，实际draw calls/FPS仍需测量。

## 精确实现条件与风险

1. 每个原三角独立追加3个 position、UV、color，不焊接几何坐标相同但UV/tint不同的顶点；Polygons 每项恰好引用本三角3个索引。不能把全部点作为一个外环重三角化，也不能用多个片轮廓重新选择对角线。UV长度、VertexColors长度必须与Polygon顶点数完全一致。
2. Godot4.6.3 polygon_2d.cpp:288–299：VertexColors数量匹配时直接采用它，Color仅作不匹配时的后备。因此三顶点均填原 primitive.Tint，Polygon.Color可显式White；不要依赖Color乘一次统一色，也不要提前再乘父级Modulate导致双重调色。保留原节点/CanvasItem的Modulate、SelfModulate、filter、repeat、材质与blend状态。当前Tint.A固定1，但native texture仍可半透明。
3. 源码:301–331按Polygons数组顺序追加每个三角索引，:336–375提交一个mesh。保留slice.Primitives现有顺序，不能为了减少状态按颜色/coverage排序，也不要经HashSet/Dictionary重新枚举。native-alpha半透明重叠对顺序敏感；两个三角alpha a,b叠合也不等于只留一次union。几何重复检查继续保留，合批不是自动去重。
4. 覆盖模式opaque/native-alpha是CPU材质合同，不自动对应不同GPU blend。若texture/filter/blend相同，可在一个批中保持原顺序；不要因为opaque名称擅自关闭alpha blend。若以后出现不同CanvasItemMaterial/clip/light等状态，不跨状态合并。
5. 保留每个support的父Node2D、名字、Scale/Position、ZAsRelative/ZIndex、FrontOffset及相对兄弟顺序。BoardView按该entry.Node及其Visual排序/拾取；替换其绘制children不应换父owner。不能把45个support合成一个Polygon后继续返回45个“虚拟”深度条目。
6. 同parent内的contact shadow依旧只一次且原前后顺序不变。当前shadow挂在first surface，避免批节点替换时顺带重挂/删掉它。不要引入CanvasGroup、额外裁剪或隐藏parent。
7. 数值不重新量化：使用原primitive顶点减相同anchor、原UV，保持同float与三角绕序。共享边原来分draw现在同mesh，GPU栅格可能有边界差异，不能因CPU Covers完全不变就宣称像素相等。

## 最小正确性验收（在计时区间外）

- 导出原未合批和合批的三角有序流：每个support的position/UV/tint逐项相同；条目数、contact、site、shadow数不变。
- 非白Tint的两三角、独立UV接缝、真实native-alpha孔洞与半透明边；后者不能只用本次原子栏opaque木材证明。加一个诊断用不同Tint半透明相叠双三角，使倒序会产生可检测像素差，确认测试能识别错误顺序。
- 真材质接缝局部截图至少zoom1、1.25、2，整数/半像素平移；检查边缘、重叠、阴影。不能套旧全source RGBA相等到已重建面，但同一formal输入的未合批/合批像素对照是合理合同。
- 保留真实演员前后/左右support的pick与alpha见证，及1468道路、pointer/site回归。CPU采样与GPU滤波仍分开报告。

## 有界性能比较

旧wrapper与formal未合批的ABBA（热身10秒＋连续20秒）是迁移基线，不单独证明batch收益。之后必须有相同formal输入的unbatched/batched对照，固定renderer/viewport/camera/actors/zoom；保持原parent深度分区完全相同。计时期间不截图、导出、build或import。

至少记录实际draw calls、primitive/node数、深度partitions和ground patches、帧时间分位数/过程时间；冷构造/编译耗时另测，不能混入稳态。保留ABBA的漂移/顺序结果，不挑最好一轮。若draw calls下降但帧时间无稳定收益，准确记为降低复杂度而未证速度提升，不继续扩大跨support合并。

## 依据

项目：`godot/Presentation/ProjectedGround.cs:RebuildPartitions` 已使用独立顶点、UV、VertexColors、Polygons；并非需要新渲染后端。

官方固定版本源码：https://raw.githubusercontent.com/godotengine/godot/4.6.3-stable/scene/2d/polygon_2d.cpp （288–375）。源码确认颜色选择及索引提交顺序；不据此冒充目标GPU实际栅格/性能验证。

## 已实现 diff 复核（2026-11-01 19:05 UTC，未提交工作树）

已读 CompiledSceneSurface.Instantiate 相对 cfd5f370 的实际 diff，未发现显式运行时正确性错误：

- 每个primitive在追加前用当前vertices.Count建立自己的索引，再追加坐标/UV/三个相同Tint，顺序一致；Compiled Primitive 本身已经是三角，未对拼接总顶点池重新选对角线。
- UV、颜色数量与顶点数对应，Color显式White；Linear与Repeat.Disabled未改变。
- support父Node、缩放、owner.AddChild顺序、SurfaceVisual、Covers、FrontOffset、selection均未改变。单slice仍一个depth owner，独立站点不受Polygon数量影响。
- 节点已先归owner所有，后续构建抛错仍由上层view生命周期清理。

父writer报告的45support/846vertices误差0、2961coverage差0及Polygon282→45未由本只读审查者重新运行，不能写成我的执行结果。半透明12组/倒序负控及ABBA仍由writer占用GUI执行，结果尚未在此报告认定通过。

**测试读取格式必须同步**：旧 NorthwestMeshProbe 直接 `Geometry2D.TriangulatePolygon(draw.Polygon)` 的写法只适用于单闭环。合批后 `Polygon` 是独立三角顶点池，必须读取 `draw.Polygons` 各项的索引及相应UV/VertexColors；不能把总顶点池当大外环，也不能只读Polygon.Color作为每三角Tint。新的exact-equivalence oracle若已按索引读取则无需额外改运行时。

性能边界仍同前：旧180/formal未批318 calls、相同39 partitions/34579 patches 是父writer提供的迁移基线，不能代替formal未批/批版ABBA；45个Polygon不是预先宣称45个全场draw calls。
