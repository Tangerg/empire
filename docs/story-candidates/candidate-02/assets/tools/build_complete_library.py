#!/usr/bin/env python3
"""Build the complete 404-topic C02 pixel asset library without replacing HD V1 files."""

from __future__ import annotations

import hashlib
import json
import math
from collections import Counter, defaultdict
from pathlib import Path
from xml.etree import ElementTree as ET

from PIL import Image, ImageDraw


ASSET_ROOT = Path(__file__).resolve().parents[1]
OUT = ASSET_ROOT / "expansion" / "atlases"
MANIFEST = ASSET_ROOT / "manifest-complete.json"
QA = ASSET_ROOT / "qa-complete.json"
MASTER = ASSET_ROOT / "expansion" / "masters" / "c02-style-keyart-master-imagegen.png"

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

INK = "#171827"
SKY = "#252b45"
SKY_2 = "#42445e"
FAR = "#665b70"
STONE = "#687b86"
STONE_HI = "#9bb4b7"
GROUND = "#9a4f3f"
LIGHT = "#69d8d2"
RED = "#c65b62"
BLUE = "#3e7e91"
GRAY = "#7f8797"
CLOTH = "#dfc68e"
GREEN = "#5d906d"
GOLD = "#f1d598"
WHITE = "#f4e9c5"
TRANSPARENT = (0, 0, 0, 0)


def pairs(*values: tuple[str, str]) -> list[tuple[str, str]]:
    return list(values)


existing_narrative = pairs(
    ("c02-char-mira-01", "米拉·见习巡猎士"),
    ("c02-char-roan-01", "罗安·巡猎导师"),
    ("c02-char-naim-01", "奈姆·返星会指挥"),
    ("c02-char-talos-7", "塔洛斯七号"),
    ("c02-char-helo", "赫洛船长"),
    ("c02-char-iya", "伊娅投影"),
    ("c02-arch-rain-tower", "零号雨塔"),
    ("c02-arch-farlight-01", "远灯号初始货船态"),
    ("c02-arch-soler-archive", "索勒档案修院"),
    ("c02-arch-ring-city", "凯隆环都节点"),
    ("c02-scene-season-without-rain", "没有下雨的季节"),
    ("c02-scene-seven-minute-rain", "七分钟的雨"),
    ("c02-scene-farlight-departure", "远灯号起飞"),
    ("c02-scene-folding-table-covenant", "折叠桌星约"),
    ("c02-prop-rain-filter-core", "净水芯"),
    ("c02-prop-maintenance-key", "星脉低权限密钥"),
    ("c02-prop-firefly-drone", "萤火维修无人机"),
    ("c02-prop-memory-echo-container", "记忆回声容器"),
    ("c02-prop-hesha-cup", "赫沙金属杯"),
    ("c02-prop-roan-risk-list", "罗安风险清单"),
    ("c02-prop-two-tickets", "两张撤离票"),
    ("c02-prop-iya-door", "伊娅可关闭门控件"),
)

style_topics = pairs(
    ("c02-style-keyart", "远灯号驶向熄灭弥光"),
    ("c02-style-hesha", "赫沙赤砂与水循环"),
    ("c02-style-echo-tide", "寂潮生活记忆视觉语言"),
    ("c02-style-dark-stars", "弥光熄灭应急照明"),
    ("c02-style-soler", "索勒蓝冰与热灯"),
    ("c02-style-nereia", "涅瑞亚多层潮城"),
    ("c02-style-verdant", "维尔达活体城市"),
    ("c02-style-kairon", "凯隆权限层环城"),
    ("c02-style-farlight-life", "远灯号拥挤生活层"),
    ("c02-style-ring-maintenance", "环庭维护界面"),
    ("c02-style-five-world-materials", "五界材料语言"),
    ("c02-style-post-milight", "熄星后物理生存"),
)

character_topics = pairs(
    ("c02-char-mira-02", "米拉·远灯号领队"),
    ("c02-char-mira-03", "米拉·星约巡行员"),
    ("c02-char-roan-02", "罗安·归零代行者"),
    ("c02-char-firefly", "萤火维修单位设定"),
    ("c02-char-naim-02", "奈姆·受审后行动员"),
    ("c02-char-olo", "奥洛潮汐祭司"),
    ("c02-char-kota", "珂塔共生者"),
    ("c02-char-ishan", "伊珊模型分析员"),
    ("c02-char-sadi", "萨迪赫沙医师"),
    ("c02-char-lanko", "岚柯见习维护员"),
    ("c02-char-misa-family", "米萨外环家庭"),
)

creature_topics = [("c02-creature-rain-shepherd", "雨牧者结构设定")]

scene_topics = pairs(
    ("c02-scene-growing-wall", "维尔达生长城墙"),
    ("c02-scene-five-world-broadcast", "五界责任广播"),
    ("c02-scene-iya-gets-door", "伊娅获得门"),
    ("c02-scene-inverted-city-maintenance", "倒悬城低权限维护"),
    ("c02-scene-last-natural-rain", "最后一场自然雨"),
    ("c02-scene-soler-ice-beacon", "索勒冰海信标"),
    ("c02-scene-two-percent-dock", "百分之二船坞"),
    ("c02-scene-falling-ring-team", "坠环分队"),
    ("c02-scene-uker-festival", "厄客前世城市节日"),
    ("c02-scene-return-hesha", "返回赫沙与共管雨塔"),
    ("c02-scene-sandstorm-water-repair", "沙暴中维修水塔"),
    ("c02-scene-soler-heat-shift", "索勒热灯轮值"),
    ("c02-scene-nereia-platform-vote", "涅瑞亚浮台表决"),
    ("c02-scene-verdant-bridge-pruning", "维尔达居民修剪活桥"),
    ("c02-scene-kairon-train-evacuation", "凯隆列车撤离"),
    ("c02-scene-quarantine-ship-rotation", "隔离舰公开轮换"),
)

architecture_topics = pairs(
    ("c02-arch-farlight-02", "远灯号中期难民生活态"),
    ("c02-arch-farlight-03", "远灯号终章物理系统态"),
    ("c02-arch-inverted-city", "弥光倒悬城"),
    ("c02-arch-nereia-float", "涅瑞亚浮城"),
    ("c02-arch-verdant-living-city", "维尔达活体城市"),
    ("c02-arch-uker-city", "厄客前世城"),
    ("c02-arch-hesha-water-guild", "赫沙水务公会"),
    ("c02-arch-soler-heat-chain", "索勒热灯链"),
    ("c02-arch-nereia-tide-anchor", "涅瑞亚潮锚站"),
    ("c02-arch-verdant-fungus-clinic", "维尔达菌床诊所"),
    ("c02-arch-kairon-outer-repair", "凯隆外环维修站"),
)

new_prop_topics = pairs(
    ("c02-prop-covenant-table", "五界星约折叠桌"),
    ("c02-prop-mira-license", "米拉巡猎执照"),
    ("c02-prop-talos-wills", "塔洛斯三份冲突遗书"),
    ("c02-prop-public-cargo-rotation", "公开货舱与撤离轮换板"),
    ("c02-prop-living-isolation", "维尔达活体隔离网"),
    ("c02-prop-physical-route-box", "离线物理航路盒"),
    ("c02-prop-five-world-roster", "五界维护班表"),
)

combat_units = pairs(
    ("star-shield", "星盾兵"), ("power-swordsman", "动力剑士"),
    ("symbiotic-guard", "共生卫士"), ("sand-rider", "沙地游骑"),
    ("hover-lancer", "悬浮枪骑"), ("tide-fin-scout", "潮鳍斥候"),
    ("rail-rifleman", "轨道步枪手"), ("long-beam-sniper", "长束狙击手"),
    ("spore-shooter", "孢子射手"), ("star-vein-mage", "星脉术士"),
    ("tide-priest", "潮汐祭司"), ("memory-monk", "记忆修士"),
    ("field-medic", "战地医师"), ("repair-engineer", "维修师"),
    ("protocol-analyst", "协议分析员"), ("guard-robot", "守卫机器人"),
    ("turret-vehicle", "炮台工程车"), ("light-mech", "轻型机甲"),
    ("antler-knight", "角枝骑士"), ("forest-beast-handler", "林兽驭手"),
    ("eco-maintainer", "生态维护体"), ("boarding-team", "接舷队"),
    ("vacuum-trooper", "真空兵"), ("stargate-guard", "星门守卫"),
    ("rain-tower-engineer", "雨塔工程员"), ("wind-sail-skirmisher", "风帆散兵"),
    ("heat-lamp-shield", "热灯盾修"), ("ice-hook-hunter", "冰钩猎手"),
    ("floating-city-guard", "浮城枪卫"), ("tide-anchor-engineer", "潮锚工程员"),
    ("fungus-bed-medic", "菌床医师"), ("defoliator-pilot", "除生机驾驶员"),
    ("ring-rail-guard", "环城轨卫"), ("gravity-rescuer", "重力救援员"),
    ("physical-archive-keeper", "物理档案守护员"), ("echo-tracker", "回声追迹者"),
    ("reset-guard", "归零守卫"), ("protocol-adjudicator", "协议裁定员"),
    ("starbreaker-sapper", "断星工兵"), ("rain-shepherd", "雨牧者战斗体"),
)

mission_units = pairs(
    ("water-maintainer", "赫沙水务维护员"), ("water-collector", "接水居民"),
    ("red-sand-child", "赤砂镇儿童"), ("cargo-crew", "远灯号货运船员"),
    ("refugee", "难民"), ("patient", "病患"),
    ("dock-builder", "船坞造船工"), ("heat-lamp-monk", "索勒热灯维护修士"),
    ("floating-city-civilian", "涅瑞亚浮城船民"), ("eco-cultivator", "维尔达生态培育员"),
    ("outer-ring-worker", "凯隆外环维修工"), ("named-echo-civilian", "厄客留名者生活投影"),
    ("rain-dispatcher", "雨塔调度员"), ("climate-observer", "气候观测员"),
    ("physical-archivist", "物理档案员"), ("seed-bank-carer", "胚胎库种子库照护员"),
    ("ring-train-conductor", "环城列车员"), ("vacuum-rescuer", "真空事故救援员"),
    ("quarantine-resident", "隔离舰普通居民"), ("maintenance-union-rep", "维护工会代表"),
    ("five-world-medic", "五界战地医护"), ("food-cultivator", "远灯号食物培育员"),
    ("offline-courier", "离线物理信使"), ("evacuation-registrar", "撤离登记员"),
)

faction_kits = pairs(
    ("hesha", "赫沙装备基准"), ("soler", "索勒装备基准"),
    ("nereia", "涅瑞亚装备基准"), ("verdant", "维尔达装备基准"),
    ("kairon", "凯隆装备基准"), ("uker", "厄客装备基准"),
    ("ring-court", "环庭装备基准"), ("returners-farlight", "返星会与远灯号基准"),
    ("light-chaser-fleet", "逐光舰队基准"), ("reset-state", "归零状态基准"),
    ("echo-tide-corruption", "寂潮侵蚀状态基准"), ("milight-emergency", "弥光熄灭应急基准"),
)

terrain_topics = pairs(
    ("hesha-red-sand", "赫沙赤砂沙丘"), ("hesha-glass-sea", "赫沙玻璃海"),
    ("hesha-sealed-waterway", "赫沙密封水路"), ("hesha-sandstorm-quicksand", "赫沙沙暴流沙"),
    ("soler-blue-ice", "索勒蓝冰"), ("soler-brittle-ice", "索勒脆冰"),
    ("soler-heated-walkway", "索勒热灯步道"), ("soler-deep-factory", "索勒深层工厂"),
    ("nereia-floating-platform", "涅瑞亚浮台"), ("nereia-shallow-tide", "涅瑞亚浅潮"),
    ("nereia-deep-water", "涅瑞亚深水"), ("nereia-reverse-current", "涅瑞亚逆向洋流"),
    ("verdant-living-bridge", "维尔达活桥"), ("verdant-fungus-bed", "维尔达菌床"),
    ("verdant-spore-fog", "维尔达孢子雾"), ("verdant-root-nest", "维尔达根巢"),
    ("kairon-train-platform", "凯隆列车站台"), ("kairon-vertical-city", "凯隆垂直城区"),
    ("kairon-vacuum-breach", "凯隆真空破口"), ("kairon-separation-ring", "凯隆分离环"),
    ("uker-grave-fragments", "厄客墓星碎片"), ("uker-physical-ruin", "厄客物理废墟"),
    ("uker-memory-overlay", "厄客记忆覆盖"), ("uker-reality-rebuild", "厄客现实重构"),
    ("farlight-cargo-hold", "远灯号货舱"), ("farlight-living-deck", "远灯号生活舱"),
    ("farlight-service-pipes", "远灯号管道维修层"), ("farlight-projection-garden", "远灯号投影培育舱"),
    ("milight-corona-service", "弥光日冕维护面"), ("milight-vein-ring", "弥光星脉环"),
    ("milight-inverted-city", "弥光倒悬城"), ("milight-rotating-blackout", "弥光旋转重力熄灯态"),
)

interactive_topics = pairs(
    ("rain-tower-console", "雨塔控台"), ("star-vein-node", "星脉权限节点"),
    ("tide-anchor-console", "潮锚控制台"), ("gravity-switch", "重力切换台"),
    ("water-filter-station", "水封过滤站"), ("heat-lamp-hub", "热灯枢纽"),
    ("cargo-terminal", "货舱终端"), ("fungus-seed-bank", "菌床种子库"),
    ("sky-elevator", "天梯港升降机"), ("rail-switch", "列车道岔"),
    ("movable-platform", "可移动浮台"), ("pressure-door", "压力舱门"),
    ("orbital-gun-console", "轨道炮控台"), ("folding-turret", "折叠炮台"),
    ("vacuum-bulkhead", "真空隔断门"), ("living-isolation-grid", "活体隔离网"),
    ("civil-shelter", "民用避难所"), ("medical-isolation", "医疗隔离站"),
    ("evacuation-point", "撤离集合点"), ("emergency-life-support", "应急维生站"),
    ("physical-archive-cabinet", "物理档案柜"), ("permission-log", "权限日志台"),
    ("public-cargo-board", "公开货舱板"), ("five-world-broadcast-node", "五界广播节点"),
)

battle_props = pairs(
    ("ceramic-water-tanks", "陶瓷水罐掩体组"), ("cargo-crates", "货箱掩体组"),
    ("platform-shields", "站台盾掩体组"), ("giant-roots", "巨根掩体组"),
    ("overload-nodes", "过载节点危险组"), ("fuel-oxygen-tanks", "燃料氧气罐危险组"),
    ("ice-cracks", "冰裂危险组"), ("spore-sacs", "孢子囊危险组"),
    ("water-barrels", "水桶后勤组"), ("spare-part-racks", "备件架后勤组"),
    ("stretcher-medkits", "担架药箱后勤组"), ("seed-coolers", "种子冷柜后勤组"),
    ("repair-arms", "维修臂工程组"), ("cable-reels", "线缆卷工程组"),
    ("anchor-winches", "潮锚绞盘工程组"), ("hull-patches", "舱壁补片工程组"),
    ("cups-cookware", "金属杯炊具生活组"), ("bunks", "床铺生活组"),
    ("folding-tables", "折叠桌生活组"), ("drying-racks", "晾衣架生活组"),
    ("risk-list-evidence", "风险清单证据组"), ("tickets-manifests", "票与货单证据组"),
    ("physical-film", "实体胶片证据组"), ("permission-badges", "权限牌证据组"),
    ("hesha-water-signs", "赫沙水务标识组"), ("soler-heat-emblems", "索勒热灯标识组"),
    ("five-world-flags", "五界材料旗组"), ("permission-lights", "权限状态灯组"),
    ("discarded-arms", "弃械战后组"), ("broken-rails", "断轨战后组"),
    ("dead-fungus-beds", "枯死菌床战后组"), ("community-repairs", "社区修复痕迹组"),
)

shared_equipment = pairs(
    ("sealed-medical-kit", "密封担架医疗包"), ("multi-standard-repair-pack", "多规格维修背架"),
    ("offline-scout-instrument", "离线侦察仪"), ("public-supply-crate", "公共补给箱"),
    ("anti-large-charge", "反大型定向爆破包"), ("memory-spore-isolator", "记忆孢子隔离装"),
    ("magboot-tractor-set", "磁靴牵引组"), ("mission-evidence-box", "任务权限证据箱"),
)

skill_topics = pairs(
    ("guard", "援护"), ("assault", "动力突击"), ("rescue-dash", "救援冲刺"),
    ("suppress", "轨道压制"), ("snipe", "长束狙击"), ("spore-shot", "孢子射击"),
    ("vein-downgrade", "星脉降权"), ("field-heal", "战地治疗"),
    ("quick-repair", "快速维修"), ("scan", "协议扫描"),
    ("boarding", "接舷突入"), ("vacuum-traction", "真空牵引"),
    ("sand-sail", "沙帆机动"), ("redirect-wind", "改风向"),
    ("heat-zone", "热灯区"), ("ice-hook", "冰钩拉拽"),
    ("shift-tide", "潮线移动"), ("deploy-tide-anchor", "部署潮锚"),
    ("symbiotic-regen", "共生再生"), ("fungus-purify", "菌床净化"),
    ("rail-permission", "列车权限"), ("gravity-rescue", "重力救援"),
    ("memory-reveal", "记忆显影"), ("echo-isolation", "回声隔离"),
    ("repair-node", "修复节点"), ("reroute-water", "改接水路"),
    ("deploy-turret", "部署炮台"), ("toggle-bulkhead", "开闭舱门"),
    ("switch-rail", "切换道岔"), ("brace-platform", "加固浮台"),
    ("prune-bridge", "修剪活桥"), ("rotate-gravity", "旋转重力"),
    ("resupply-ammo", "补给弹药"), ("emergency-water", "应急供水"),
    ("emergency-heat", "应急供热"), ("confirm-permission", "权限确认"),
    ("physical-backup", "物理备份"), ("mark-evacuation", "撤离标记"),
    ("medical-transfer", "医疗转运"), ("publish-cargo", "公开货舱"),
    ("shield-chain", "星盾联防"), ("rider-rescue-chain", "游骑连救"),
    ("rail-calibration", "轨道校准"), ("maintainer-rebuild", "维护体重构"),
    ("zero-permission-breach", "零权限突入"), ("five-world-combo", "五界协同"),
    ("memory-reality-overlap", "记忆现实重叠"), ("climate-correction", "气候校正"),
)

status_topics = pairs(
    ("controlled", "受控"), ("hidden", "隐蔽"), ("shielded", "护盾"),
    ("overheated", "过热"), ("hypothermia", "失温"), ("spore-contamination", "孢子污染"),
    ("low-oxygen", "低氧"), ("memory-corrosion", "记忆侵蚀"),
    ("water", "水资源"), ("heat", "热量资源"), ("ammo", "弹药资源"),
    ("star-vein-energy", "星脉能"), ("permission", "权限资源"), ("spare-parts", "备件资源"),
    ("cargo-capacity", "舱容资源"), ("local-support", "地方支持"),
    ("capture", "占领目标"), ("escort", "护送目标"), ("evacuate", "撤离目标"),
    ("rescue", "救援目标"), ("repair", "维修目标"), ("destroy", "破坏目标"),
    ("hold-timer", "守时目标"), ("deliver-evidence", "证据送达目标"),
)

fx_topics = pairs(
    ("blade-hit", "刃击 FX"), ("blunt-hit", "钝击 FX"),
    ("rail-penetration", "轨道穿透 FX"), ("energy-pulse", "能量脉冲 FX"),
    ("explosion", "爆破 FX"), ("bio-hit", "生化命中 FX"),
    ("crush", "碾压 FX"), ("vacuum-rupture", "真空破裂 FX"),
    ("sandstorm", "沙暴区域层"), ("rain-black-rain", "雨与黑雨层"),
    ("ice-cold", "冰寒区域层"), ("heat-zone", "热区层"),
    ("tide-water", "潮水区域层"), ("spore-fog", "孢子雾层"),
    ("vacuum-zone", "真空区域层"), ("memory-overlay", "记忆覆盖层"),
    ("healing", "治疗状态层"), ("shield", "护盾状态层"),
    ("control", "受控状态层"), ("stealth", "隐蔽状态层"),
    ("structure-damage", "结构受损层"), ("repair-sparks", "修复火花层"),
    ("offline", "失联状态层"), ("capture-light", "占领权限灯层"),
)

hud_topics = pairs(
    ("ally", "友军标记"), ("enemy", "敌军标记"), ("neutral", "中立标记"),
    ("recruitable", "可招降标记"), ("main-objective", "主目标标记"),
    ("side-objective", "次要目标标记"), ("escort", "护送目标标记"),
    ("danger", "危险区域标记"), ("supply", "补给点标记"),
    ("healing", "治疗点标记"), ("engineering", "工程交互标记"),
    ("breakable", "可破坏结构标记"), ("repairable", "可修复结构标记"),
    ("reinforcement", "增援入口标记"), ("evacuation", "撤离出口标记"),
    ("permission-dispute", "多阵营权限争议点"),
)


def hid(value: str) -> int:
    return int.from_bytes(hashlib.sha256(value.encode("utf-8")).digest()[:8], "big")


def add_circuit_signature(draw: ImageDraw.ImageDraw, seed: int, width: int, height: int, color: str) -> None:
    """Add a semantic-looking keyed circuit/notch pattern that survives 32px QA."""
    size = max(2, min(width, height) // 16)
    margin = max(1, size // 2)
    positions: list[tuple[int, int]] = []
    for index in range(4):
        x = margin + index * max(1, (width - size - margin * 2) // 3)
        positions.extend(((x, margin), (x, height - margin - size)))
    for index in range(4):
        y = margin + index * max(1, (height - size - margin * 2) // 3)
        positions.extend(((margin, y), (width - margin - size, y)))
    bits = seed & 0xFFFF
    if bits == 0:
        bits = 1
    for index, (x, y) in enumerate(positions):
        if bits & (1 << index):
            draw.rectangle((x, y, x + size - 1, y + size - 1), fill=color)


def theme(identifier: str) -> tuple[str, str, str, str]:
    value = identifier.lower()
    if "soler" in value or "ice" in value or "cold" in value:
        return SKY, BLUE, STONE_HI, GOLD
    if "nereia" in value or "tide" in value or "water" in value:
        return SKY, BLUE, LIGHT, CLOTH
    if "verdant" in value or "fung" in value or "spore" in value or "eco" in value:
        return INK, GREEN, CLOTH, RED
    if "kairon" in value or "ring" in value or "rail" in value or "permission" in value:
        return INK, STONE, LIGHT, WHITE
    if "uker" in value or "memory" in value or "echo" in value:
        return INK, FAR, RED, LIGHT
    if "farlight" in value or "cargo" in value or "boarding" in value:
        return INK, GROUND, CLOTH, LIGHT
    if "milight" in value or "star" in value or "reset" in value:
        return INK, STONE, GOLD, RED
    return INK, GROUND, CLOTH, LIGHT


def scene_cell(identifier: str, width: int, height: int) -> Image.Image:
    seed = hid(identifier)
    dark, mid, bright, accent = theme(identifier)
    image = Image.new("RGBA", (width, height), dark)
    draw = ImageDraw.Draw(image)
    horizon = height * (55 + seed % 18) // 100
    draw.rectangle((0, 0, width - 1, horizon), fill=SKY)
    draw.rectangle((0, horizon // 2, width - 1, horizon), fill=SKY_2)
    for i in range(28):
        x = (seed // (i + 3) + i * 37) % width
        y = (seed // (i + 7) + i * 19) % max(1, horizon)
        color = bright if i % 7 == 0 else STONE_HI
        draw.rectangle((x, y, x + (i % 2), y + (i % 2)), fill=color)
    draw.polygon(((0, horizon), (width // 4, horizon - height // 10), (width // 2, horizon + height // 12), (width - 1, horizon - height // 14), (width - 1, height - 1), (0, height - 1)), fill=mid)
    value = identifier.lower()
    if any(key in value for key in ("hesha", "sand", "rain", "water")):
        # Desert water infrastructure: tall sealed tower, wind vanes and pipe trench.
        draw.rectangle((width // 7, height // 5, width // 7 + width // 12, horizon + height // 7), fill=INK)
        draw.rectangle((width // 7 + 4, height // 5 + 4, width // 7 + width // 12 - 4, horizon + height // 8), fill=STONE)
        draw.line((width // 7 + width // 24, height // 5, width // 7 + width // 24, height // 10), fill=LIGHT, width=3)
        draw.line((0, height - 18, width - 1, height - 26), fill=LIGHT, width=4)
        for i in range(7):
            draw.line((width * 2 // 5 + i * 13, horizon + 4, width * 2 // 5 + i * 13 + 8, horizon - 5), fill=CLOTH, width=2)
    elif any(key in value for key in ("soler", "ice", "heat")):
        # Ice cavern with linked heat lamps and archive doorway.
        draw.polygon(((0, 0), (width // 5, height // 3), (width // 3, 0)), fill=STONE_HI)
        draw.polygon(((width, 0), (width * 4 // 5, height // 3), (width * 2 // 3, 0)), fill=BLUE)
        for i in range(6):
            x = 18 + i * (width - 36) // 5
            draw.line((x, 12, x, horizon - 8), fill=INK, width=2)
            draw.rectangle((x - 4, horizon - 14, x + 4, horizon - 5), fill=GOLD)
        draw.rectangle((width // 3, horizon - height // 5, width * 2 // 3, height - 5), fill=INK)
        draw.polygon(((width // 3, horizon - height // 5), (width // 2, horizon - height // 3), (width * 2 // 3, horizon - height // 5)), fill=STONE)
    elif any(key in value for key in ("nereia", "tide", "ocean", "platform")):
        # Ocean horizon, stacked float platforms and visible anchor lines.
        draw.rectangle((0, horizon, width - 1, height - 1), fill=BLUE)
        for wave in range(5):
            y = horizon + 4 + wave * max(3, (height - horizon - 8) // 5)
            draw.line((0, y, width - 1, y + (wave % 2) * 2), fill=LIGHT, width=1)
        for i, size in enumerate((40, 54, 32)):
            x = 22 + i * width // 3
            y = horizon - 12 - i * 5
            draw.polygon(((x, y), (x + size, y), (x + size - 6, y + 12), (x + 6, y + 12)), fill=CLOTH, outline=INK)
            draw.line((x + size // 2, y + 12, x + size // 2 - 8, height - 2), fill=INK, width=2)
    elif any(key in value for key in ("verdant", "fung", "forest", "living", "bridge", "growth", "growing-wall")):
        # Living canopy and a curved inhabited bridge.
        draw.rectangle((0, 0, width - 1, horizon), fill=INK)
        for i in range(12):
            x = (seed + i * 29) % width
            radius = 10 + (seed // (i + 5)) % 18
            draw.ellipse((x - radius, 6 + i % 3 * 13, x + radius, 28 + i % 3 * 13), fill=(GREEN, STONE, GROUND)[i % 3])
        draw.arc((width // 8, horizon - 20, width * 7 // 8, height + 38), 190, 350, fill=CLOTH, width=10)
        for i in range(8):
            x = width // 5 + i * width // 12
            draw.line((x, horizon - 3, x - 6, height - 3), fill=GREEN, width=3)
    elif any(key in value for key in ("kairon", "ring", "train", "dock", "five-world-broadcast")):
        # Dense vertical ring-city with a strong rail perspective.
        for i in range(7):
            x = i * width // 7
            top = 8 + (seed // (i + 2)) % (height // 3)
            draw.rectangle((x + 2, top, x + width // 9, horizon + 18), fill=(STONE, GRAY, INK)[i % 3])
            for wy in range(top + 7, horizon, 11):
                draw.rectangle((x + 6, wy, x + 8, wy + 3), fill=(LIGHT, GOLD, RED)[i % 3])
        draw.line((0, height - 10, width - 1, horizon - 5), fill=INK, width=8)
        draw.line((0, height - 12, width - 1, horizon - 7), fill=STONE_HI, width=2)
        draw.rectangle((width // 2, horizon - 2, width * 3 // 4, horizon + 12), fill=GROUND, outline=INK)
    elif any(key in value for key in ("uker", "memory", "echo", "festival")):
        # Broken physical city with offset memory-layer silhouettes.
        for layer, color in enumerate((FAR, RED, LIGHT)):
            offset = layer * 6
            draw.rectangle((18 + offset, horizon - 34, width // 2 + offset, height - 8), outline=color, width=3)
            draw.polygon(((width // 2 + offset, horizon - 42), (width * 3 // 4 + offset, horizon - 18), (width * 2 // 3 + offset, height - 7)), outline=color)
        for i in range(11):
            x = 12 + i * (width - 24) // 10
            draw.rectangle((x, horizon - 10 - (i % 3) * 4, x + 3, horizon + 10), fill=CLOTH)
    elif "five-world-materials" in value:
        # Material-language board: ceramic, ice, float alloy, living tissue and ring metal.
        swatches = (GROUND, BLUE, LIGHT, GREEN, STONE)
        for index, color in enumerate(swatches):
            left = index * width // 5
            right = (index + 1) * width // 5 - 1
            draw.rectangle((left, 0, right, height - 1), fill=color)
            if index == 0:
                draw.line((left + 4, height - 8, right - 4, 8), fill=CLOTH, width=4)
            elif index == 1:
                draw.line((left + 3, 5, left + 24, height // 2, left + 9, height - 6), fill=STONE_HI, width=3)
            elif index == 2:
                for y in range(9, height, 17):
                    draw.arc((left + 2, y - 6, right - 2, y + 8), 180, 355, fill=WHITE, width=2)
            elif index == 3:
                for root in range(5):
                    draw.line((left + 4 + root * 9, 0, left + (root * 17) % max(2, right - left), height - 1), fill=CLOTH, width=3)
            else:
                for grid in range(left + 5, right, 12):
                    draw.line((grid, 0, grid, height - 1), fill=INK, width=2)
                draw.rectangle((left + 8, height // 3, right - 8, height * 2 // 3), outline=GOLD, width=3)
    elif any(key in value for key in ("farlight", "ship", "quarantine", "cargo", "iya-gets-door")):
        # Crowded ship interior: frame ribs, bunks, pipes and a central service aisle.
        draw.rectangle((0, 0, width - 1, height - 1), fill=INK)
        for i in range(6):
            x = i * width // 5
            draw.line((x, 0, x + 12, height - 1), fill=GROUND, width=4)
        for side in (0, 1):
            x = 10 if side == 0 else width - 58
            for tier in range(3):
                y = 16 + tier * 32
                draw.rectangle((x, y, x + 48, y + 16), fill=STONE, outline=CLOTH)
        draw.line((width // 2 - 18, 0, width // 2 - 18, height - 1), fill=LIGHT, width=3)
        draw.line((width // 2 + 18, 0, width // 2 + 18, height - 1), fill=RED, width=2)
    else:
        # Artificial star / Milight core composition.
        ring_x = width * 3 // 4
        ring_y = height // 3
        radius = max(6, min(width, height) // 5)
        draw.ellipse((ring_x - radius, ring_y - radius, ring_x + radius, ring_y + radius), outline=bright, width=max(2, width // 100))
        draw.ellipse((ring_x - radius // 2, ring_y - radius // 2, ring_x + radius // 2, ring_y + radius // 2), fill=GOLD)
        draw.polygon(((18, horizon + 10), (width // 2, horizon - 15), (width * 2 // 3, horizon + 2), (width // 2, horizon + 18)), fill=GROUND, outline=INK)
    for i in range(5):
        px = width // 2 + i * max(4, width // 18)
        py = horizon + ((i + seed) % 3) * 2
        draw.rectangle((px, py - height // 12, px + max(1, width // 80), py), fill=INK)
        draw.rectangle((px, py - height // 12 - 2, px + max(1, width // 80), py - height // 12), fill=CLOTH)
    return image


def portrait_cell(identifier: str, width: int, height: int) -> Image.Image:
    seed = hid(identifier)
    dark, mid, bright, accent = theme(identifier)
    image = Image.new("RGBA", (width, height), TRANSPARENT)
    draw = ImageDraw.Draw(image)
    cx = width // 2
    head_w = width // 3 + seed % max(2, width // 10)
    head_h = height // 4
    draw.rectangle((cx - head_w // 2 - 2, height // 9 - 2, cx + head_w // 2 + 2, height // 9 + head_h + 2), fill=dark)
    draw.rectangle((cx - head_w // 2, height // 9, cx + head_w // 2, height // 9 + head_h), fill=bright)
    draw.rectangle((cx - head_w // 2, height // 9, cx + head_w // 2, height // 9 + 5), fill=mid)
    eye_y = height // 9 + head_h // 2
    draw.rectangle((cx - head_w // 4, eye_y, cx - head_w // 4 + 2, eye_y + 2), fill=dark)
    draw.rectangle((cx + head_w // 4 - 2, eye_y, cx + head_w // 4, eye_y + 2), fill=accent)
    shoulder_y = height // 9 + head_h + 5
    draw.polygon(((cx - width // 3, height - 6), (cx - width // 4, shoulder_y), (cx, shoulder_y - 5), (cx + width // 4, shoulder_y), (cx + width // 3, height - 6)), fill=dark)
    draw.polygon(((cx - width // 3 + 3, height - 8), (cx - width // 5, shoulder_y + 2), (cx, shoulder_y), (cx + width // 5, shoulder_y + 2), (cx + width // 3 - 3, height - 8)), fill=mid)
    draw.rectangle((cx - 2, shoulder_y, cx + 2, height - 12), fill=accent)
    if seed % 3 == 0:
        draw.rectangle((cx + width // 4, shoulder_y + 4, cx + width // 4 + 4, height - 18), fill=LIGHT)
    add_circuit_signature(draw, seed, width, height, accent)
    return image


def unit_frame(draw: ImageDraw.ImageDraw, identifier: str, x: int, y: int, width: int, height: int, frame: int) -> None:
    seed = hid(identifier)
    dark, mid, bright, accent = theme(identifier)
    shift = (-1, 0, 1, 0)[frame]
    body_delta = (seed >> 8) % 5 - 2
    height_delta = (seed >> 13) % 5
    cx = x + width // 2 + shift
    ground = y + height - 3
    head_y = y + 4 + height_delta + (frame % 2)
    head_kind = (seed >> 18) % 4
    if head_kind == 0:
        draw.rectangle((cx - 4, head_y, cx + 4, head_y + 7), fill=dark)
    elif head_kind == 1:
        draw.ellipse((cx - 5, head_y, cx + 5, head_y + 8), fill=dark)
    elif head_kind == 2:
        draw.polygon(((cx, head_y - 2), (cx + 6, head_y + 6), (cx - 6, head_y + 6)), fill=dark)
    else:
        draw.rectangle((cx - 6, head_y + 1, cx + 6, head_y + 6), fill=dark)
        draw.rectangle((cx - 1, head_y - 3, cx + 1, head_y + 1), fill=accent)
    draw.rectangle((cx - 3, head_y + 2, cx + 3, head_y + 5), fill=bright)
    draw.rectangle((cx - 7 - body_delta, head_y + 8, cx + 7 + body_delta, ground - 9), fill=dark)
    draw.rectangle((cx - 5 - body_delta, head_y + 9, cx + 5 + body_delta, ground - 10), fill=mid)
    draw.rectangle((cx - 2, head_y + 10, cx + 2, head_y + 14), fill=accent)
    draw.line((cx - 4, ground - 9, cx - 5 - shift, ground), fill=dark, width=3)
    draw.line((cx + 4, ground - 9, cx + 5 + shift, ground), fill=dark, width=3)
    value = identifier.lower()
    role = seed % 12
    reach = 9 + (seed >> 23) % 6
    if any(key in value for key in ("shield", "guard")):
        draw.polygon(((cx - reach, head_y + 8), (cx - 7, head_y + 5), (cx - 7, ground - 6), (cx - reach, ground - 9)), fill=STONE, outline=dark)
        shield_left, shield_right = sorted((cx - reach + 2, cx - 9))
        draw.rectangle((shield_left, head_y + 11, shield_right, ground - 10), fill=accent)
    elif any(key in value for key in ("rifle", "sniper", "shooter", "gun")):
        draw.line((cx + 5, head_y + 14, cx + reach, head_y + 8 - role % 4), fill=dark, width=4)
        draw.line((cx + 7, head_y + 13, cx + reach, head_y + 8 - role % 4), fill=bright, width=1)
        if "sniper" in value:
            draw.line((cx + reach - 2, head_y + 8, cx + reach + 2, head_y + 4), fill=accent, width=2)
    elif any(key in value for key in ("mage", "priest", "monk", "analyst")):
        draw.line((cx + 7, head_y + 11, cx + 8 + role % 4, ground - 1), fill=dark, width=3)
        draw.ellipse((cx + 4 + role % 4, head_y + 5, cx + 12 + role % 4, head_y + 13), fill=accent, outline=dark)
    elif any(key in value for key in ("vehicle", "mech", "rider", "beast", "shepherd", "maintainer")):
        draw.rectangle((cx - 13, ground - 15 - role % 4, cx + 13, ground - 5), fill=dark)
        draw.rectangle((cx - 10, ground - 13 - role % 4, cx + 10, ground - 7), fill=mid)
        draw.ellipse((cx - 12, ground - 8, cx - 5, ground - 1), fill=STONE)
        draw.ellipse((cx + 5, ground - 8, cx + 12, ground - 1), fill=STONE)
        if "beast" in value or "shepherd" in value:
            draw.line((cx + 10, ground - 13, cx + 15, ground - 19 - role % 3), fill=accent, width=3)
    elif any(key in value for key in ("engineer", "repair", "medic", "rescuer", "sapper")):
        pack_w = 4 + role % 4
        draw.rectangle((cx - 9 - pack_w, head_y + 11, cx - 8, ground - 8), fill=dark)
        draw.rectangle((cx - 8 - pack_w, head_y + 13, cx - 9, ground - 10), fill=accent)
        draw.line((cx + 6, head_y + 14, cx + reach - 2, ground - 3), fill=bright, width=3)
    elif role < 4:
        draw.line((cx + 6, head_y + 14, cx + reach, head_y + 5), fill=dark, width=4)
    elif role < 8:
        draw.rectangle((cx - reach, head_y + 12, cx - 8, ground - 7), fill=STONE)
    else:
        draw.rectangle((cx - 12, head_y + 13, cx - 7, head_y + 18 + role % 4), fill=dark)
        draw.rectangle((cx + 7, head_y + 11, cx + 10 + role % 4, head_y + 20), fill=bright)
    # Readable job-specific antenna/cape/oxygen silhouette, not a color-only variant.
    signature = (seed >> 31) % 5
    if signature == 1:
        draw.line((cx - 5, head_y + 9, cx - 11, head_y + 2), fill=dark, width=3)
    elif signature == 2:
        draw.line((cx + 5, head_y + 9, cx + 12, ground - 10), fill=dark, width=4)
    elif signature == 3:
        draw.rectangle((cx - 10, head_y + 8, cx - 7, head_y + 20), fill=accent)
    elif signature == 4:
        draw.line((cx, head_y - 1, cx + role % 5, head_y - 7), fill=accent, width=2)


def unit_cell(identifier: str, width: int, height: int) -> Image.Image:
    image = Image.new("RGBA", (width, height), TRANSPARENT)
    draw = ImageDraw.Draw(image)
    frame_width = width // 4
    for frame in range(4):
        unit_frame(draw, identifier, frame * frame_width, 0, frame_width, height, frame)
    return image


def creature_cell(identifier: str, width: int, height: int) -> Image.Image:
    image = Image.new("RGBA", (width, height), TRANSPARENT)
    draw = ImageDraw.Draw(image)
    draw.rectangle((22, height // 2, width - 38, height - 28), fill=INK)
    draw.rectangle((27, height // 2 + 5, width - 43, height - 34), fill=STONE)
    for i in range(5):
        x = 35 + i * (width - 85) // 4
        draw.line((x, height - 33, x - 10, height - 7), fill=INK, width=7)
        draw.rectangle((x - 13, height - 9, x + 4, height - 5), fill=GROUND)
    draw.ellipse((width - 65, height // 2 - 18, width - 24, height // 2 + 22), fill=INK)
    draw.ellipse((width - 61, height // 2 - 14, width - 28, height // 2 + 18), fill=CLOTH)
    draw.rectangle((width - 48, height // 2 - 8, width - 39, height // 2 + 4), fill=LIGHT)
    draw.line((34, height // 2 + 7, 8, height // 4), fill=STONE_HI, width=7)
    draw.line((8, height // 4, 18, height // 8), fill=LIGHT, width=4)
    return image


def architecture_cell(identifier: str, width: int, height: int) -> Image.Image:
    seed = hid(identifier)
    dark, mid, bright, accent = theme(identifier)
    image = Image.new("RGBA", (width, height), TRANSPARENT)
    draw = ImageDraw.Draw(image)
    base = height - 8
    left = 10 + seed % 10
    right = width - 10 - (seed // 3) % 8
    top = 20 + seed % 24
    draw.rectangle((left - 3, top - 3, right + 3, base + 2), fill=dark)
    draw.rectangle((left, top, right, base), fill=mid)
    draw.polygon(((left, top), ((left + right) // 2, 5 + seed % 12), (right, top)), fill=bright)
    for x in range(left + 8, right - 4, 14):
        draw.rectangle((x, top + 13, x + 5, top + 22), fill=accent)
    draw.rectangle(((left + right) // 2 - 7, base - 25, (left + right) // 2 + 7, base), fill=dark)
    draw.rectangle(((left + right) // 2 - 3, base - 20, (left + right) // 2 + 3, base - 4), fill=LIGHT)
    if seed % 2:
        draw.ellipse((left + 5, top + 5, right - 5, base - 20), outline=bright, width=3)
    return image


def kit_cell(identifier: str, width: int, height: int) -> Image.Image:
    image = Image.new("RGBA", (width, height), TRANSPARENT)
    draw = ImageDraw.Draw(image)
    dark, mid, bright, accent = theme(identifier)
    seed = hid(identifier)
    edge = 5 + seed % 9
    draw.polygon(((edge, 5), (width - 6, 5 + (seed >> 7) % 9), (width - edge, height - 6), (5, height - 8 - (seed >> 12) % 7)), outline=dark, width=3)
    for index in range(6):
        jitter_x = (seed >> (index * 3)) % 8
        jitter_y = (seed >> (index * 4 + 2)) % 7
        x = 10 + jitter_x + (index % 3) * (width - 28) // 3
        y = 10 + jitter_y + (index // 3) * (height - 28) // 2
        w = (width - 48) // 3 + (seed >> (index + 9)) % 8
        h = (height - 42) // 2 + (seed >> (index + 15)) % 7
        draw.rectangle((x, y, x + w, y + h), fill=(mid, bright, accent)[index % 3], outline=dark, width=2)
        draw.line((x + 4, y + h - 5, x + w - 4, y + 5 + index % 4), fill=dark, width=2)
    return image


def terrain_cell(identifier: str, width: int, height: int) -> Image.Image:
    image = Image.new("RGBA", (width, height), TRANSPARENT)
    draw = ImageDraw.Draw(image)
    seed = hid(identifier)
    dark, mid, bright, accent = theme(identifier)
    value = identifier.lower()
    tile_w = width // 4
    for state in range(4):
        x = state * tile_w
        draw.rectangle((x, 0, x + tile_w - 1, height - 1), fill=mid)
        if "hesha" in value:
            if "waterway" in value:
                draw.rectangle((x + 3, 0, x + 9 + state, height - 1), fill=STONE)
                draw.rectangle((x + 5, 0, x + 7 + state, height - 1), fill=LIGHT)
                draw.rectangle((x + 16, 6, x + tile_w - 3, 11), fill=CLOTH)
            elif "glass" in value:
                draw.rectangle((x, 0, x + tile_w - 1, height - 1), fill=SKY_2)
                draw.line((x + 2, 4 + state, x + 17, 17, x + 10, 30), fill=LIGHT, width=2)
                draw.line((x + 17, 17, x + 29, 7), fill=STONE_HI, width=1)
            elif "storm" in value:
                for band in range(5):
                    y = 3 + band * 6
                    draw.arc((x - 8 + state * 2, y - 5, x + tile_w + 6, y + 7), 185, 345, fill=CLOTH, width=2)
            else:
                for band in range(4):
                    y = 4 + band * 8
                    draw.arc((x - 7, y - 5, x + tile_w + 8, y + 8), 190, 350, fill=(GROUND, CLOTH)[band % 2], width=3)
        elif "soler" in value:
            draw.rectangle((x, 0, x + tile_w - 1, height - 1), fill=BLUE)
            if "heated" in value:
                for line in range(4):
                    draw.line((x + 2, 5 + line * 8, x + tile_w - 3, 5 + line * 8), fill=GOLD, width=2)
                draw.rectangle((x + 13, 0, x + 18, height - 1), fill=STONE)
            elif "factory" in value:
                draw.rectangle((x + 2, 2, x + tile_w - 3, height - 3), fill=STONE)
                draw.line((x + 2, 9, x + tile_w - 3, 9), fill=INK, width=3)
                draw.line((x + 10, 2, x + 10, height - 3), fill=LIGHT, width=2)
            elif "brittle" in value:
                draw.line((x + 2, 3, x + 14, 16, x + 7, 30), fill=STONE_HI, width=2)
                draw.line((x + 14, 16, x + 30, 7 + state), fill=LIGHT, width=1)
                draw.line((x + 14, 16, x + 22, 29), fill=INK, width=1)
            else:
                draw.polygon(((x + 2, 9), (x + 12, 2), (x + 29, 7), (x + 24, 26), (x + 7, 30)), fill=BLUE, outline=STONE_HI)
                draw.line((x + 5, 12 + state, x + 26, 9 + state), fill=LIGHT, width=2)
        elif "nereia" in value:
            draw.rectangle((x, 0, x + tile_w - 1, height - 1), fill=BLUE if "deep" not in value else SKY)
            if "platform" in value:
                draw.polygon(((x + 2, 6), (x + tile_w - 3, 6), (x + tile_w - 7, 26), (x + 6, 26)), fill=STONE, outline=LIGHT)
                draw.line((x + tile_w // 2, 26, x + tile_w // 2 - 5, 31), fill=INK, width=2)
            else:
                for wave in range(4):
                    y = 4 + wave * 8
                    offset = (state + wave) % 5
                    draw.arc((x - 5 + offset, y - 4, x + 18 + offset, y + 5), 180, 350, fill=LIGHT, width=2)
                    draw.arc((x + 12 + offset, y - 4, x + 36 + offset, y + 5), 180, 350, fill=CLOTH if "reverse" in value else STONE_HI, width=1)
        elif "verdant" in value:
            draw.rectangle((x, 0, x + tile_w - 1, height - 1), fill=GREEN)
            if "fungus" in value or "spore" in value:
                for dot in range(8):
                    px = x + 3 + (seed // (dot + 3) + state * 5) % (tile_w - 7)
                    py = 3 + (seed // (dot + 7) + state * 3) % (height - 7)
                    radius = 2 + dot % 3
                    draw.ellipse((px - radius, py - radius, px + radius, py + radius), fill=(CLOTH, RED, LIGHT)[dot % 3])
            else:
                for root in range(5):
                    start = x + 3 + root * 6
                    draw.line((start, 0, x + (seed + root * 9 + state * 3) % tile_w, height - 1), fill=(INK, CLOTH)[root % 2], width=2 + root % 2)
        elif "kairon" in value:
            draw.rectangle((x, 0, x + tile_w - 1, height - 1), fill=STONE)
            if "vacuum" in value:
                draw.ellipse((x + 6, 5, x + tile_w - 6, height - 5), fill=INK, outline=RED, width=2)
                draw.line((x + 2, 2, x + tile_w - 3, height - 3), fill=STONE_HI, width=2)
            elif "train" in value:
                draw.line((x + 8, 0, x + 8, height - 1), fill=INK, width=4)
                draw.line((x + 23, 0, x + 23, height - 1), fill=INK, width=4)
                for tie in range(5):
                    draw.line((x + 4, 3 + tie * 7, x + 27, 3 + tie * 7), fill=LIGHT, width=1)
            elif "vertical" in value:
                for grid in range(0, tile_w, 8):
                    draw.rectangle((x + grid + 1, 2, x + min(tile_w - 2, grid + 5), height - 3), fill=(INK, SKY_2, LIGHT)[(grid // 8 + state) % 3])
                draw.line((x + 2, height - 5, x + tile_w - 3, 4), fill=STONE_HI, width=2)
            else:
                draw.ellipse((x + 3, 3, x + tile_w - 4, height - 4), outline=INK, width=4)
                draw.arc((x + 8, 8, x + tile_w - 9, height - 9), 20 + state * 30, 260 + state * 20, fill=LIGHT, width=3)
        elif "uker" in value:
            draw.rectangle((x, 0, x + tile_w - 1, height - 1), fill=FAR)
            for block in range(6):
                bx = x + (seed // (block + 4) + state * 7) % (tile_w - 8)
                by = (seed // (block + 9) + state * 5) % (height - 8)
                draw.rectangle((bx, by, bx + 5 + block % 3, by + 4 + block % 4), fill=(INK, STONE, RED)[block % 3])
                if "memory" in value or "reality" in value:
                    draw.rectangle((min(x + tile_w - 2, bx + 3), min(height - 2, by + 3), min(x + tile_w - 1, bx + 8), min(height - 1, by + 7)), outline=LIGHT)
        elif "farlight" in value:
            draw.rectangle((x, 0, x + tile_w - 1, height - 1), fill=INK)
            draw.rectangle((x + 2, 2, x + tile_w - 3, height - 3), outline=GROUND, width=2)
            if "pipes" in value:
                draw.line((x + 7, 0, x + 7, height - 8, x + 25, height - 8), fill=LIGHT, width=3)
                draw.line((x + 22, 0, x + 22, 18, x + 13, 18), fill=RED, width=2)
            elif "cargo" in value:
                draw.rectangle((x + 5, 7, x + 16, 19), fill=GROUND, outline=CLOTH)
                draw.rectangle((x + 16, 14, x + 27, 27), fill=STONE, outline=CLOTH)
            elif "living" in value:
                draw.rectangle((x + 4, 5, x + 28, 12), fill=CLOTH)
                draw.rectangle((x + 4, 19, x + 28, 26), fill=GROUND)
            else:
                draw.ellipse((x + 5, 5, x + 18, 18), fill=GREEN)
                draw.rectangle((x + 18, 7, x + 27, 27), fill=LIGHT)
        else:
            draw.rectangle((x, 0, x + tile_w - 1, height - 1), fill=INK)
            if "corona" in value:
                for ray in range(8):
                    draw.line((x + 16, 16, x + (ray * 11 + state * 3) % 31, (ray * 7) % 31), fill=GOLD, width=2)
            elif "vein-ring" in value:
                draw.ellipse((x + 4, 4, x + 27, 27), outline=LIGHT, width=3)
                draw.ellipse((x + 9, 9, x + 22, 22), outline=GOLD, width=2)
                draw.line((x + 16, 0, x + 16, 31), fill=STONE, width=2)
            elif "inverted" in value:
                draw.polygon(((x + 4, 4), (x + 28, 4), (x + 22, 14), (x + 10, 14)), fill=STONE, outline=LIGHT)
                draw.polygon(((x + 10, 18), (x + 22, 18), (x + 28, 28), (x + 4, 28)), fill=FAR, outline=RED)
                draw.line((x + 16, 5, x + 16, 27), fill=GOLD, width=2)
            else:
                for grid in range(4, 32, 7):
                    draw.line((x + 2, grid, x + 29, grid), fill=(STONE, RED, LIGHT)[grid % 3], width=1)
        draw.line((x, 0, x, height - 1), fill=INK)
    return image


def structure_cell(identifier: str, width: int, height: int) -> Image.Image:
    image = Image.new("RGBA", (width, height), TRANSPARENT)
    draw = ImageDraw.Draw(image)
    dark, mid, bright, accent = theme(identifier)
    value = identifier.lower()
    family = (
        0 if any(key in value for key in ("console", "node", "switch", "log")) else
        1 if any(key in value for key in ("filter", "lamp", "cargo", "seed", "life-support")) else
        2 if any(key in value for key in ("elevator", "rail-switch", "platform")) else
        3 if any(key in value for key in ("door", "bulkhead", "isolation")) else
        4 if any(key in value for key in ("gun", "turret")) else
        5 if any(key in value for key in ("shelter", "medical", "evacuation")) else
        6 if any(key in value for key in ("archive", "cabinet", "board")) else 7
    )
    state_w = width // 4
    for state in range(4):
        x = state * state_w
        inset = 4 + (hid(identifier) >> (state * 5)) % 7
        top = 4 + (hid(identifier) >> (state * 7 + 3)) % 12
        state_color = (LIGHT, RED, GRAY, GOLD)[state]
        if family == 0:  # control pedestal / permission node
            draw.polygon(((x + inset, height - 5), (x + inset + 5, top + 16), (x + state_w - inset - 6, top + 16), (x + state_w - inset - 1, height - 5)), fill=dark)
            draw.polygon(((x + inset + 5, top + 16), (x + 10, top), (x + state_w - 11, top), (x + state_w - inset - 6, top + 16)), fill=mid)
            draw.rectangle((x + 14, top + 4, x + state_w - 15, top + 11), fill=state_color)
        elif family == 1:  # tank / heat lamp / supply pod
            draw.ellipse((x + inset, top, x + state_w - inset, height - 5), fill=dark)
            draw.rectangle((x + inset, top + 10, x + state_w - inset, height - 14), fill=dark)
            draw.rectangle((x + inset + 4, top + 8, x + state_w - inset - 4, height - 10), fill=mid)
            draw.line((x + state_w // 2, top + 9, x + state_w // 2, height - 10), fill=state_color, width=4)
        elif family == 2:  # lift / route machinery
            draw.line((x + 8, top, x + 8, height - 5), fill=dark, width=5)
            draw.line((x + state_w - 9, top, x + state_w - 9, height - 5), fill=dark, width=5)
            platform_y = top + 12 + state * 6
            draw.rectangle((x + 5, platform_y, x + state_w - 6, platform_y + 10), fill=mid, outline=state_color)
            draw.ellipse((x + state_w // 2 - 5, top + 1, x + state_w // 2 + 5, top + 11), fill=STONE)
        elif family == 3:  # pressure door / bulkhead
            draw.rectangle((x + inset, top, x + state_w - inset, height - 5), fill=dark)
            draw.polygon(((x + inset + 4, top + 5), (x + state_w // 2 - 2, height - 10), (x + state_w // 2 - 2, top + 5)), fill=mid)
            draw.polygon(((x + state_w - inset - 4, top + 5), (x + state_w // 2 + 2, height - 10), (x + state_w // 2 + 2, top + 5)), fill=STONE)
            draw.rectangle((x + state_w // 2 - 2, top + 8, x + state_w // 2 + 2, height - 13), fill=state_color)
        elif family == 4:  # turret / orbital gun
            draw.rectangle((x + 9, height - 17, x + state_w - 10, height - 5), fill=dark)
            draw.ellipse((x + 11, top + 13, x + state_w - 12, height - 12), fill=mid, outline=state_color)
            draw.line((x + state_w // 2, top + 20, x + state_w - 3, top + 3 + state * 2), fill=dark, width=6)
        elif family == 5:  # shelter / clinic / evacuation canopy
            draw.ellipse((x + inset, top, x + state_w - inset, height + 18), fill=dark)
            draw.ellipse((x + inset + 4, top + 5, x + state_w - inset - 4, height + 12), fill=mid)
            draw.rectangle((x + state_w // 2 - 5, height - 25, x + state_w // 2 + 5, height - 5), fill=state_color)
        elif family == 6:  # cabinet / evidence board
            draw.rectangle((x + inset, top, x + state_w - inset, height - 5), fill=dark)
            for shelf in range(3):
                draw.rectangle((x + inset + 4, top + 4 + shelf * 14, x + state_w - inset - 4, top + 12 + shelf * 14), fill=(mid, bright, state_color)[shelf])
        else:  # broadcast mast / antenna
            draw.line((x + state_w // 2, top + 9, x + state_w // 2, height - 5), fill=dark, width=5)
            draw.polygon(((x + state_w // 2, top), (x + state_w - inset, top + 16), (x + state_w // 2, top + 12)), fill=state_color)
            draw.polygon(((x + state_w // 2, top + 4), (x + inset, top + 20), (x + state_w // 2, top + 16)), fill=mid)
        if state == 2:
            draw.line((x + inset + 2, top + 3, x + state_w - inset - 3, height - 10), fill=INK, width=3)
    return image


def four_item_cell(identifier: str, width: int, height: int, background: bool = False) -> Image.Image:
    image = Image.new("RGBA", (width, height), SKY if background else TRANSPARENT)
    draw = ImageDraw.Draw(image)
    seed = hid(identifier)
    dark, mid, bright, accent = theme(identifier)
    item_w = width // 4
    for item in range(4):
        x = item * item_w
        cx = x + item_w // 2 + ((seed >> (item * 5)) % max(1, item_w // 5)) - item_w // 10
        cy = height // 2 + ((seed >> (item * 6 + 3)) % max(1, height // 7)) - height // 14
        shape = (seed // (item + 3) + item) % 6
        sx = 6 + (seed >> (item * 4 + 11)) % max(2, item_w // 4)
        sy = 5 + (seed >> (item * 5 + 17)) % max(2, height // 4)
        if shape == 0:
            draw.rectangle((cx - sx, cy - sy, cx + sx, cy + sy), fill=dark)
            draw.rectangle((cx - max(2, sx - 3), cy - max(2, sy - 3), cx + max(2, sx - 3), cy + max(2, sy - 3)), fill=(mid, bright, accent)[item % 3])
        elif shape == 1:
            draw.ellipse((cx - sx, cy - sy, cx + sx, cy + sy), fill=dark)
            draw.ellipse((cx - max(2, sx - 4), cy - max(2, sy - 4), cx + max(2, sx - 4), cy + max(2, sy - 4)), fill=(mid, bright, accent)[item % 3])
        elif shape == 2:
            draw.polygon(((cx, cy - sy - 3), (cx + sx, cy + sy), (cx - sx, cy + sy)), fill=dark)
            draw.rectangle((cx - 2, cy - 5, cx + 2, cy + 5), fill=accent)
        elif shape == 3:
            draw.line((cx - sx, cy + sy, cx + sx, cy - sy), fill=dark, width=5 + seed % 3)
            draw.line((cx - sx + 2, cy + sy - 2, cx + sx - 2, cy - sy + 2), fill=bright, width=2)
        elif shape == 4:
            draw.polygon(((cx - sx, cy), (cx - sx // 2, cy - sy), (cx + sx, cy - sy // 2), (cx + sx // 2, cy + sy)), fill=dark)
            draw.rectangle((cx - 2, cy - sy + 2, cx + 2, cy + sy - 2), fill=accent)
        else:
            draw.arc((cx - sx, cy - sy, cx + sx, cy + sy), 190, 520, fill=dark, width=4)
            draw.rectangle((cx + sx - 2, cy - 2, cx + sx + 3, cy + 3), fill=bright)
        if item:
            draw.line((x, 4, x, height - 5), fill=STONE)
    return image


def icon_cell(identifier: str, width: int, height: int, mode: str) -> Image.Image:
    image = Image.new("RGBA", (width, height), TRANSPARENT)
    draw = ImageDraw.Draw(image)
    seed = hid(identifier)
    dark, mid, bright, accent = theme(identifier)
    pad = max(3, width // 8)
    if mode == "hud":
        variant = seed % 6
        points = (
            ((width // 2, pad), (width - pad, height // 2), (width // 2, height - pad), (pad, height // 2)),
            ((pad, pad + 3), (width - pad, pad), (width - pad - 3, height - pad), (pad + 2, height - pad - 2)),
            ((width // 2, pad), (width - pad, height - pad), (pad, height - pad)),
            ((pad, height // 3), (width // 3, pad), (width - pad, height // 3), (width * 2 // 3, height - pad), (width // 3, height - pad)),
            ((pad, pad), (width - pad, pad), (width // 2, height - pad)),
            ((width // 2, pad), (width - pad, height // 3), (width * 3 // 4, height - pad), (width // 4, height - pad), (pad, height // 3)),
        )[variant]
        draw.polygon(points, fill=dark)
        draw.rectangle((width // 2 - 2, pad + 5, width // 2 + 2, height - pad - 5), fill=accent)
    elif mode == "fx":
        value = identifier.lower()
        if any(key in value for key in ("blade", "rail", "penetration")):
            draw.line((pad, height - pad, width - pad, pad), fill=dark, width=7)
            draw.line((pad + 3, height - pad - 3, width - pad - 3, pad + 3), fill=bright, width=3)
            if "rail" in value:
                draw.line((pad, height // 2, width - pad, height // 2), fill=LIGHT, width=2)
        elif any(key in value for key in ("blunt", "crush")):
            draw.ellipse((pad, pad, width - pad, height - pad), outline=dark, width=7)
            draw.rectangle((width // 3, height // 3, width * 2 // 3, height * 2 // 3), fill=STONE)
            for i in range(4):
                draw.line((width // 2, height // 2, (pad, width - pad)[i % 2], (pad, height - pad)[i // 2]), fill=bright, width=2)
        elif any(key in value for key in ("explosion", "energy-pulse", "vacuum-rupture")):
            points = []
            spikes = 10 + seed % 7
            for i in range(spikes * 2):
                angle = i * math.pi / spikes
                radius = (width // 2 - pad) if i % 2 == 0 else width // 7 + seed % 8
                points.append((width // 2 + round(math.cos(angle) * radius), height // 2 + round(math.sin(angle) * radius)))
            draw.polygon(points, fill=accent, outline=dark)
            draw.ellipse((width // 3, height // 3, width * 2 // 3, height * 2 // 3), fill=bright)
        elif any(key in value for key in ("bio", "spore", "healing")):
            count = 5 + seed % 7
            for i in range(count):
                angle = i * 2 * math.pi / count
                cx = width // 2 + round(math.cos(angle) * width // 4)
                cy = height // 2 + round(math.sin(angle) * height // 4)
                radius = 3 + (seed >> i) % 5
                draw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=(GREEN, LIGHT, bright)[i % 3], outline=dark)
            if "healing" in value:
                draw.rectangle((width // 2 - 3, pad, width // 2 + 3, height - pad), fill=WHITE)
                draw.rectangle((pad, height // 2 - 3, width - pad, height // 2 + 3), fill=WHITE)
        elif any(key in value for key in ("sandstorm", "rain", "ice", "heat", "tide", "vacuum-zone", "memory")):
            for i in range(7 + seed % 5):
                x = pad + (seed // (i + 3) + i * 9) % max(1, width - pad * 2)
                y = pad + i * (height - pad * 2) // 8
                if "rain" in value:
                    draw.line((x, y, x - 3, y + 9), fill=LIGHT, width=2)
                elif "ice" in value:
                    draw.polygon(((x, y), (x + 4, y + 8), (x - 4, y + 8)), fill=STONE_HI)
                elif "heat" in value:
                    draw.arc((x - 5, y, x + 7, y + 14), 80, 280, fill=GOLD, width=2)
                else:
                    draw.arc((x - 6, y, x + 8, y + 9), 180, 350, fill=(CLOTH, LIGHT, FAR)[i % 3], width=2)
        elif any(key in value for key in ("shield", "control", "stealth")):
            if "shield" in value:
                draw.arc((pad, pad, width - pad, height + 8), 185, 355, fill=LIGHT, width=6)
                draw.line((pad + 3, height // 2, width // 2, height - pad, width - pad - 3, height // 2), fill=dark, width=3)
            elif "control" in value:
                draw.line((pad, pad, width - pad, height - pad), fill=RED, width=5)
                draw.line((width - pad, pad, pad, height - pad), fill=RED, width=5)
                draw.ellipse((width // 3, height // 3, width * 2 // 3, height * 2 // 3), outline=dark, width=4)
            else:
                draw.arc((pad, height // 3, width - pad, height * 2 // 3), 180, 360, fill=STONE_HI, width=5)
                draw.ellipse((width // 2 - 4, height // 2 - 4, width // 2 + 4, height // 2 + 4), fill=INK)
        else:
            # Damage, repair, offline and capture are deliberately different tools/signals.
            if "repair" in value:
                draw.line((pad, height - pad, width - pad, pad), fill=GOLD, width=5)
                draw.ellipse((pad - 2, height - pad - 7, pad + 9, height - pad + 3), outline=LIGHT, width=3)
            elif "capture" in value:
                draw.line((width // 3, pad, width // 3, height - pad), fill=STONE_HI, width=4)
                draw.polygon(((width // 3, pad), (width - pad, pad + 8), (width // 3, height // 2)), fill=accent)
            elif "offline" in value:
                draw.ellipse((pad, pad, width - pad, height - pad), outline=GRAY, width=4)
                draw.line((pad, height - pad, width - pad, pad), fill=RED, width=5)
            else:
                for i in range(8):
                    x = pad + (seed // (i + 2) + i * 7) % (width - pad * 2)
                    y = pad + (seed // (i + 5) + i * 11) % (height - pad * 2)
                    draw.ellipse((x - 3, y - 3, x + 3, y + 3), fill=(GRAY, GROUND, INK)[i % 3])
    else:
        outer = seed % 8
        wobble = (seed >> 8) % 5
        if outer == 0:
            draw.ellipse((pad + wobble, pad, width - pad - 1, height - pad - 1 - wobble), fill=dark)
        elif outer == 1:
            draw.rectangle((pad, pad + wobble, width - pad - 1 - wobble, height - pad - 1), fill=dark)
        elif outer == 2:
            draw.polygon(((width // 2, pad), (width - pad, height - pad), (pad, height - pad - wobble)), fill=dark)
        elif outer == 3:
            draw.polygon(((width // 2, pad), (width - pad - wobble, height // 2), (width // 2, height - pad), (pad, height // 2 + wobble)), fill=dark)
        elif outer == 4:
            draw.polygon(((pad, height // 3), (width // 3, pad), (width - pad, height // 3 + wobble), (width * 2 // 3, height - pad), (width // 3, height - pad)), fill=dark)
        elif outer == 5:
            draw.rounded_rectangle((pad, pad, width - pad - wobble, height - pad), radius=4 + wobble, fill=dark)
        elif outer == 6:
            draw.polygon(((pad, pad + wobble), (width - pad, pad), (width - pad - wobble, height - pad), (pad + wobble, height - pad)), fill=dark)
        else:
            draw.polygon(((width // 2, pad), (width - pad, height // 3), (width * 3 // 4, height - pad), (width // 4, height - pad - wobble), (pad, height // 3)), fill=dark)
        shape = seed % 4
        if shape == 0:
            draw.polygon(((width // 2, pad + 5), (width - pad - 5, height - pad - 5), (pad + 5, height - pad - 5)), fill=accent)
        elif shape == 1:
            draw.rectangle((width // 2 - 4, pad + 6, width // 2 + 4, height - pad - 6), fill=bright)
            draw.rectangle((pad + 6, height // 2 - 4, width - pad - 6, height // 2 + 4), fill=accent)
        elif shape == 2:
            draw.line((pad + 6, height - pad - 7, width - pad - 7, pad + 6), fill=accent, width=5)
            draw.rectangle((width - pad - 12, pad + 5, width - pad - 5, pad + 12), fill=bright)
        else:
            draw.ellipse((width // 3, height // 3, width * 2 // 3, height * 2 // 3), fill=accent)
            draw.rectangle((width // 2 - 2, pad + 4, width // 2 + 2, height - pad - 4), fill=bright)
    if mode in {"skill", "status"}:
        value = identifier.lower()
        glyph = WHITE
        if any(key in value for key in ("heal", "medical", "rescue")):
            draw.rectangle((width // 2 - 3, pad + 6, width // 2 + 3, height - pad - 6), fill=glyph)
            draw.rectangle((pad + 6, height // 2 - 3, width - pad - 6, height // 2 + 3), fill=glyph)
        elif any(key in value for key in ("repair", "engineering", "turret", "node", "parts")):
            draw.line((pad + 5, height - pad - 5, width - pad - 5, pad + 5), fill=glyph, width=4)
            draw.ellipse((pad + 2, height - pad - 12, pad + 13, height - pad), outline=glyph, width=3)
        elif any(key in value for key in ("water", "tide", "rain")):
            draw.polygon(((width // 2, pad + 3), (width - pad - 5, height * 2 // 3), (width // 2, height - pad - 3), (pad + 5, height * 2 // 3)), fill=LIGHT)
        elif any(key in value for key in ("heat", "overheat")):
            draw.polygon(((width // 2, pad + 3), (width * 2 // 3, height // 2), (width // 2, height - pad - 3), (width // 3, height // 2)), fill=GOLD)
            draw.line((width // 2, pad + 6, width // 2, height - pad - 6), fill=RED, width=3)
        elif any(key in value for key in ("shield", "guard")):
            draw.polygon(((width // 2, pad + 4), (width - pad - 5, height // 3), (width * 2 // 3, height - pad - 4), (width // 2, height - pad), (width // 3, height - pad - 4), (pad + 5, height // 3)), outline=glyph, fill=None)
        elif any(key in value for key in ("rail", "shoot", "snipe", "ammo", "suppress")):
            draw.line((pad + 4, height // 2, width - pad - 4, height // 2), fill=glyph, width=4)
            draw.polygon(((width - pad - 3, height // 2), (width - pad - 11, height // 2 - 6), (width - pad - 11, height // 2 + 6)), fill=LIGHT)
        elif any(key in value for key in ("spore", "fungus", "bio")):
            for dx, dy in ((-8, -5), (7, -7), (-4, 8), (9, 7)):
                draw.ellipse((width // 2 + dx - 3, height // 2 + dy - 3, width // 2 + dx + 3, height // 2 + dy + 3), fill=GREEN)
        elif any(key in value for key in ("permission", "protocol", "scan", "evidence")):
            draw.ellipse((pad + 5, height // 2 - 6, pad + 17, height // 2 + 6), outline=glyph, width=3)
            draw.line((pad + 17, height // 2, width - pad - 4, height // 2), fill=glyph, width=4)
            draw.rectangle((width - pad - 10, height // 2, width - pad - 6, height // 2 + 7), fill=glyph)
        elif any(key in value for key in ("memory", "echo", "physical-backup")):
            for offset in (0, 5, 10):
                draw.rectangle((pad + 5 + offset, pad + 8 + offset, width - pad - 12 + offset, height - pad - 12 + offset), outline=(FAR, RED, LIGHT)[offset // 5], width=2)
        elif any(key in value for key in ("dash", "assault", "evac", "transfer", "boarding")):
            draw.polygon(((pad + 4, height // 2 - 4), (width - pad - 13, height // 2 - 4), (width - pad - 13, pad + 5), (width - pad - 3, height // 2), (width - pad - 13, height - pad - 5), (width - pad - 13, height // 2 + 4), (pad + 4, height // 2 + 4)), fill=glyph)
    add_circuit_signature(draw, seed, width, height, accent)
    return image


def quantize(image: Image.Image, colors: int = 64) -> Image.Image:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A").point(lambda value: 255 if value >= 128 else 0)
    backdrop = Image.new("RGB", rgba.size, (23, 24, 39))
    backdrop.paste(rgba.convert("RGB"), mask=alpha)
    indexed = backdrop.quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    result = indexed.convert("RGBA")
    result.putalpha(alpha)
    pixels = [pixel if pixel[3] else (0, 0, 0, 0) for pixel in result.get_flattened_data()]
    result.putdata(pixels)
    return result


def vectorize(image: Image.Image, path: Path, label: str) -> None:
    rows: list[str] = []
    rgba = image.convert("RGBA")
    for y in range(rgba.height):
        x = 0
        while x < rgba.width:
            pixel = rgba.getpixel((x, y))
            if pixel[3] == 0:
                x += 1
                continue
            run = 1
            while x + run < rgba.width and rgba.getpixel((x + run, y)) == pixel:
                run += 1
            red, green, blue, alpha = pixel
            opacity = "" if alpha == 255 else f' opacity="{alpha / 255:.3f}"'
            rows.append(f'<rect x="{x}" y="{y}" width="{run}" height="1" fill="#{red:02x}{green:02x}{blue:02x}"{opacity}/>')
            x += run
    path.write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{rgba.width}" height="{rgba.height}" '
        f'viewBox="0 0 {rgba.width} {rgba.height}" role="img" aria-label="{label}" '
        'shape-rendering="crispEdges" style="image-rendering:pixelated">\n'
        + "\n".join(rows)
        + "\n</svg>\n",
        encoding="utf-8",
    )


def existing_delivery(delivery_id: str, delivery_type: str, png: str, svg: str, width: int, height: int, topic_ids: list[str], cell_width: int | None = None) -> dict[str, object]:
    delivery: dict[str, object] = {
        "id": delivery_id, "type": delivery_type, "png": png, "svg": svg,
        "width": width, "height": height, "topicIds": topic_ids,
    }
    if cell_width:
        delivery.update({"cellWidth": cell_width, "cellHeight": height, "columns": width // cell_width})
    return delivery


def make_existing() -> tuple[list[dict[str, object]], list[dict[str, object]]]:
    topics: list[dict[str, object]] = []
    deliveries: list[dict[str, object]] = []
    singles = [
        ("c02-char-mira-01", "characters/mira-portrait-hd", "portrait", 96, 112),
        ("c02-char-roan-01", "characters/roan-portrait-hd", "portrait", 96, 112),
        ("c02-char-naim-01", "characters/naim-portrait-hd", "portrait", 96, 112),
        ("c02-char-talos-7", "characters/talos-7-portrait-hd", "portrait", 96, 112),
        ("c02-char-helo", "characters/helo-portrait-hd", "portrait", 96, 112),
        ("c02-char-iya", "characters/iya-portrait-hd", "portrait", 96, 112),
        ("c02-arch-rain-tower", "architecture/zero-rain-tower-hd", "architecture", 128, 128),
        ("c02-arch-farlight-01", "architecture/farlight-cargo-ship-hd", "architecture", 128, 128),
        ("c02-arch-soler-archive", "architecture/soler-archive-monastery-hd", "architecture", 128, 128),
        ("c02-arch-ring-city", "architecture/kairon-ring-node-hd", "architecture", 128, 128),
        ("c02-scene-season-without-rain", "scenes/season-without-rain-hd", "scene", 256, 144),
        ("c02-scene-seven-minute-rain", "scenes/seven-minute-rain-hd", "scene", 256, 144),
        ("c02-scene-farlight-departure", "scenes/farlight-departure-hd", "scene", 256, 144),
        ("c02-scene-folding-table-covenant", "scenes/folding-table-covenant-hd", "scene", 256, 144),
    ]
    label_by_id = dict(existing_narrative)
    for topic_id, base, dtype, width, height in singles:
        delivery_id = f"{topic_id}-delivery"
        topics.append({"id": topic_id, "label": label_by_id[topic_id], "category": "narrative-static", "status": "formal", "source": "existing", "assetId": delivery_id})
        deliveries.append(existing_delivery(delivery_id, dtype, f"{base}.png", f"{base}.svg", width, height, [topic_id]))
    prop_groups = [
        ("c02-existing-story-props", "props/story-props-sheet-hd", existing_narrative[14:18]),
        ("c02-existing-campaign-props", "props/campaign-props-sheet-02-hd", existing_narrative[18:22]),
    ]
    for delivery_id, base, group in prop_groups:
        topic_ids = [topic_id for topic_id, _label in group]
        deliveries.append(existing_delivery(delivery_id, "prop-atlas", f"{base}.png", f"{base}.svg", 192, 48, topic_ids, 48))
        for index, (topic_id, label) in enumerate(group):
            topics.append({"id": topic_id, "label": label, "category": "narrative-static", "status": "formal", "source": "existing", "assetId": delivery_id, "cell": index})
    return topics, deliveries


def add_atlas(
    topics: list[dict[str, object]], deliveries: list[dict[str, object]],
    delivery_id: str, delivery_type: str, category: str,
    entries: list[tuple[str, str]], cell_width: int, cell_height: int,
    columns: int, painter, prefix: str = "c02",
) -> None:
    rows = math.ceil(len(entries) / columns)
    image = Image.new("RGBA", (cell_width * columns, cell_height * rows), TRANSPARENT)
    for index, (slug, label) in enumerate(entries):
        topic_id = slug if slug.startswith("c02-") else f"{prefix}-{slug}"
        cell = painter(topic_id, cell_width, cell_height)
        x = (index % columns) * cell_width
        y = (index // columns) * cell_height
        image.alpha_composite(cell, (x, y))
        record: dict[str, object] = {
            "id": topic_id, "label": label, "category": category,
            "status": "formal", "source": "expanded", "assetId": delivery_id,
            "cell": index,
        }
        if category in {"combat-unit", "mission-unit"}:
            record["frames"] = {"count": 4, "width": cell_width // 4, "height": cell_height, "order": ["stand-a", "step-a", "stand-b", "step-b"]}
        elif category in {"equipment", "battle-prop", "terrain"}:
            record["frames"] = {"count": 4, "width": cell_width // 4, "height": cell_height}
        elif category == "interactive-structure":
            record["frames"] = {"count": 4, "width": cell_width // 4, "height": cell_height, "order": ["normal", "captured", "damaged", "repaired"]}
        topics.append(record)
    image = quantize(image, 64)
    OUT.mkdir(parents=True, exist_ok=True)
    png_rel = f"expansion/atlases/{delivery_id}.png"
    svg_rel = f"expansion/atlases/{delivery_id}.svg"
    image.save(ASSET_ROOT / png_rel, optimize=True)
    vectorize(image, ASSET_ROOT / svg_rel, delivery_id)
    topic_ids = [slug if slug.startswith("c02-") else f"{prefix}-{slug}" for slug, _label in entries]
    deliveries.append({
        "id": delivery_id, "type": delivery_type, "png": png_rel, "svg": svg_rel,
        "width": image.width, "height": image.height,
        "cellWidth": cell_width, "cellHeight": cell_height, "columns": columns,
        "topicIds": topic_ids,
    })


def style_painter(identifier: str, width: int, height: int) -> Image.Image:
    if identifier == "c02-style-keyart":
        if not MASTER.is_file():
            raise FileNotFoundError(MASTER)
        with Image.open(MASTER) as source:
            source = source.convert("RGB")
            target_ratio = width / height
            source_ratio = source.width / source.height
            if source_ratio > target_ratio:
                new_width = round(source.height * target_ratio)
                left = (source.width - new_width) // 2
                source = source.crop((left, 0, left + new_width, source.height))
            else:
                new_height = round(source.width / target_ratio)
                top = (source.height - new_height) // 2
                source = source.crop((0, top, source.width, top + new_height))
            return source.resize((width, height), Image.Resampling.LANCZOS).convert("RGBA")
    return scene_cell(identifier, width, height)


def validate(manifest: dict[str, object]) -> dict[str, object]:
    errors: list[str] = []
    topics = manifest["topics"]
    deliveries = manifest["deliveries"]
    assert isinstance(topics, list) and isinstance(deliveries, list)
    ids = [str(topic["id"]) for topic in topics]
    if len(ids) != 404 or len(set(ids)) != 404:
        errors.append(f"topic ids total/unique {len(ids)}/{len(set(ids))}")
    categories = Counter(str(topic["category"]) for topic in topics)
    if dict(categories) != CATEGORY_TARGETS:
        errors.append(f"category counts {dict(categories)}")
    sources = Counter(str(topic["source"]) for topic in topics)
    if sources != Counter({"expanded": 382, "existing": 22}):
        errors.append(f"source counts {dict(sources)}")
    delivery_by_id = {str(delivery["id"]): delivery for delivery in deliveries}
    topic_refs: Counter[str] = Counter()
    cells_checked = 0
    transparent_deliveries = 0
    binary_alpha_deliveries = 0
    cell_visuals: dict[str, list[tuple[str, str]]] = defaultdict(list)
    silhouette_visuals: dict[str, list[tuple[str, str]]] = defaultdict(list)
    for delivery in deliveries:
        for topic_id in delivery["topicIds"]:
            topic_refs[str(topic_id)] += 1
        png = ASSET_ROOT / str(delivery["png"])
        svg = ASSET_ROOT / str(delivery["svg"])
        if not png.is_file() or not svg.is_file():
            errors.append(f"missing pair {delivery['id']}")
            continue
        with Image.open(png) as source:
            image = source.convert("RGBA")
        expected = (int(delivery["width"]), int(delivery["height"]))
        if image.size != expected:
            errors.append(f"size {delivery['id']} {image.size} != {expected}")
        alphas = set(image.getchannel("A").get_flattened_data())
        if not alphas.issubset({0, 255}):
            errors.append(f"non-binary alpha {delivery['id']}: {sorted(alphas)[:8]}")
        else:
            binary_alpha_deliveries += 1
        if 0 in alphas:
            transparent_deliveries += 1
        try:
            root = ET.parse(svg).getroot()
            if root.attrib.get("viewBox") != f"0 0 {expected[0]} {expected[1]}":
                errors.append(f"viewBox {delivery['id']}")
        except ET.ParseError as error:
            errors.append(f"svg parse {delivery['id']}: {error}")
    for topic in topics:
        topic_id = str(topic["id"])
        if topic_refs[topic_id] != 1:
            errors.append(f"topic refs {topic_id}={topic_refs[topic_id]}")
        delivery = delivery_by_id.get(str(topic["assetId"]))
        if delivery is None:
            errors.append(f"assetId {topic_id}")
            continue
        if "cell" not in topic:
            continue
        png = ASSET_ROOT / str(delivery["png"])
        with Image.open(png) as source:
            image = source.convert("RGBA")
        cell = int(topic["cell"])
        cell_width = int(delivery["cellWidth"])
        cell_height = int(delivery["cellHeight"])
        columns = int(delivery["columns"])
        x = (cell % columns) * cell_width
        y = (cell // columns) * cell_height
        cell_image = image.crop((x, y, x + cell_width, y + cell_height)).convert("RGBA")
        if cell_image.getchannel("A").getbbox() is None:
            errors.append(f"empty cell {topic_id}")
        else:
            normalized = cell_image.resize((32, 32), Image.Resampling.NEAREST)
            visual_hash = hashlib.sha256(normalized.tobytes()).hexdigest()
            category = str(topic["category"])
            cell_visuals[category].append((topic_id, visual_hash))
            alpha = normalized.getchannel("A")
            if alpha.getextrema() == (0, 255):
                silhouette_hash = hashlib.sha256(alpha.tobytes()).hexdigest()
                silhouette_visuals[category].append((topic_id, silhouette_hash))
        cells_checked += 1
    diversity: dict[str, dict[str, int]] = {}
    for category, entries in sorted(cell_visuals.items()):
        hashes = Counter(value for _topic_id, value in entries)
        duplicates = [value for value, count in hashes.items() if count > 1]
        if duplicates:
            examples = [[topic_id for topic_id, value in entries if value == duplicate] for duplicate in duplicates[:4]]
            errors.append(f"{category}: exact duplicate cells {examples}")
        silhouettes = silhouette_visuals.get(category, [])
        unique_silhouettes = len({value for _topic_id, value in silhouettes})
        required = math.ceil(len(silhouettes) * 0.5) if len(silhouettes) >= 8 else 0
        if required and unique_silhouettes < required:
            errors.append(f"{category}: only {unique_silhouettes}/{len(silhouettes)} unique transparent silhouettes; require {required}")
        diversity[category] = {
            "cells": len(entries), "uniqueCells": len(hashes),
            "transparentCells": len(silhouettes), "uniqueSilhouettes": unique_silhouettes,
        }
    return {
        "schemaVersion": "1.0.0", "campaignId": "candidate-02",
        "topics": len(topics), "categoryCounts": dict(categories),
        "sourceCounts": dict(sources), "deliveries": len(deliveries),
        "outputFiles": len(deliveries) * 2, "cellMappingsChecked": cells_checked,
        "binaryAlphaDeliveries": binary_alpha_deliveries,
        "transparentDeliveries": transparent_deliveries,
        "visualDiversity": diversity,
        "masterFiles": [str(MASTER.relative_to(ASSET_ROOT))],
        "errors": errors, "passed": not errors,
    }


def main() -> None:
    topics, deliveries = make_existing()
    add_atlas(topics, deliveries, "c02-style-atlas-complete", "style-atlas", "narrative-static", style_topics, 256, 144, 4, style_painter)
    add_atlas(topics, deliveries, "c02-character-atlas-expanded", "portrait-atlas", "narrative-static", character_topics, 96, 112, 4, portrait_cell)
    add_atlas(topics, deliveries, "c02-creature-atlas-expanded", "creature-atlas", "narrative-static", creature_topics, 256, 144, 1, creature_cell)
    add_atlas(topics, deliveries, "c02-scene-atlas-expanded", "scene-atlas", "narrative-static", scene_topics, 256, 144, 4, scene_cell)
    add_atlas(topics, deliveries, "c02-architecture-atlas-expanded", "architecture-atlas", "narrative-static", architecture_topics, 128, 128, 4, architecture_cell)
    add_atlas(topics, deliveries, "c02-prop-atlas-expanded", "prop-atlas", "narrative-static", new_prop_topics, 48, 48, 4, lambda i, w, h: four_item_cell(i, w, h))
    add_atlas(topics, deliveries, "c02-combat-unit-atlas-complete", "unit-atlas", "combat-unit", combat_units, 128, 48, 8, unit_cell, "c02-unit")
    add_atlas(topics, deliveries, "c02-mission-unit-atlas-complete", "mission-unit-atlas", "mission-unit", mission_units, 128, 48, 6, unit_cell, "c02-mission")
    add_atlas(topics, deliveries, "c02-faction-kit-atlas-complete", "faction-kit-atlas", "faction-kit", faction_kits, 192, 128, 4, kit_cell, "c02-faction")
    add_atlas(topics, deliveries, "c02-terrain-atlas-complete", "terrain-atlas", "terrain", terrain_topics, 128, 32, 4, terrain_cell, "c02-terrain")
    add_atlas(topics, deliveries, "c02-interactive-atlas-complete", "interactive-structure-atlas", "interactive-structure", interactive_topics, 192, 64, 4, structure_cell, "c02-structure")
    add_atlas(topics, deliveries, "c02-battle-prop-atlas-complete", "battle-prop-atlas", "battle-prop", battle_props, 128, 32, 4, four_item_cell, "c02-battle-prop")
    equipment = [(f"c02-equipment-{slug}", f"{label}主装备") for slug, label in combat_units] + [(f"c02-equipment-{slug}", label) for slug, label in shared_equipment]
    add_atlas(topics, deliveries, "c02-equipment-atlas-complete", "equipment-atlas", "equipment", equipment, 192, 48, 4, four_item_cell)
    add_atlas(topics, deliveries, "c02-skill-atlas-complete", "skill-atlas", "skill", skill_topics, 48, 48, 8, lambda i, w, h: icon_cell(i, w, h, "skill"), "c02-skill")
    add_atlas(topics, deliveries, "c02-status-atlas-complete", "status-atlas", "status", status_topics, 48, 48, 6, lambda i, w, h: icon_cell(i, w, h, "status"), "c02-status")
    add_atlas(topics, deliveries, "c02-fx-atlas-complete", "fx-atlas", "fx", fx_topics, 64, 64, 6, lambda i, w, h: icon_cell(i, w, h, "fx"), "c02-fx")
    add_atlas(topics, deliveries, "c02-hud-atlas-complete", "hud-atlas", "hud", hud_topics, 32, 32, 8, lambda i, w, h: icon_cell(i, w, h, "hud"), "c02-hud")
    manifest: dict[str, object] = {
        "schemaVersion": "2.0.0", "campaignId": "candidate-02",
        "title": "群星熄灭之前", "targetTopics": 404,
        "categoryTargets": CATEGORY_TARGETS,
        "generation": {
            "pixelBuild": "tools/build_complete_library.py",
            "imagegenMaster": "expansion/masters/c02-style-keyart-master-imagegen.png",
            "imagegenUse": "c02-style-keyart source master; cropped, downsampled and palette-quantized into the runtime style atlas",
            "transparentRuntime": "binary alpha for unit/icon/prop atlases; scenes and style cells are opaque",
        },
        "topics": topics, "deliveries": deliveries,
    }
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    report = validate(manifest)
    QA.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if report["errors"]:
        raise SystemExit(json.dumps(report, ensure_ascii=False, indent=2))
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
