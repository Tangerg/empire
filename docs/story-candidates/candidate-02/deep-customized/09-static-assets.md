# 《群星熄灭之前》静态素材定制清单

正式素材放在 [`../assets`](../assets/)。编号与深化稿一致。`P0` 为关键叙事资源，`P1` 为完整主线，`P2` 为生活与支线扩充。

## 0. 已同步素材对接

现有文件继续保留美术侧命名，剧本使用稳定编号。当前范围、规格和验收见[美术与素材入口](../ART-ASSETS.md)，原始 HD 映射保留在 [`manifest-hd.json`](../assets/manifest-hd.json)。`draft-v1/` 不进入游戏构建。

| 已同步文件 | 深化编号 | 使用状态 |
| --- | --- | --- |
| [`mira-portrait-hd.png`](../assets/characters/mira-portrait-hd.png) | `C02-CHAR-MIRA-01` | 正式质量标杆，可直接对接第一章 |
| [`roan-portrait-hd.png`](../assets/characters/roan-portrait-hd.png) | `C02-CHAR-ROAN-01` | 正式质量标杆；风险腕带、旧灾害官外套与撤离设备均已落实 |
| [`naim-portrait-hd.png`](../assets/characters/naim-portrait-hd.png) | `C02-CHAR-NAIM-01` | 正式质量标杆；完整露面、爆破盒与盗接线路体现责任线 |
| [`talos-7-portrait-hd.png`](../assets/characters/talos-7-portrait-hd.png) | `C02-CHAR-TALOS` | 正式质量标杆；方头、多代维修板与单条状态灯形成非人轮廓 |
| [`zero-rain-tower-hd.png`](../assets/architecture/zero-rain-tower-hd.png) | `C02-ARCH-RAIN-TOWER` | 正式正常态；黑雨、地方共管仍作为后续变体 |
| [`farlight-cargo-ship-hd.png`](../assets/architecture/farlight-cargo-ship-hd.png) | `C02-ARCH-FARLIGHT-01` | 正式初期货船态；货舱、床位和维修工作台已有可读层次 |
| [`season-without-rain-hd.png`](../assets/scenes/season-without-rain-hd.png) | `C02-CH01-S00` | 正式赫沙失雨场景标杆 |
| [`seven-minute-rain-hd.png`](../assets/scenes/seven-minute-rain-hd.png) | `C02-CH01-S04` | 正式七分钟雨；雨牧者、轨道伤痕、接水与不可饮用同框 |
| [`helo-portrait-hd.png`](../assets/characters/helo-portrait-hd.png) / [`helo-walk-sheet-hd.png`](../assets/units/helo-walk-sheet-hd.png) | `C02-CHAR-HELUO` | 正式头像与四帧单位；民用船长旧夹克、货运许可、工具和载量思维已落实 |
| [`iya-portrait-hd.png`](../assets/characters/iya-portrait-hd.png) / [`iya-walk-sheet-hd.png`](../assets/units/iya-walk-sheet-hd.png) | `C02-CHAR-IYA` | 正式头像与四帧单位；自选民用衣着、窄投影边与本地关闭控件体现私人边界 |
| [`soler-archive-monastery-hd.png`](../assets/architecture/soler-archive-monastery-hd.png) | `C02-ARCH-SOLER-ARCHIVE` | 正式建筑；冷外层、暖内核、档案劳动和热源阶层可直接用于第三章 |
| [`kairon-ring-node-hd.png`](../assets/architecture/kairon-ring-node-hd.png) | `C02-ARCH-RING-CITY` | 正式环都节点；轨道、分层权限门、内环洁净面和外环维修面同体 |
| [`farlight-departure-hd.png`](../assets/scenes/farlight-departure-hd.png) | `C02-CH02-S09` | 正式起飞场景；难民、有限舱位、未装物资和赫沙沙暴同时可读 |
| [`folding-table-covenant-hd.png`](../assets/scenes/folding-table-covenant-hd.png) | `C02-CH06-S30` | 正式星约场景；折叠桌五界材料、独立接口、伊娅门与拥挤生活舱已落实 |
| [`campaign-props-sheet-02-hd.png`](../assets/props/campaign-props-sheet-02-hd.png) | `C02-PROP-HESHA-CUP` / `C02-PROP-ROAN-RISK-LIST` / `C02-PROP-TWO-TICKETS` / `C02-PROP-IYA-DOOR` | 正式四格图集；依次为金属杯、风险清单、两张撤离票、可关闭门控件 |

## 1. 风格基准 `assets/style`

| 优先级 | 文件名 | 内容 | 关键要求 |
| --- | --- | --- | --- |
| P0 | `C02-STYLE-KEYART.png` | 远灯号驶向正在熄灭的弥光 | 前景可见拥挤生活舱，不做纯飞船海报 |
| P0 | `C02-STYLE-HESHA.png` | 赤砂镇与水循环 | 光热强、阴影珍贵、所有建筑可见水封 |
| P0 | `C02-STYLE-ECHO-TIDE.png` | 寂潮视觉语言 | 由生活动作、名字和城市片段叠合，不做紫色能量恶魔 |
| P0 | `C02-STYLE-DARK-STARS.png` | 弥光熄灭后 | 先有能源恐慌与应急灯，再有星空美感 |
| P1 | `C02-STYLE-SOLER.png` | 索勒热灯与蓝冰 | 宗教空间与维护设施重叠 |
| P1 | `C02-STYLE-NEREIA.png` | 潮城 | 多层浮台、深海城与边缘浮城差异 |
| P1 | `C02-STYLE-VERDANT.png` | 维尔达 | 活体结构与人类城市共存，不做无人森林 |
| P1 | `C02-STYLE-KAIRON.png` | 凯隆环城 | 清洁繁荣与权限层级同框 |

## 2. 角色 `assets/characters`

| 优先级 | 文件名 | 阶段 | 关键提示 |
| --- | --- | --- | --- |
| P0 | `C02-CHAR-MIRA-01.png` | 19 岁见习巡猎士 | 工具磨损、便携终端、无天选接口外观 |
| P0 | `C02-CHAR-MIRA-02.png` | 20 岁远灯号领队 | 多世界工具混合、执照背面记录 |
| P0 | `C02-CHAR-MIRA-03.png` | 21 岁星约巡行员 | 无中央领袖礼服，可拆权限模块 |
| P0 | `C02-CHAR-ROAN-01.png` | 巡猎导师 | 风险腕带、旧环庭徽章、实用防护服 |
| P0 | `C02-CHAR-ROAN-02.png` | 归零代行者 | 身体与投影差异，避免神化白袍 |
| P0 | `C02-CHAR-FIREFLY.png` | 萤火 | 小型维修磨损、三声信号灯、不做过度萌化 |
| P1 | `C02-CHAR-HELUO.png` | 船长 | 货运标签、旧逐光许可、工具腰带 |
| P1 | `C02-CHAR-NAIM-01.png` | 返星会指挥 | 实用爆破装，不做纯酷黑甲 |
| P1 | `C02-CHAR-NAIM-02.png` | 受审后的行动员 | 去除指挥标志，保留维修班表 |
| P1 | `C02-CHAR-TALOS.png` | 七号守墓人 | 多次维修层、编号清楚、非光滑新机 |
| P1 | `C02-CHAR-OLO.png` | 潮汐祭司 | 维护手势与宗教饰物一体 |
| P1 | `C02-CHAR-KOTA.png` | 共生者 | 多物种接口，不让植物只作服装装饰 |
| P1 | `C02-CHAR-ISHAN.png` | 模型分析员 | 数据标注层、无冷酷反派式极简造型 |
| P1 | `C02-CHAR-IYA.png` | 留名者投影 | 可主动改变衣着与亮度，拥有门与私人空间 |
| P2 | `C02-CHAR-SADI.png` | 赫沙医师到地方指挥 | 医疗工具逐渐增加节点钥匙 |
| P2 | `C02-CHAR-LANKO.png` | 赤砂镇孩子/见习维护员 | 同一只金属杯贯穿 |
| P2 | `C02-CHAR-MISA-FAMILY.png` | 凯隆外环家庭 | 维修工生活服、折叠桌构件 |

### 生物与大型单位

| 优先级 | 文件名 | 用途 | 关键提示 |
| --- | --- | --- | --- |
| P0 | `C02-CREATURE-RAIN-SHEPHERD.png` | 第四关雨牧者本体与行动关节拆分 | 古代巨型气候维护体；压毁建筑与恢复季雨必须同时可读，不做纯 Boss 怪兽 |

## 3. 关键场景 `assets/scenes`

| 优先级 | 文件名 | 场景 | 构图重点 |
| --- | --- | --- | --- |
| P0 | `C02-CH01-S00.png` | 漏水阀与金属杯 | 米拉修滤网、岚柯接水、罗安检查备件 |
| P0 | `C02-CH01-S04.png` | 雨牧者与七分钟雨 | 巨型维护体、轨道炮、满街容器 |
| P0 | `C02-CH02-S09.png` | 远灯号起飞 | 难民货舱、有限床位、赫沙半球沙暴 |
| P0 | `C02-CH03-S15.png` | 生长城墙 | 人类城市、森林节点、除草剂机同框 |
| P0 | `C02-CH04-S19.png` | 五界广播 | 五座中继与责任标签，不做英雄演讲特写 |
| P0 | `C02-CH05-S25.png` | 伊娅获得门 | 投影室、关闭开关、众人学会敲门 |
| P0 | `C02-CH06-S30.png` | 折叠桌星约 | 五界材料组成的桌，各代表保留独立接口 |
| P0 | `C02-CH07-S33.png` | 倒悬城低权限维护 | 萤火、铆钉、旋转重力与普通维护井 |
| P0 | `C02-CH07-S35E.png` | 最后一场雨 | 先测量过滤再饮用，熄灭弥光与星空 |
| P1 | `C02-CH03-S11.png` | 索勒冰海信标 | 热灯链、冰下机械、潮汐术 |
| P1 | `C02-CH04-S17.png` | 百分之二船坞 | 造船工、胚胎库、评分名单、私人名额 |
| P1 | `C02-CH04-S20.png` | 坠环分队 | 外环居民、轨道列车、环城分离 |
| P1 | `C02-CH05-S22.png` | 前世城市节日 | 门牌、成年名字、四回合重置痕迹 |
| P1 | `C02-CH06-S26.png` | 返回赫沙 | 旧地图被沙掩、地方共管零号塔 |

## 4. 建筑与空间 `assets/architecture`

| 优先级 | 文件名 | 空间 | 变体 |
| --- | --- | --- | --- |
| P0 | `C02-ARCH-RAIN-TOWER.png` | 零号雨塔 | 正常、黑雨、地方共管三态 |
| P0 | `C02-ARCH-FARLIGHT-01.png` | 远灯号初始 | 货船、私有钥匙、空货舱 |
| P0 | `C02-ARCH-FARLIGHT-02.png` | 远灯号中期 | 难民床位、活土、投影室、折叠桌 |
| P0 | `C02-ARCH-FARLIGHT-03.png` | 远灯号终章 | 星脉失效后的物理系统与公开货舱 |
| P0 | `C02-ARCH-RING-CITY.png` | 凯隆环城 | 核心、外环、列车、分离锁 |
| P0 | `C02-ARCH-INVERTED-CITY.png` | 弥光倒悬城 | 重力旋转、维护井、织母中枢 |
| P1 | `C02-ARCH-SOLER-ARCHIVE.png` | 索勒档案 | 热源层级、蓝冰、普通生活遗迹 |
| P1 | `C02-ARCH-NEREIA-FLOAT.png` | 涅瑞亚浮城 | 可移动表决船与潮汐锚 |
| P1 | `C02-ARCH-VERDANT-LIVING-CITY.png` | 维尔达 | 活桥、居民区、菌群与母树接口 |
| P1 | `C02-ARCH-UKER-CITY.png` | 厄客前世城 | 节日生活、转写中心、重置循环 |

## 5. 道具 `assets/props`

| 优先级 | 文件名 | 用途 |
| --- | --- | --- |
| P0 | `C02-PROP-HESHA-CUP.png` | 五界水与最后自然雨 |
| P0 | `C02-PROP-ROAN-RISK-LIST.png` | 关爱、控制与最终四层审查 |
| P0 | `C02-PROP-TWO-TICKETS.png` | 赫洛私人名额与公开撤离规则 |
| P0 | `C02-PROP-IYA-DOOR.png` | 死者隐私与关闭权 |
| P0 | `C02-PROP-COVENANT-TABLE.png` | 外环家庭餐桌到星约会议桌 |
| P1 | `C02-PROP-MIRA-LICENSE.png` | 执照背面问题与地方公会编号 |
| P1 | `C02-PROP-RAIN-CORE.png` | 雨牧者控制核与证据校验 |
| P1 | `C02-PROP-TALOS-WILLS.png` | 三份互相冲突遗书 |
| P1 | `C02-PROP-PUBLIC-CARGO-LIST.png` | 船长私产转公共应急规则 |
| P1 | `C02-PROP-LIVING-ISOLATION.png` | 维尔达隔离网的小规模原型 |
| P1 | `C02-PROP-MAINTENANCE-KEY.png` | 萤火与铆钉低权限民用密钥 |

## 6. 首批建议出图

1. `C02-STYLE-KEYART`
2. `C02-CHAR-MIRA-01`
3. `C02-CHAR-MIRA-03`
4. `C02-CHAR-ROAN-01`
5. `C02-CHAR-TALOS`
6. `C02-CHAR-IYA`
7. `C02-CH01-S04`
8. `C02-CH02-S09`
9. `C02-CH04-S19`
10. `C02-CH06-S30`
11. `C02-CH07-S35E`
12. `C02-PROP-HESHA-CUP`

## 7. 视觉禁区

- 不把每颗行星画成单一地貌与单一民族
- 不用蓝色全息界面覆盖所有文明的本地设计
- 不把寂潮画成纯黑暗怪物，应保留家庭、街道和劳动动作
- 不把罗安代行者形态神化或邪恶化
- 不让伊娅永远半透明白衣，她可主动选择投影样式
- 不把珂塔画成植物装饰的人类精灵，必须体现多物种接口
- 不让远灯号始终整洁空旷，生活痕迹和空间冲突应逐章增加
- 弥光熄灭后先显示黑暗与应急，再显示浪漫星空
