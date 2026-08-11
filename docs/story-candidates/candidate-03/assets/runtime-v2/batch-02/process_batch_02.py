#!/usr/bin/env python3
"""Build Candidate 03 runtime-v2 Batch 02 from preserved ImageGen masters."""

from __future__ import annotations

import base64
from collections import Counter, deque
from dataclasses import dataclass
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
from typing import Any

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, ImageOps


HERE = Path(__file__).resolve().parent
ASSETS = HERE.parents[1]
MASTERS = HERE / "masters"
INTERMEDIATE = HERE / "intermediate"
PREVIEWS = HERE / "previews"
MANIFEST_PATH = ASSETS / "manifest-runtime-v2-b02.json"
QA_PATH = ASSETS / "qa-runtime-v2-b02.json"
COMPLETE_PATH = ASSETS / "manifest-complete.json"
V1_PATH = ASSETS / "manifest-runtime-v2.json"

LANCZOS = Image.Resampling.LANCZOS
NEAREST = Image.Resampling.NEAREST
NO_DITHER = Image.Dither.NONE


@dataclass(frozen=True)
class Out:
    id: str
    topic_id: str
    label: str
    content_id: str
    type: str
    stem: str
    directory: str
    width: int
    height: int
    master: str
    metadata: dict[str, Any]

    @property
    def png(self) -> Path:
        return HERE / self.directory / f"{self.stem}.png"

    @property
    def svg(self) -> Path:
        return HERE / self.directory / f"{self.stem}.svg"


COMBAT = [
    ("c03-b02-unit-modao", "c03-unit-ff5e54ed", "陌刀队", "grain.unit.modao", "modao", "combat-infantry-alpha.png", 0, 32, 48, {"columns": 1, "rows": 1}, "unit"),
    ("c03-b02-unit-heavy-crossbow", "c03-unit-6fa9292e", "强弩营", "grain.unit.heavy-crossbow", "heavy-crossbow", "combat-infantry-alpha.png", 1, 32, 48, {"columns": 1, "rows": 1}, "unit"),
    ("c03-b02-unit-divine-arm-crossbow", "c03-unit-15c058cc", "神臂弩", "grain.unit.divine-arm-crossbow", "divine-arm-crossbow", "combat-infantry-alpha.png", 2, 32, 48, {"columns": 1, "rows": 1}, "unit"),
    ("c03-b02-unit-light-cavalry", "c03-unit-0137a889", "游骑", "grain.unit.light-cavalry", "light-cavalry", "combat-special-alpha.png", 0, 64, 48, {"columns": 2, "rows": 1}, "mounted"),
    ("c03-b02-unit-armored-cavalry", "c03-unit-c11ad030", "甲骑", "grain.unit.armored-cavalry", "armored-cavalry", "combat-special-alpha.png", 1, 64, 48, {"columns": 2, "rows": 1}, "mounted"),
    ("c03-b02-unit-border-scout", "c03-unit-58b98436", "边军斥候", "grain.unit.border-scout", "border-scout", "combat-infantry-alpha.png", 3, 32, 48, {"columns": 1, "rows": 1}, "unit"),
    ("c03-b02-unit-mengchong", "c03-unit-222078e0", "艨艟", "grain.unit.mengchong", "mengchong", "combat-special-alpha.png", 2, 96, 64, {"columns": 3, "rows": 2}, "vessel"),
    ("c03-b02-unit-tower-ship-marine", "c03-unit-25f91749", "楼船兵", "grain.unit.tower-ship-marine", "tower-ship-marine", "combat-special-alpha.png", 3, 32, 48, {"columns": 1, "rows": 1}, "unit"),
]

MISSION = [
    ("c03-b02-mission-refugee", "c03-mission-57df34e0", "饥民与灾民", "grain.mission.refugee", "refugee", "mission-civilians-a-alpha.png", 0),
    ("c03-b02-mission-farmer", "c03-mission-8e6e6bf3", "农户", "grain.mission.farmer", "farmer", "mission-civilians-a-alpha.png", 1),
    ("c03-b02-mission-sowing-team", "c03-mission-0d1763ad", "播种队", "grain.mission.sowing-team", "sowing-team", "mission-civilians-a-alpha.png", 2),
    ("c03-b02-mission-dike-apprentice", "c03-mission-1ba1c2b5", "河工学徒", "grain.mission.dike-apprentice", "dike-apprentice", "mission-civilians-a-alpha.png", 3),
    ("c03-b02-mission-water-league-deckhand", "c03-mission-74ec4633", "水盟船工", "grain.mission.water-league-deckhand", "water-league-deckhand", "mission-civilians-b-alpha.png", 0),
    ("c03-b02-mission-salt-worker", "c03-mission-d647c582", "盐工", "grain.mission.salt-worker", "salt-worker", "mission-civilians-b-alpha.png", 1),
    ("c03-b02-mission-granary-porter", "c03-mission-2edb8e76", "仓库工", "grain.mission.granary-porter", "granary-porter", "mission-civilians-b-alpha.png", 2),
    ("c03-b02-mission-grain-cart-driver", "c03-mission-b283af47", "粮车夫", "grain.mission.grain-cart-driver", "grain-cart-driver", "mission-civilians-b-alpha.png", 3),
]

TERRAINS = [
    ("c03-b02-terrain-cracked-field", "c03-terrain-99390ac8", "龟裂灾田", "grain.terrain.cracked-famine-field", "cracked-famine-field", "repeat-variants"),
    ("c03-b02-terrain-receding-mud", "c03-terrain-980abbed", "退水烂泥", "grain.terrain.receding-mud", "receding-mud", "repeat-variants"),
    ("c03-b02-terrain-reclaimed-furrow", "c03-terrain-b499c3bf", "复耕田垄", "grain.terrain.reclaimed-furrow", "reclaimed-furrow", "repeat-variants"),
    ("c03-b02-terrain-stone-seep-ditch", "c03-terrain-44bffd59", "石脚渗沟", "grain.terrain.stone-seep-ditch", "stone-seep-ditch", "nesw-16"),
    ("c03-b02-terrain-flood-channel", "c03-terrain-769e91a5", "分洪渠", "grain.terrain.flood-channel", "flood-channel", "nesw-16"),
    ("c03-b02-terrain-reed-bank", "c03-terrain-e1b682d1", "芦苇岸", "grain.terrain.reed-bank", "reed-bank", "repeat-variants"),
    ("c03-b02-terrain-pontoon-edge", "c03-terrain-98a758f5", "浮桥板与桥缘", "grain.terrain.pontoon-edge", "pontoon-edge", "nesw-16"),
    ("c03-b02-terrain-wall-walk", "c03-terrain-f3c866de", "城墙顶面", "grain.terrain.wall-walk", "wall-walk", "nesw-16"),
]

STRUCTURES = [
    ("c03-b02-structure-command-banner", "c03-structure-8c1f5eac", "中军旗点", "grain.structure.command-banner", "command-banner", 0, {"columns": 3, "rows": 2}),
    ("c03-b02-structure-city-gate-control", "c03-structure-14b4ae0d", "城门控制位", "grain.structure.city-gate-control", "city-gate-control", 1, {"columns": 3, "rows": 2}),
    ("c03-b02-structure-navigation-beacon", "c03-structure-59225afb", "航道信号台", "grain.structure.navigation-beacon", "navigation-beacon", 2, {"columns": 2, "rows": 2}),
    ("c03-b02-structure-field-supply-depot", "c03-structure-f793619a", "野战粮站", "grain.structure.field-supply-depot", "field-supply-depot", 3, {"columns": 3, "rows": 2}),
]

PROPS = [
    ("c03-b02-prop-grain-crate-cover", "c03-battle-prop-b6bd8efb", "粮箱掩体组", "grain.prop.grain-crate-cover", "grain-crate-cover", {"columns": 1, "rows": 1}),
    ("c03-b02-prop-wood-shield-wall", "c03-battle-prop-0fb580c9", "木盾墙组", "grain.prop.wood-shield-wall", "wood-shield-wall", {"columns": 2, "rows": 1}),
    ("c03-b02-prop-saltbag-cover", "c03-battle-prop-ff4a64b9", "盐袋掩体组", "grain.prop.saltbag-cover", "saltbag-cover", {"columns": 1, "rows": 1}),
    ("c03-b02-prop-overturned-grain-cart", "c03-battle-prop-a7b85246", "翻倒粮车组", "grain.prop.overturned-grain-cart", "overturned-grain-cart", {"columns": 2, "rows": 1}),
    ("c03-b02-prop-powder-barrel", "c03-battle-prop-809c152b", "火药桶危险组", "grain.prop.powder-barrel", "powder-barrel", {"columns": 1, "rows": 1}),
    ("c03-b02-prop-fire-oil-jar", "c03-battle-prop-7a878eb8", "火油罐危险组", "grain.prop.fire-oil-jar", "fire-oil-jar", {"columns": 1, "rows": 1}),
    ("c03-b02-prop-loose-dike-earth", "c03-battle-prop-ede52a71", "松动堤土危险组", "grain.prop.loose-dike-earth", "loose-dike-earth", {"columns": 2, "rows": 1}),
    ("c03-b02-prop-fire-arrow-bundle", "c03-battle-prop-1f1ab459", "燃烧箭束危险组", "grain.prop.fire-arrow-bundle", "fire-arrow-bundle", {"columns": 1, "rows": 1}),
]

EQUIPMENT = [
    ("c03-b02-equipment-modao", "c03-equip-ff5e54ed", "陌刀队主装备组", "grain.equipment.modao", "equipment-modao"),
    ("c03-b02-equipment-heavy-crossbow", "c03-equip-6fa9292e", "强弩营主装备组", "grain.equipment.heavy-crossbow", "equipment-heavy-crossbow"),
    ("c03-b02-equipment-divine-arm-crossbow", "c03-equip-15c058cc", "神臂弩主装备组", "grain.equipment.divine-arm-crossbow", "equipment-divine-arm-crossbow"),
    ("c03-b02-equipment-light-cavalry", "c03-equip-0137a889", "游骑主装备组", "grain.equipment.light-cavalry", "equipment-light-cavalry"),
    ("c03-b02-equipment-armored-cavalry", "c03-equip-c11ad030", "甲骑主装备组", "grain.equipment.armored-cavalry", "equipment-armored-cavalry"),
    ("c03-b02-equipment-border-scout", "c03-equip-58b98436", "边军斥候主装备组", "grain.equipment.border-scout", "equipment-border-scout"),
    ("c03-b02-equipment-mengchong", "c03-equip-222078e0", "艨艟主装备组", "grain.equipment.mengchong", "equipment-mengchong"),
    ("c03-b02-equipment-tower-ship-marine", "c03-equip-25f91749", "楼船兵主装备组", "grain.equipment.tower-ship-marine", "equipment-tower-ship-marine"),
]

SKILLS = [
    ("c03-b02-skill-armor-break", "c03-skill-9238e301", "破甲", "grain.skill.armor-break", "skill-armor-break"),
    ("c03-b02-skill-aimed-shot", "c03-skill-140c5ddd", "瞄射", "grain.skill.aimed-shot", "skill-aimed-shot"),
    ("c03-b02-skill-suppression", "c03-skill-344cc154", "压制", "grain.skill.suppression", "skill-suppression"),
    ("c03-b02-skill-cavalry-flank", "c03-skill-96ff26d1", "骑军侧击", "grain.skill.cavalry-flank", "skill-cavalry-flank"),
    ("c03-b02-skill-pursuit", "c03-skill-59e0edce", "追击", "grain.skill.pursuit", "skill-pursuit"),
    ("c03-b02-skill-boarding", "c03-skill-4181fbc8", "接舷", "grain.skill.boarding", "skill-boarding"),
    ("c03-b02-skill-field-treatment", "c03-skill-7d81885c", "治疗", "grain.skill.field-treatment", "skill-field-treatment"),
    ("c03-b02-skill-banner-inspire", "c03-skill-ee087273", "旗鼓振奋", "grain.skill.banner-inspire", "skill-banner-inspire"),
]

STATUS = [
    ("c03-b02-status-wounded", "c03-status-444f7804", "负伤", "grain.status.wounded", "status-wounded"),
    ("c03-b02-status-burning", "c03-status-355f61aa", "着火", "grain.status.burning", "status-burning"),
    ("c03-b02-status-soaked", "c03-status-396c70f2", "湿透", "grain.status.soaked", "status-soaked"),
    ("c03-b02-status-routed", "c03-status-a9b5519b", "溃退", "grain.status.routed", "status-routed"),
]

HUD = [
    ("c03-b02-hud-ally", "c03-hud-358588ad", "友军标记", "grain.hud.ally", "hud-ally"),
    ("c03-b02-hud-enemy", "c03-hud-abe1e247", "敌军标记", "grain.hud.enemy", "hud-enemy"),
    ("c03-b02-hud-neutral", "c03-hud-f870305b", "中立标记", "grain.hud.neutral", "hud-neutral"),
    ("c03-b02-hud-recruitable", "c03-hud-ed346324", "可招降标记", "grain.hud.recruitable", "hud-recruitable"),
]

FX = [
    ("c03-b02-fx-musket-hit", "c03-fx-bdc68cab", "火铳命中", "grain.fx.musket-hit", "musket-hit", 14, "add", False, {"x": 16, "y": 16}),
    ("c03-b02-fx-demolition-blast", "c03-fx-e590996f", "爆破命中", "grain.fx.demolition-blast", "demolition-blast", 12, "normal", False, {"x": 16, "y": 31}),
    ("c03-b02-fx-heavy-rain-area", "c03-fx-aab458ba", "暴雨区域", "grain.fx.heavy-rain-area", "heavy-rain-area", 10, "normal", True, {"x": 16, "y": 31}),
    ("c03-b02-fx-snowstorm-area", "c03-fx-78d874c1", "风雪区域", "grain.fx.snowstorm-area", "snowstorm-area", 10, "normal", True, {"x": 16, "y": 16}),
]

SCENES = [
    ("c03-b02-scene-pre-dawn-grain-cart", "c03-scene-8be502ba", "天未亮的押粮车", "grain.scene.pre-dawn-grain-cart", "pre-dawn-grain-cart", "scene-pre-dawn-grain-cart-master.png"),
    ("c03-b02-scene-breach-boundary-dispute", "c03-scene-737e345e", "决口后的界碑争议", "grain.scene.breach-boundary-dispute", "breach-boundary-dispute", "scene-breach-boundary-dispute-master.png"),
    ("c03-b02-scene-reed-bank-white-lamp", "c03-scene-016bc02a", "芦苇滩白灯赠粮", "grain.scene.reed-bank-white-lamp", "reed-bank-white-lamp", "scene-reed-bank-white-lamp-master.png"),
    ("c03-b02-scene-first-banner-law", "c03-scene-f40c6e2b", "第一面军旗与军法宣读", "grain.scene.first-banner-law", "first-banner-law", "scene-first-banner-law-master.png"),
]

MASK_ORDER = ["none", "n", "e", "ne", "s", "ns", "es", "nes", "w", "nw", "ew", "new", "sw", "nsw", "esw", "nesw"]


def setup() -> None:
    for name in ["units", "mission-units", "terrain", "structures", "props", "equipment", "skills", "status", "hud", "fx", "scenes", "intermediate", "previews"]:
        (HERE / name).mkdir(parents=True, exist_ok=True)


def chroma_helper() -> Path:
    root = Path(os.environ.get("CODEX_HOME", Path.home() / ".codex"))
    path = root / "skills/.system/imagegen/scripts/remove_chroma_key.py"
    if not path.is_file():
        raise FileNotFoundError(path)
    return path


def remove_keys() -> None:
    jobs = {
        "combat-infantry-master.png": "combat-infantry-alpha.png",
        "combat-special-master.png": "combat-special-alpha.png",
        "mission-civilians-a-master.png": "mission-civilians-a-alpha.png",
        "mission-civilians-b-master.png": "mission-civilians-b-alpha.png",
        "structures-b02-master.png": "structures-b02-alpha.png",
        "props-b02-master.png": "props-b02-alpha.png",
        "equipment-b02-master.png": "equipment-b02-alpha.png",
        "skills-b02-master.png": "skills-b02-alpha.png",
        "status-hud-b02-master.png": "status-hud-b02-alpha.png",
        "fx-b02-master.png": "fx-b02-alpha.png",
    }
    helper = chroma_helper()
    for source, target in jobs.items():
        subprocess.run([
            sys.executable, str(helper), "--input", str(MASTERS / source), "--out", str(INTERMEDIATE / target),
            "--auto-key", "border", "--soft-matte", "--transparent-threshold", "12", "--opaque-threshold", "220", "--despill", "--force",
        ], check=True)


def bounds(size: int, count: int) -> list[int]:
    return [round(index * size / count) for index in range(count + 1)]


def cell(image: Image.Image, cols: int, rows: int, col: int, row: int) -> Image.Image:
    xs, ys = bounds(image.width, cols), bounds(image.height, rows)
    return image.crop((xs[col], ys[row], xs[col + 1], ys[row + 1]))


def alpha_bbox(image: Image.Image, threshold: int = 12) -> tuple[int, int, int, int] | None:
    return image.convert("RGBA").getchannel("A").point(lambda value: 255 if value >= threshold else 0).getbbox()


def crop_alpha(image: Image.Image, threshold: int = 12, pad: int = 2) -> Image.Image:
    rgba = image.convert("RGBA")
    box = alpha_bbox(rgba, threshold)
    if box is None:
        raise ValueError("empty alpha cell")
    left, top, right, bottom = box
    return rgba.crop((max(0, left - pad), max(0, top - pad), min(rgba.width, right + pad), min(rgba.height, bottom + pad)))


def isolate_primary(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    mask = rgba.getchannel("A").point(lambda value: 255 if value >= 12 else 0).filter(ImageFilter.MaxFilter(3))
    pixels = mask.load()
    seen = bytearray(mask.width * mask.height)
    groups: list[tuple[int, list[tuple[int, int]]]] = []
    for y in range(mask.height):
        for x in range(mask.width):
            pos = y * mask.width + x
            if seen[pos] or pixels[x, y] == 0:
                continue
            queue = deque([(x, y)])
            seen[pos] = 1
            points: list[tuple[int, int]] = []
            while queue:
                px, py = queue.popleft()
                points.append((px, py))
                for nx, ny in ((px - 1, py), (px + 1, py), (px, py - 1), (px, py + 1)):
                    if 0 <= nx < mask.width and 0 <= ny < mask.height:
                        npos = ny * mask.width + nx
                        if not seen[npos] and pixels[nx, ny]:
                            seen[npos] = 1
                            queue.append((nx, ny))
            groups.append((len(points), points))
    if not groups:
        return rgba

    def score(group: tuple[int, list[tuple[int, int]]]) -> float:
        count, points = group
        mean_x = sum(point[0] for point in points) / count
        return count * (1.0 if mask.width * 0.15 <= mean_x <= mask.width * 0.85 else 0.15)

    _, selected = max(groups, key=score)
    keep = Image.new("L", rgba.size, 0)
    keep_px = keep.load()
    for x, y in selected:
        keep_px[x, y] = 255
    keep = keep.filter(ImageFilter.MaxFilter(3))
    rgba.putalpha(ImageChops.multiply(rgba.getchannel("A"), keep))
    return rgba


def quant_rgba(image: Image.Image, colors: int, binary: bool) -> Image.Image:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    if binary:
        alpha = alpha.point(lambda value: 255 if value >= 64 else 0)
    rgb = Image.new("RGB", rgba.size)
    rgb.paste(rgba.convert("RGB"), mask=alpha)
    rgb = rgb.quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=NO_DITHER).convert("RGB")
    result = rgb.convert("RGBA")
    result.putalpha(alpha)
    return result


def quant_opaque(image: Image.Image, colors: int) -> Image.Image:
    return image.convert("RGB").quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=NO_DITHER).convert("RGB")


def fit(image: Image.Image, size: tuple[int, int], limit: tuple[int, int], *, align: str = "bottom", flip: bool = False, binary: bool = True, colors: int = 64) -> Image.Image:
    source = crop_alpha(image)
    if flip:
        source = ImageOps.mirror(source)
    scale = min(limit[0] / source.width, limit[1] / source.height)
    resized = source.resize((max(1, round(source.width * scale)), max(1, round(source.height * scale))), LANCZOS)
    canvas = Image.new("RGBA", size)
    x = (size[0] - resized.width) // 2
    y = (size[1] - resized.height) // 2 if align == "center" else size[1] - resized.height
    canvas.alpha_composite(resized, (x, y))
    result = quant_rgba(canvas, colors, binary)
    if align == "bottom":
        box = alpha_bbox(result)
        if box is not None and box[3] < size[1]:
            grounded = Image.new("RGBA", size)
            grounded.alpha_composite(result, (0, size[1] - box[3]))
            result = grounded
    return result


def fit_human(image: Image.Image) -> Image.Image:
    source = crop_alpha(isolate_primary(image))
    source = ImageOps.mirror(source)
    target_h = 46
    target_w = min(30, max(1, round(source.width * target_h / source.height)))
    resized = source.resize((target_w, target_h), LANCZOS)
    canvas = Image.new("RGBA", (32, 48))
    canvas.alpha_composite(resized, ((32 - target_w) // 2, 2))
    return quant_rgba(canvas, 48, True)


def save_pair(image: Image.Image, out: Out) -> None:
    out.png.parent.mkdir(parents=True, exist_ok=True)
    image.save(out.png, optimize=True)
    encoded = base64.b64encode(out.png.read_bytes()).decode("ascii")
    out.svg.write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{out.width}" height="{out.height}" viewBox="0 0 {out.width} {out.height}">\n'
        f'  <image width="{out.width}" height="{out.height}" style="image-rendering:pixelated" href="data:image/png;base64,{encoded}"/>\n</svg>\n',
        encoding="utf-8",
    )


def build_units(outputs: list[Out]) -> None:
    opened: dict[str, Image.Image] = {}
    for asset_id, topic, label, content, stem, source_name, row, fw, fh, footprint, z_order in COMBAT:
        master = opened.setdefault(source_name, Image.open(INTERMEDIATE / source_name).convert("RGBA"))
        frames = []
        for col in range(4):
            source = isolate_primary(cell(master, 4, 4, col, row))
            frame = fit_human(source) if (fw, fh) == (32, 48) else fit(source, (fw, fh), (fw - 2, fh - 2), flip=True, colors=64)
            frames.append(frame)
        sheet = Image.new("RGBA", (fw * 4, fh))
        for index, frame in enumerate(frames):
            sheet.alpha_composite(frame, (index * fw, 0))
        out = Out(asset_id, topic, label, content, "combat-unit", stem, "units", sheet.width, sheet.height, source_name.replace("-alpha", "-master"), {
            "frameWidth": fw, "frameHeight": fh, "frames": 4, "frameOrder": ["idle-a", "step-a", "idle-b", "step-b"],
            "anchor": {"x": fw // 2, "y": fh - 1}, "footprint": footprint, "sourceFacing": "right", "zOrder": z_order,
        })
        save_pair(sheet, out)
        outputs.append(out)

    for asset_id, topic, label, content, stem, source_name, row in MISSION:
        master = opened.setdefault(source_name, Image.open(INTERMEDIATE / source_name).convert("RGBA"))
        frames = [fit_human(cell(master, 4, 4, col, row)) for col in range(4)]
        sheet = Image.new("RGBA", (128, 48))
        for index, frame in enumerate(frames):
            sheet.alpha_composite(frame, (index * 32, 0))
        out = Out(asset_id, topic, label, content, "mission-unit", stem, "mission-units", 128, 48, source_name.replace("-alpha", "-master"), {
            "frameWidth": 32, "frameHeight": 48, "frames": 4, "frameOrder": ["idle-a", "step-a", "idle-b", "step-b"],
            "anchor": {"x": 16, "y": 47}, "footprint": {"columns": 1, "rows": 1}, "sourceFacing": "right", "zOrder": "mission-unit",
        })
        save_pair(sheet, out)
        outputs.append(out)


def seamless(source: Image.Image, variant: int) -> Image.Image:
    margin_x, margin_y = source.width // 5, source.height // 5
    width, height = source.width - margin_x * 2, source.height - margin_y * 2
    shifts = [(-0.12, -0.10), (0.12, -0.08), (-0.10, 0.12), (0.10, 0.10)]
    sx, sy = shifts[variant % 4]
    left, top = round(margin_x + sx * margin_x), round(margin_y + sy * margin_y)
    tile = source.crop((left, top, left + width, top + height)).resize((32, 32), LANCZOS).convert("RGB")
    px = tile.load()

    def mix(a: tuple[int, int, int], b: tuple[int, int, int], amount: float) -> tuple[int, int, int]:
        return tuple(round(av * (1 - amount) + bv * amount) for av, bv in zip(a, b))

    for y in range(32):
        target = tuple(round((a + b) / 2) for a, b in zip(px[0, y], px[31, y]))
        for offset, amount in enumerate((1.0, 0.65, 0.35, 0.15)):
            px[offset, y], px[31 - offset, y] = mix(px[offset, y], target, amount), mix(px[31 - offset, y], target, amount)
    for x in range(32):
        target = tuple(round((a + b) / 2) for a, b in zip(px[x, 0], px[x, 31]))
        for offset, amount in enumerate((1.0, 0.65, 0.35, 0.15)):
            px[x, offset], px[x, 31 - offset] = mix(px[x, offset], target, amount), mix(px[x, 31 - offset], target, amount)
    result = quant_opaque(tile, 64)
    px = result.load()
    for y in range(32):
        px[31, y] = px[0, y]
    for x in range(32):
        px[x, 31] = px[x, 0]
    return result


def connection_mask(bits: int, width: int) -> Image.Image:
    mask = Image.new("L", (32, 32))
    draw = ImageDraw.Draw(mask)
    lo, hi = 16 - width // 2, 15 + (width - width // 2)
    draw.ellipse((lo - 1, lo - 1, hi + 1, hi + 1), fill=255)
    if bits & 1: draw.rectangle((lo, 0, hi, 16), fill=255)
    if bits & 2: draw.rectangle((16, lo, 31, hi), fill=255)
    if bits & 4: draw.rectangle((lo, 16, hi, 31), fill=255)
    if bits & 8: draw.rectangle((0, lo, 16, hi), fill=255)
    return mask


def connected(material: Image.Image, ground: Image.Image, bits: int, width: int) -> Image.Image:
    mask = connection_mask(bits, width)
    tile = ground.copy()
    tile.paste(material, (0, 0), mask)
    outline = ImageChops.subtract(mask.filter(ImageFilter.MaxFilter(3)), mask)
    tile.paste(Image.eval(tile, lambda value: round(value * 0.62)), (0, 0), outline)
    return tile


def build_terrain(outputs: list[Out]) -> None:
    master = Image.open(MASTERS / "terrain-b02-master.png").convert("RGB")
    cells = [cell(master, 4, 2, index % 4, index // 4) for index in range(8)]
    connectors = {3: (2, 9, "drainage"), 4: (2, 14, "flood-channel"), 6: (4, 12, "pontoon"), 7: (0, 12, "wall-walk")}
    for index, (asset_id, topic, label, content, stem, mode) in enumerate(TERRAINS):
        if mode == "nesw-16":
            ground_index, band, group = connectors[index]
            material, ground = seamless(cells[index], 0), seamless(cells[ground_index], 1)
            frames = [connected(material, ground, bits, band) for bits in range(16)]
            sheet = Image.new("RGB", (512, 32))
            for frame, tile in enumerate(frames): sheet.paste(tile, (frame * 32, 0))
            meta = {"frameWidth": 32, "frameHeight": 32, "frames": 16, "tileMode": "nesw-16", "variantOrder": MASK_ORDER,
                    "maskBits": {"north": 1, "east": 2, "south": 4, "west": 8}, "connectGroup": group, "footprint": {"columns": 1, "rows": 1}}
        else:
            frames = [seamless(cells[index], variant) for variant in range(4)]
            sheet = Image.new("RGB", (128, 32))
            for frame, tile in enumerate(frames): sheet.paste(tile, (frame * 32, 0))
            meta = {"frameWidth": 32, "frameHeight": 32, "frames": 4, "tileMode": "repeat-variants",
                    "variantOrder": ["hash-0", "hash-1", "hash-2", "hash-3"], "variantSelector": "stable-coordinate-hash", "footprint": {"columns": 1, "rows": 1}}
        out = Out(asset_id, topic, label, content, "terrain", stem, "terrain", sheet.width, 32, "terrain-b02-master.png", meta)
        save_pair(sheet, out)
        outputs.append(out)


def build_structures_props(outputs: list[Out]) -> None:
    master = Image.open(INTERMEDIATE / "structures-b02-alpha.png").convert("RGBA")
    for asset_id, topic, label, content, stem, row, footprint in STRUCTURES:
        atlas = Image.new("RGBA", (384, 128))
        for col in range(3):
            frame = fit(cell(master, 3, 4, col, row), (128, 128), (126, 126), colors=80)
            atlas.alpha_composite(frame, (col * 128, 0))
        out = Out(asset_id, topic, label, content, "interactive-structure", stem, "structures", 384, 128, "structures-b02-master.png", {
            "frameWidth": 128, "frameHeight": 128, "frames": 3, "states": ["normal", "damaged", "captured"],
            "stateRows": [{"row": 0, "states": ["normal", "damaged", "captured"]}], "stateLayout": "horizontal",
            "anchor": {"x": 64, "y": 127}, "footprint": footprint,
            "collision": {"x": 8, "y": 64, "width": 112, "height": 64}, "interactionHotzone": {"x": 32, "y": 72, "width": 64, "height": 56},
        })
        save_pair(atlas, out)
        outputs.append(out)

    master = Image.open(INTERMEDIATE / "props-b02-alpha.png").convert("RGBA")
    for index, (asset_id, topic, label, content, stem, footprint) in enumerate(PROPS):
        item = fit(cell(master, 4, 2, index % 4, index // 4), (64, 64), (62, 62), colors=64)
        out = Out(asset_id, topic, label, content, "battle-prop", stem, "props", 64, 64, "props-b02-master.png", {
            "anchor": {"x": 32, "y": 63}, "footprint": footprint, "zOrder": "map-object",
        })
        save_pair(item, out)
        outputs.append(out)


def build_icons(outputs: list[Out]) -> None:
    for data, source_name, directory, asset_type in [
        (EQUIPMENT, "equipment-b02-alpha.png", "equipment", "equipment"),
        (SKILLS, "skills-b02-alpha.png", "skills", "skill"),
    ]:
        master = Image.open(INTERMEDIATE / source_name).convert("RGBA")
        for index, (asset_id, topic, label, content, stem) in enumerate(data):
            icon = fit(cell(master, 4, 2, index % 4, index // 4), (32, 32), (30, 30), align="center", colors=48)
            out = Out(asset_id, topic, label, content, asset_type, stem, directory, 32, 32, source_name.replace("-alpha", "-master"), {
                "anchor": {"x": 16, "y": 16}, "displaySizes": [16, 24, 32],
            })
            save_pair(icon, out)
            outputs.append(out)

    master = Image.open(INTERMEDIATE / "status-hud-b02-alpha.png").convert("RGBA")
    for row, (data, directory, asset_type) in enumerate([(STATUS, "status", "status"), (HUD, "hud", "hud")]):
        for col, (asset_id, topic, label, content, stem) in enumerate(data):
            icon = fit(cell(master, 4, 2, col, row), (24, 24), (22, 22), align="center", colors=36)
            out = Out(asset_id, topic, label, content, asset_type, stem, directory, 24, 24, "status-hud-b02-master.png", {
                "anchor": {"x": 12, "y": 12}, "displaySizes": [16, 24],
            })
            save_pair(icon, out)
            outputs.append(out)


def build_fx_scenes(outputs: list[Out]) -> None:
    master = Image.open(INTERMEDIATE / "fx-b02-alpha.png").convert("RGBA")
    for row, (asset_id, topic, label, content, stem, fps, blend, loop, anchor) in enumerate(FX):
        sheet = Image.new("RGBA", (128, 32))
        for col in range(4):
            frame = fit(cell(master, 4, 4, col, row), (32, 32), (31, 31), align="center" if row in {0, 3} else "bottom", binary=False, colors=64)
            sheet.alpha_composite(frame, (col * 32, 0))
        out = Out(asset_id, topic, label, content, "fx", stem, "fx", 128, 32, "fx-b02-master.png", {
            "frameWidth": 32, "frameHeight": 32, "frames": 4, "frameOrder": [0, 1, 2, 3],
            "fps": fps, "blendMode": blend, "loop": loop, "anchor": anchor,
        })
        save_pair(sheet, out)
        outputs.append(out)

    for asset_id, topic, label, content, stem, master_name in SCENES:
        image = Image.open(MASTERS / master_name).convert("RGB")
        target = 16 / 9
        if image.width / image.height > target:
            width = round(image.height * target)
            left = (image.width - width) // 2
            image = image.crop((left, 0, left + width, image.height))
        else:
            height = round(image.width / target)
            top = (image.height - height) // 2
            image = image.crop((0, top, image.width, top + height))
        scene = quant_opaque(image.resize((256, 144), LANCZOS), 96)
        out = Out(asset_id, topic, label, content, "narrative-static", stem, "scenes", 256, 144, master_name, {
            "opaque": True, "dialogueSafeArea": {"x": 0, "y": 112, "width": 256, "height": 32}, "displayScale": [1, 2, 3],
        })
        save_pair(scene, out)
        outputs.append(out)


def rel(path: Path) -> str:
    return path.relative_to(ASSETS).as_posix()


def write_manifest(outputs: list[Out]) -> None:
    assets = []
    for out in outputs:
        item = {
            "id": out.id, "topicId": out.topic_id, "label": out.label, "contentId": out.content_id, "type": out.type,
            "png": rel(out.png), "svg": rel(out.svg), "width": out.width, "height": out.height,
            "sourceMaster": f"runtime-v2/batch-02/masters/{out.master}",
        }
        item.update(out.metadata)
        assets.append(item)
    manifest = {
        "schemaVersion": "1.0.0", "campaignId": "candidate-03", "campaignTitle": "布衣定鼎", "batchId": "b02",
        "qualityTier": "runtime-v2-candidate", "runtimeReady": False, "extends": "manifest-runtime-v2.json",
        "source": "built-in-imagegen+official-chroma-key+deterministic-postprocess",
        "notes": "Additive production batch; Batch 01 remains unchanged. Not a 404-topic completion claim.",
        "assetCount": len(assets), "assets": assets,
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def checker(size: tuple[int, int], unit: int = 4) -> Image.Image:
    image = Image.new("RGB", size, "#26313a")
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], unit):
        for x in range(0, size[0], unit):
            if (x // unit + y // unit) % 2: draw.rectangle((x, y, x + unit - 1, y + unit - 1), fill="#33424c")
    return image


def paste_alpha(bg: Image.Image, fg: Image.Image, xy: tuple[int, int]) -> None:
    fg = fg.convert("RGBA")
    bg.paste(fg, xy, fg)


def terrain_frame(image: Image.Image, index: int) -> Image.Image:
    return image.crop((index * 32, 0, (index + 1) * 32, 32))


def make_previews() -> None:
    canvas = checker((1024, 928), 4)
    draw = ImageDraw.Draw(canvas)
    font = ImageFont.load_default()
    ink = "#f0dfba"
    draw.text((8, 6), "C03 RUNTIME-V2 BATCH-02 / 1X / 68 ASSETS", fill=ink, font=font)

    draw.text((8, 22), "COMBAT", fill=ink, font=font)
    y = 36
    for _, _, _, _, stem, *_ in COMBAT:
        image = Image.open(HERE / "units" / f"{stem}.png")
        paste_alpha(canvas, image, (8, y))
        draw.text((410, y + 18), stem, fill=ink, font=font)
        y += image.height

    draw.text((520, 22), "MISSION", fill=ink, font=font)
    y = 36
    for _, _, _, _, stem, *_ in MISSION:
        paste_alpha(canvas, Image.open(HERE / "mission-units" / f"{stem}.png"), (520, y))
        draw.text((654, y + 18), stem, fill=ink, font=font)
        y += 48

    draw.text((8, 462), "TERRAIN", fill=ink, font=font)
    for index, (_, _, _, _, stem, mode) in enumerate(TERRAINS):
        sheet = Image.open(HERE / "terrain" / f"{stem}.png")
        preview = terrain_frame(sheet, 15 if mode == "nesw-16" else index % 4)
        canvas.paste(preview, (8 + index * 40, 478))

    draw.text((8, 518), "PROPS", fill=ink, font=font)
    for index, (_, _, _, _, stem, _) in enumerate(PROPS):
        paste_alpha(canvas, Image.open(HERE / "props" / f"{stem}.png"), (8 + index * 72, 534))

    draw.text((8, 604), "EQUIPMENT / SKILLS / STATUS / HUD", fill=ink, font=font)
    groups = [(EQUIPMENT, "equipment"), (SKILLS, "skills"), (STATUS, "status"), (HUD, "hud")]
    x = 8
    for data, directory in groups:
        for item in data:
            stem = item[4]
            image = Image.open(HERE / directory / f"{stem}.png")
            paste_alpha(canvas, image, (x, 622))
            x += image.width + 4
        x += 12

    draw.text((8, 664), "FX", fill=ink, font=font)
    y = 680
    for _, _, _, _, stem, *_ in FX:
        paste_alpha(canvas, Image.open(HERE / "fx" / f"{stem}.png"), (8, y))
        y += 32

    draw.text((520, 438), "STRUCTURES N/D/C", fill=ink, font=font)
    y = 454
    for _, _, _, _, stem, *_ in STRUCTURES:
        image = Image.open(HERE / "structures" / f"{stem}.png")
        paste_alpha(canvas, image, (520, y))
        y += 112

    canvas.save(PREVIEWS / "batch-02-contact-1x.png", optimize=True)
    canvas.resize((2048, 1856), NEAREST).save(PREVIEWS / "batch-02-contact-2x.png", optimize=True)

    scenes = Image.new("RGB", (512, 288))
    for index, (_, _, _, _, stem, _) in enumerate(SCENES):
        scenes.paste(Image.open(HERE / "scenes" / f"{stem}.png").convert("RGB"), ((index % 2) * 256, (index // 2) * 144))
    scenes.save(PREVIEWS / "batch-02-scenes-1x.png", optimize=True)
    scenes.resize((1024, 576), NEAREST).save(PREVIEWS / "batch-02-scenes-2x.png", optimize=True)

    terrain = Image.new("RGB", (8 * 96, 96))
    for index, (_, _, _, _, stem, mode) in enumerate(TERRAINS):
        sheet = Image.open(HERE / "terrain" / f"{stem}.png").convert("RGB")
        for row in range(3):
            for col in range(3):
                frame = 15 if mode == "nesw-16" else (row * 3 + col) % 4
                terrain.paste(terrain_frame(sheet, frame), (index * 96 + col * 32, row * 32))
    terrain.save(PREVIEWS / "terrain-3x3.png", optimize=True)


def qa(outputs: list[Out]) -> dict[str, Any]:
    errors: list[str] = []
    checks: list[dict[str, Any]] = []
    complete = json.loads(COMPLETE_PATH.read_text(encoding="utf-8"))
    topics = {topic["id"]: topic for topic in complete["topics"]}
    v1_ids = {asset["contentId"] for asset in json.loads(V1_PATH.read_text(encoding="utf-8"))["assets"]}
    category_alias = {"combat-unit": "combat-unit", "mission-unit": "mission-unit", "terrain": "terrain", "interactive-structure": "interactive-structure",
                      "battle-prop": "battle-prop", "equipment": "equipment", "skill": "skill", "status": "status", "hud": "hud", "fx": "fx", "narrative-static": "narrative-static"}
    topic_ok = all(out.topic_id in topics and topics[out.topic_id]["category"] == category_alias[out.type] and topics[out.topic_id]["source"] == "expanded" for out in outputs)
    unique_ok = len({out.id for out in outputs}) == len(outputs) == len({out.topic_id for out in outputs}) == len({out.content_id for out in outputs})
    overlap_ok = not ({out.content_id for out in outputs} & v1_ids)
    checks.append({"id": "manifest.topic-category-expanded", "passed": topic_ok})
    checks.append({"id": "manifest.unique-ids", "passed": unique_ok, "count": len(outputs)})
    checks.append({"id": "manifest.no-batch01-content-overlap", "passed": overlap_ok})
    if not topic_ok: errors.append("topic/category/source mapping failed")
    if not unique_ok: errors.append("duplicate or missing id/topic/content id")
    if not overlap_ok: errors.append("Batch 01 contentId overlap")

    files_ok = True
    for out in outputs:
        if not out.png.is_file() or not out.svg.is_file() or Image.open(out.png).size != (out.width, out.height):
            files_ok = False
            errors.append(f"file/size failed: {out.id}")
    checks.append({"id": "files.png-svg-size", "passed": files_ok, "count": len(outputs)})

    unit_rows = []
    for out in [item for item in outputs if item.type in {"combat-unit", "mission-unit"}]:
        image = Image.open(out.png).convert("RGBA")
        fw, fh = out.metadata["frameWidth"], out.metadata["frameHeight"]
        boxes = [alpha_bbox(image.crop((frame * fw, 0, (frame + 1) * fw, fh))) for frame in range(4)]
        grounded = all(box is not None and box[3] == fh for box in boxes)
        distinct = len({hashlib.sha256(image.crop((frame * fw, 0, (frame + 1) * fw, fh)).tobytes()).hexdigest() for frame in range(4)}) == 4
        passed = grounded and distinct
        unit_rows.append({"id": out.id, "passed": passed, "frameBoxes": boxes, "fourDistinctFrames": distinct})
        if not passed: errors.append(f"unit frames failed: {out.id}")
    checks.append({"id": "units.frames-anchor-distinct", "passed": all(row["passed"] for row in unit_rows), "assets": unit_rows})

    terrain_rows = []
    for out in [item for item in outputs if item.type == "terrain"]:
        sheet = Image.open(out.png).convert("RGB")
        frames = [terrain_frame(sheet, index) for index in range(out.metadata["frames"])]
        unique = len({frame.tobytes() for frame in frames})
        if out.metadata["tileMode"] == "nesw-16":
            horizontal = all(frames[a].crop((31, 0, 32, 32)).tobytes() == frames[b].crop((0, 0, 1, 32)).tobytes()
                             for a in range(16) for b in range(16) if bool(a & 2) == bool(b & 8))
            vertical = all(frames[a].crop((0, 31, 32, 32)).tobytes() == frames[b].crop((0, 0, 32, 1)).tobytes()
                           for a in range(16) for b in range(16) if bool(a & 4) == bool(b & 1))
            passed = unique == 16 and horizontal and vertical
        else:
            passed = unique == 4 and all(frame.crop((0, 0, 1, 32)).tobytes() == frame.crop((31, 0, 32, 32)).tobytes()
                                             and frame.crop((0, 0, 32, 1)).tobytes() == frame.crop((0, 31, 32, 32)).tobytes() for frame in frames)
        terrain_rows.append({"id": out.id, "passed": passed, "uniqueFrames": unique})
        if not passed: errors.append(f"terrain variants/edges failed: {out.id}")
    checks.append({"id": "terrain.variants-compatible-edges", "passed": all(row["passed"] for row in terrain_rows), "assets": terrain_rows})

    hashes: dict[str, list[str]] = {}
    for out in outputs:
        image = Image.open(out.png).convert("RGBA").resize((32, 32), NEAREST)
        hashes.setdefault(out.type, []).append(hashlib.sha256(image.tobytes()).hexdigest())
    duplicates = {category: [value for value, count in Counter(values).items() if count > 1] for category, values in hashes.items()}
    duplicate_ok = not any(duplicates.values())
    checks.append({"id": "visual.no-exact-duplicate-category-files", "passed": duplicate_ok, "duplicateHashCounts": {k: len(v) for k, v in duplicates.items()}})
    if not duplicate_ok: errors.append("exact duplicate category output")

    transparent_ok = True
    for out in [item for item in outputs if item.type not in {"terrain", "narrative-static"}]:
        image = Image.open(out.png).convert("RGBA")
        corners = [image.getpixel((0, 0))[3], image.getpixel((image.width - 1, 0))[3], image.getpixel((0, image.height - 1))[3], image.getpixel((image.width - 1, image.height - 1))[3]]
        if any(corners): transparent_ok = False
    checks.append({"id": "alpha.transparent-corners", "passed": transparent_ok})
    if not transparent_ok: errors.append("transparent corner QA failed")

    preview_names = ["batch-02-contact-1x.png", "batch-02-contact-2x.png", "batch-02-scenes-1x.png", "batch-02-scenes-2x.png", "terrain-3x3.png"]
    previews_ok = all((PREVIEWS / name).is_file() for name in preview_names)
    checks.append({"id": "previews.required", "passed": previews_ok, "files": [f"runtime-v2/batch-02/previews/{name}" for name in preview_names]})
    if not previews_ok: errors.append("preview missing")

    summary = Counter(out.type for out in outputs)
    report = {
        "schemaVersion": "1.0.0", "campaignId": "candidate-03", "batchId": "b02", "qualityTier": "runtime-v2-candidate",
        "runtimeReady": False, "passed": not errors, "summary": {"assetCount": len(outputs), "coverage": dict(summary), "machineErrors": len(errors)},
        "checks": checks, "errors": errors,
        "manualReview": {"required": True, "completed": ["1x/2x contact preview", "scene contact preview", "terrain 3x3 preview", "unit silhouette and weapon review", "FX frame progression review"],
                         "pendingBeforeRuntimeReady": ["in-engine registration using contentId", "game-board screenshot", "collision/hotzone test", "large-unit z-order test"]},
        "completionClaim": "Additive 68-asset Batch 02 only; not the 404-topic library completion.",
    }
    QA_PATH.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return report


def main() -> None:
    setup()
    remove_keys()
    outputs: list[Out] = []
    build_units(outputs)
    build_terrain(outputs)
    build_structures_props(outputs)
    build_icons(outputs)
    build_fx_scenes(outputs)
    write_manifest(outputs)
    make_previews()
    report = qa(outputs)
    print(json.dumps(report["summary"], ensure_ascii=False, indent=2))
    if not report["passed"]:
        for error in report["errors"]: print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)


if __name__ == "__main__":
    main()
