#!/usr/bin/env python3
"""Generate and validate the complete C01 atlas expansion.

The existing formal HD files are never overwritten. New deterministic pixel art
is written below assets/complete/, then indexed together with the accepted
existing narrative topics in manifest-complete.json.
"""

from __future__ import annotations

import hashlib
import json
import math
from collections import Counter
from pathlib import Path
from xml.etree import ElementTree as ET

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "manifest-complete.json"
QA_JSON = ROOT / "qa-complete.json"

CATEGORY_TARGETS = {
    "narrative-static": 80,
    "combat-unit": 40,
    "mission-unit": 24,
    "faction-kit": 12,
    "terrain": 32,
    "interactive-structure": 24,
    "battle-prop": 32,
    "equipment": 48,
    "skill": 48,
    "status": 24,
    "fx": 24,
    "hud": 16,
}

PAL = {
    "sky": "#303746",
    "sky2": "#414a5a",
    "far": "#5a6570",
    "ink": "#171b23",
    "ink2": "#262c37",
    "stone": "#6f747c",
    "stone_hi": "#9ca0a5",
    "ground": "#4b433e",
    "amber": "#d5a14d",
    "red": "#824344",
    "blue": "#3e5872",
    "gray": "#7b7d80",
    "cloth": "#c2b8a7",
    "green": "#51684d",
    "green_hi": "#74866b",
    "purple": "#66536f",
    "water": "#405c70",
    "water_hi": "#71889a",
    "rust": "#8a5a3f",
    "bone": "#b7ad94",
    "white": "#d9d5ca",
    "black": "#0d1016",
}

ACCENTS = [PAL["amber"], PAL["red"], PAL["blue"], PAL["green"], PAL["purple"], PAL["rust"]]


def item(identifier: str, label: str) -> dict[str, str]:
    return {"id": identifier, "label": label}


EXISTING_TOPICS = [
    item("C01-CHAR-LEIA-01", "莱娅 18 岁见习旗官"),
    item("C01-CHAR-RODERICK-01", "罗德里克教程导师"),
    item("C01-CHAR-CAIN-01", "凯恩帝国敌将"),
    item("C01-CHAR-MIREL-01", "米蕾尔教廷守墓人"),
    item("C01-CHAR-BRAN-01", "布兰苇草村猎人"),
    item("C01-CHAR-TASHA-01", "塔莎佣兵首领"),
    item("C01-ARCH-REDSTONE", "赤石誓约烽塔"),
    item("C01-ARCH-GRAY-CAMP", "灰旗野战营"),
    item("C01-ARCH-THREE-BRIDGES", "三桥河谷"),
    item("C01-ARCH-SILVERWOOD", "银林树城"),
    item("C01-CH01-S05", "焚村后的第一面灰旗"),
    item("C01-CH01-S01", "双子丘陵初次指挥"),
    item("C01-CH02-S06", "白河夜渡"),
    item("C01-CH07-S33-EST", "七塔王城终局远景"),
    item("C01-PROP-FIRST-GREY-FLAG", "第一面灰旗"),
    item("C01-PROP-FREE-OATH-STONE", "自由誓石"),
    item("C01-PROP-CROWN-FRAGMENT", "誓约王冠碎片"),
    item("C01-PROP-DRAGON-ALLIANCE-CLASP", "巨龙盟约扣"),
    item("C01-PROP-LEFT-GLOVE", "莱娅左手护腕"),
    item("C01-PROP-EXILE-HARDTACK", "逃亡硬饼"),
    item("C01-PROP-CONTROL-FRAGMENT", "控制誓文残片"),
    item("C01-PROP-THREE-PEOPLES-CLASP", "三族盟约扣件"),
]

STATIC_EXPANDED = [
    item("C01-STYLE-KEYART", "莱娅面对王冠与多面军旗"),
    item("C01-STYLE-BORDER-AUTUMN", "灰境边境秋季视觉基准"),
    item("C01-STYLE-OATH-LIGHT", "誓火视觉语言"),
    item("C01-STYLE-LONG-NIGHT", "七塔长夜灰光"),
    item("C01-STYLE-ELDER-PEOPLES", "古老诸族文化并置"),
    item("C01-STYLE-CAPITAL-ORDER", "阿斯塔里亚王都秩序"),
    item("C01-CHAR-LEIA-02", "莱娅 22 岁灰旗女王"),
    item("C01-CHAR-LEIA-03", "莱娅 27 岁联盟统帅"),
    item("C01-CHAR-RODERICK-02", "罗德里克王冠军统帅"),
    item("C01-CHAR-IVRA-01", "伊芙拉幼龙"),
    item("C01-CHAR-IVRA-02", "伊芙拉青年龙"),
    item("C01-CHAR-IVRA-03", "伊芙拉成年指挥官"),
    item("C01-CHAR-CAIN-02", "凯恩独立改革军统帅"),
    item("C01-CHAR-BRAN-02", "布兰自由斥候指挥官"),
    item("C01-CHAR-MIREL-02", "米蕾尔最后誓约建立者"),
    item("C01-CHAR-NOBANNER-01", "无旗者不稳定聚忆体"),
    item("C01-CHAR-NOBANNER-02", "无旗者协调者"),
    item("C01-CHAR-SEVERIN", "摄政王塞维恩"),
    item("C01-CHAR-ADA-01", "艾达 8 岁桥上孩子"),
    item("C01-CHAR-ADA-02", "艾达 17 岁王都医师"),
    item("C01-CHAR-TORREN-01", "托伦北丘士兵"),
    item("C01-CHAR-TORREN-02", "托伦旧旗军官"),
    item("C01-CH01-S00", "罗德里克系护腕"),
    item("C01-CH01-S02", "三桥停火"),
    item("C01-CH02-S09", "伊芙拉断缰"),
    item("C01-CH03-S16", "无旗者形成"),
    item("C01-CH04-S19", "龙眠共同逃生"),
    item("C01-CH05-S24", "第一次命令后的安静"),
    item("C01-CH05-S26", "灰誓议会与灰夜"),
    item("C01-CH06-S32", "联盟失联后自行行动"),
    item("C01-CH07-S34", "万人跪拜之城"),
    item("C01-CH07-S36E", "最后议会与无骑龙影"),
    item("C01-CH03-S11", "墓园姓名木片"),
    item("C01-CH03-S14", "山炉共同修炉"),
    item("C01-CH05-S22", "安瑟尔秩序街道"),
    item("C01-CH06-S29", "最后誓约逐一确认"),
    item("C01-SCENE-PUBLIC-RATIONS", "灰旗营地公开分粮"),
    item("C01-SCENE-REFUGEE-BRIDGEWORK", "难民与工程师共同修桥"),
    item("C01-SCENE-NURSERY-DEBATE", "银林育儿屋争议"),
    item("C01-SCENE-ARCHIVE-FORGE-OPEN", "山炉档案炉开封"),
    item("C01-SCENE-OLD-FLAG-HOMECOMING", "旧旗士兵归乡"),
    item("C01-ARCH-TWIN-HILLS", "双子丘陵战斗地点"),
    item("C01-ARCH-VEINPORT", "维恩港三阶段治理区"),
    item("C01-ARCH-CAPITAL", "阿斯塔里亚王都"),
    item("C01-ARCH-LOAK-BAKERY", "洛岬玛尔塔面包房"),
    item("C01-ARCH-FORGE-CITY", "山炉炉城"),
    item("C01-ARCH-ANSEL-TOWER", "安瑟尔控制治疗塔"),
    item("C01-ARCH-BONE-TOWER", "白骨军塔"),
    item("C01-ARCH-FIELD-HOSPITAL", "圣辉野战医院"),
    item("C01-ARCH-MERCENARY-MARKET", "佣兵补给市集"),
    item("C01-ARCH-DRAGON-COVENANT-RUIN", "古龙盟约遗址"),
    item("C01-PROP-ADA-MEDICINE", "艾达双文字药瓶"),
    item("C01-PROP-IVRA-BROKEN-BRIDLE", "伊芙拉断裂缰具"),
    item("C01-PROP-EXPIRING-MANDATE", "可拆战时统帅期限扣"),
    item("C01-PROP-NAME-TAGS", "墓园活人木片与亡者姓名牌"),
    item("C01-PROP-CASUALTY-REGISTER", "普通士兵伤亡册"),
    item("C01-PROP-GRANARY-KEY", "三方共管粮仓钥匙"),
    item("C01-PROP-EVACUATION-MAP", "多语言撤离路线图"),
]

COMBAT = [
    ("SWORDSMAN", "剑士"), ("SPEARMAN", "枪兵"), ("ARCHER", "弓箭手"),
    ("CLERIC", "牧师"), ("ENGINEER", "工程师"), ("BANNER-GUARD", "旗卫"),
    ("LEGION-SHIELD", "军团盾卫"), ("SILVER-LONGBOW", "长弓守卫"),
    ("RUNE-ARTIFICER", "符文工匠"), ("WOLF-RIDER", "狼骑兵"),
    ("GRAVEKEEPER", "守墓人"), ("SKELETON-GUARD", "骸骨卫士"),
    ("RANGER", "游侠"), ("ASSASSIN", "刺客"), ("MAGE", "法师"),
    ("KNIGHT", "骑士"), ("BALLISTA", "弩车"), ("LANCE-CAVALRY", "长枪骑兵"),
    ("BATTLE-MAGE", "战斗法师"), ("EAGLE-SCOUT", "鹰骑斥候"),
    ("WOODLAND-WALKER", "林地行者"), ("DRUID", "德鲁伊"),
    ("WHITE-STAG-RIDER", "白鹿骑手"), ("RUNE-SHIELD", "符文盾卫"),
    ("AXE-BREAKER", "战斧兵"), ("STONE-GOLEM", "石魔像"),
    ("SHAMAN", "萨满"), ("JAVELIN-HUNTER", "投矛猎手"),
    ("HEAVY-KNIGHT", "重装骑士"), ("SPIRIT-FIRE", "灵火精怪"),
    ("CANNON-WAGON", "火炮车"), ("TROLL", "巨魔"),
    ("BERSERKER", "狂战士"), ("TEMPLAR", "圣殿骑士"),
    ("INQUISITOR", "审判官"), ("GHOST", "幽魂"),
    ("CEMETERY-COLOSSUS", "墓地巨像"), ("IVRA-GROWTH", "伊芙拉成长单位"),
    ("WYVERN-RIDER", "飞龙骑手"), ("ANCIENT-DRAGON", "古龙"),
]

MISSION = [
    ("BORDER-FARMER", "边境农户"), ("REFUGEE-ADULT", "难民成人"),
    ("REFUGEE-CHILD", "难民儿童"), ("EVACUATION-DRIVER", "撤离车夫"),
    ("BAKER", "面包师"), ("MINER", "采矿工"), ("FORGE-ARTISAN", "炉城工匠"),
    ("BRIDGE-LABORER", "桥梁劳工"), ("RELIEF-COOK", "救济厨工"),
    ("CITY-DOCTOR", "城市医师"), ("WOUNDED", "伤员"), ("WALL-LABORER", "城墙劳工"),
    ("SCRIBE", "抄写员"), ("NAME-REGISTRAR", "姓名登记员"),
    ("COUNCIL-DELEGATE", "地方议会代表"), ("FIELD-MESSENGER", "战地传令员"),
    ("CARAVAN-ESCORT", "商团护运员"), ("CEMETERY-CARETAKER", "墓园看守"),
    ("TOWN-MILITIA", "城镇民兵"), ("MERCENARY-QUARTERMASTER", "佣兵军需员"),
    ("CONTROLLED-SOLDIER", "被控制的普通士兵"), ("RELEASED-VETERAN", "解除控制的退伍兵"),
    ("NURSERY-CARETAKER", "森林育儿屋照护者"), ("DETAINED-WITNESS", "被押证人"),
]

FACTIONS = [
    ("LORNE", "洛恩王国装备基准"), ("VILSA", "维尔萨帝国装备基准"),
    ("HOLY-LIGHT", "圣辉教廷装备基准"), ("GRAY-BANNER", "灰旗自由领装备基准"),
    ("SILVERWOOD", "银林装备基准"), ("MOUNTAIN-FORGE", "山炉装备基准"),
    ("WASTELAND", "荒原诸部装备基准"), ("MILITIA-MERCENARY", "城市民兵与佣兵装备基准"),
    ("DRAGON-COVENANT", "巨龙盟约装备基准"), ("OATH-CONTROLLED", "誓文受控态基准"),
    ("NAMED-DEAD", "归名亡者态基准"), ("LATE-ALLIANCE", "后期联盟态基准"),
]

TERRAIN_GROUPS = [
    ("BORDER", ["晒麦农田", "雨泥道路", "焦土农田", "难民车辙"]),
    ("RIVER", ["河岸", "浅水", "桥头地面", "断桥边缘"]),
    ("CAPITAL", ["城墙步道", "王都街道", "医院区地面", "宫城地面"]),
    ("SILVERWOOD", ["普通林地", "母树根区", "育儿屋边缘", "隐蔽草丛"]),
    ("FORGE", ["炉城石地", "熔沟", "符文地面", "档案炉区"]),
    ("WASTELAND", ["风蚀土", "岩坡", "部族营地", "风暴危险区"]),
    ("GRAVEYARD", ["普通墓碑地", "记名区", "污染地", "归名净化区"]),
    ("OATHLIGHT", ["普通节点地", "誓文受控区", "失联区", "自由誓石区"]),
]

STRUCTURES = [
    ("GRAY-FLAG-POINT", "灰旗旗点"), ("LORNE-KEEP", "洛恩堡垒据点"),
    ("OATH-TOWER-CONSOLE", "誓约塔控制台"), ("CAPITAL-GATE", "王都宫门"),
    ("JOINT-GRANARY", "三方共管粮仓"), ("FORGE-WORKSHOP", "山炉工坊"),
    ("HOLY-MEDICINE-SHED", "圣辉药棚"), ("MERCENARY-DEPOT", "佣兵补给仓"),
    ("THREE-BRIDGE-SPAN", "三桥桥段"), ("SUNKEN-BELL-SLUICE", "沉钟水闸"),
    ("WALL-LIFT-GATE", "城墙升降门"), ("OLD-CITY-TUNNEL", "旧城地道口"),
    ("REPAIRABLE-GATE", "可修城门"), ("BALLISTA-EMPLACEMENT", "弩车部署位"),
    ("FORGE-GUN-POSITION", "山炉炮位"), ("WASTELAND-CHEVAUX", "荒原拒马"),
    ("FIELD-HOSPITAL", "圣辉野战医院"), ("REFUGEE-SHELTER", "难民避难棚"),
    ("PUBLIC-RATION-POINT", "公开分粮点"), ("WOUNDED-SHELTER", "伤兵棚"),
    ("ARCHIVE-FURNACE", "山炉档案炉"), ("NAME-MEMORIAL", "墓园姓名碑"),
    ("GRAY-OATH-NOTICE", "灰誓公开告示"), ("MILITARY-REGISTRY", "军籍登记点"),
]

BATTLE_PROP_GROUPS = [
    ("COVER", ["木箱堆", "临时盾墙", "风蚀岩", "翻倒粮车"]),
    ("HAZARD", ["油桶与火盆", "攻城火药", "失控誓石", "亡者污染残片"]),
    ("LOGISTICS", ["粮袋车", "水桶架", "备件架", "战地药箱"]),
    ("ENGINEERING", ["木桩", "量绳", "桥板", "修塔脚手架"]),
    ("LIFE", ["营地炊具", "难民床铺", "议会折叠桌", "晾晒衣物"]),
    ("EVIDENCE", ["伤亡名册", "公开告示", "军籍牌", "商团货单"]),
    ("FACTION", ["灰旗组", "洛恩盾标", "维尔萨道路牌", "圣辉状态灯"]),
    ("AFTERMATH", ["焦木废墟", "弃械堆", "伤员分区", "共同修复痕迹"]),
]

SHARED_EQUIPMENT = [
    ("FIELD-MEDICAL-KIT", "战地治疗包"), ("BRIDGE-TOWER-TOOLS", "桥塔工程包"),
    ("LONG-RANGE-SCOUT-KIT", "长距侦察包"), ("QUARTERMASTER-SUPPLY-KIT", "军需补给包"),
    ("ANTI-LARGE-KIT", "反大型破甲包"), ("ANTI-OATH-GEAR", "抗誓文护具"),
    ("MOBILITY-GEAR", "涉水攀岩机动具"), ("EVIDENCE-SEAL-KIT", "证据封存交互包"),
]

SKILLS = [
    "占领", "护卫", "反骑", "射击", "侦察", "背刺", "治疗", "净化", "破甲术", "冲锋", "修复", "指挥延伸",
    "灰旗共誓", "洛恩封地援护", "维尔萨盾墙", "道路急行", "银林隐蔽", "林地再生", "山炉符文加固", "荒原风暴鼓舞", "圣辉照明盾", "归名唤醒", "自由之翼", "誓文拒绝",
    "架桥", "拆桥", "修塔", "切断控制", "部署弩车", "架设炮位", "清理污染", "开启地道",
    "战地补给", "分粮", "急救", "抬运伤员", "公开伤亡", "权限确认", "地方动员", "撤离引导",
    "双旗推进", "右侧交给你", "最后誓约", "旧誓新答", "白鹿穿林", "符文过载", "亡者归名", "古龙盟约",
]

STATUS = [
    "中毒", "沉默", "护卫", "誓文受控", "溃退", "隐蔽", "风暴暴露", "受伤",
    "资金", "粮秣", "弹药", "士气", "誓约权限", "工程材料", "补给", "地方支持",
    "占领目标", "护送目标", "撤离目标", "救援目标", "维修目标", "破坏目标", "守时目标", "证据送达",
]

FX = [
    "斩击", "穿刺", "箭矢命中", "钝击", "普通火焰", "琥珀誓火", "攻城碎屑", "亡者记忆冲击",
    "秋雨", "河流浅水", "荒原强风", "山炉热浪", "焦土余火", "墓园污染", "银林灵火", "七塔灰光",
    "治疗", "受控", "隐蔽", "援护盾", "结构受损", "工程修复", "塔网失联", "据点占领",
]

HUD = [
    "友军", "敌军", "中立", "可招降", "主目标", "次要目标", "护送目标", "危险区域",
    "补给点", "治疗点", "工程交互", "可破坏结构", "可修复结构", "增援入口", "撤离出口", "多阵营权限争议点",
]


def slug(index: int) -> str:
    return f"{index + 1:02d}"


def expand_topics() -> dict[str, list[dict[str, str]]]:
    topics: dict[str, list[dict[str, str]]] = {
        "narrative-static": STATIC_EXPANDED,
        "combat-unit": [item(f"C01-UNIT-{code}", label) for code, label in COMBAT],
        "mission-unit": [item(f"C01-MISSION-{code}", label) for code, label in MISSION],
        "faction-kit": [item(f"C01-FACTION-{code}", label) for code, label in FACTIONS],
        "terrain": [],
        "interactive-structure": [item(f"C01-STRUCT-{code}", label) for code, label in STRUCTURES],
        "battle-prop": [],
        "equipment": [item(f"C01-EQUIP-{code}", f"{label}主装备") for code, label in COMBAT]
        + [item(f"C01-EQUIP-{code}", label) for code, label in SHARED_EQUIPMENT],
        "skill": [item(f"C01-SKILL-{slug(i)}", label) for i, label in enumerate(SKILLS)],
        "status": [item(f"C01-STATUS-{slug(i)}", label) for i, label in enumerate(STATUS)],
        "fx": [item(f"C01-FX-{slug(i)}", label) for i, label in enumerate(FX)],
        "hud": [item(f"C01-HUD-{slug(i)}", label) for i, label in enumerate(HUD)],
    }
    for family, labels in TERRAIN_GROUPS:
        for index, label in enumerate(labels):
            topics["terrain"].append(item(f"C01-TERRAIN-{family}-{index + 1}", label))
    for family, labels in BATTLE_PROP_GROUPS:
        for index, label in enumerate(labels):
            topics["battle-prop"].append(item(f"C01-BPROP-{family}-{index + 1}", label))
    return topics


def hex_rgba(value: str, alpha: int = 255) -> tuple[int, int, int, int]:
    value = value.lstrip("#")
    return int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16), alpha


def seeded(identifier: str) -> int:
    return int(hashlib.sha256(identifier.encode("utf-8")).hexdigest()[:16], 16)


def color_for(identifier: str, offset: int = 0) -> tuple[int, int, int, int]:
    seed = seeded(identifier)
    return hex_rgba(ACCENTS[(seed + offset) % len(ACCENTS)])


def rect(draw: ImageDraw.ImageDraw, xy: tuple[int, int, int, int], color: str | tuple[int, int, int, int]) -> None:
    draw.rectangle(xy, fill=hex_rgba(color) if isinstance(color, str) else color)


def identity_rune(draw: ImageDraw.ImageDraw, identifier: str, x: int, y: int, scale: int = 2) -> None:
    bits = seeded(identifier)
    for row in range(3):
        for col in range(5):
            if bits & (1 << (row * 5 + col)):
                rect(draw, (x + col * scale, y + row * scale, x + col * scale + scale - 1, y + row * scale + scale - 1), PAL["amber"])


def semantic_badge(draw: ImageDraw.ImageDraw, identifier: str, width: int, height: int) -> None:
    """Add a readable oath-notch motif that survives 32px atlas inspection.

    It is deliberately larger than the technical identity rune and doubles as a
    faction/material marker: a vertical witness line with a unique set of oath
    notches. Because it is opaque on transparent cells it also records a real
    silhouette distinction instead of relying on palette swaps.
    """
    scale = max(2, min(width, height) // 24)
    origin_x = max(1, width - scale * 7 - 2)
    origin_y = max(1, height - scale * 5 - 2)
    bits = seeded(identifier)
    accent = color_for(identifier, 3)
    rect(draw, (origin_x, origin_y, origin_x + scale - 1, origin_y + scale * 4 - 1), PAL["amber"])
    for row in range(4):
        for column in range(5):
            if bits & (1 << (row * 5 + column)):
                rect(
                    draw,
                    (origin_x + (column + 1) * scale, origin_y + row * scale, origin_x + (column + 2) * scale - 1, origin_y + (row + 1) * scale - 1),
                    accent if row % 2 else PAL["cloth"],
                )


def render_scene(identifier: str, size: tuple[int, int]) -> Image.Image:
    width, height = size
    image = Image.new("RGBA", size, hex_rgba(PAL["sky"]))
    draw = ImageDraw.Draw(image)
    seed = seeded(identifier)
    horizon = height // 2 + (seed % max(3, height // 10))
    rect(draw, (0, 0, width - 1, horizon // 2), PAL["sky"])
    rect(draw, (0, horizon // 2, width - 1, horizon), PAL["sky2"])
    draw.polygon([(0, horizon), (width // 4, horizon - height // 5), (width // 2, horizon - height // 12), (3 * width // 4, horizon - height // 4), (width, horizon), (width, height), (0, height)], fill=hex_rgba(PAL["far"]))
    rect(draw, (0, horizon, width - 1, height - 1), PAL["ground"])
    accent = color_for(identifier)
    water = "WATER" in identifier or "BRIDGE" in identifier or "RIVER" in identifier or seed % 4 == 0
    if water:
        rect(draw, (0, height * 3 // 4, width - 1, height - 1), PAL["water"])
        for x in range(seed % 11, width, max(9, width // 12)):
            rect(draw, (x, height * 3 // 4 + (x % 7), min(width - 1, x + width // 18), height * 3 // 4 + (x % 7) + 1), PAL["water_hi"])
    if any(word in identifier for word in ("S26", "S36E", "COUNCIL", "KEYART")):
        rect(draw, (0, 0, width - 1, height - 1), PAL["ink2"])
        rect(draw, (0, height // 5, width - 1, height - 1), PAL["ground"])
        for column in (width // 8, width * 7 // 8):
            rect(draw, (column - 5, height // 8, column + 5, height - 8), PAL["stone"])
        draw.ellipse((width // 5, height // 2, width * 4 // 5, height - 14), fill=hex_rgba(PAL["rust"]), outline=hex_rgba(PAL["stone_hi"]), width=3)
        for index in range(8):
            angle = index * math.pi / 4
            px = width // 2 + int(math.cos(angle) * width // 3)
            py = height * 2 // 3 + int(math.sin(angle) * height // 5)
            rect(draw, (px - 3, py - 8, px + 3, py + 4), color_for(identifier, index))
        draw.polygon([(width // 2 - 8, height // 4), (width // 2, height // 7), (width // 2 + 8, height // 4), (width // 2 + 4, height // 3), (width // 2 - 4, height // 3)], fill=hex_rgba(PAL["amber"]))
        semantic_badge(draw, identifier, width, height)
        return image
    if any(word in identifier for word in ("S02", "BRIDGEWORK")):
        rect(draw, (0, height // 2, width - 1, height - 1), PAL["water"])
        bridge_y = height * 3 // 5
        rect(draw, (width // 12, bridge_y, width * 11 // 12, bridge_y + height // 7), PAL["stone"])
        for support in range(5):
            sx = width // 10 + support * width // 5
            rect(draw, (sx, bridge_y + height // 7, sx + width // 30, height - 3), PAL["stone_hi"])
        for index in range(10):
            px = width // 8 + index * width // 13
            rect(draw, (px, bridge_y - 7 - index % 2 * 3, px + 3, bridge_y + 2), color_for(identifier, index))
        semantic_badge(draw, identifier, width, height)
        return image
    if any(word in identifier for word in ("PUBLIC-RATIONS", "CAMP", "REFUGEE")):
        rect(draw, (0, horizon, width - 1, height - 1), PAL["ground"])
        for index in range(5):
            tx = width // 12 + index * width // 5
            draw.polygon([(tx, height - 12), (tx + width // 14, horizon + 5), (tx + width // 7, height - 12)], fill=hex_rgba(PAL["cloth"] if index % 2 else PAL["gray"]))
        rect(draw, (width // 3, height * 2 // 3, width * 2 // 3, height * 2 // 3 + 7), PAL["rust"])
        for index in range(9):
            px = width // 5 + index * width // 15
            rect(draw, (px, height * 2 // 3 - 8 + index % 3, px + 3, height * 2 // 3 + 2), color_for(identifier, index))
        semantic_badge(draw, identifier, width, height)
        return image
    if any(word in identifier for word in ("S11", "S16", "S29", "NOBANNER", "GRAVE")):
        rect(draw, (0, horizon, width - 1, height - 1), PAL["purple"])
        for row in range(3):
            for column in range(7):
                gx = width // 12 + column * width // 8 + (row % 2) * 5
                gy = horizon + 8 + row * height // 8
                rect(draw, (gx, gy, gx + width // 32, gy + height // 9), PAL["stone_hi"])
                rect(draw, (gx + 2, gy - 3, gx + width // 32 - 2, gy + 3), PAL["cloth"])
        for index in range(5):
            draw.ellipse((width // 3 + index * width // 16, horizon - 8 - index % 2 * 7, width // 3 + index * width // 16 + 12, horizon + 6), fill=color_for(identifier, index))
        semantic_badge(draw, identifier, width, height)
        return image
    if any(word in identifier for word in ("S34", "S22", "CAPITAL-ORDER")):
        rect(draw, (0, horizon - height // 6, width - 1, height - 1), PAL["stone"])
        for index in range(7):
            tx = width // 20 + index * width // 7
            tower_h = height // 3 + (index % 3) * height // 10
            rect(draw, (tx, horizon - tower_h, tx + width // 13, height - 8), PAL["ink2"])
            rect(draw, (tx + 4, horizon - tower_h + 8, tx + 8, horizon - tower_h + 14), color_for(identifier, index))
        rect(draw, (width // 3, height * 3 // 4, width * 2 // 3, height - 1), PAL["ground"])
        for index in range(12):
            px = width // 3 + 5 + (index % 6) * width // 20
            py = height * 3 // 4 + 6 + (index // 6) * 12
            rect(draw, (px, py, px + 3, py + 8), PAL["cloth"])
        semantic_badge(draw, identifier, width, height)
        return image
    if any(word in identifier for word in ("SILVER", "NURSERY", "WOOD")):
        for index in range(5):
            tx = index * width // 5 + width // 12
            rect(draw, (tx, horizon - height // 3, tx + max(2, width // 40), height - 6), PAL["stone_hi"])
            draw.ellipse((tx - width // 14, horizon - height // 2, tx + width // 12, horizon - height // 5), fill=hex_rgba(PAL["green_hi"]))
    if any(word in identifier for word in ("FORGE", "ARCHIVE")):
        for index in range(3):
            fx = width // 4 + index * width // 4
            rect(draw, (fx, horizon - height // 4, fx + width // 12, height - 8), PAL["ink2"])
            rect(draw, (fx + 3, horizon, fx + width // 12 - 3, horizon + height // 10), PAL["rust"])
            rect(draw, (fx + width // 30, horizon - height // 3, fx + width // 20, horizon - height // 4), PAL["amber"])
    if "DRAGON" in identifier or "IVRA" in identifier:
        draw.polygon([(width // 3, horizon - 8), (width // 2, horizon - height // 3), (width * 2 // 3, horizon - 8), (width // 2, horizon - height // 7)], fill=hex_rgba(PAL["purple"]))
        draw.polygon([(width // 2, horizon - height // 3), (width * 3 // 4, horizon - height // 2), (width * 2 // 3, horizon - height // 5)], fill=hex_rgba(PAL["blue"]))
    count = 3 + seed % 5
    for index in range(count):
        bx = (index * width // count + (seed >> (index * 3)) % max(2, width // 12)) % max(1, width - width // 12)
        bw = max(5, width // (14 + index % 3))
        bh = max(8, height // (4 + index % 3))
        by = horizon - bh // 2 + (index % 2) * height // 12
        rect(draw, (bx, by, bx + bw, min(height - 4, by + bh)), PAL["stone"] if index % 2 else PAL["ink2"])
        rect(draw, (bx + bw // 3, by + bh // 3, bx + bw // 3 + 2, by + bh // 3 + 3), accent)
        if index % 2 == 0:
            rect(draw, (bx + bw // 2, by - bh // 3, bx + bw // 2 + 1, by), PAL["black"])
            draw.polygon([(bx + bw // 2 + 2, by - bh // 3), (bx + bw // 2 + bw, by - bh // 4), (bx + bw // 2 + 2, by - bh // 6)], fill=accent)
    for index in range(4 + seed % 4):
        px = (seed >> (index * 5)) % max(1, width - 4)
        py = height - 8 - (index % 3) * 3
        rect(draw, (px, py - 4, px + 2, py), PAL["cloth"])
        rect(draw, (px, py + 1, px + 2, py + 3), color_for(identifier, index))
    identity_rune(draw, identifier, width - 13, height - 8, 2)
    semantic_badge(draw, identifier, width, height)
    return image


def render_portrait(identifier: str, size: tuple[int, int], fullbody: bool = False) -> Image.Image:
    width, height = size
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    seed = seeded(identifier)
    accent = color_for(identifier)
    dark = hex_rgba(PAL["ink2"])
    skin = hex_rgba("#aa8066")
    cx = width // 2
    mounted = any(word in identifier for word in ("RIDER", "CAVALRY", "KNIGHT", "WOLF"))
    spectral = any(word in identifier for word in ("GHOST", "SPIRIT", "NOBANNER"))
    ranged = any(word in identifier for word in ("ARCHER", "LONGBOW", "RANGER", "JAVELIN", "SCOUT"))
    polearm = any(word in identifier for word in ("SPEAR", "LANCE", "JAVELIN", "BANNER"))
    shielded = any(word in identifier for word in ("SWORD", "SHIELD", "TEMPLAR", "GUARD", "KNIGHT"))
    caster = any(word in identifier for word in ("MAGE", "CLERIC", "DRUID", "SHAMAN", "INQUISITOR", "GRAVEKEEPER"))
    worker = any(word in identifier for word in ("ENGINEER", "ARTIFICER", "LABOR", "MINER", "ARTISAN", "BAKER", "COOK", "DOCTOR", "REGISTRAR", "SCRIBE", "QUARTERMASTER"))
    if "DRAGON" in identifier or "IVRA" in identifier or "WYVERN" in identifier:
        draw.polygon([(width // 8, height * 3 // 4), (cx, height // 3), (width * 7 // 8, height * 3 // 4), (width * 3 // 4, height - 8), (width // 4, height - 8)], fill=accent)
        draw.polygon([(cx - 4, height // 3), (cx + width // 3, height // 5), (cx + width // 5, height // 2)], fill=dark)
        draw.polygon([(cx - 2, height // 3), (cx - width // 3, height // 5), (cx - width // 5, height // 2)], fill=color_for(identifier, 2))
        rect(draw, (cx + width // 7, height // 3, cx + width // 7 + 3, height // 3 + 3), PAL["amber"])
    elif "GOLEM" in identifier or "COLOSSUS" in identifier or "BALLISTA" in identifier or "WAGON" in identifier:
        body_top = height // 4
        rect(draw, (width // 5, body_top, width * 4 // 5, height - 10), PAL["stone"])
        rect(draw, (width // 3, body_top - 8, width * 2 // 3, body_top + 8), PAL["stone_hi"])
        rect(draw, (width // 4, height // 2, width * 3 // 4, height // 2 + 4), accent)
        for x in (width // 4, width * 3 // 4 - 4):
            rect(draw, (x, height - 15, x + 5, height - 6), PAL["ink"])
    elif spectral:
        draw.polygon([(cx, 9), (width - 9, height // 2), (cx + width // 5, height - 9), (cx, height * 4 // 5), (cx - width // 5, height - 9), (8, height // 2)], fill=accent)
        rect(draw, (cx - 7, height // 3, cx - 3, height // 3 + 4), PAL["amber"])
        rect(draw, (cx + 3, height // 3, cx + 7, height // 3 + 4), PAL["amber"])
        for index in range(3):
            rect(draw, (cx - 14 + index * 12, height - 17 - index * 2, cx - 10 + index * 12, height - 8), color_for(identifier, index))
    elif mounted and fullbody:
        rect(draw, (width // 8, height * 3 // 5, width * 7 // 8, height - 10), PAL["rust"])
        rect(draw, (width * 3 // 4, height * 3 // 5 - 10, width - 7, height - 25), PAL["rust"])
        for lx in (width // 5, width // 3, width * 2 // 3, width * 4 // 5):
            rect(draw, (lx, height - 20, lx + 4, height - 6), PAL["ink2"])
        rect(draw, (cx - 8, height // 3, cx + 9, height * 2 // 3), accent)
        rect(draw, (cx - 7, height // 3 - 15, cx + 7, height // 3), skin)
        if polearm:
            rect(draw, (width * 3 // 4, 8, width * 3 // 4 + 2, height * 2 // 3), PAL["stone_hi"])
            draw.polygon([(width * 3 // 4 - 5, 10), (width * 3 // 4 + 1, 2), (width * 3 // 4 + 7, 10)], fill=hex_rgba(PAL["amber"]))
        if "EAGLE" in identifier or "WHITE-STAG" in identifier or "WOLF" in identifier:
            draw.polygon([(width // 6, height * 2 // 3), (3, height // 2), (width // 3, height * 3 // 5)], fill=color_for(identifier, 2))
    else:
        if fullbody:
            torso_top = height // 2 - (5 if caster else 0)
            if caster:
                draw.polygon([(cx - 14, torso_top), (cx + 14, torso_top), (cx + 20, height - 7), (cx - 20, height - 7)], fill=accent)
            else:
                rect(draw, (cx - 11, torso_top, cx + 11, height - 24), accent)
            rect(draw, (cx - 10, height - 24, cx - 3, height - 7), PAL["ink2"])
            rect(draw, (cx + 3, height - 24, cx + 10, height - 7), PAL["ink2"])
            head_y = height // 2 - 15
            rect(draw, (cx - 9, head_y, cx + 9, head_y + 17), skin)
            if caster:
                draw.polygon([(cx - 12, head_y + 4), (cx, head_y - 12), (cx + 12, head_y + 4)], fill=dark)
            elif shielded:
                rect(draw, (cx - 11, head_y - 4, cx + 11, head_y + 4), PAL["stone_hi"])
            else:
                draw.polygon([(cx - 11, head_y + 3), (cx, head_y - 8), (cx + 11, head_y + 3)], fill=dark)
            weapon_x = cx + 14 if seed % 2 else cx - 16
            if ranged:
                draw.arc((weapon_x - 8, height // 3, weapon_x + 10, height - 12), -80, 80, fill=hex_rgba(PAL["rust"]), width=2)
                rect(draw, (weapon_x + 3, height // 3 + 2, weapon_x + 4, height - 13), PAL["cloth"])
            elif worker:
                rect(draw, (weapon_x, height // 2 - 7, weapon_x + 3, height - 10), PAL["rust"])
                rect(draw, (weapon_x - 7, height // 2 - 10, weapon_x + 9, height // 2 - 5), PAL["stone_hi"])
                rect(draw, (cx - 16, height // 2 + 4, cx - 11, max(height // 2 + 5, height - 8)), PAL["cloth"])
            else:
                rect(draw, (weapon_x, height // 3, weapon_x + 2, height - 10), PAL["stone_hi"])
                if polearm:
                    draw.polygon([(weapon_x - 4, height // 3), (weapon_x + 1, height // 3 - 9), (weapon_x + 6, height // 3)], fill=hex_rgba(PAL["amber"]))
                else:
                    rect(draw, (weapon_x - 3, height // 3, weapon_x + 5, height // 3 + 3), PAL["amber"])
            if shielded:
                draw.ellipse((cx - 24, height // 2 + 3, cx - 9, height // 2 + 22), fill=color_for(identifier, 2), outline=hex_rgba(PAL["stone_hi"]), width=2)
        else:
            rect(draw, (width // 8, height * 3 // 4, width * 7 // 8, height - 1), accent)
            rect(draw, (cx - width // 5, height // 3, cx + width // 5, height * 3 // 4), skin)
            draw.polygon([(cx - width // 4, height // 2), (cx, height // 5), (cx + width // 4, height // 2)], fill=dark)
            rect(draw, (cx - width // 6, height // 2, cx - width // 10, height // 2 + 3), PAL["ink"])
            rect(draw, (cx + width // 10, height // 2, cx + width // 6, height // 2 + 3), PAL["ink"])
            if seed % 3 == 0:
                rect(draw, (width // 8, height * 3 // 4 - 8, width * 7 // 8, height * 3 // 4 - 3), PAL["stone_hi"])
    if fullbody and "C01-MISSION" in identifier:
        top = max(3, height // 6)
        middle = height // 2
        bottom = height - 6
        tool_x = max(2, width - max(5, width // 6))
        if "FARMER" in identifier:
            rect(draw, (cx - width // 3, top + 5, cx + width // 3, top + 8), PAL["rust"])
            rect(draw, (tool_x, top, tool_x + 2, bottom), PAL["rust"])
            for tine in range(3):
                rect(draw, (tool_x - 4 + tine * 4, top, tool_x - 2 + tine * 4, top + 7), PAL["stone_hi"])
        elif "REFUGEE-CHILD" in identifier:
            draw.polygon([(cx - 11, middle - 6), (cx, top), (cx + 11, middle - 6)], fill=hex_rgba(PAL["cloth"]))
            rect(draw, (cx + 8, middle, cx + 14, middle + 11), PAL["rust"])
        elif "REFUGEE-ADULT" in identifier:
            rect(draw, (cx - 16, middle - 6, cx - 10, middle + 14), PAL["rust"])
            draw.line((cx - 12, middle - 8, cx + 8, bottom - 5), fill=hex_rgba(PAL["cloth"]), width=max(1, width // 32))
        elif "DRIVER" in identifier:
            draw.ellipse((cx + 6, bottom - 15, cx + 21, bottom), outline=hex_rgba(PAL["rust"]), width=max(2, width // 24))
            rect(draw, (cx - 18, bottom - 17, cx + 12, bottom - 10), PAL["rust"])
        elif "BAKER" in identifier or "COOK" in identifier:
            draw.ellipse((cx - 10, top - 2, cx + 10, top + 8), fill=hex_rgba(PAL["cloth"]))
            draw.ellipse((tool_x - 10, middle + 3, tool_x + 8, middle + 16), fill=hex_rgba(PAL["rust"]), outline=hex_rgba(PAL["stone_hi"]), width=2)
        elif "MINER" in identifier:
            rect(draw, (cx - 11, top + 4, cx + 11, top + 9), PAL["stone_hi"])
            rect(draw, (cx - 2, top, cx + 3, top + 5), PAL["amber"])
            draw.arc((tool_x - 12, top + 4, tool_x + 8, middle + 18), 190, 345, fill=hex_rgba(PAL["stone_hi"]), width=3)
        elif any(word in identifier for word in ("ARTISAN", "LABORER")):
            rect(draw, (tool_x, top, tool_x + 3, bottom), PAL["rust"])
            rect(draw, (tool_x - 9, top + 2, tool_x + 10, top + 7), PAL["stone_hi"])
            if "BRIDGE" in identifier:
                rect(draw, (2, bottom - 10, width - 3, bottom - 5), PAL["rust"])
        elif "DOCTOR" in identifier:
            rect(draw, (cx - 3, middle - 9, cx + 3, middle + 9), PAL["cloth"])
            rect(draw, (cx - 10, middle - 3, cx + 10, middle + 3), PAL["cloth"])
            rect(draw, (tool_x - 8, middle + 4, tool_x + 7, middle + 17), PAL["red"])
        elif "WOUNDED" in identifier:
            rect(draw, (cx - 12, top + 8, cx + 12, top + 13), PAL["cloth"])
            rect(draw, (tool_x, middle - 5, tool_x + 2, bottom), PAL["stone_hi"])
        elif "SCRIBE" in identifier or "REGISTRAR" in identifier:
            rect(draw, (tool_x - 12, middle - 6, tool_x + 7, middle + 17), PAL["cloth"])
            for line in range(3):
                rect(draw, (tool_x - 9, middle - 2 + line * 5, tool_x + 3, middle - 1 + line * 5), PAL["ink2"])
        elif "DELEGATE" in identifier:
            draw.ellipse((tool_x - 10, middle - 6, tool_x + 8, middle + 16), fill=hex_rgba(PAL["cloth"]), outline=hex_rgba(PAL["amber"]), width=2)
        elif "MESSENGER" in identifier:
            rect(draw, (tool_x, top, tool_x + 2, bottom), PAL["rust"])
            draw.polygon([(tool_x + 3, top), (width - 2, top + 5), (tool_x + 3, top + 12)], fill=accent)
        elif "ESCORT" in identifier or "MILITIA" in identifier:
            rect(draw, (tool_x, top, tool_x + 2, bottom), PAL["stone_hi"])
            draw.ellipse((cx - 20, middle - 2, cx - 6, middle + 16), fill=accent, outline=hex_rgba(PAL["stone_hi"]), width=2)
        elif "CARETAKER" in identifier:
            rect(draw, (tool_x, top, tool_x + 2, bottom), PAL["rust"])
            draw.ellipse((tool_x - 6, bottom - 8, tool_x + 8, bottom), fill=hex_rgba(PAL["stone_hi"]))
            rect(draw, (cx - 18, middle, cx - 10, middle + 11), PAL["amber"])
        elif "QUARTERMASTER" in identifier:
            rect(draw, (tool_x - 10, middle - 4, tool_x + 7, middle + 17), PAL["cloth"])
            rect(draw, (2, bottom - 12, 14, bottom), PAL["rust"])
        elif "CONTROLLED" in identifier:
            for band in (-12, 0, 12):
                rect(draw, (cx + band - 1, top, cx + band + 1, bottom), PAL["amber"])
        elif "VETERAN" in identifier:
            rect(draw, (tool_x, middle - 5, tool_x + 2, bottom), PAL["rust"])
            draw.polygon([(cx - 13, top + 8), (cx, top), (cx + 13, top + 8)], fill=hex_rgba(PAL["gray"]))
        elif "NURSERY" in identifier:
            draw.ellipse((tool_x - 12, middle - 2, tool_x + 8, middle + 17), fill=hex_rgba(PAL["green_hi"]))
            rect(draw, (tool_x - 3, middle + 4, tool_x + 1, middle + 8), PAL["cloth"])
        elif "WITNESS" in identifier:
            draw.line((cx - 13, middle + 5, cx + 13, middle + 5), fill=hex_rgba(PAL["stone_hi"]), width=3)
            for link in (-9, 0, 9):
                draw.ellipse((cx + link - 4, middle, cx + link + 4, middle + 10), outline=hex_rgba(PAL["stone_hi"]), width=2)
    identity_rune(draw, identifier, max(1, width - 13), max(1, height - 8), 2)
    semantic_badge(draw, identifier, width, height)
    return image


def render_walk(identifier: str, size: tuple[int, int]) -> Image.Image:
    width, height = size
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    frame_width = width // 4
    seed = seeded(identifier)
    for frame in range(4):
        sub = render_portrait(identifier + f"-F{frame}", (frame_width, height), fullbody=True)
        if frame % 2:
            sub = sub.transform(sub.size, Image.Transform.AFFINE, (1, 0, 0, 0, 1, -1), resample=Image.Resampling.NEAREST)
        image.alpha_composite(sub, (frame * frame_width, 0))
    draw = ImageDraw.Draw(image)
    for frame in range(4):
        rect(draw, (frame * frame_width + 6, height - 4, frame * frame_width + frame_width - 6, height - 3), PAL["ink"])
    identity_rune(draw, identifier, width - 12, 2, 1)
    semantic_badge(draw, identifier, width, height)
    return image


def render_faction(identifier: str, size: tuple[int, int]) -> Image.Image:
    width, height = size
    image = Image.new("RGBA", size, hex_rgba(PAL["ink2"]))
    draw = ImageDraw.Draw(image)
    accent = color_for(identifier)
    rect(draw, (4, 4, width - 5, height - 5), PAL["ground"])
    draw.polygon([(12, height - 12), (12, 20), (width // 3, 28), (width // 3, height - 12)], fill=accent)
    draw.polygon([(width // 2, 18), (width * 2 // 3, 28), (width * 2 // 3 - 4, height // 2), (width // 2 + 4, height // 2)], fill=hex_rgba(PAL["stone_hi"]))
    draw.ellipse((width * 2 // 3, 18, width - 16, height // 2 + 18), fill=hex_rgba(PAL["stone"]), outline=accent, width=3)
    for index in range(4):
        rect(draw, (width // 2 + index * 12, height - 28, width // 2 + index * 12 + 8, height - 12), ACCENTS[(seeded(identifier) + index) % len(ACCENTS)])
    identity_rune(draw, identifier, width - 15, height - 10, 2)
    semantic_badge(draw, identifier, width, height)
    return image


def render_terrain(identifier: str, size: tuple[int, int]) -> Image.Image:
    width, height = size
    image = Image.new("RGBA", size, hex_rgba(PAL["ground"]))
    draw = ImageDraw.Draw(image)
    seed = seeded(identifier)
    terrain_palettes = {
        "BORDER": (PAL["ground"], PAL["rust"]), "RIVER": (PAL["water"], PAL["water_hi"]),
        "CAPITAL": (PAL["stone"], PAL["stone_hi"]), "SILVERWOOD": (PAL["green"], PAL["green_hi"]),
        "FORGE": (PAL["ink2"], PAL["rust"]), "WASTELAND": (PAL["rust"], PAL["amber"]),
        "GRAVEYARD": (PAL["purple"], PAL["bone"]), "OATHLIGHT": (PAL["gray"], PAL["amber"]),
    }
    family = next((name for name in terrain_palettes if name in identifier), "BORDER")
    base, alt = terrain_palettes[family]
    variant = int(identifier.rsplit("-", 1)[-1])
    tile = width // 4
    for row in range(4):
        for col in range(4):
            x, y = col * tile, row * tile
            rect(draw, (x, y, x + tile - 1, y + tile - 1), base)
            if family == "BORDER" and variant == 1:
                for stripe in range(4):
                    rect(draw, (x + 3, y + 4 + stripe * 7, x + tile - 4, y + 6 + stripe * 7), alt)
            elif family == "BORDER" and variant == 2:
                draw.ellipse((x + 3, y + 8, x + tile - 5, y + tile - 6), fill=hex_rgba(PAL["ink2"]))
                rect(draw, (x + 6, y + tile // 2, x + tile - 8, y + tile // 2 + 2), PAL["water_hi"])
            elif family == "BORDER" and variant == 3:
                for dot in range(7):
                    dx = 3 + (seed >> (dot * 3)) % (tile - 7)
                    dy = 3 + (seed >> (dot * 4 + 1)) % (tile - 7)
                    rect(draw, (x + dx, y + dy, x + dx + 3, y + dy + 3), PAL["black"])
            elif family == "BORDER":
                for track in (9, 21):
                    rect(draw, (x + track, y, x + track + 3, y + tile - 1), alt)
                    for notch in range(4):
                        rect(draw, (x + track - 2, y + 3 + notch * 8, x + track + 5, y + 5 + notch * 8), alt)
            elif family == "RIVER" and variant == 1:
                draw.polygon([(x, y), (x + tile // 2, y), (x + tile // 3, y + tile), (x, y + tile)], fill=hex_rgba(PAL["stone"]))
                rect(draw, (x + tile // 2, y + 6, x + tile - 4, y + 8), alt)
            elif family == "RIVER" and variant == 2:
                for wave in range(4):
                    rect(draw, (x + 3 + (wave % 2) * 6, y + 4 + wave * 7, x + tile - 6, y + 5 + wave * 7), alt)
            elif family == "RIVER" and variant == 3:
                for bx in range(3):
                    rect(draw, (x + 2 + bx * 10, y + 4, x + 9 + bx * 10, y + tile - 5), PAL["rust"])
                    rect(draw, (x + 1, y + 9 + bx * 8, x + tile - 2, y + 11 + bx * 8), PAL["stone_hi"])
            elif family == "RIVER":
                draw.polygon([(x, y), (x + tile, y), (x + tile, y + tile // 3), (x + 22, y + 15), (x + 16, y + 23), (x + 8, y + 18), (x, y + 28)], fill=hex_rgba(PAL["stone"]))
            elif family == "CAPITAL" and variant == 1:
                for brick_y in range(0, tile, 8):
                    rect(draw, (x, y + brick_y, x + tile - 1, y + brick_y + 2), alt)
                    rect(draw, (x + (brick_y // 8 % 2) * 8 + 8, y + brick_y, x + (brick_y // 8 % 2) * 8 + 10, y + brick_y + 8), alt)
            elif family == "CAPITAL" and variant == 2:
                for cob_y in range(4, tile, 10):
                    for cob_x in range(3 + (cob_y % 7), tile, 11):
                        draw.ellipse((x + cob_x, y + cob_y, x + cob_x + 6, y + cob_y + 4), fill=hex_rgba(alt))
            elif family == "CAPITAL" and variant == 3:
                rect(draw, (x + tile // 2 - 3, y + 4, x + tile // 2 + 3, y + tile - 5), PAL["cloth"])
                rect(draw, (x + 5, y + tile // 2 - 3, x + tile - 6, y + tile // 2 + 3), PAL["cloth"])
            elif family == "CAPITAL":
                for band in range(4):
                    rect(draw, (x, y + band * 8, x + tile - 1, y + band * 8 + 3), alt if band % 2 else PAL["stone"])
            elif family == "SILVERWOOD" and variant == 1:
                for tree in (8, 22):
                    rect(draw, (x + tree, y + 12, x + tree + 3, y + tile - 2), PAL["stone_hi"])
                    draw.ellipse((x + tree - 7, y + 2, x + tree + 9, y + 18), fill=hex_rgba(alt))
            elif family == "SILVERWOOD" and variant == 2:
                for branch in range(4):
                    draw.line((x + tile // 2, y + tile // 2, x + 2 + branch * 9, y + (branch % 2) * (tile - 4) + 2), fill=hex_rgba(PAL["stone_hi"]), width=3)
            elif family == "SILVERWOOD" and variant == 3:
                rect(draw, (x + 3, y + 3, x + tile - 4, y + 6), PAL["cloth"])
                for post in (5, 15, 25):
                    rect(draw, (x + post, y + 3, x + post + 2, y + tile - 4), PAL["rust"])
            elif family == "SILVERWOOD":
                for grass in range(8):
                    gx = 2 + (seed >> grass * 3) % (tile - 5)
                    gy = 4 + (seed >> (grass * 4 + 2)) % (tile - 8)
                    draw.polygon([(x + gx, y + gy + 6), (x + gx + 2, y + gy), (x + gx + 4, y + gy + 6)], fill=hex_rgba(alt))
            elif family == "FORGE" and variant == 1:
                for slab in range(4):
                    rect(draw, (x + 2, y + slab * 8 + 2, x + tile - 3, y + slab * 8 + 6), PAL["stone"])
            elif family == "FORGE" and variant == 2:
                rect(draw, (x + 11, y, x + 20, y + tile - 1), PAL["amber"])
                rect(draw, (x + 14, y, x + 17, y + tile - 1), PAL["red"])
            elif family == "FORGE" and variant == 3:
                draw.ellipse((x + 6, y + 6, x + tile - 7, y + tile - 7), outline=hex_rgba(PAL["amber"]), width=3)
                rect(draw, (x + 14, y + 4, x + 18, y + tile - 5), PAL["amber"])
            elif family == "FORGE":
                for shelf in range(3):
                    rect(draw, (x + 3, y + 5 + shelf * 10, x + tile - 4, y + 7 + shelf * 10), PAL["rust"])
                    for tag in range(3):
                        rect(draw, (x + 6 + tag * 8, y + 8 + shelf * 10, x + 9 + tag * 8, y + 13 + shelf * 10), PAL["cloth"])
            elif family == "WASTELAND" and variant == 1:
                for wind in range(4):
                    draw.line((x + 2, y + 5 + wind * 8, x + tile - 3, y + 1 + wind * 8), fill=hex_rgba(alt), width=2)
            elif family == "WASTELAND" and variant == 2:
                draw.polygon([(x + 3, y + tile - 4), (x + 10, y + 7), (x + 18, y + 16), (x + 25, y + 5), (x + tile - 3, y + tile - 4)], fill=hex_rgba(PAL["stone"]))
            elif family == "WASTELAND" and variant == 3:
                draw.polygon([(x + 4, y + tile - 4), (x + tile // 2, y + 5), (x + tile - 5, y + tile - 4)], fill=hex_rgba(PAL["cloth"]))
                rect(draw, (x + tile // 2 - 2, y + 6, x + tile // 2 + 2, y + tile - 4), PAL["rust"])
            elif family == "WASTELAND":
                for bolt in (6, 18, 27):
                    draw.line((x + bolt, y + 2, x + bolt - 5, y + 14, x + bolt + 2, y + 13, x + bolt - 3, y + 28), fill=hex_rgba(PAL["amber"]), width=2)
            elif family == "GRAVEYARD" and variant == 1:
                for grave in (5, 17, 27):
                    rect(draw, (x + grave, y + 9, x + grave + 5, y + tile - 5), PAL["stone_hi"])
                    draw.ellipse((x + grave, y + 5, x + grave + 5, y + 12), fill=hex_rgba(PAL["stone_hi"]))
            elif family == "GRAVEYARD" and variant == 2:
                for tag in range(5):
                    rect(draw, (x + 3 + tag * 6, y + 5 + (tag % 2) * 9, x + 6 + tag * 6, y + 12 + (tag % 2) * 9), PAL["cloth"])
            elif family == "GRAVEYARD" and variant == 3:
                for pool in range(3):
                    draw.ellipse((x + 2 + pool * 9, y + 6 + pool * 7, x + 15 + pool * 6, y + 17 + pool * 7), fill=hex_rgba(PAL["green"]))
            elif family == "GRAVEYARD":
                draw.ellipse((x + 5, y + 5, x + tile - 6, y + tile - 6), outline=hex_rgba(PAL["amber"]), width=3)
                rect(draw, (x + tile // 2 - 2, y + 3, x + tile // 2 + 2, y + tile - 4), PAL["cloth"])
            elif family == "OATHLIGHT" and variant == 1:
                draw.ellipse((x + 6, y + 6, x + tile - 7, y + tile - 7), outline=hex_rgba(alt), width=4)
                rect(draw, (x + tile // 2 - 2, y + 3, x + tile // 2 + 2, y + tile - 4), alt)
            elif family == "OATHLIGHT" and variant == 2:
                for chain in range(4):
                    draw.ellipse((x + 3 + chain * 7, y + 8 + (chain % 2) * 7, x + 11 + chain * 7, y + 16 + (chain % 2) * 7), outline=hex_rgba(PAL["red"]), width=2)
            elif family == "OATHLIGHT" and variant == 3:
                draw.line((x + 3, y + 4, x + tile - 4, y + tile - 5), fill=hex_rgba(PAL["black"]), width=4)
                draw.line((x + tile - 4, y + 4, x + 3, y + tile - 5), fill=hex_rgba(PAL["black"]), width=4)
            else:
                for stone in range(4):
                    draw.polygon([(x + 4 + stone * 7, y + 23), (x + 7 + stone * 7, y + 6 + (stone % 2) * 4), (x + 11 + stone * 7, y + 23)], fill=hex_rgba(alt))
    identity_rune(draw, identifier, width - 13, height - 8, 2)
    semantic_badge(draw, identifier, width, height)
    return image


def render_structure(identifier: str, size: tuple[int, int]) -> Image.Image:
    width, height = size
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    state_width = width // 3
    for state in range(3):
        x0 = state * state_width
        accent = color_for(identifier, state)
        rect(draw, (x0 + 6, height - 14, x0 + state_width - 7, height - 8), PAL["ink"])
        if any(word in identifier for word in ("BRIDGE", "SLUICE", "CHEVAUX")):
            rect(draw, (x0 + 5, height // 2, x0 + state_width - 6, height // 2 + 10), PAL["rust"])
            for support in range(3):
                sx = x0 + 10 + support * (state_width - 20) // 2
                rect(draw, (sx, height // 2 + 10, sx + 4, height - 15), PAL["stone"])
        elif any(word in identifier for word in ("HOSPITAL", "SHELTER", "RATION")):
            draw.polygon([(x0 + 7, height - 15), (x0 + state_width // 2, height // 4), (x0 + state_width - 8, height - 15)], fill=hex_rgba(PAL["cloth"]))
            rect(draw, (x0 + state_width // 2 - 3, height // 2, x0 + state_width // 2 + 3, height // 2 + 16), accent)
        elif any(word in identifier for word in ("NOTICE", "REGISTRY", "MEMORIAL")):
            rect(draw, (x0 + 10, height // 3, x0 + state_width - 11, height * 2 // 3), PAL["cloth"])
            rect(draw, (x0 + 14, height * 2 // 3, x0 + 18, height - 15), PAL["rust"])
            rect(draw, (x0 + state_width - 19, height * 2 // 3, x0 + state_width - 15, height - 15), PAL["rust"])
        elif any(word in identifier for word in ("GATE", "KEEP")):
            rect(draw, (x0 + 5, height // 3, x0 + state_width - 6, height - 15), PAL["stone"])
            for tower_x in (x0 + 7, x0 + state_width - 17):
                rect(draw, (tower_x, height // 5, tower_x + 10, height - 15), PAL["stone_hi"])
                for tooth in range(3):
                    rect(draw, (tower_x + tooth * 4, height // 5 - 5, tower_x + tooth * 4 + 2, height // 5), PAL["stone_hi"])
            draw.ellipse((x0 + state_width // 2 - 8, height // 2, x0 + state_width // 2 + 8, height - 8), fill=hex_rgba(PAL["ink2"]))
        elif any(word in identifier for word in ("GRANARY", "DEPOT", "WORKSHOP", "FURNACE")):
            rect(draw, (x0 + 7, height // 2, x0 + state_width - 8, height - 15), PAL["rust"])
            draw.polygon([(x0 + 4, height // 2), (x0 + state_width // 2, height // 4), (x0 + state_width - 5, height // 2)], fill=hex_rgba(PAL["cloth"]))
            chimney_height = 10 + (seeded(identifier) % 18)
            rect(draw, (x0 + state_width - 18, height // 2 - chimney_height, x0 + state_width - 12, height // 2), PAL["stone"])
            for crate in range(1 + seeded(identifier) % 3):
                rect(draw, (x0 + 10 + crate * 10, height - 27 - crate * 3, x0 + 18 + crate * 10, height - 16), accent)
        elif any(word in identifier for word in ("BALLISTA", "GUN-POSITION")):
            draw.ellipse((x0 + 8, height - 35, x0 + 26, height - 17), fill=hex_rgba(PAL["rust"]))
            draw.ellipse((x0 + state_width - 27, height - 35, x0 + state_width - 9, height - 17), fill=hex_rgba(PAL["rust"]))
            rect(draw, (x0 + 12, height // 2, x0 + state_width - 13, height // 2 + 8), PAL["stone_hi"])
            draw.polygon([(x0 + state_width - 14, height // 2 - 5), (x0 + state_width - 3, height // 2 + 4), (x0 + state_width - 14, height // 2 + 13)], fill=accent)
        else:
            rect(draw, (x0 + 12, height // 3, x0 + state_width - 13, height - 15), PAL["stone"])
            draw.polygon([(x0 + 8, height // 3), (x0 + state_width // 2, 8 + state * 3), (x0 + state_width - 9, height // 3)], fill=hex_rgba(PAL["stone_hi"]))
            rect(draw, (x0 + state_width // 2 - 4, height * 2 // 3, x0 + state_width // 2 + 4, height - 15), PAL["ink2"])
        rect(draw, (x0 + state_width // 2 - 2, height // 2, x0 + state_width // 2 + 2, height // 2 + 5), accent)
        if state == 1:
            rect(draw, (x0 + 9, 8, x0 + 11, height // 2), PAL["black"])
            draw.polygon([(x0 + 12, 9), (x0 + state_width - 8, 15), (x0 + 12, 24)], fill=accent)
        if state == 2:
            rect(draw, (x0 + state_width // 2, height // 3, x0 + state_width // 2 + 3, height - 20), PAL["black"])
            rect(draw, (x0 + 16, height - 23, x0 + 27, height - 18), PAL["rust"])
    identity_rune(draw, identifier, width - 12, 2, 1)
    semantic_badge(draw, identifier, width, height)
    return image


def render_four_items(identifier: str, size: tuple[int, int], equipment: bool = False) -> Image.Image:
    width, height = size
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    cell = width // 4
    for index in range(4):
        cx = index * cell + cell // 2
        accent = color_for(identifier, index)
        kind = (seeded(identifier) + index) % 5
        if equipment and index == 0:
            if any(word in identifier for word in ("ARCHER", "LONGBOW", "RANGER", "SCOUT")):
                kind = 3
            elif any(word in identifier for word in ("SPEAR", "LANCE", "JAVELIN")):
                kind = 0
            elif any(word in identifier for word in ("MAGE", "CLERIC", "DRUID", "SHAMAN", "INQUISITOR")):
                kind = 1
            elif any(word in identifier for word in ("ENGINEER", "ARTIFICER", "KIT", "TOOLS")):
                kind = 2
            else:
                kind = 4
        if kind == 0:
            rect(draw, (cx - 2, 8, cx + 2, height - 9), PAL["stone_hi"])
            draw.polygon([(cx - 7, 9), (cx, 2), (cx + 7, 9)], fill=accent)
        elif kind == 1:
            draw.ellipse((cx - 11, height // 2 - 11, cx + 11, height // 2 + 11), fill=accent, outline=hex_rgba(PAL["stone_hi"]), width=3)
            rect(draw, (cx - 2, height // 2 - 5, cx + 2, height // 2 + 5), PAL["amber"])
        elif kind == 2:
            rect(draw, (cx - 10, height // 2 - 7, cx + 10, height // 2 + 8), accent)
            rect(draw, (cx - 5, height // 2 - 11, cx + 5, height // 2 - 7), PAL["stone_hi"])
            rect(draw, (cx - 2, height // 2 - 3, cx + 2, height // 2 + 3), PAL["cloth"])
        elif kind == 3:
            draw.polygon([(cx - 12, height - 8), (cx - 5, 7), (cx + 11, 12), (cx + 8, height - 10)], fill=accent)
            rect(draw, (cx - 3, 12, cx + 1, height - 14), PAL["stone_hi"])
        else:
            rect(draw, (cx - 11, height // 2 - 4, cx + 8, height // 2 + 4), PAL["stone_hi"])
            draw.polygon([(cx + 8, height // 2 - 8), (cx + 15, height // 2), (cx + 8, height // 2 + 8)], fill=accent)
            rect(draw, (cx - 14, height // 2 - 2, cx - 10, height // 2 + 2), PAL["rust"])
        if equipment:
            rect(draw, (index * cell + 2, height - 5, index * cell + cell - 3, height - 3), ACCENTS[(seeded(identifier) + index) % len(ACCENTS)])
            length = 3 + ((seeded(identifier) >> (index * 4)) & 0b111)
            rect(draw, (index * cell + 3, 3 + index, index * cell + 3 + length, 7 + index), accent)
        else:
            family = next((name for name, _labels in BATTLE_PROP_GROUPS if name in identifier), "COVER")
            variant = int(identifier.rsplit("-", 1)[-1])
            if family == "COVER":
                rect(draw, (index * cell + 3, height - 12 - variant * 2, index * cell + cell - 4, height - 7), PAL["stone"] if variant == 3 else PAL["rust"])
            elif family == "HAZARD":
                draw.polygon([(cx - 7, height - 8), (cx, 5 + variant), (cx + 7, height - 8)], fill=hex_rgba(PAL["red"] if variant < 3 else PAL["green"]))
            elif family == "LOGISTICS":
                rect(draw, (cx - 9, height // 2 - 5, cx + 9, height // 2 + 8), PAL["cloth"])
                rect(draw, (cx - 4, height // 2 - 9, cx + 4, height // 2 - 5), accent)
            elif family == "ENGINEERING":
                rect(draw, (cx - 2, 7, cx + 2, height - 8), PAL["rust"])
                rect(draw, (cx - 9, 7 + variant, cx + 9, 11 + variant), PAL["stone_hi"])
            elif family == "LIFE":
                rect(draw, (index * cell + 4, height // 2, index * cell + cell - 5, height // 2 + 8 + variant), PAL["cloth"])
            elif family == "EVIDENCE":
                rect(draw, (cx - 9, 7, cx + 9, height - 8), PAL["cloth"])
                for line in range(variant + 1):
                    rect(draw, (cx - 6, 11 + line * 5, cx + 5 + line, 12 + line * 5), PAL["ink2"])
            elif family == "FACTION":
                rect(draw, (cx - 1, 4, cx + 2, height - 7), PAL["rust"])
                draw.polygon([(cx + 3, 5), (cx + 12 + variant, 10), (cx + 3, 17 + variant)], fill=accent)
            else:
                for rubble in range(variant + 1):
                    draw.polygon([(index * cell + 3 + rubble * 6, height - 7), (index * cell + 6 + rubble * 6, height - 17 - rubble * 2), (index * cell + 11 + rubble * 6, height - 7)], fill=hex_rgba(PAL["stone"]))
    identity_rune(draw, identifier, width - 12, 2, 1)
    semantic_badge(draw, identifier, width, height)
    return image


def render_icon(identifier: str, size: tuple[int, int], mode: str) -> Image.Image:
    width, height = size
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    seed = seeded(identifier)
    accent = color_for(identifier)
    cx, cy = width // 2, height // 2
    if mode == "fx":
        frame = width // 3
        for index in range(3):
            ox = index * frame + frame // 2
            radius = 5 + index * 3
            draw.polygon([(ox, cy - radius), (ox + radius, cy), (ox, cy + radius), (ox - radius, cy)], fill=accent)
            rect(draw, (ox - 2, cy - radius - 3, ox + 2, cy + radius + 3), PAL["amber"] if index == 2 else PAL["stone_hi"])
        identity_rune(draw, identifier, width - 11, height - 6, 1)
    else:
        kind = seed % 6
        if kind == 0:
            draw.polygon([(cx, 4), (width - 6, cy), (cx, height - 5), (5, cy)], fill=accent)
        elif kind == 1:
            draw.ellipse((7, 7, width - 8, height - 8), fill=accent, outline=hex_rgba(PAL["stone_hi"]), width=3)
        elif kind == 2:
            rect(draw, (cx - 3, 5, cx + 3, height - 6), PAL["stone_hi"])
            draw.polygon([(cx - 10, 8), (cx, 2), (cx + 10, 8)], fill=accent)
        elif kind == 3:
            rect(draw, (6, cy - 5, width - 7, cy + 5), accent)
            rect(draw, (cx - 4, 6, cx + 4, height - 7), PAL["amber"])
        elif kind == 4:
            draw.polygon([(5, height - 7), (cx, 5), (width - 6, height - 7)], fill=accent)
            rect(draw, (cx - 2, 12, cx + 2, height - 12), PAL["cloth"])
        else:
            draw.arc((5, 5, width - 6, height - 6), 20, 320, fill=accent, width=5)
            draw.polygon([(width - 13, 4), (width - 4, 10), (width - 14, 14)], fill=accent)
        identity_rune(draw, identifier, width - 11, height - 6, 1)
    semantic_badge(draw, identifier, width, height)
    return image


def vectorize(image: Image.Image, output: Path, label: str) -> None:
    rgba = image.convert("RGBA")
    active: dict[tuple[int, int, tuple[int, int, int, int]], tuple[int, int]] = {}
    finished: list[tuple[int, int, int, int, tuple[int, int, int, int]]] = []
    for y in range(rgba.height):
        runs: dict[tuple[int, int, tuple[int, int, int, int]], bool] = {}
        x = 0
        while x < rgba.width:
            pixel = rgba.getpixel((x, y))
            run = 1
            while x + run < rgba.width and rgba.getpixel((x + run, y)) == pixel:
                run += 1
            if pixel[3]:
                runs[(x, run, pixel)] = True
            x += run
        for key, (start, height) in list(active.items()):
            if key in runs:
                active[key] = (start, height + 1)
            else:
                rx, rw, color = key
                finished.append((rx, start, rw, height, color))
                del active[key]
        for key in runs:
            if key not in active:
                active[key] = (y, 1)
    for key, (start, height) in active.items():
        rx, rw, color = key
        finished.append((rx, start, rw, height, color))
    finished.sort(key=lambda value: (value[1], value[0]))
    rows = []
    for x, y, width, height, color in finished:
        red, green, blue, alpha = color
        opacity = "" if alpha == 255 else f' opacity="{alpha / 255:.3f}"'
        rows.append(f'<rect x="{x}" y="{y}" width="{width}" height="{height}" fill="#{red:02x}{green:02x}{blue:02x}"{opacity}/>')
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{rgba.width}" height="{rgba.height}" '
        f'viewBox="0 0 {rgba.width} {rgba.height}" role="img" aria-label="{label}" '
        f'shape-rendering="crispEdges" style="image-rendering:pixelated">\n'
        + "\n".join(rows)
        + "\n</svg>\n",
        encoding="utf-8",
    )


def existing_deliveries() -> tuple[list[dict[str, object]], dict[str, dict[str, object]]]:
    specs = [
        ("C01-EXISTING-CHAR-LEIA-01", "portrait", "characters/laiya-18-portrait-hd", 96, 112, [EXISTING_TOPICS[0]]),
        ("C01-EXISTING-CHAR-RODERICK-01", "portrait", "characters/roderick-portrait-hd", 96, 112, [EXISTING_TOPICS[1]]),
        ("C01-EXISTING-CHAR-CAIN-01", "portrait", "characters/kain-portrait-hd", 96, 112, [EXISTING_TOPICS[2]]),
        ("C01-EXISTING-CHAR-MIREL-01", "portrait", "characters/mirelle-portrait-hd", 96, 112, [EXISTING_TOPICS[3]]),
        ("C01-EXISTING-CHAR-BRAN-01", "portrait", "characters/bran-portrait-hd", 96, 112, [EXISTING_TOPICS[4]]),
        ("C01-EXISTING-CHAR-TASHA-01", "portrait", "characters/tasha-portrait-hd", 96, 112, [EXISTING_TOPICS[5]]),
        ("C01-EXISTING-ARCH-REDSTONE", "architecture", "architecture/redstone-oath-tower-hd", 128, 128, [EXISTING_TOPICS[6]]),
        ("C01-EXISTING-ARCH-GRAY-CAMP", "architecture", "architecture/gray-banner-field-camp-hd", 128, 128, [EXISTING_TOPICS[7]]),
        ("C01-EXISTING-ARCH-THREE-BRIDGES", "architecture", "architecture/three-bridges-river-valley-hd", 128, 128, [EXISTING_TOPICS[8]]),
        ("C01-EXISTING-ARCH-SILVERWOOD", "architecture", "architecture/silverwood-tree-city-hd", 128, 128, [EXISTING_TOPICS[9]]),
        ("C01-EXISTING-CH01-S05", "scene", "scenes/gray-flag-over-burned-village-hd", 256, 144, [EXISTING_TOPICS[10]]),
        ("C01-EXISTING-CH01-S01", "scene", "scenes/twin-hills-first-command-hd", 256, 144, [EXISTING_TOPICS[11]]),
        ("C01-EXISTING-CH02-S06", "scene", "scenes/white-river-night-crossing-hd", 256, 144, [EXISTING_TOPICS[12]]),
        ("C01-EXISTING-CH07-S33-EST", "scene", "scenes/seven-towers-at-dusk-hd", 256, 144, [EXISTING_TOPICS[13]]),
        ("C01-EXISTING-PROPS-01", "prop-sheet", "props/story-props-sheet-hd", 192, 48, EXISTING_TOPICS[14:18]),
        ("C01-EXISTING-PROPS-02", "prop-sheet", "props/campaign-props-sheet-02-hd", 192, 48, EXISTING_TOPICS[18:22]),
    ]
    deliveries: list[dict[str, object]] = []
    topic_records: dict[str, dict[str, object]] = {}
    for delivery_id, kind, stem, width, height, mapped in specs:
        cell_width = width // len(mapped)
        cells = []
        for index, topic in enumerate(mapped):
            cell = {"x": index * cell_width, "y": 0, "width": cell_width, "height": height}
            cells.append({"topicId": topic["id"], **cell})
            topic_records[topic["id"]] = {
                "id": topic["id"], "label": topic["label"], "category": "narrative-static",
                "assetId": delivery_id, "status": "formal", "source": "existing", "cell": cell,
            }
        deliveries.append({
            "id": delivery_id, "type": kind, "png": f"{stem}.png", "svg": f"{stem}.svg",
            "width": width, "height": height, "topicIds": [topic["id"] for topic in mapped],
            "cells": cells, "source": "existing",
        })
    return deliveries, topic_records


def delivery_spec(identifier: str, kind: str, path: str, topics: list[dict[str, str]], cell: tuple[int, int], cols: int, renderer: str) -> dict[str, object]:
    rows = math.ceil(len(topics) / cols)
    cell_width, cell_height = cell
    return {
        "id": identifier, "type": kind, "png": f"{path}.png", "svg": f"{path}.svg",
        "width": cols * cell_width, "height": rows * cell_height,
        "cellWidth": cell_width, "cellHeight": cell_height, "columns": cols, "rows": rows,
        "topicIds": [topic["id"] for topic in topics], "source": "expanded", "renderer": renderer,
    }


def expanded_delivery_specs(topics: dict[str, list[dict[str, str]]]) -> list[dict[str, object]]:
    static = topics["narrative-static"]
    style, characters, scenes, architecture, props = static[:6], static[6:22], static[22:41], static[41:51], static[51:58]
    return [
        delivery_spec("C01-ATLAS-STYLE-V1", "style-atlas", "complete/static/style-reference-atlas-v1", style, (192, 112), 3, "scene"),
        delivery_spec("C01-ATLAS-CHAR-STAGES-V1", "character-atlas", "complete/static/character-stage-atlas-v1", characters, (96, 112), 4, "portrait"),
        delivery_spec("C01-ATLAS-STORY-SCENES-V1", "scene-atlas", "complete/static/story-scene-atlas-v1", scenes, (256, 144), 4, "scene"),
        delivery_spec("C01-ATLAS-ARCHITECTURE-V1", "architecture-atlas", "complete/static/architecture-atlas-v1", architecture, (128, 128), 5, "architecture"),
        delivery_spec("C01-ATLAS-NARRATIVE-PROPS-V1", "prop-atlas", "complete/static/narrative-prop-atlas-v1", props, (48, 48), 7, "icon"),
        delivery_spec("C01-ATLAS-COMBAT-PACKAGES-V1", "combat-unit-package-atlas", "complete/units/combat-unit-package-atlas-v1", topics["combat-unit"], (320, 112), 4, "combat-package"),
        delivery_spec("C01-ATLAS-MISSION-PACKAGES-V1", "mission-unit-package-atlas", "complete/mission-units/mission-unit-package-atlas-v1", topics["mission-unit"], (224, 112), 4, "mission-package"),
        delivery_spec("C01-ATLAS-FACTION-KITS-V1", "faction-kit-atlas", "complete/factions/faction-kit-atlas-v1", topics["faction-kit"], (192, 112), 4, "faction"),
        delivery_spec("C01-ATLAS-TERRAIN-V1", "terrain-tileset-atlas", "complete/terrain/terrain-group-atlas-v1", topics["terrain"], (128, 128), 4, "terrain"),
        delivery_spec("C01-ATLAS-STRUCTURES-V1", "structure-state-atlas", "complete/structures/interactive-structure-atlas-v1", topics["interactive-structure"], (192, 96), 4, "structure"),
        delivery_spec("C01-ATLAS-BATTLE-PROPS-V1", "battle-prop-atlas", "complete/battle-props/battle-prop-atlas-v1", topics["battle-prop"], (128, 64), 4, "items"),
        delivery_spec("C01-ATLAS-EQUIPMENT-V1", "equipment-atlas", "complete/equipment/equipment-atlas-v1", topics["equipment"], (192, 48), 6, "equipment"),
        delivery_spec("C01-ATLAS-SKILLS-V1", "skill-icon-atlas", "complete/skills/skill-icon-atlas-v1", topics["skill"], (48, 48), 12, "icon"),
        delivery_spec("C01-ATLAS-STATUS-V1", "status-icon-atlas", "complete/status/status-icon-atlas-v1", topics["status"], (48, 48), 8, "icon"),
        delivery_spec("C01-ATLAS-FX-V1", "fx-atlas", "complete/fx/fx-atlas-v1", topics["fx"], (96, 48), 8, "fx"),
        delivery_spec("C01-ATLAS-HUD-V1", "hud-atlas", "complete/ui/hud-atlas-v1", topics["hud"], (48, 48), 8, "icon"),
    ]


def render_cell(renderer: str, topic_id: str, cell: tuple[int, int]) -> Image.Image:
    if renderer in {"scene", "architecture"}:
        if renderer == "architecture":
            return render_structure(topic_id, cell)
        return render_scene(topic_id, cell)
    if renderer == "portrait":
        return render_portrait(topic_id, cell, fullbody=False)
    if renderer == "fullbody":
        return render_portrait(topic_id, cell, fullbody=True)
    if renderer == "walk":
        return render_walk(topic_id, cell)
    if renderer == "combat-package":
        image = Image.new("RGBA", cell, (0, 0, 0, 0))
        image.alpha_composite(render_portrait(topic_id + "-FULL", (96, 112), fullbody=True), (0, 0))
        image.alpha_composite(render_portrait(topic_id + "-PORTRAIT", (96, 112), fullbody=False), (96, 0))
        image.alpha_composite(render_walk(topic_id + "-WALK", (128, 48)), (192, 64))
        return image
    if renderer == "mission-package":
        image = Image.new("RGBA", cell, (0, 0, 0, 0))
        image.alpha_composite(render_portrait(topic_id + "-FULL", (96, 112), fullbody=True), (0, 0))
        image.alpha_composite(render_walk(topic_id + "-WALK", (128, 48)), (96, 64))
        return image
    if renderer == "faction":
        return render_faction(topic_id, cell)
    if renderer == "terrain":
        return render_terrain(topic_id, cell)
    if renderer == "structure":
        return render_structure(topic_id, cell)
    if renderer == "items":
        return render_four_items(topic_id, cell)
    if renderer == "equipment":
        return render_four_items(topic_id, cell, equipment=True)
    if renderer == "fx":
        return render_icon(topic_id, cell, "fx")
    return render_icon(topic_id, cell, "icon")


def build_atlas(spec: dict[str, object], topic_lookup: dict[str, dict[str, str]]) -> dict[str, object]:
    width, height = int(spec["width"]), int(spec["height"])
    cell_width, cell_height = int(spec["cellWidth"]), int(spec["cellHeight"])
    cols = int(spec["columns"])
    image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    cells = []
    for index, topic_id in enumerate(spec["topicIds"]):
        x, y = (index % cols) * cell_width, (index // cols) * cell_height
        cell_image = render_cell(str(spec["renderer"]), str(topic_id), (cell_width, cell_height))
        image.alpha_composite(cell_image, (x, y))
        cells.append({"topicId": topic_id, "x": x, "y": y, "width": cell_width, "height": cell_height})
    png_path = ROOT / str(spec["png"])
    svg_path = ROOT / str(spec["svg"])
    png_path.parent.mkdir(parents=True, exist_ok=True)
    image.save(png_path, optimize=True)
    vectorize(image, svg_path, str(spec["id"]))
    result = {key: value for key, value in spec.items() if key != "renderer"}
    result["cells"] = cells
    return result


def rasterize_svg(path: Path, size: tuple[int, int]) -> Image.Image:
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    root = ET.parse(path).getroot()
    if root.attrib.get("viewBox") != f"0 0 {size[0]} {size[1]}":
        raise ValueError(f"viewBox mismatch in {path}")
    for node in root.iter():
        if not node.tag.endswith("rect"):
            continue
        x, y = int(node.attrib["x"]), int(node.attrib["y"])
        width, height = int(node.attrib["width"]), int(node.attrib["height"])
        raw = node.attrib["fill"].lstrip("#")
        alpha = round(float(node.attrib.get("opacity", "1")) * 255)
        color = (int(raw[:2], 16), int(raw[2:4], 16), int(raw[4:6], 16), alpha)
        draw.rectangle((x, y, x + width - 1, y + height - 1), fill=color)
    return image


def validate(manifest: dict[str, object]) -> dict[str, object]:
    errors: list[str] = []
    warnings: list[str] = []
    topics = manifest["topics"]
    deliveries = manifest["deliveries"]
    ids = [topic["id"] for topic in topics]
    if len(topics) != 404:
        errors.append(f"topic total {len(topics)} != 404")
    duplicates = [identifier for identifier, count in Counter(ids).items() if count > 1]
    if duplicates:
        errors.append(f"duplicate topic ids: {duplicates}")
    counts = Counter(topic["category"] for topic in topics)
    for category, target in CATEGORY_TARGETS.items():
        if counts[category] != target:
            errors.append(f"{category}: {counts[category]} != {target}")
    delivery_map = {delivery["id"]: delivery for delivery in deliveries}
    topic_category = {topic["id"]: topic["category"] for topic in topics}
    diversity_cells: dict[str, list[tuple[str, str]]] = {category: [] for category in CATEGORY_TARGETS}
    diversity_silhouettes: dict[str, list[tuple[str, str]]] = {category: [] for category in CATEGORY_TARGETS}
    checked_files = 0
    checked_cells = 0
    svg_matches = 0
    for topic in topics:
        delivery = delivery_map.get(topic["assetId"])
        if delivery is None:
            errors.append(f"{topic['id']}: missing assetId {topic['assetId']}")
        elif topic["id"] not in delivery["topicIds"]:
            errors.append(f"{topic['id']}: not listed by primary delivery")
    for delivery in deliveries:
        png_path, svg_path = ROOT / delivery["png"], ROOT / delivery["svg"]
        for path in (png_path, svg_path):
            checked_files += 1
            if not path.is_file():
                errors.append(f"{delivery['id']}: missing {path}")
        if not png_path.is_file() or not svg_path.is_file():
            continue
        png = Image.open(png_path).convert("RGBA")
        expected = int(delivery["width"]), int(delivery["height"])
        if png.size != expected:
            errors.append(f"{delivery['id']}: PNG {png.size} != {expected}")
            continue
        alphas = {pixel[3] for pixel in png.get_flattened_data()}
        if not alphas.issubset({0, 255}):
            errors.append(f"{delivery['id']}: non-binary alpha {sorted(alphas)}")
        try:
            rebuilt = rasterize_svg(svg_path, expected)
        except (ET.ParseError, KeyError, TypeError, ValueError) as error:
            errors.append(f"{delivery['id']}: SVG invalid: {error}")
        else:
            if list(png.get_flattened_data()) != list(rebuilt.get_flattened_data()):
                errors.append(f"{delivery['id']}: PNG/SVG pixel mismatch")
            else:
                svg_matches += 1
        hashes = set()
        for cell in delivery["cells"]:
            crop = png.crop((cell["x"], cell["y"], cell["x"] + cell["width"], cell["y"] + cell["height"]))
            checked_cells += 1
            if crop.getchannel("A").getbbox() is None:
                errors.append(f"{delivery['id']}: empty cell {cell['topicId']}")
            digest = hashlib.sha256(crop.tobytes()).hexdigest()
            if digest in hashes:
                errors.append(f"{delivery['id']}: duplicate cell art {cell['topicId']}")
            hashes.add(digest)
            normalized = crop.resize((32, 32), Image.Resampling.NEAREST)
            category = str(topic_category[cell["topicId"]])
            diversity_cells[category].append((cell["topicId"], hashlib.sha256(normalized.tobytes()).hexdigest()))
            alpha = normalized.getchannel("A")
            if alpha.getextrema() == (0, 255):
                diversity_silhouettes[category].append((cell["topicId"], hashlib.sha256(alpha.tobytes()).hexdigest()))
    generated = [delivery for delivery in deliveries if delivery["source"] == "expanded"]
    existing = [delivery for delivery in deliveries if delivery["source"] == "existing"]
    diversity: dict[str, dict[str, int]] = {}
    for category in CATEGORY_TARGETS:
        entries = diversity_cells[category]
        unique_cells = len({value for _topic_id, value in entries})
        silhouettes = diversity_silhouettes[category]
        unique_silhouettes = len({value for _topic_id, value in silhouettes})
        if unique_cells != len(entries):
            errors.append(f"{category}: normalized visual diversity {unique_cells}/{len(entries)}")
        if len(silhouettes) >= 8 and unique_silhouettes < math.ceil(len(silhouettes) * 0.5):
            errors.append(f"{category}: silhouette diversity {unique_silhouettes}/{len(silhouettes)} below 50%")
        diversity[category] = {
            "cells": len(entries), "uniqueCells": unique_cells,
            "transparentCells": len(silhouettes), "uniqueSilhouettes": unique_silhouettes,
        }
    result = {
        "schemaVersion": 1,
        "campaign": "candidate-01",
        "targetTopics": 404,
        "checkedTopics": len(topics),
        "categoryCounts": dict(sorted(counts.items())),
        "existingTopics": sum(topic["source"] == "existing" for topic in topics),
        "expandedTopics": sum(topic["source"] == "expanded" for topic in topics),
        "deliveries": len(deliveries),
        "existingDeliveries": len(existing),
        "generatedDeliveries": len(generated),
        "checkedFiles": checked_files,
        "checkedCells": checked_cells,
        "pngSvgPixelMatches": svg_matches,
        "visualDiversity": diversity,
        "errors": errors,
        "warnings": warnings,
        "passed": not errors,
    }
    return result


def main() -> None:
    expanded = expand_topics()
    for category, target in CATEGORY_TARGETS.items():
        expected = target - (22 if category == "narrative-static" else 0)
        if len(expanded[category]) != expected:
            raise ValueError(f"expanded {category}: {len(expanded[category])} != {expected}")
    existing_delivs, existing_records = existing_deliveries()
    topic_lookup = {topic["id"]: topic for values in expanded.values() for topic in values}
    generated_delivs = [build_atlas(spec, topic_lookup) for spec in expanded_delivery_specs(expanded)]
    primary: dict[str, tuple[str, dict[str, int]]] = {}
    for delivery in generated_delivs:
        for cell in delivery["cells"]:
            if cell["topicId"] not in primary:
                primary[cell["topicId"]] = (
                    delivery["id"],
                    {"x": cell["x"], "y": cell["y"], "width": cell["width"], "height": cell["height"]},
                )
    records = list(existing_records.values())
    for category in CATEGORY_TARGETS:
        for topic in expanded[category]:
            asset_id, cell = primary[topic["id"]]
            record: dict[str, object] = {
                "id": topic["id"], "label": topic["label"], "category": category,
                "assetId": asset_id, "status": "formal", "source": "expanded", "cell": cell,
            }
            if category in {"combat-unit", "mission-unit"}:
                record["frameOrder"] = ["idle-a", "step-a", "idle-b", "step-b"]
            records.append(record)
    manifest = {
        "schemaVersion": "2.0.0",
        "campaignId": "candidate-01",
        "title": "断冠之誓",
        "targetTopics": 404,
        "categoryTargets": CATEGORY_TARGETS,
        "topicSummary": {"existing": 22, "expanded": 382, "total": 404},
        "topics": records,
        "deliveries": existing_delivs + generated_delivs,
        "supplementalExisting": {
            "namedHeroWalkSheets": 6,
            "note": "既有六张有姓名角色四帧图集继续保留，但不占40个通用战斗单位母型，也不重复占404题材。",
        },
    }
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    report = validate(manifest)
    QA_JSON.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))
    if not report["passed"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
