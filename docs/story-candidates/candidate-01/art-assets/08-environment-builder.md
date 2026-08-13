# 使用《断冠之誓》的通用地图建造包

原 404 项预算解决角色、建筑、图标和地形题材覆盖；通用环境建造包解决“如何把这些内容稳定拼成一张高质量 SRPG 地图”。两套清单互补，不能把 1188 个连接变体与拼装部件误计成新增剧情题材。

## 当前交付

环境包位于 [`../assets/final-fantasy-v1/environment-builder-v1/`](../assets/final-fantasy-v1/environment-builder-v1/README.md)，V1.1 包含 36 套运行时图集和 1188 个部件：

- 8 类基础地表与四变体；
- 草土、草林、草雪、土石四套八邻域 Blob47 过渡；
- 土路、泥路、石路、林径四套 N/E/S/W 16-mask，每个掩码四个变体，另有四套路肩覆盖；
- 河流、浅溪、沼泽、冰水四套 N/E/S/W 16-mask，每个掩码四个变体；
- 温带/寒地两套自动高差边缘和两套悬崖/坡道模块；
- 森林、区域小景、桥墙工事、营地地基、大型地标、地面贴花和农村生活件。

## 与现有素材保持一致

环境母图以 [`reference/art-direction-map.png`](./reference/art-direction-map.png) 及正式包现有单位/建筑为双重参考。镜头固定为高斜俯视，轮廓使用深棕黑线，材质为哑光两段卡通明暗；地面保持中低对比度，让现有单位与交互目标仍然占最高识别层。

混搭结果见 [`compatibility-map-temperate-1x.png`](../assets/final-fantasy-v1/environment-builder-v1/previews/compatibility-map-temperate-1x.png)。

## 接入高低差

环境包不创建第二套高度规则。渲染器直接读取现有 `GameMap.elevation`：相邻格每增加 1 层，绘制一层岩壁；合法坡道使用 `rampCells`；断崖和墙体的阻挡仍以地图边与现有战斗系统为真值。

视觉示例见 [`elevation-system-1x.png`](../assets/final-fantasy-v1/environment-builder-v1/previews/elevation-system-1x.png)。

## 晋级条件

当前素材包机器 QA 已通过，但仍保持 `runtimeReady=false`。只有完成以下接入后才能晋级：

1. 第一关不再用 SVG/Canvas 曲线画道路和山体；
2. 至少三张不同地貌关卡使用同一套图集搭建；
3. 实机验证高差、坡道、遮挡、单位脚底和建筑地基；
4. 1×、2×与编辑器预览均无接缝、错层或素材贴纸感。
