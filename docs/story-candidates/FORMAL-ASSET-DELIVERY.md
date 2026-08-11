# 三套剧本正式像素素材交付索引

> 本文记录最初的 HD 剧情样包。后续建立的 **1212/1212** 仅是题材 ID 与 atlas 槽位原型，不是 1212 份可玩美术。游戏内素材请优先使用 [Runtime V2 素材画廊](./RUNTIME-V2-ASSET-GALLERY.html)、[Runtime V2 覆盖状态](./RUNTIME-V2-STATUS.md)、[运行时素材契约](./GAME-RUNTIME-ASSET-CONTRACT.md) 和各套 `manifest-runtime-v2*.json`；原型结构另见 [题材覆盖原型说明](./COMPLETE-ASSET-DELIVERY.md)。

本索引只统计三套剧本 `assets/` 下的正式 `*-hd.png` / `*-hd.svg`，不包含已否决并归档在 `draft-v1/` 的早期草稿，也不把 `masters/` 中的高分辨率母版当作运行时资源。

补产前的缺口与优先级基线见 [`ASSET-LIBRARY-GAP-AUDIT.md`](./ASSET-LIBRARY-GAP-AUDIT.md)。

## 交付总量

| 内容 | 每套 | 三套合计 |
| --- | ---: | ---: |
| 人物头像 | 6 | 18 |
| 四帧单位图集 | 6 | 18（72 帧） |
| 建筑 / 地点 / 载具 | 4 | 12 |
| 16:9 剧情场景 | 4 | 12 |
| 四格叙事道具图集 | 2 | 6（24 件道具） |
| PNG/SVG 逻辑资产对 | 22 | 66 |
| 正式输出文件 | 44 | 132 |

三套正式 SVG 均由逐行合并的像素矩形构成，不嵌入 base64 位图；PNG 与 SVG 使用同一有限色结果，可按项目对性能、编辑性和发布体积的要求择一接入。跨三套批量接入使用统一的 [`FORMAL-ASSET-MANIFEST.json`](./FORMAL-ASSET-MANIFEST.json)，可通过 [`build_delivery_manifest.py`](./pixel-master-tools/build_delivery_manifest.py) 从三个单套 manifest 重建。

## 三套视觉区分

| 剧本 | 视觉核心 | 正式人物 | 正式地点与场景 |
| --- | --- | --- | --- |
| 《断冠之誓》 | 湿钢、烟石、焦木、灰旗、少量琥珀誓火；权力由旗、塔和队形表达 | 莱娅、罗德里克、凯恩、米蕾尔、布兰、塔莎 | 赤石誓约烽塔、灰旗野战营、三桥河谷、银林树城、焚村灰旗、双子丘陵、白河夜渡、七塔王城 |
| 《群星熄灭之前》 | 赤砂、旧陶瓷、暴露管线、劳动磨损；青色只用于水与状态 | 米拉、罗安、奈姆、塔洛斯七号、赫洛、伊娅 | 零号雨塔、远灯号、索勒档案修院、凯隆环都节点、旱季、七分钟雨、远灯号起飞、折叠桌星约 |
| 《布衣定鼎》 | 粗麻、靛布、土木、粮食、名册与白灯；不使用法术或仙侠视觉 | 沈砺、陆青禾、韩岳、裴昭、江照夜、阿勒坦 | 淮右县仓、淮右河堤、临川行台、大泽混合连营、开仓赈粮、雨夜渡淮、大泽精准火攻、一碗新粮 |

每套都使用原创角色、建筑和纹章。经典战棋与 HD-2D 只作为轮廓清晰、有限色、三分之四视角、材质光照和叙事密度的高层参考，不复刻既有作品的角色或专有设计。

## 运行时规格

| 类型 | PNG / SVG 尺寸 | 色彩预算 | 透明规则 | 切片 |
| --- | ---: | ---: | --- | --- |
| 人物头像 | `96×112` | 48 个前景色 | 二值透明 | 单张 |
| 单位图集 | `128×48` | 48 个前景色 | 二值透明 | 横向 4 帧，每帧 `32×48` |
| 建筑 / 地点 | `128×128` | 64 个前景色 | 二值透明 | 单张；碰撞体另配 |
| 剧情场景 | `256×144` | 96 色 | 全不透明 | 16:9 合成画面 |
| 道具图集 | `192×48` | 64 个前景色 | 二值透明 | 横向 4 格，每格 `48×48` |

引擎导入时关闭双线性过滤、MipMap 和有损纹理压缩，使用 nearest-neighbor 与整数倍率缩放。当前场景的雨、火、灯光和水面反射已经烘焙；若运行时需要开关 FX，应在后续制作独立效果层。

## 每套交付入口

- [《断冠之誓》正式说明](./candidate-01/assets/README.md) · [高清画廊](./candidate-01/assets/gallery-hd.html) · [manifest](./candidate-01/assets/manifest-hd.json) · [QA 报告](./candidate-01/assets/qa-report.md)
- [《群星熄灭之前》正式说明](./candidate-02/assets/README.md) · [高清画廊](./candidate-02/assets/gallery-hd.html) · [manifest](./candidate-02/assets/manifest-hd.json) · [QA 报告](./candidate-02/assets/qa-report.md)
- [《布衣定鼎》正式说明](./candidate-03/assets/README.md) · [高清画廊](./candidate-03/assets/gallery-hd.html) · [manifest](./candidate-03/assets/manifest-hd.json) · [QA 报告](./candidate-03/assets/qa-report.md)

## 生产与复用

- 高分辨率生成母版保存在各自 `assets/masters/`，正式资源由 [`pixel-master-tools/process_master.py`](./pixel-master-tools/process_master.py) 完成色键去除、裁切、有限色量化、目标尺寸输出和像素矩形 SVG 生成。
- 三套 `assets/style/tokens.json` 与 `assets/style/palette.svg` 是后续扩充角色阶段、阵营变体、建筑受损态和场景 FX 的颜色依据。
- 新素材必须先通过对应 `qa-report.md` 的尺寸、颜色、透明、色键与 XML 规则，再加入 `manifest-hd.json` 和正式画廊。
