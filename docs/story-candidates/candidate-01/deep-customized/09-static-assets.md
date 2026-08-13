# 《断冠之誓》静态素材定制清单

正式素材放在 [`../assets`](../assets/)。编号与深化稿一致。`P0` 为首批关键演出，`P1` 为完整主线，`P2` 为支线与氛围。

## 0. 已同步素材对接

现有文件继续保留美术侧命名，剧本用稳定编号引用。当前范围、规格和验收见[美术与素材入口](../ART-ASSETS.md)，原始 HD 映射保留在 [`manifest-hd.json`](../assets/manifest-hd.json)。`draft-v1/` 不进入游戏构建。

| 已同步文件 | 深化编号 | 使用状态 |
| --- | --- | --- |
| [`laiya-18-portrait-hd.png`](../assets/characters/laiya-18-portrait-hd.png) | `C01-CHAR-LEIA-01` | 正式质量标杆，可直接对接第一章 |
| [`roderick-portrait-hd.png`](../assets/characters/roderick-portrait-hd.png) | `C01-CHAR-RODERICK-01` | 正式质量标杆 |
| [`kain-portrait-hd.png`](../assets/characters/kain-portrait-hd.png) | `C01-CHAR-CAIN-01` | 正式质量标杆；保留素材侧 `kain` 拼法 |
| [`mirelle-portrait-hd.png`](../assets/characters/mirelle-portrait-hd.png) | `C01-CHAR-MIREL-01` | 正式质量标杆 |
| [`redstone-oath-tower-hd.png`](../assets/architecture/redstone-oath-tower-hd.png) | `C01-ARCH-REDSTONE` | 正式质量标杆，可扩写受损与控制节点态 |
| [`gray-banner-field-camp-hd.png`](../assets/architecture/gray-banner-field-camp-hd.png) | `C01-ARCH-GRAY-CAMP` | 正式质量标杆；营帐、泥沟、补给与指挥区可直接拆成地块语义 |
| [`gray-flag-over-burned-village-hd.png`](../assets/scenes/gray-flag-over-burned-village-hd.png) | `C01-CH01-S05` | 对应灰旗诞生；后续补人物近景和湿面粉灭火细节 |
| [`twin-hills-first-command-hd.png`](../assets/scenes/twin-hills-first-command-hd.png) | `C01-CH01-S01` | 对应双子丘陵首次独立指挥，已把抢麦、抢高地和支援断裂放入同一镜头 |
| [`bran-portrait-hd.png`](../assets/characters/bran-portrait-hd.png) / [`bran-walk-sheet-hd.png`](../assets/units/bran-walk-sheet-hd.png) | `C01-CHAR-BRAN-01` | 正式头像与四帧单位；短弩、修靴工具、焦痕护腕和幸存者名袋已统一 |
| [`tasha-portrait-hd.png`](../assets/characters/tasha-portrait-hd.png) / [`tasha-walk-sheet-hd.png`](../assets/units/tasha-walk-sheet-hd.png) | `C01-CHAR-TASHA-01` | 正式头像与四帧单位；账本、筹绳、靴牌、工程工具和黄褐军需轮廓已统一 |
| [`three-bridges-river-valley-hd.png`](../assets/architecture/three-bridges-river-valley-hd.png) | `C01-ARCH-THREE-BRIDGES` | 三条分流与三种桥制清楚可数；中桥保留难民通行、白布停火和修复痕迹 |
| [`silverwood-tree-city-hd.png`](../assets/architecture/silverwood-tree-city-hd.png) | `C01-ARCH-SILVERWOOD` | 银皮母树、生活步道、空育儿亭和人类边缘屋已进入同一地点立绘 |
| [`white-river-night-crossing-hd.png`](../assets/scenes/white-river-night-crossing-hd.png) | `C01-CH02-S06` | 对应白河夜渡；粮车、工具车、伤员车和布兰浅滩引路均可在目标尺寸辨认 |
| [`seven-towers-at-dusk-hd.png`](../assets/scenes/seven-towers-at-dusk-hd.png) | `C01-CH07-S33-EST` | 王都终局建立远景；严格保留七座可数主塔、正常运行的城市与多旗联军 |
| [`campaign-props-sheet-02-hd.png`](../assets/props/campaign-props-sheet-02-hd.png) | `C01-PROP-LEFT-GLOVE` / `C01-PROP-EXILE-HARDTACK` / `C01-PROP-CONTROL-FRAGMENT` / `C01-PROP-THREE-PEOPLES-CLASP` | 四格固定顺序：莱娅左手护腕、逃亡硬饼、控制誓文残片、三族盟约扣件；无可读文字与现成纹章 |

## 1. 风格基准 `assets/style`

| 优先级 | 文件名 | 内容 | 要求 |
| --- | --- | --- | --- |
| P0 | `C01-STYLE-KEYART.png` | 莱娅面对王冠与多面军旗 | 王冠在远处，前景是可拆盟约扣，避免天选女王构图 |
| P0 | `C01-STYLE-BORDER-AUTUMN.png` | 灰境边境 | 农田、桥、烟与军旗并存，先有人居后有奇幻 |
| P0 | `C01-STYLE-OATH-LIGHT.png` | 誓火视觉语言 | 意志、名字、见证三层纹理，控制誓文需明显失去拒绝层 |
| P0 | `C01-STYLE-LONG-NIGHT.png` | 七塔灰光 | 灰白、低温、局部设施过亮，避免普通黑暗末日 |
| P1 | `C01-STYLE-ELDER-PEOPLES.png` | 多族文化并置 | 不用统一中世纪欧洲模板 |
| P1 | `C01-STYLE-CAPITAL-ORDER.png` | 王冠王都 | 整洁、安全、秩序真实吸引人，同时保留同步跪拜不安 |

## 2. 角色阶段 `assets/characters`

| 优先级 | 文件名 | 阶段 | 关键提示 |
| --- | --- | --- | --- |
| P0 | `C01-CHAR-LEIA-01.png` | 18 岁见习旗官 | 蓝灰轻甲、护腕穿戴痕迹、无个人纹章 |
| P0 | `C01-CHAR-LEIA-02.png` | 22 岁灰旗女王 | 多族扣件、龙翼徽记开始个人化、左肩伤痕 |
| P0 | `C01-CHAR-LEIA-03.png` | 27 岁联盟统帅 | 无王冠，多旗纹披风，装备偏指挥与防护 |
| P0 | `C01-CHAR-RODERICK-01.png` | 教程导师 | 旧旗、左手手套、实用老骑士装 |
| P0 | `C01-CHAR-RODERICK-02.png` | 王冠军统帅 | 同一装备老化，不做邪恶黑甲 |
| P0 | `C01-CHAR-IVRA-01.png` | 幼龙 | 明确受伤与警惕，不做可爱宠物比例 |
| P0 | `C01-CHAR-IVRA-02.png` | 青年龙 | 可骑乘但无永久鞍具 |
| P0 | `C01-CHAR-IVRA-03.png` | 成年独立指挥官 | 自有盟约标识，无骑手与缰绳 |
| P1 | `C01-CHAR-CAIN-01.png` | 帝国敌将 | 军屯出身、标准化军装、印章袋 |
| P1 | `C01-CHAR-CAIN-02.png` | 独立改革军统帅 | 保留帝国旗，不改穿灰旗制服 |
| P1 | `C01-CHAR-BRAN-01.png` | 苇草村猎人 | 修靴工具、猎具、无刺客贵族感 |
| P1 | `C01-CHAR-BRAN-02.png` | 自由斥候指挥官 | 保留村庄布条与幸存者名单袋 |
| P1 | `C01-CHAR-MIREL-01.png` | 教廷守墓人 | 工整名册、净化印、克制宗教装 |
| P1 | `C01-CHAR-MIREL-02.png` | 最后誓约建立者 | 净化印拆分为公共工具 |
| P1 | `C01-CHAR-TASHA-01.png` | 佣兵首领 | 靴牌、账本、工程工具 |
| P1 | `C01-CHAR-NOBANNER-01.png` | 不稳定聚忆体 | 多轮廓、多嘴型、拒绝单一骷髅王模板 |
| P1 | `C01-CHAR-NOBANNER-02.png` | 协调者 | 仍保留复数差异，不变成正常单人幽灵 |
| P1 | `C01-CHAR-SEVERIN.png` | 摄政王 | 朴素政治服、疲惫、无享乐暴君符号 |
| P2 | `C01-CHAR-ADA-01.png` | 桥上 8 岁孩子 | 双文字药瓶 |
| P2 | `C01-CHAR-ADA-02.png` | 王都医师 | 两国标签习惯、塔疗手套 |
| P2 | `C01-CHAR-TORREN-01.png` | 北丘士兵 | 腿伤与旧洛恩装备 |
| P2 | `C01-CHAR-TORREN-02.png` | 旧旗军官 | 罗德里克手套保管者 |

## 3. 关键场景 `assets/scenes`

| 优先级 | 文件名 | 场景 | 构图重点 |
| --- | --- | --- | --- |
| P0 | `C01-CH01-S00.png` | 罗德里克系护腕 | 亲密通过手部动作表达 |
| P0 | `C01-CH01-S02.png` | 三桥停火 | 两军放平武器，平民穿过，远处无标弩车 |
| P0 | `C01-CH01-S05.png` | 灰旗诞生 | 烟熏旗布、湿面粉灭火、撤离村民 |
| P0 | `C01-CH02-S09.png` | 伊芙拉断缰 | 幼龙、断链、三类物资车 |
| P0 | `C01-CH03-S16.png` | 无旗者形成 | 多种姓名与军旗碎片，不做单一 Boss 站姿 |
| P0 | `C01-CH04-S19.png` | 龙眠共同逃生 | 骑乘发生在撤离动作中，不做驯龙加冕 |
| P0 | `C01-CH05-S24.png` | 第一次命令后的安静 | 医师、士兵、平民、亡者同时停止动作 |
| P0 | `C01-CH05-S26.png` | 灰誓议会与灰夜 | 多厅代表、防守、七塔灰光同框 |
| P0 | `C01-CH06-S32.png` | 联盟失联后自行行动 | 四战区独立完成目标 |
| P0 | `C01-CH07-S34.png` | 万人跪拜之城 | 医院与粮仓正常运作，街道同步跪拜 |
| P0 | `C01-CH07-S36E.png` | 最后议会与无骑龙影 | 吵闹议会在内，伊芙拉独飞在外 |
| P1 | `C01-CH02-S07.png` | 洛岬三方治理 | 粮仓、城门、面包房三角关系 |
| P1 | `C01-CH03-S11.png` | 墓园姓名木片 | 活人腰牌与亡者碑名呼应 |
| P1 | `C01-CH03-S14.png` | 山炉共同修炉 | 对立派别共同守结构节点 |
| P1 | `C01-CH04-S17.png` | 白旗会议混战 | 代表保留独立旗帜，不围绕莱娅排布 |
| P1 | `C01-CH05-S22.png` | 安瑟尔秩序街道 | 安全、医疗、跪拜三种感受并存 |
| P1 | `C01-CH06-S29.png` | 最后誓约 | 个体亡者逐一确认，不做大军宣誓 |
| P1 | `C01-CH07-S33.png` | 王都多旗破城 | 城墙挂多面旗，视觉刻意不统一 |

## 4. 建筑与地点 `assets/architecture`

| 优先级 | 文件名 | 地点 | 变体要求 |
| --- | --- | --- | --- |
| P0 | `C01-ARCH-TWIN-HILLS.png` | 双子丘陵 | 第 0 年晒麦版、第 4 年纪念石版 |
| P0 | `C01-ARCH-THREE-BRIDGES.png` | 三桥河谷 | 难民停火、后期修复痕迹 |
| P0 | `C01-ARCH-REDSTONE.png` | 赤石堡垒与塔 | 控制节点、观察营、王都回声变体 |
| P0 | `C01-ARCH-VEINPORT.png` | 维恩港 | 起义、治理危机、议会三阶段 |
| P0 | `C01-ARCH-CAPITAL.png` | 阿斯塔里亚王都 | 外城三路、内城功能、王宫中枢 |
| P1 | `C01-ARCH-LOAK-BAKERY.png` | 洛岬玛尔塔面包房 | 逃亡硬饼、繁荣灰翼饼、粮荒价牌 |
| P1 | `C01-ARCH-FORGE-CITY.png` | 山炉炉城 | 熔炉、档案炉、可修结构 |
| P1 | `C01-ARCH-SILVERWOOD.png` | 银林 | 育儿屋、母树、人类边缘村庄 |
| P1 | `C01-ARCH-ANSEL-TOWER.png` | 跪下的城市 | 控制、治疗、净水线路可视拆分 |
| P1 | `C01-ARCH-BONE-TOWER.png` | 白骨军塔 | 姓名索引、个体确认区 |

## 5. 道具 `assets/props`

| 优先级 | 文件名 | 用途 |
| --- | --- | --- |
| P0 | `C01-PROP-LEFT-GLOVE.png` | 师徒关系与旧旗终战回扣 |
| P0 | `C01-PROP-ADA-MEDICINE.png` | 两国文字、桥上孩子与王都医师 |
| P0 | `C01-PROP-FIRST-GREY-FLAG.png` | 烟熏旧布、未拆净蓝线 |
| P0 | `C01-PROP-IVRA-BROKEN-BRIDLE.png` | 自由跟随与控制差异 |
| P0 | `C01-PROP-BROKEN-DRAGON-CLASP.png` | 第一次王冠命令后盟约破裂 |
| P0 | `C01-PROP-EXPIRING-MANDATE.png` | 可拆战时统帅期限扣 |
| P1 | `C01-PROP-NAME-TAGS.png` | 墓园活人木片与亡者姓名 |
| P1 | `C01-PROP-RETURNED-TRUE-NAME.png` | 不可朗诵的古龙真名载体 |
| P1 | `C01-PROP-MERCENARY-BOOTS.png` | 塔莎抚恤与身份凭证 |
| P1 | `C01-PROP-BREAD-PRICE-BOARD.png` | 九年粮价生活线 |
| P1 | `C01-PROP-GREY-OATH-ARTICLES.png` | 可质疑、退出、公开伤亡规则 |
| P1 | `C01-PROP-FREE-OATH-STONES.png` | 多形状、互相制约、不可组合为王冠 |

## 6. 首批建议出图

1. `C01-STYLE-KEYART`
2. `C01-CHAR-LEIA-01`
3. `C01-CHAR-LEIA-03`
4. `C01-CHAR-RODERICK-01`
5. `C01-CHAR-IVRA-01`
6. `C01-CHAR-IVRA-03`
7. `C01-CH01-S05`
8. `C01-CH04-S19`
9. `C01-CH05-S24`
10. `C01-CH07-S34`
11. `C01-CH07-S36E`
12. `C01-PROP-LEFT-GLOVE`

## 7. 视觉禁区

- 不把莱娅画成隐藏王族或天选女王
- 不让伊芙拉长期佩戴鞍、缰绳或灰旗纹章
- 不把精灵统一画成高贵白衣，把荒原诸部统一画成野蛮兽群
- 不把亡者全部画成骷髅或蓝色透明人
- 不用黑化铠甲表达罗德里克、凯恩等理念变化
- 不让王冠一眼就是邪恶尖刺，应当庄严、实用、令人信任
- 灰誓结局不做安静大一统画面，保留多旗、多语言和争吵的视觉复杂性
