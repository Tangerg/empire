# 《布衣定鼎》静态素材定制清单

正式资源目录为 [`../assets`](../assets/)。文件名使用下列稳定编号，文本与素材可独立迭代。

优先级：`P0` 用于第一阶段关键演出，`P1` 用于完整战役，`P2` 用于支线与氛围扩充。

## 0. 已同步素材对接

当前正式素材继续保留美术侧命名，深化稿通过稳定编号对接，不要求批量重命名。规格见[素材包说明](../assets/README.md)，正式成品总览见[高清画廊](../assets/gallery-hd.html)，机器接入以 [`manifest-hd.json`](../assets/manifest-hd.json) 为准。`draft-v1/` 只保留被否决的早期构图草案，不进入正式构建。

| 已同步文件 | 深化编号 | 使用状态 |
| --- | --- | --- |
| [`shen-li-22-portrait-hd.png`](../assets/characters/shen-li-22-portrait-hd.png) | `C03-CHAR-SHEN-01` | 正式质量标杆；河工头巾、粗麻、量杆与账页均已落实 |
| [`lu-qinghe-portrait-hd.png`](../assets/characters/lu-qinghe-portrait-hd.png) | `C03-CHAR-LU-01` | 正式质量标杆；炭笔、药包与旧账裙袋形成账房轮廓 |
| [`han-yue-portrait-hd.png`](../assets/characters/han-yue-portrait-hd.png) | `C03-CHAR-HAN-01` | 正式质量标杆；铁匠烧伤、自制札甲与改制锤均可读 |
| [`pei-zhao-portrait-hd.png`](../assets/characters/pei-zhao-portrait-hd.png) | `C03-CHAR-PEI-01` | 正式质量标杆；制式甲、图袋和克制朱砂区别于义军体系 |
| [`jiang-zhaoye-portrait-hd.png`](../assets/characters/jiang-zhaoye-portrait-hd.png) | `C03-CHAR-JIANG-01` | 正式水盟船主锚点；防水短衣、船钩、账绳和工作束发均已落实 |
| [`jiang-zhaoye-walk-sheet-hd.png`](../assets/units/jiang-zhaoye-walk-sheet-hd.png) | `C03-CHAR-JIANG-01`（单位） | 四帧战术单位；保留船钩、账绳和水盟独立身份，不作海盗化 |
| [`aletan-portrait-hd.png`](../assets/characters/aletan-portrait-hd.png) | `C03-CHAR-ALATAN-01` | 正式大朔边将锚点；多文化织带、雪地骑装、复合弓与修补札甲可读 |
| [`aletan-walk-sheet-hd.png`](../assets/units/aletan-walk-sheet-hd.png) | `C03-CHAR-ALATAN-01`（单位） | 四帧战术单位；保留边军专业性与独立盟将视觉边界 |
| [`county-granary-hd.png`](../assets/architecture/county-granary-hd.png) | `C03-ARCH-GRANARY` | 正式县仓标杆；封门、石基、称量棚与粮囤清晰 |
| [`huai-right-bank-dike-hd.png`](../assets/architecture/huai-right-bank-dike-hd.png) | `C03-ARCH-HUAI-DIKE-01` | 正式河堤模块；石脚、闸门、渗沟、巡检路和抢修工具齐全 |
| [`linchuan-government-hub-hd.png`](../assets/architecture/linchuan-government-hub-hd.png) | `C03-ARCH-LINCHUAN-HUB` | 正式第三章基地；户曹、工曹、学馆、史馆和市井服务五区同院可读 |
| [`great-lake-mixed-fleet-hd.png`](../assets/architecture/great-lake-mixed-fleet-hd.png) | `C03-ARCH-GREAT-LAKE-FLEET` | 正式大泽连营；军、粮、医、工、民功能船与栈桥构成混合水上基地 |
| [`opening-the-county-granary-hd.png`](../assets/scenes/opening-the-county-granary-hd.png) | `C03-CH01-S03` | 正式官仓开门场景，量粮秩序与灾民压力同框 |
| [`rain-night-crossing-hd.png`](../assets/scenes/rain-night-crossing-hd.png) | `C03-CH02-S08` | 正式雨夜渡淮，船、浮桥、浅滩三线与登记/追击层同时可读 |
| [`great-lake-precision-fire-attack-hd.png`](../assets/scenes/great-lake-precision-fire-attack-hd.png) | `C03-CH04-S18` | 正式大泽精准火攻；受控火线、白灯绳标、撤民航道和民船同框 |
| [`one-bowl-of-new-grain-hd.png`](../assets/scenes/one-bowl-of-new-grain-hd.png) | `C03-CH07-S35E` | 正式推荐尾声；普通食肆、无官印粮袋、公开木牌与河堤日常同框 |
| [`campaign-props-sheet-02-hd.png`](../assets/props/campaign-props-sheet-02-hd.png) | `C03-PROP-FIRST-BANNER` · `C03-PROP-PEI-CAMPAIGN-MAP` · `C03-PROP-FIELD-MARKERS` · `C03-PROP-PEI-CLAN-ROLL` | 正式四格道具；四项证据均无可读文字，格序与机器清单一致 |

## 1. 风格基准 `assets/style`

| 优先级 | 文件名 | 内容 | 关键要求 |
| --- | --- | --- | --- |
| P0 | `C03-STYLE-KEYART.png` | 沈砺站在官仓与宫门之间的主视觉 | 不戴龙纹王冠；前景是粮袋与湿泥，远景才出现军旗 |
| P0 | `C03-STYLE-HUAI-RAIN.png` | 淮右灾年色彩与材质 | 灰绿水、黄褐泥、湿麻布、低饱和白灯 |
| P0 | `C03-STYLE-LAKE-FIRE.png` | 大泽风火基准 | 火光照出民船轮廓，避免纯壮观战争海报 |
| P0 | `C03-STYLE-COURT-WINTER.png` | 开国后宫城基准 | 冷灰石阶、朱门、大片留白，人物被建筑压小 |
| P1 | `C03-STYLE-JIANGDONG-SPRING.png` | 江东建设期 | 水田新绿、白墙深瓦、忙碌而非仙境化 |
| P1 | `C03-STYLE-YANYUN-SNOW.png` | 燕云雪原 | 风蚀旗布、多族营具、低能见度 |
| P1 | `C03-STYLE-INK-MAP.png` | 战役地图笔触 | 像军中反复改写的水陆图，不做现代卫星地图 |

## 2. 角色阶段立绘 `assets/characters`

| 优先级 | 文件名 | 角色阶段 | 画面提示 |
| --- | --- | --- | --- |
| P0 | `C03-CHAR-SHEN-01.png` | 22 岁河运差役 | 粗布短褐、旧秤绳、鞋底藏账，不持帝王武器 |
| P0 | `C03-CHAR-SHEN-02.png` | 28 岁江东统帅 | 实用札甲、灰白军旗、明显睡眠不足 |
| P0 | `C03-CHAR-SHEN-03.png` | 36 岁北伐吴王 | 甲胄更整齐，仍保留旧河工护腕 |
| P0 | `C03-CHAR-SHEN-04.png` | 40 岁开国皇帝 | 冕服克制，手边是奏册而非宝剑，眼神警觉 |
| P0 | `C03-CHAR-LU-01.png` | 淮上账房 | 药包、炭笔、旧账裙袋 |
| P0 | `C03-CHAR-LU-02.png` | 临川户曹 | 官服与便于行动的袖口并存，手持多层册页 |
| P0 | `C03-CHAR-LU-03.png` | 开国法制主事 | 不作后妃造型，佩官印与审理文书 |
| P0 | `C03-CHAR-HAN-01.png` | 铁匠义军 | 自制甲、铁锤改兵器、笑意直接 |
| P0 | `C03-CHAR-HAN-02.png` | 大泽前锋 | 重甲有修补痕，私人旗号开始明显 |
| P0 | `C03-CHAR-HAN-03.png` | 开国功臣 | 华贵赏甲下仍穿旧护腕，疲惫与防备并存 |
| P1 | `C03-CHAR-PEI-01.png` | 朝廷追粮将领 | 制式甲、无奢华白马意象、军图袋 |
| P1 | `C03-CHAR-PEI-02.png` | 新军统帅 | 旧门阀纹章被拆除一半，佩统一军籍印 |
| P1 | `C03-CHAR-JIANG-01.png` | 水盟船主 | 防水短衣、篙钩、账绳，不做海盗皮甲 |
| P1 | `C03-CHAR-JIANG-02.png` | 大泽水师统帅 | 舰队令旗与旧船主饰物并存 |
| P1 | `C03-CHAR-XIAO-01.png` | 落第县吏 | 旧囚衣外披文书袋，外表不阴险 |
| P1 | `C03-CHAR-XIAO-02.png` | 密察司主事 | 克制官服、密封卷宗、无夸张暗色反派装 |
| P1 | `C03-CHAR-ALATAN-01.png` | 大朔边将 | 实用雪地骑装、多文化配件、非野蛮化 |
| P1 | `C03-CHAR-WEN-01.png` | 随军史官 | 便携木牍、染墨袖口、无战斗武器 |
| P2 | `C03-CHAR-XIAOMAN-01.png` | 11 岁灾民 | 空碗、破损田契布包 |
| P2 | `C03-CHAR-XIAOMAN-02.png` | 临川学馆书吏 | 田尺、炭笔、同一只补过的布包 |
| P2 | `C03-CHAR-XIAOMAN-03.png` | 开国户曹书吏 | 官服朴素、手持新旧两册 |
| P2 | `C03-CHAR-PENG-01.png` | 水盟船工 | 长篙、破油衣 |
| P2 | `C03-CHAR-PENG-02.png` | 伤残渡口主人 | 木腿、渡口牌、沉船名册副本 |

## 3. 关键场景图 `assets/scenes`

| 优先级 | 文件名 | 场景 | 构图重点 |
| --- | --- | --- | --- |
| P0 | `C03-CH01-S01.png` | 天未亮的押粮车 | 秤杆、漏米、敲空碗的人群 |
| P1 | `C03-CH01-S02.png` | 决口后的界碑争议 | 退水、错位界碑、灾民田与豪强新田同框 |
| P0 | `C03-CH01-S03.png` | 官仓开门 | 门内粮食有限且发霉，避免金山式粮仓 |
| P1 | `C03-CH01-S04.png` | 芦苇滩白灯赠粮 | 顾重山背发热孩子，互助白灯沿河亮起 |
| P0 | `C03-CH01-S05.png` | 白灯夜后 | 一边开仓，一边书吏巷留下尸体 |
| P1 | `C03-CH02-S06.png` | 第一面军旗与军法宣读 | 铁锅改甲、混编新兵、未染色军旗，欢呼与沉默并存 |
| P0 | `C03-CH02-S08.png` | 雨夜渡淮 | 船、浮桥、浅滩三线同时存在 |
| P1 | `C03-CH02-S10.png` | 白灯赠别与八十九日粮 | 补过三次的旧灯、离营队伍、账册数字压住出发气势 |
| P1 | `C03-CH03-S11.png` | 和州借城 | 吊筐糙米、城外营地与只为伤员开启的侧门 |
| P0 | `C03-CH03-S13.png` | 田契之争后的播种 | 两方人在同一田埂两侧下种 |
| P0 | `C03-CH04-S18.png` | 大泽精准火攻 | 火线、撤民航道和民船同框 |
| P0 | `C03-CH04-S20.png` | 梁震旗舰 | 水火、粮舱、俘虏层，沈砺与梁震不做单挑构图 |
| P0 | `C03-CH05-S25.png` | 吴王之台 | 金框白灯、跪与不跪的不同代表 |
| P0 | `C03-CH06-S30B.png` | 登基与讨饷并置 | 丹墀仪式、宫外老兵、户曹抄册三层画面 |
| P0 | `C03-CH07-S31.png` | 新朝田册之乱 | 与第一关空碗构图镜像，换成宁朝军旗 |
| P0 | `C03-CH07-S33.png` | 公开审理 | 皇帝密令、韩岳军印、萧慎卷宗同时置案 |
| P0 | `C03-CH07-S35E.png` | 一碗新粮 | 普通食肆、公开税额、无官印粮袋 |
| P1 | `C03-CH03-S15.png` | 临川新政告示 | 五处张贴、百姓现场追问 |
| P1 | `C03-CH04-S16.png` | 湖口破船 | 船工舀水，炮台在远处施压 |
| P1 | `C03-CH05-S21.png` | 富城两条队伍 | 商车入城与灾民被拦形成对照 |
| P1 | `C03-CH06-S26.png` | 三路出师 | 粮船、步军、工匠渡口同屏 |
| P1 | `C03-CH06-S29.png` | 燕云盟约 | 雪地小城、多族居民与独立旗号 |

## 4. 建筑与地点 `assets/architecture`

| 优先级 | 文件名 | 用途 | 阶段变体 |
| --- | --- | --- | --- |
| P0 | `C03-ARCH-HUAI-DIKE-01.png` | 决口关、开场故乡 | 松沙新堤、旧分洪渠 |
| P0 | `C03-ARCH-HUAI-DIKE-FINAL.png` | 最终镜头 | 加固但非宏伟，刻修检名册 |
| P0 | `C03-ARCH-GRANARY.png` | 官仓与多次粮仓关卡 | 官印封条、霉粮、双层门 |
| P0 | `C03-ARCH-LINCHUAN-HUB.png` | 第三章后基地 | 户曹、工曹、学馆、史馆、市井模块 |
| P0 | `C03-ARCH-GREAT-LAKE-FLEET.png` | 大泽连营 | 军民功能船混合，不只战舰 |
| P0 | `C03-ARCH-PALACE-GATE.png` | 宫门惊变与终关 | 宫门、市坊、军营、史馆可拆区域 |
| P1 | `C03-ARCH-HEZHOU-GATE.png` | 借城 | 吊筐、城外灾民营、侧门 |
| P1 | `C03-ARCH-FLOATING-BRIDGE.png` | 夜渡与北伐 | 模块化桥板、承重表现 |
| P1 | `C03-ARCH-WUYUE-GATE.png` | 富城闭门 | 水闸、盐仓、商业街连通 |
| P1 | `C03-ARCH-OLD-CAPITAL.png` | 旧都与登基 | 多阵营长期共存痕迹 |
| P1 | `C03-ARCH-YANYUN-POST.png` | 雪原补给 | 驿站、水源、互市棚 |

## 5. 道具 `assets/props`

| 优先级 | 文件名 | 叙事用途 |
| --- | --- | --- |
| P0 | `C03-PROP-GRAIN-SEAL.png` | 开场官粮封条与最终无封条粮袋对照 |
| P0 | `C03-PROP-SHOE-LEDGER.png` | 沈砺早期私人小纸 |
| P0 | `C03-PROP-FIRST-LEDGER-PAGE.png` | 三名无辜书吏名字，终章证据 |
| P0 | `C03-PROP-TEMP-ROLL.png` | 无籍灾民临时册，赵小满人物线 |
| P0 | `C03-PROP-WHITE-LANTERN-PLAIN.png` | 白炬互助起点 |
| P0 | `C03-PROP-WHITE-LANTERN-PATCHED.png` | 顾重山赠别 |
| P0 | `C03-PROP-WHITE-LANTERN-GILDED.png` | 称王时被国家神圣化 |
| P0 | `C03-PROP-SUNKEN-ROLL.png` | 大泽阵亡、伤残与欠饷证据 |
| P0 | `C03-PROP-UNSTABLE-LIST.png` | 开国不安定者名册 |
| P0 | `C03-PROP-TWO-REGISTERS.png` | 第一章灾民册与第七章新田册并置 |
| P1 | `C03-PROP-FIRST-BANNER.png` | 沈砺第一面未染色军旗 |
| P1 | `C03-PROP-PEI-CAMPAIGN-MAP.png` | 河工图、商路账、军图叠合 |
| P1 | `C03-PROP-FIELD-MARKERS.png` | 田契争议与丈量玩法 |
| P1 | `C03-PROP-PEI-CLAN-ROLL.png` | 裴昭放弃部曲军籍 |
| P1 | `C03-PROP-ALATAN-COVENANT.png` | 互市、军法、自治三项盟约 |

## 6. 第一批建议出图

若本轮只先做 12 张，建议顺序为：

1. `C03-STYLE-KEYART`
2. `C03-CHAR-SHEN-01`
3. `C03-CHAR-SHEN-04`
4. `C03-CHAR-LU-01`
5. `C03-CHAR-HAN-01`
6. `C03-CHAR-HAN-03`
7. `C03-CH01-S03`
8. `C03-CH02-S08`
9. `C03-CH04-S18`
10. `C03-CH05-S25`
11. `C03-CH07-S33`
12. `C03-CH07-S35E`

这 12 张能覆盖主角起终态、核心关系、三次规模跃迁与最终主题回扣。

## 7. 画面禁区

- 不使用真龙、龙椅光环或天降祥瑞证明沈砺合法
- 不把白炬军统一画成邪教或纯红色农民暴徒
- 不把北方多族角色画成同一毛皮蛮族模板
- 不以遍地尸体制造廉价历史厚重感，优先画空碗、破门、旧账和伤残生活
- 不让女性角色服装脱离职业活动需求
- 不把江东画成无忧桃源，富庶必须带仓库、税卡、佃户和水务系统
- 不把终章宫城画成奇幻巨构，压迫感来自尺度、门禁和程序距离
