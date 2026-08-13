# 验收并晋级《群星熄灭之前》的素材

本页定义《群星熄灭之前》的机器检查、目检和实机晋级规则。只有进入真实关卡并留下截图证据的素材才能标记为可用。

## 区分四个质量状态

| 状态 | 使用范围 |
| --- | --- |
| `prototype` | 只验证题材 ID、atlas 切片或旧接线 |
| `runtime-v2-candidate` | 通过机器检查与 1× 目检，尚未进入真实关卡 |
| `runtime-v2-partial` | 清单中的部分资产通过实机验收 |
| `runtime-v2` | 整包资产全部通过实机验收 |

部分通过时保持 `runtimeReady: false`，并在 `gameIntegration.runtimeReadyAssetIds` 中精确登记已通过资产。

## 依次执行验收

每批素材按以下顺序验收：

1. 检查路径、PNG 签名、画布尺寸、帧数、锚点和清单引用
2. 以 1× 尺寸检查轮廓、色键残留、最小线宽和透明角
3. 检查单位四帧脚底、武器裁切、镜像和大型足迹
4. 使用 `3×3` 与随机 `12×8` 地图检查连接地形
5. 检查设施状态切换、碰撞区、热区和阵营灯光层
6. 检查图标的原尺寸、灰度、深色底和浅色底
7. 接入真实关卡，记录破图、回退素材和截图路径
8. 只晋级截图实际覆盖的资产 ID

## 使用本地检查入口

检查时优先使用本剧本文件：

- `assets/qa-runtime-v2.json`
- `assets/qa-runtime-v2-b02.json`
- `assets/qa-runtime-v2-b03.json`
- `assets/runtime-v2/previews/`
- `assets/runtime-v2/batch-02/previews/`
- `assets/runtime-v2/batch-03/previews/`

当前可先检查 `assets/runtime-v2/previews/c02-v2-runtime-preview-1x.png`、第二批综合预览和第三批战斗单位预览。这些预览不等于实机验收。

## 阻止错误晋级

出现以下情况时保持候选状态：

- 游戏回退到程序占位图
- 水道、轨道或菌床出现接缝
- 单位脚底漂移或载具足迹错误
- 设施状态改变锚点或占地
- 图标依赖霓虹颜色才能区分
- 特效遮住单位轮廓超过两帧
- 风格偏离本剧本唯一基准图
