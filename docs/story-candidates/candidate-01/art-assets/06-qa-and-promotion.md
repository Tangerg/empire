# 验收《断冠之誓》的正式素材

正式素材包不再沿用 `runtime-v2-candidate` 等旧状态。当前包使用 `final-cartoon-fantasy-v1` 质量层级，并分为素材包验收、程序契约验收和真实界面验收三层。

## 三层验收

1. **素材包验收**：404/404 文件存在、尺寸与 Manifest 一致、`topicId` 唯一、透明背景与帧条满足规格。
2. **程序契约验收**：Manifest 可被 Vite 完整解析；单位帧语义、地形连接、建筑三态和领域绑定都有自动测试。
3. **真实界面验收**：在剧情、战场、HUD、缩放和不同窗口尺寸中确认锚点、清晰度、遮挡和信息层级。

## 本地入口

- 正式 QA：[`qa-final-fantasy-v1.json`](../../../../packages/story-candidate-01/assets/final-fantasy-v1/qa-final-fantasy-v1.json)
- 单位预览：[`units-1x.png`](../../../../packages/story-candidate-01/assets/final-fantasy-v1/previews/units-1x.png)
- 地图素材预览：[`map-assets-1x.png`](../../../../packages/story-candidate-01/assets/final-fantasy-v1/previews/map-assets-1x.png)
- 图标与特效预览：[`icons-fx-1x.png`](../../../../packages/story-candidate-01/assets/final-fantasy-v1/previews/icons-fx-1x.png)
- 剧情素材预览：[`narrative-1x.png`](../../../../packages/story-candidate-01/assets/final-fantasy-v1/previews/narrative-1x.png)
- 通用环境包 QA：[`qa-environment-builder-v1.json`](../../../../packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/qa-environment-builder-v1.json)
- 环境与现有资产混搭：[`compatibility-map-temperate-1x.png`](../../../../packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/previews/compatibility-map-temperate-1x.png)
- 高差语义预览：[`elevation-system-1x.png`](../../../../packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/previews/elevation-system-1x.png)

运行素材包重建与 QA：

```bash
python3 docs/story-candidates/candidate-01/assets/final-fantasy-v1/tools/build_final_fantasy_v1.py
python3 docs/story-candidates/candidate-01/assets/final-fantasy-v1/environment-builder-v1/tools/build_environment_builder_v1.py
```

运行程序契约验收：

```bash
npm test
npm run build
```

## 阻止错误晋级

出现以下任一情况时，具体绑定不得标记为实机通过：

- 回退到程序占位图或引用归档路径；
- 连通地形缺少底图、出现接缝或连接方向错误；
- 单位锚点漂移、攻击帧被当作移动帧、武器被裁切；
- 建筑受损/占领切换改变足迹或遮住交互单位；
- 图标在原尺寸无法识别，特效长期遮住单位或伤害数值；
- 剧情静态图仍使用像素化重采样；
- 视觉层为了读素材而污染战斗规则或剧情内容模型。
