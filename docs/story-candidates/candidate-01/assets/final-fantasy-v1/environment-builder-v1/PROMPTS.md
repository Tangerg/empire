# 环境包母图生成记录

本包使用 Codex 内置图像生成能力制作原创母图。所有透明素材先以纯洋红色键背景生成，再使用 imagegen skill 自带的 `remove_chroma_key.py` 去背；运行时连接图集和 1×/2×输出由本地确定性构建器生成。

共同约束：以剧本一已批准的 `art-assets/reference/art-direction-map.png` 和 `final-fantasy-v1/previews/map-assets-1x.png` 作为风格、比例与材质参考；高斜俯视战术卡通、深棕黑描边、哑光灰蓝/苔绿/雨褐/旧石色、两段明暗；不复制任何商业作品的地图、建筑、徽记或角色。

## 母图清单

1. `surface-common-4x4.png`：草地、林下地、泥土、旧石地共 16 格。
2. `surface-regional-4x4.png`：雪地、荒原、墓园、炉城地面共 16 格。
3. `route-materials-4x4.png`：土路、泥路、石路、林间小径共 16 格。
4. `water-materials-4x4.png`：河水、浅溪、沼泽、冰水共 16 格。
5. `cliffs-temperate-4x4-key.png`：温带丘陵、岩壁、坡道和山口 16 件。
6. `cliffs-highland-4x4-key.png`：高地与寒地岩壁、雪坡和山口 16 件。
7. `forest-temperate-4x4-key.png`：阔叶、混合林、林缘、灌木、树根和倒木 16 件。
8. `scenery-regional-4x4-key.png`：雪原、荒原、墓园与炉城区域小景 16 件。
9. `crossings-fortifications-4x4-key.png`：桥梁、桥头、木墙、石墙、门和拒马 16 件。
10. `camps-foundations-4x4-key.png`：营地、建筑地基、入口和工作区 16 件。
11. `decals-small-6x4-key.png`：草丛、碎石、泥斑、车辙、脚印、树根和废弃工具 24 件。
12. `landmarks-large-4x4-key.png`：16 个 3×3/4×4 战役大型据点与区域地标。

详细语义、连接掩码和运行时尺寸以 `manifest-environment-builder-v1.json` 为准。农村生活母图由内置图像生成器产出，原始生成文件为：

`/Users/tangerg/.codex/generated_images/019ff0c9-147e-7a43-906d-49777cc0db7a/exec-412910d5-8f64-4c1a-9f5d-fdf02c2d2918.png`

## 最终提示词摘要

以下是 13 张批准母图实际使用的语义提示集合。每条都继承上面的共同视觉约束；透明部件额外要求 `#ff00ff` 实心键色背景、每格只有一个完整部件、不得跨格、无文字/水印/格线。

1. **Common surfaces**：4×4 edge-to-edge ground texture board；四行依次为 meadow grass、forest floor、packed earth/mud、old stone/cobble；无物件、无投影、可平铺。
2. **Regional surfaces**：4×4 edge-to-edge ground texture board；四行依次为 snow、wasteland、graveyard soil、forge stone；无建筑与大型物件。
3. **Route materials**：4×4 edge-to-edge material swatches；四行依次为 dry dirt road、mud road、old stone road、forest trail；只画材质，不预制连接形状。
4. **Water materials**：4×4 edge-to-edge water swatches；四行依次为 slow blue river、shallow rocky stream、marsh channel、icy water；只画水面材质。
5. **Temperate cliffs**：4×4 modular elevation kit；温带丘顶、直岩壁、内外角、端头、一层/二层坡道、山口、碎石坡脚，全部为独立高斜俯视部件。
6. **Highland cliffs**：4×4 modular elevation kit；寒地岩壁、雪顶台地、冰坡、二层山脊、雪山口和岩柱，独立透明部件。
7. **Temperate forest**：4×4 forest scenery kit；阔叶树、针阔混合林、林冠边缘、灌木、蕨类、枯叶、树桩、倒木、苔石，独立透明部件。
8. **Regional scenery**：4×4 region scenery kit；雪境、荒地、墓园、锻炉区各一行，含雪松、冰石、荆棘、路标、墓碑、死树、煤堆、炉渣等独立部件。
9. **Crossings and fortifications**：4×4 structure kit；横/纵木桥与石桥、桥头、直/角木墙、直/角石墙、寨门、城门、岗楼、拒马。
10. **Camps and foundations**：4×4 foundation kit；灰旗/敌军营地、石砌地基、村庄地基、帐篷组、补给场、工坊、牲畜围栏和营门，保持地面接触阴影。
11. **Small decals**：6×4 low-contrast decals；草丛、碎石、泥斑、龟裂土、车辙、脚印、落叶、树根、树桩、倒木、苔石、木板、工具、绳索、帆布、灰烬等。
12. **Large landmarks**：4×4 large 3×3/4×4 landmarks；灰旗营寨、洛恩/维尔萨城堡、山口要塞、长厅、边境村镇、野战医院、军械库、教堂墓园、河关、市场、废墟与敌军堡垒。
13. **Rural life**：4×4 reusable border-village life atlas；三种晒麦铺面、脱粒场、麦捆、粮袋与篮筐、晾晒架、小谷仓、农舍、水井、畜栏、直/角围栏、农具手推车和路边驿棚；建筑保持 2×2/3×2 的朴素战场尺度。

Rural life 最终提示词还固定了以下约束：Image 1 只作为批准风格图，Image 2 只作为现有运行时比例与材质参考，Image 3 只作为营地/地基信息密度参考；`4×4` 等分、一格一件、底部中心接地、高斜俯视、深暖棕黑描边、哑光两段阴影、低至中饱和；无人物、动物、武器、城堡、现代机械、文字、UI 或商业作品可识别资产。

构建器不直接照搬母图格子作为连接语义。地表会进行边缘锁定；道路、水体、高差会重新生成 N/E/S/W 16-mask；材质过渡会生成 Blob47；最终 1×/2× PNG 以 `qa-environment-builder-v1.json` 为准。
