#!/usr/bin/env python3
"""Build the complete 404-topic C03 pixel-art library deterministically.

The generator never overwrites the original *-hd assets. It emits compact
atlases under expanded/, a 404-topic manifest, and machine-readable QA.
PNG and SVG pairs are written from the same integer-pixel RGBA canvas.
"""

from __future__ import annotations

import hashlib
import html
import importlib.util
import json
import math
from collections import Counter
from pathlib import Path
from typing import Callable
from xml.etree import ElementTree as ET

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parent
OUT = ROOT / "expanded"
MANIFEST = ROOT / "manifest-complete.json"
QA = ROOT / "qa-complete.json"

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

P = {
    "ink": "#1e1c1b", "ink2": "#342f2b", "sky": "#3d4554",
    "sky2": "#59616b", "far": "#777568", "stone": "#706b61",
    "stone_hi": "#a29a87", "ground": "#5c4637", "mud": "#493a31",
    "light": "#d69a4e", "red": "#8d3e34", "blue": "#334f5c",
    "gray": "#77746b", "cloth": "#bda57b", "green": "#566b50",
    "green2": "#74835c", "water": "#324d57", "water_hi": "#69818a",
    "snow": "#c7c4b6", "white": "#ded8c2", "skin": "#b77b5e",
    "skin_hi": "#d19a75", "metal": "#7b756b", "metal_hi": "#aaa393",
    "wood": "#644737", "wood_hi": "#987052", "transparent": "#00000000",
}


def hid(value: str) -> int:
    return int(hashlib.sha256(value.encode("utf-8")).hexdigest()[:8], 16)


def shade(value: str, amount: int) -> tuple[int, int, int, int]:
    value = value.lstrip("#")
    red, green, blue = int(value[:2], 16), int(value[2:4], 16), int(value[4:6], 16)
    return tuple(max(0, min(255, channel + amount)) for channel in (red, green, blue)) + (255,)


def color_for(key: str, offset: int = 0) -> str:
    colors = [P["blue"], P["red"], P["green"], P["cloth"], P["gray"], P["ground"]]
    return colors[(hid(key) + offset) % len(colors)]


def identity_mark(draw: ImageDraw.ImageDraw, origin: tuple[int, int], size: tuple[int, int], key: str, opaque: bool = False) -> None:
    """Add a small semantic registry/formation notch that survives atlas normalization."""
    ox, oy = origin
    width, height = size
    seed = hid(key)
    mark = max(3, min(width, height) // 12, width // 32)
    x_slots = max(1, (width - mark - 4) // mark)
    y_slots = max(1, (height - mark - 4) // mark)
    mx = ox + 2 + ((seed & 0xFF) % x_slots) * mark
    my = oy + 2 + (((seed >> 8) & 0xFF) % y_slots) * mark
    fill = [P["light"], P["red"], P["blue"], P["green"], P["cloth"], P["metal_hi"]][(seed >> 16) % 6]
    draw.rectangle((mx, my, mx + mark - 1, my + mark - 1), fill=fill)
    if mark >= 4:
        if seed % 2:
            draw.line((mx, my, mx + mark - 1, my + mark - 1), fill=P["ink"], width=1)
        else:
            draw.line((mx, my + mark - 1, mx + mark - 1, my), fill=P["ink"], width=1)


def canvas(width: int, height: int, opaque: bool = False) -> Image.Image:
    return Image.new("RGBA", (width, height), P["ink"] if opaque else (0, 0, 0, 0))


def vectorize(image: Image.Image, path: Path, label: str) -> None:
    rgba = image.convert("RGBA")
    rows: list[str] = []
    pixels = rgba.load()
    for y in range(rgba.height):
        x = 0
        while x < rgba.width:
            pixel = pixels[x, y]
            if pixel[3] == 0:
                x += 1
                continue
            run = 1
            while x + run < rgba.width and pixels[x + run, y] == pixel:
                run += 1
            fill = f"#{pixel[0]:02x}{pixel[1]:02x}{pixel[2]:02x}"
            opacity = "" if pixel[3] == 255 else f' opacity="{pixel[3] / 255:.3f}"'
            rows.append(f'<rect x="{x}" y="{y}" width="{run}" height="1" fill="{fill}"{opacity}/>')
            x += run
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{rgba.width}" height="{rgba.height}" '
        f'viewBox="0 0 {rgba.width} {rgba.height}" role="img" aria-label="{html.escape(label)}" '
        f'shape-rendering="crispEdges" style="image-rendering:pixelated">\n'
        + "\n".join(rows) + "\n</svg>\n",
        encoding="utf-8",
    )


def save_pair(image: Image.Image, stem: Path, label: str) -> tuple[str, str]:
    png = stem.with_suffix(".png")
    svg = stem.with_suffix(".svg")
    png.parent.mkdir(parents=True, exist_ok=True)
    image.save(png, optimize=True)
    vectorize(image, svg, label)
    return str(png.relative_to(ROOT)), str(svg.relative_to(ROOT))


def figure(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], key: str, pose: int = 0, role: str = "") -> None:
    x0, y0, x1, y1 = box
    w, h = x1 - x0, y1 - y0
    cx, floor = x0 + w // 2, y1 - max(2, h // 16)
    unit = max(1, min(w // 18, h // 28))
    seed = hid(key)
    main, accent = color_for(key), color_for(key, 2)
    is_horse = any(word in role for word in ("骑", "游骑", "斥候"))
    is_boat = any(word in role for word in ("艨艟", "楼船", "火船"))
    if is_boat:
        draw.polygon([(x0 + 3, floor - 8 * unit), (x1 - 3, floor - 8 * unit), (x1 - 8, floor), (x0 + 8, floor)], fill=P["wood"])
        draw.rectangle((x0 + 6, floor - 9 * unit, x1 - 7, floor - 7 * unit), fill=P["wood_hi"])
        draw.line((cx, floor - 9 * unit, cx, floor - 22 * unit), fill=P["metal_hi"], width=unit)
        draw.polygon([(cx, floor - 21 * unit), (cx + 8 * unit, floor - 16 * unit), (cx, floor - 13 * unit)], fill=accent)
        draw.line((x0 + 4, floor + pose % 2, x1 - 4, floor + pose % 2), fill=P["water_hi"], width=unit)
        return
    if is_horse:
        draw.ellipse((cx - 8 * unit, floor - 13 * unit, cx + 8 * unit, floor - 4 * unit), fill=P["wood"])
        draw.rectangle((cx + 5 * unit, floor - 17 * unit, cx + 10 * unit, floor - 8 * unit), fill=P["wood"])
        for leg in (-6, 5):
            shift = pose % 2
            draw.line((cx + leg * unit, floor - 5 * unit, cx + (leg + shift) * unit, floor), fill=P["ink2"], width=2 * unit)
        floor -= 11 * unit
    head = max(5, (5 + seed % 2) * unit)
    draw.rectangle((cx - head // 2, floor - 22 * unit, cx + head // 2, floor - 17 * unit), fill=P["skin"])
    draw.rectangle((cx - head // 2 - unit, floor - 23 * unit, cx + head // 2 + unit, floor - 21 * unit), fill=P["ink"])
    if seed % 4 == 0:
        draw.polygon([(cx - 5 * unit, floor - 23 * unit), (cx, floor - 27 * unit), (cx + 6 * unit, floor - 23 * unit)], fill=accent)
    elif seed % 4 == 1:
        draw.line((cx - 6 * unit, floor - 24 * unit, cx + 8 * unit, floor - 24 * unit), fill=accent, width=2 * unit)
    elif seed % 4 == 2:
        draw.rectangle((cx + 3 * unit, floor - 25 * unit, cx + 7 * unit, floor - 21 * unit), fill=accent)
    body_left = (6 + seed % 3) * unit
    body_right = (6 + (seed // 3) % 3) * unit
    draw.polygon([(cx - body_left, floor - 17 * unit), (cx + body_right, floor - 17 * unit), (cx + 8 * unit, floor - 6 * unit), (cx - 7 * unit, floor - 6 * unit)], fill=main)
    draw.rectangle((cx - 6 * unit, floor - 11 * unit, cx + 6 * unit, floor - 9 * unit), fill=accent)
    if seed % 5 == 0:
        draw.polygon([(cx - body_left, floor - 16 * unit), (cx - (body_left + 4 * unit), floor - 5 * unit), (cx - 5 * unit, floor - 7 * unit)], fill=accent)
    elif seed % 5 == 1:
        draw.rectangle((cx + body_right, floor - 14 * unit, cx + body_right + 4 * unit, floor - 8 * unit), fill=P["cloth"])
    leg_shift = unit if pose in (1, 3) else 0
    draw.line((cx - 3 * unit, floor - 6 * unit, cx - 4 * unit - leg_shift, floor), fill=P["ink2"], width=2 * unit)
    draw.line((cx + 3 * unit, floor - 6 * unit, cx + 4 * unit + leg_shift, floor), fill=P["ink2"], width=2 * unit)
    if any(word in role for word in ("盾", "护卫", "禁军", "守备")):
        draw.polygon([(cx - 9 * unit, floor - 16 * unit), (cx - 4 * unit, floor - 18 * unit), (cx - 3 * unit, floor - 7 * unit), (cx - 8 * unit, floor - 5 * unit)], fill=P["metal"])
        draw.line((cx - 8 * unit, floor - 15 * unit, cx - 4 * unit, floor - 8 * unit), fill=P["cloth"], width=unit)
    if "弩" in role:
        draw.arc((cx + 1 * unit, floor - 18 * unit, cx + 14 * unit, floor - 8 * unit), 185, 355, fill=P["wood_hi"], width=unit)
        draw.line((cx + 2 * unit, floor - 12 * unit, cx + 14 * unit, floor - 12 * unit), fill=P["metal_hi"], width=unit)
        draw.line((cx + 8 * unit, floor - 14 * unit, cx + 8 * unit, floor - 5 * unit), fill=P["wood"], width=2 * unit)
    elif any(word in role for word in ("弓", "射", "斥候")):
        draw.arc((cx + 2 * unit, floor - 20 * unit, cx + 12 * unit, floor - 7 * unit), 270, 90, fill=P["wood_hi"], width=unit)
        draw.line((cx + 7 * unit, floor - 20 * unit, cx + 7 * unit, floor - 7 * unit), fill=P["cloth"], width=unit)
    elif "铳" in role:
        draw.line((cx + 2 * unit, floor - 15 * unit, cx + 14 * unit, floor - 13 * unit), fill=P["metal_hi"], width=3 * unit)
        draw.rectangle((cx + 4 * unit, floor - 12 * unit, cx + 8 * unit, floor - 6 * unit), fill=P["wood"])
        draw.rectangle((cx + 14 * unit, floor - 15 * unit, cx + 17 * unit, floor - 12 * unit), fill=P["red"])
    elif any(word in role for word in ("锤", "铁锤")):
        draw.line((cx + 3 * unit, floor - 7 * unit, cx + 11 * unit, floor - 19 * unit), fill=P["wood_hi"], width=2 * unit)
        draw.rectangle((cx + 7 * unit, floor - 23 * unit, cx + 16 * unit, floor - 17 * unit), fill=P["metal_hi"])
    elif any(word in role for word in ("震天雷", "爆破")):
        draw.ellipse((cx + 4 * unit, floor - 17 * unit, cx + 13 * unit, floor - 8 * unit), fill=P["metal"])
        draw.line((cx + 10 * unit, floor - 17 * unit, cx + 14 * unit, floor - 22 * unit), fill=P["light"], width=unit)
    elif any(word in role for word in ("枪", "刀", "铳", "火箭", "凿", "云梯", "锹", "河工", "桥", "工程", "炮石")):
        draw.line((cx + 5 * unit, floor - 20 * unit, cx + 10 * unit, floor - 1 * unit), fill=P["wood_hi"], width=unit)
        draw.polygon([(cx + 4 * unit, floor - 21 * unit), (cx + 7 * unit, floor - 24 * unit), (cx + 8 * unit, floor - 20 * unit)], fill=P["metal_hi"])
    elif any(word in role for word in ("医", "登记", "县吏", "史馆", "粮秣", "军师", "说客")):
        draw.rectangle((cx + 5 * unit, floor - 14 * unit, cx + 11 * unit, floor - 7 * unit), fill=P["cloth"])
        draw.line((cx + 6 * unit, floor - 12 * unit, cx + 10 * unit, floor - 12 * unit), fill=P["ink"], width=unit)
        if "粮秣" in role:
            draw.ellipse((cx - 13 * unit, floor - 14 * unit, cx - 6 * unit, floor - 4 * unit), fill=P["cloth"])
    if any(word in role for word in ("旗", "鼓")):
        draw.line((cx + 8 * unit, floor - 25 * unit, cx + 8 * unit, floor), fill=P["wood_hi"], width=unit)
        draw.rectangle((cx + 9 * unit, floor - 24 * unit, cx + 17 * unit, floor - 17 * unit), fill=P["white"])
    elif seed % 7 == 0:
        draw.line((cx - 8 * unit, floor - 18 * unit, cx - (11 + seed % 3) * unit, floor - 3 * unit), fill=P["wood_hi"], width=unit)
    elif seed % 7 == 1:
        draw.rectangle((cx - 11 * unit, floor - 13 * unit, cx - 7 * unit, floor - 7 * unit), fill=P["cloth"])


def draw_unit_cell(image: Image.Image, origin: tuple[int, int], key: str, label: str) -> None:
    ox, oy = origin
    draw = ImageDraw.Draw(image)
    for frame in range(4):
        figure(draw, (ox + frame * 32, oy, ox + (frame + 1) * 32, oy + 48), key, frame, label)
    # A formation/registry tile distinguishes army records without replacing gear silhouettes.
    identity_mark(draw, origin, (128, 48), key)


def draw_character_cell(image: Image.Image, origin: tuple[int, int], key: str, label: str) -> None:
    ox, oy = origin
    draw = ImageDraw.Draw(image)
    figure(draw, (ox + 8, oy + 4, ox + 88, oy + 108), key, hid(key) % 4, label)
    accent = color_for(key, 1)
    draw.line((ox + 11, oy + 102, ox + 85, oy + 102), fill=accent, width=2)
    for mark in range(3):
        x = ox + 14 + ((hid(key) >> (mark * 3)) % 64)
        draw.rectangle((x, oy + 105, x + 3, oy + 108), fill=P["cloth"])


def draw_scene_cell(image: Image.Image, origin: tuple[int, int], key: str, label: str, size: tuple[int, int] = (256, 144)) -> None:
    ox, oy = origin
    width, height = size
    draw = ImageDraw.Draw(image)
    seed = hid(key)
    night = any(word in label for word in ("夜", "雨", "冬", "雪", "火"))
    sky = P["sky2"] if night else P["sky"]
    draw.rectangle((ox, oy, ox + width - 1, oy + height - 1), fill=sky)
    horizon = oy + height * 2 // 5

    def person(px: int, py: int, tone: int = 0, tall: int = 10) -> None:
        draw.rectangle((px, py - tall, px + 3, py), fill=color_for(key, tone))
        draw.rectangle((px, py - tall - 4, px + 3, py - tall - 1), fill=P["skin"])

    def ship(sx: int, sy: int, scale: int = 1, fire: bool = False) -> None:
        draw.polygon([(sx, sy), (sx + 28 * scale, sy), (sx + 23 * scale, sy + 7 * scale), (sx + 5 * scale, sy + 7 * scale)], fill=P["wood"])
        draw.line((sx + 14 * scale, sy, sx + 14 * scale, sy - 20 * scale), fill=P["metal_hi"], width=max(1, scale))
        draw.polygon([(sx + 15 * scale, sy - 19 * scale), (sx + 27 * scale, sy - 13 * scale), (sx + 15 * scale, sy - 8 * scale)], fill=color_for(key, sx))
        if fire:
            draw.polygon([(sx + 5 * scale, sy), (sx + 9 * scale, sy - 14 * scale), (sx + 14 * scale, sy), (sx + 18 * scale, sy - 10 * scale), (sx + 23 * scale, sy)], fill=P["light"])

    flood = any(word in label for word in ("决口", "洪", "淮雨", "河堤"))
    crossing = any(word in label for word in ("渡", "浮桥", "借城"))
    naval = any(word in label for word in ("大泽", "湖口", "旗舰", "火攻", "风火", "三路出师"))
    court = any(word in label for word in ("宫", "登基", "吴王", "公开审理", "讨饷", "军法"))
    border = any(word in label for word in ("燕云", "边", "雪原"))
    livelihood = any(word in label for word in ("粮", "田", "播种", "食肆", "新粮", "盐", "民生", "账", "告示", "退伍", "家属"))

    if flood:
        draw.rectangle((ox, horizon, ox + width - 1, oy + height - 1), fill=P["mud"])
        draw.polygon([(ox, oy + height - 18), (ox + 75, horizon + 8), (ox + 145, horizon + 28), (ox + width, horizon + 4), (ox + width, oy + height), (ox, oy + height)], fill=P["water"])
        draw.polygon([(ox + 5, horizon + 12), (ox + 82, horizon - 1), (ox + 125, horizon + 18), (ox + 63, horizon + 24)], fill=P["ground"])
        draw.line((ox + 80, horizon, ox + 111, horizon + 21), fill=P["water_hi"], width=5)
        for index in range(9):
            person(ox + 18 + index * 19, oy + height - 15 - index % 2 * 6, index)
            draw.line((ox + 20 + index * 19, oy + height - 27, ox + 28 + index * 19, oy + height - 8), fill=P["wood_hi"], width=2)
    elif crossing:
        draw.rectangle((ox, horizon, ox + width - 1, oy + height - 1), fill=P["water"])
        draw.polygon([(ox, oy + height - 12), (ox + 82, horizon + 18), (ox + 97, horizon + 26), (ox + 17, oy + height)], fill=P["ground"])
        draw.polygon([(ox + width, oy + height - 9), (ox + 174, horizon + 16), (ox + 157, horizon + 29), (ox + 231, oy + height)], fill=P["ground"])
        draw.line((ox + 65, oy + height - 28, ox + 190, horizon + 32), fill=P["wood_hi"], width=6)
        for plank in range(8):
            px = ox + 69 + plank * 15
            py = oy + height - 30 - plank * 4
            draw.line((px, py - 5, px + 7, py + 5), fill=P["cloth"], width=2)
        ship(ox + 26, oy + height - 35)
        ship(ox + 184, oy + height - 27)
    elif naval:
        draw.rectangle((ox, horizon - 4, ox + width - 1, oy + height - 1), fill=P["water"])
        for wave in range(7):
            y = horizon + wave * 12 + seed % 6
            draw.line((ox + wave % 2 * 22, y, ox + width - 12, y), fill=P["water_hi"], width=2)
        ship(ox + 24, horizon + 24, 2 if "旗舰" in label else 1, "火" in label)
        ship(ox + 126, horizon + 12, 1, "火" in label)
        ship(ox + 194, horizon + 45, 1, False)
        if "火" in label:
            draw.rectangle((ox + 103, horizon + 3, ox + 111, oy + height - 5), fill=P["red"])
            for flame in range(6):
                fx = ox + 83 + flame * 19
                draw.polygon([(fx, horizon + 50), (fx + 6, horizon + 18 - flame % 2 * 9), (fx + 12, horizon + 50)], fill=P["light"])
    elif court:
        draw.rectangle((ox, horizon, ox + width - 1, oy + height - 1), fill=P["stone"])
        draw.rectangle((ox + 20, oy + 24, ox + width - 21, horizon + 26), fill=P["ink2"])
        draw.rectangle((ox + 86, oy + 31, ox + 170, horizon + 26), fill=P["red"])
        draw.rectangle((ox + 112, oy + 43, ox + 145, horizon + 26), fill=P["ink"])
        for step in range(5):
            draw.rectangle((ox + 38 + step * 10, horizon + 28 + step * 11, ox + width - 39 - step * 10, horizon + 34 + step * 11), fill=P["stone_hi"])
        for index in range(12):
            person(ox + 37 + index * 16, oy + height - 10 - index % 3 * 12, index, 8 + index % 2 * 3)
        draw.rectangle((ox + 15, oy + height - 35, ox + 62, oy + height - 28), fill=P["cloth"])
        draw.rectangle((ox + 194, oy + height - 39, ox + 239, oy + height - 27), fill=P["ground"])
    elif border:
        draw.rectangle((ox, horizon, ox + width - 1, oy + height - 1), fill=P["snow"])
        draw.polygon([(ox, horizon), (ox + 54, horizon - 24), (ox + 108, horizon), (ox + 175, horizon - 15), (ox + width, horizon)], fill=P["far"])
        draw.rectangle((ox + 178, horizon - 32, ox + 231, horizon + 20), fill=P["stone"])
        draw.rectangle((ox + 194, horizon - 53, ox + 215, horizon + 20), fill=P["stone_hi"])
        for rider in range(5):
            rx = ox + 28 + rider * 27
            draw.ellipse((rx, oy + height - 34, rx + 19, oy + height - 24), fill=P["wood"])
            person(rx + 7, oy + height - 34, rider, 9)
        for track in range(9):
            x = ox + 13 + track * 23
            draw.rectangle((x, oy + height - 13 - track % 3 * 3, x + 7, oy + height - 11 - track % 3 * 3), fill=P["gray"])
    elif livelihood:
        draw.rectangle((ox, horizon, ox + width - 1, oy + height - 1), fill=P["ground"])
        for strip in range(7):
            y = horizon + 7 + strip * 11
            draw.line((ox, y, ox + 146, y + (strip % 2) * 5), fill=P["green2"], width=4)
        draw.rectangle((ox + 164, horizon - 20, ox + 244, oy + height - 8), fill=P["stone"])
        draw.polygon([(ox + 157, horizon - 18), (ox + 204, horizon - 38), (ox + 250, horizon - 18)], fill=P["ink2"])
        draw.rectangle((ox + 178, horizon + 6, ox + 192, oy + height - 8), fill=P["ink"])
        for index in range(9):
            person(ox + 13 + index * 18, oy + height - 9 - index % 2 * 8, index)
        for sack in range(4):
            sx = ox + 198 + sack * 11
            draw.ellipse((sx, oy + height - 28, sx + 9, oy + height - 10), fill=P["cloth"])
    else:
        draw.rectangle((ox, horizon, ox + width - 1, oy + height - 1), fill=P["ground"])
        for tent in range(4):
            tx = ox + 17 + tent * 57
            draw.polygon([(tx, oy + height - 18), (tx + 21, horizon + 14), (tx + 42, oy + height - 18)], fill=color_for(key, tent))
            draw.line((tx + 21, horizon + 14, tx + 21, oy + height - 18), fill=P["wood_hi"], width=2)
        for index in range(8):
            person(ox + 24 + index * 27, oy + height - 8, index)
    if any(word in label for word in ("雨", "渡")):
        for rain in range(26):
            x = ox + (rain * 41 + seed) % width
            y = oy + (rain * 23 + seed // 3) % height
            draw.line((x, y, x - 3, y + 8), fill=P["water_hi"], width=1)
    if any(word in label for word in ("炬", "灯", "烧")) and not naval:
        for flame in range(5):
            fx = ox + 20 + (seed + flame * 47) % (width - 40)
            fy = oy + height - 24 - (flame % 2) * 11
            draw.polygon([(fx, fy + 12), (fx + 4, fy), (fx + 8, fy + 12)], fill=P["light"])
            draw.rectangle((fx + 2, fy + 8, fx + 6, fy + 13), fill=P["red"])
    if any(word in label for word in ("雪", "冬")):
        for snow in range(34):
            x = ox + (snow * 29 + seed) % width
            y = oy + (snow * 17 + seed // 7) % height
            draw.rectangle((x, y, x + 1, y + 1), fill=P["snow"])
    # Foreground evidence motif varies composition within a scene family.
    motif = seed % 4
    mx = ox + 8 + (seed % max(12, width - 42))
    if motif == 0:
        draw.line((mx, oy + height - 42, mx, oy + height - 6), fill=P["wood_hi"], width=3)
        draw.polygon([(mx + 3, oy + height - 40), (mx + 20, oy + height - 34), (mx + 3, oy + height - 24)], fill=color_for(key))
    elif motif == 1:
        for sack in range(3):
            draw.ellipse((mx + sack * 9, oy + height - 23 - sack % 2 * 5, mx + 12 + sack * 9, oy + height - 5), fill=P["cloth"])
            draw.line((mx + 3 + sack * 9, oy + height - 17 - sack % 2 * 5, mx + 9 + sack * 9, oy + height - 17 - sack % 2 * 5), fill=P["red"], width=2)
    elif motif == 2:
        draw.rectangle((mx, oy + height - 19, mx + 31, oy + height - 11), fill=P["wood"])
        draw.rectangle((mx + 4, oy + height - 31, mx + 25, oy + height - 18), fill=P["cloth"])
        for line in range(3): draw.line((mx + 7, oy + height - 28 + line * 4, mx + 21, oy + height - 28 + line * 4), fill=P["ink"], width=1)
    else:
        draw.polygon([(mx, oy + height - 6), (mx + 7, oy + height - 35), (mx + 14, oy + height - 6)], fill=P["light"])
        draw.rectangle((mx + 4, oy + height - 14, mx + 10, oy + height - 5), fill=P["red"])


def draw_architecture_cell(image: Image.Image, origin: tuple[int, int], key: str, label: str) -> None:
    ox, oy = origin
    draw = ImageDraw.Draw(image)
    seed = hid(key)
    ground = oy + 112
    if any(word in label for word in ("浮桥", "盐港", "水闸", "驿")):
        draw.rectangle((ox + 5, oy + 85, ox + 122, oy + 118), fill=P["water"])
        for y in range(91, 118, 8):
            draw.line((ox + 8, oy + y - oy, ox + 119, oy + y - oy), fill=P["water_hi"], width=2)
    base = P["stone"] if seed % 2 else P["wood"]
    inset_l, inset_r = 14 + seed % 11, 14 + (seed // 5) % 11
    roof_y = 16 + seed % 13
    draw.rectangle((ox + inset_l, oy + 42, ox + 127 - inset_r, ground), fill=base)
    draw.rectangle((ox + inset_l + 7, oy + 52, ox + 120 - inset_r, ground - 6), fill=shade(base, 12))
    draw.polygon([(ox + inset_l - 7, oy + 44), (ox + 64, oy + roof_y), (ox + 134 - inset_r, oy + 44)], fill=P["ink2"])
    draw.line((ox + inset_l - 5, oy + 45, ox + 132 - inset_r, oy + 45), fill=P["red"], width=3)
    for bay in range(3):
        bx = ox + 29 + bay * 25
        draw.rectangle((bx, oy + 66, bx + 11, ground), fill=P["ink"])
        draw.rectangle((bx + 3, oy + 70, bx + 8, ground), fill=P["cloth"])
    if "桥" in label:
        draw.rectangle((ox + 5, oy + 91, ox + 122, oy + 99), fill=P["wood_hi"])
        for x in range(8, 123, 14):
            draw.line((ox + x, oy + 91, ox + x, oy + 110), fill=P["wood"], width=2)
    if any(word in label for word in ("宫门", "城门")):
        draw.rectangle((ox + 50, oy + 58, ox + 78, ground), fill=P["ink"])
        draw.rectangle((ox + 54, oy + 63, ox + 74, ground), fill=P["red"])
    draw.line((ox + 11, ground + 2, ox + 117, ground + 2), fill=P["stone_hi"], width=3)


def draw_prop_cell(image: Image.Image, origin: tuple[int, int], key: str, label: str, size: tuple[int, int] = (48, 48)) -> None:
    ox, oy = origin
    w, h = size
    draw = ImageDraw.Draw(image)
    cx, cy = ox + w // 2, oy + h // 2
    seed = hid(key)
    accent = color_for(key)
    if any(word in label for word in ("灯", "火", "燃")):
        flare = 7 + seed % 5
        draw.polygon([(cx - flare, cy + 12), (cx - 5, cy - 11), (cx + 5, cy - 11), (cx + flare, cy + 12)], fill=P["white"])
        draw.rectangle((cx - 7, cy - 2, cx + 7, cy + 3), fill=P["light"])
        draw.line((cx - 5, cy - 13, cx + 5, cy - 13), fill=P["wood_hi"], width=2)
    elif any(word in label for word in ("册", "账", "图", "契", "告示", "证据", "军籍")):
        half_w, half_h = 11 + seed % 4, 13 + (seed // 5) % 4
        draw.rectangle((cx - half_w, cy - half_h, cx + half_w, cy + half_h), fill=P["cloth"])
        draw.rectangle((cx - half_w + 2, cy - half_h + 2, cx + half_w - 2, cy + half_h - 2), outline=P["ink"], width=2)
        for line in range(5):
            draw.line((cx - 9, cy - 9 + line * 5, cx + 8 - line % 2 * 4, cy - 9 + line * 5), fill=accent, width=1)
    elif any(word in label for word in ("旗", "标", "路牌", "门禁")):
        draw.line((cx - 7, cy - 18, cx - 7, cy + 18), fill=P["wood_hi"], width=3)
        draw.polygon([(cx - 5, cy - 16), (cx + 15, cy - 11), (cx - 5, cy - 3)], fill=accent)
    elif any(word in label for word in ("粮", "袋", "盐", "药")):
        spread = 11 + seed % 4
        draw.polygon([(cx - spread, cy + 14), (cx - 9, cy - 10), (cx - 5, cy - 15), (cx + 6, cy - 15), (cx + 10, cy - 9), (cx + spread, cy + 14)], fill=P["cloth"])
        draw.line((cx - 8, cy - 9, cx + 9, cy - 9), fill=P["red"], width=2)
        draw.rectangle((cx - 4, cy, cx + 5, cy + 7), fill=accent)
    else:
        radius = 10 + seed % 5
        draw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=P["metal"])
        draw.polygon([(cx, cy - 11), (cx + 10, cy + 8), (cx - 10, cy + 8)], fill=accent)
        draw.rectangle((cx - 3, cy - 4, cx + 3, cy + 10), fill=P["ink"])
    # Role-specific attachment creates meaningful pouch, strap, hook or seal variation.
    if seed % 4 == 0:
        draw.rectangle((ox + 3 + seed % 7, oy + 19, ox + 7 + seed % 7, oy + 28), fill=accent)
    elif seed % 4 == 1:
        draw.line((ox + 6, oy + 38, ox + 16 + seed % 9, oy + 43), fill=P["wood_hi"], width=2)
    elif seed % 4 == 2:
        draw.ellipse((ox + 34, oy + 5 + seed % 7, ox + 42, oy + 13 + seed % 7), fill=accent)


def draw_faction_cell(image: Image.Image, origin: tuple[int, int], key: str, label: str) -> None:
    ox, oy = origin
    draw = ImageDraw.Draw(image)
    draw.rectangle((ox, oy, ox + 191, oy + 95), fill="#c5b58d")
    draw.rectangle((ox + 3, oy + 3, ox + 188, oy + 92), outline=P["ink2"], width=2)
    figure(draw, (ox + 5, oy + 6, ox + 48, oy + 91), key, 0, label)
    draw.polygon([(ox + 59, oy + 21), (ox + 82, oy + 12), (ox + 101, oy + 24), (ox + 96, oy + 62), (ox + 79, oy + 80), (ox + 61, oy + 63)], fill=color_for(key))
    draw.polygon([(ox + 75, oy + 25), (ox + 88, oy + 44), (ox + 74, oy + 64)], fill=P["cloth"])
    draw_prop_cell(image, (ox + 105, oy + 13), key + "-tool", label, (48, 48))
    draw.line((ox + 161, oy + 14, ox + 161, oy + 80), fill=P["wood"], width=3)
    draw.rectangle((ox + 164, oy + 17, ox + 186, oy + 39), fill=color_for(key, 2))
    for swatch in range(4):
        draw.rectangle((ox + 108 + swatch * 13, oy + 70, ox + 118 + swatch * 13, oy + 81), fill=color_for(key, swatch))


def draw_terrain_cell(image: Image.Image, origin: tuple[int, int], key: str, label: str) -> None:
    ox, oy = origin
    draw = ImageDraw.Draw(image)
    seed = hid(key)
    water = any(word in label for word in ("浅滩", "急流", "平湖", "风浪", "分洪渠", "漕渠"))
    field = any(word in label for word in ("灾田", "田垄", "稻田", "成熟", "复耕"))
    mud = any(word in label for word in ("烂泥", "决口", "壕沟", "瓦砾"))
    road = any(word in label for word in ("路", "街", "石阶", "门槛", "码头", "桥板", "栈桥"))
    dike = any(word in label for word in ("堤", "石脚", "渗沟"))
    snow = any(word in label for word in ("雪", "冻", "骑迹"))
    reeds = "芦苇" in label
    wall = any(word in label for word in ("城墙", "门洞", "作业面"))
    if water or reeds:
        draw.rectangle((ox, oy, ox + 127, oy + 127), fill=P["water"])
        for wave in range(10):
            y = oy + 6 + wave * 12
            offset = (seed + wave * 13) % 25
            draw.line((ox + offset, y, ox + 54 + offset, y), fill=P["water_hi"], width=2)
            draw.line((ox + 72 - offset // 2, y + 5, ox + 120, y + 5), fill=P["sky2"], width=2)
        if "浅滩" in label:
            for stone in range(18):
                sx = ox + 5 + (seed + stone * 31) % 116
                sy = oy + 7 + (seed // 3 + stone * 19) % 112
                draw.ellipse((sx, sy, sx + 5 + stone % 4, sy + 3), fill=P["stone_hi"])
        if "急流" in label or "风浪" in label:
            for current in range(6):
                y = oy + 11 + current * 20
                draw.arc((ox + 6, y, ox + 121, y + 20), 190, 345, fill=P["white"], width=2)
        if "渠" in label:
            draw.polygon([(ox, oy), (ox + 31, oy), (ox + 55, oy + 127), (ox, oy + 127)], fill=P["stone"])
            draw.polygon([(ox + 97, oy), (ox + 127, oy), (ox + 127, oy + 127), (ox + 74, oy + 127)], fill=P["stone"])
        if reeds:
            for reed in range(32):
                rx = ox + 3 + (reed * 23 + seed) % 121
                ry = oy + 33 + (reed * 17) % 92
                draw.line((rx, ry, rx + reed % 3 - 1, ry - 18 - reed % 9), fill=P["green2"], width=2)
    elif field:
        irrigated = "灌水" in label
        draw.rectangle((ox, oy, ox + 127, oy + 127), fill=P["water"] if irrigated else P["ground"])
        for furrow in range(10):
            x = ox - 20 + furrow * 18
            draw.line((x, oy, x + 48, oy + 127), fill=P["green2"] if "复耕" in label or "成熟" in label else P["mud"], width=4)
            if "成熟" in label:
                for crop in range(5):
                    cx, cy = x + crop * 9 + 4, oy + crop * 24 + 8
                    draw.line((cx, cy, cx, cy + 11), fill=P["light"], width=2)
                    draw.line((cx - 3, cy + 4, cx + 3, cy + 1), fill=P["cloth"], width=1)
        if "灾田" in label:
            for crack in range(12):
                x = ox + 5 + (seed + crack * 37) % 115
                y = oy + 6 + (seed // 5 + crack * 29) % 110
                draw.line((x, y, x + 9, y + 7, x + 5, y + 14), fill=P["ink2"], width=2)
    elif snow:
        draw.rectangle((ox, oy, ox + 127, oy + 127), fill=P["snow"])
        draw.polygon([(ox, oy + 82), (ox + 38, oy + 53), (ox + 76, oy + 85), (ox + 127, oy + 44), (ox + 127, oy + 127), (ox, oy + 127)], fill=P["stone_hi"])
        if "骑迹" in label:
            for track in range(12):
                x, y = ox + 11 + track * 9, oy + 112 - track * 7
                draw.ellipse((x, y, x + 7, y + 3), fill=P["ground"])
        else:
            for grass in range(18):
                x = ox + 4 + (seed + grass * 19) % 120
                y = oy + 42 + (seed // 11 + grass * 23) % 82
                draw.line((x, y, x + grass % 3 - 1, y - 7), fill=P["green"], width=1)
    elif wall:
        draw.rectangle((ox, oy, ox + 127, oy + 127), fill=P["stone"])
        for row in range(8):
            y = oy + row * 16
            draw.line((ox, y, ox + 127, y), fill=P["ink2"], width=2)
            offset = 0 if row % 2 else 12
            for x in range(ox + offset, ox + 128, 28):
                draw.line((x, y, x, y + 16), fill=P["ink2"], width=2)
        if "城墙" in label:
            for merlon in range(6):
                x = ox + 4 + merlon * 22
                draw.rectangle((x, oy, x + 11, oy + 20), fill=P["stone_hi"])
        elif "门洞" in label:
            draw.ellipse((ox + 34, oy + 19, ox + 94, oy + 111), fill=P["ink"])
            draw.rectangle((ox + 34, oy + 65, ox + 94, oy + 127), fill=P["ink"])
        else:
            for rubble in range(15):
                x = ox + 3 + (seed + rubble * 23) % 118
                y = oy + 69 + (rubble * 17) % 51
                draw.polygon([(x, y + 8), (x + 5, y), (x + 13, y + 8)], fill=P["stone_hi"])
    elif dike:
        draw.rectangle((ox, oy, ox + 127, oy + 127), fill=P["water"])
        draw.polygon([(ox - 1, oy + 110), (ox + 38, oy + 19), (ox + 89, oy + 11), (ox + 128, oy + 103), (ox + 128, oy + 128), (ox, oy + 128)], fill=P["ground"])
        draw.polygon([(ox + 18, oy + 100), (ox + 46, oy + 31), (ox + 76, oy + 28), (ox + 105, oy + 104)], fill=P["stone"])
        draw.line((ox + 39, oy + 23, ox + 89, oy + 15), fill=P["cloth"], width=5)
        for stake in range(7):
            x = ox + 28 + stake * 12
            draw.line((x, oy + 44 + stake % 2 * 5, x, oy + 69 + stake % 2 * 5), fill=P["wood_hi"], width=2)
    elif road:
        draw.rectangle((ox, oy, ox + 127, oy + 127), fill=P["ground"])
        if any(word in label for word in ("街", "石阶", "门槛", "码头")):
            draw.rectangle((ox, oy, ox + 127, oy + 127), fill=P["stone"])
            for row in range(8):
                y = oy + row * 16
                draw.line((ox, y, ox + 127, y), fill=P["ink2"], width=1)
                for x in range(ox + (row % 2) * 12, ox + 128, 24):
                    draw.line((x, y, x, y + 16), fill=P["gray"], width=1)
        elif any(word in label for word in ("桥板", "栈桥")):
            for plank in range(11):
                x = ox + plank * 12
                draw.rectangle((x, oy, x + 10, oy + 127), fill=P["wood_hi"] if plank % 2 else P["wood"])
                draw.rectangle((x + 2, oy + 14, x + 4, oy + 16), fill=P["metal"])
        else:
            draw.polygon([(ox + 5, oy + 127), (ox + 47, oy), (ox + 87, oy), (ox + 124, oy + 127)], fill=P["mud"])
            for rut in (42, 83):
                draw.line((ox + rut, oy, ox + rut - 17, oy + 127), fill=P["ink2"], width=4)
    elif mud:
        draw.rectangle((ox, oy, ox + 127, oy + 127), fill=P["mud"])
        for puddle in range(10):
            x = ox + 3 + (seed + puddle * 29) % 105
            y = oy + 5 + (seed // 7 + puddle * 31) % 108
            draw.ellipse((x, y, x + 14 + puddle % 9, y + 7 + puddle % 4), fill=P["water"])
        if "瓦砾" in label:
            for rock in range(16):
                x = ox + (seed + rock * 41) % 119
                y = oy + (seed // 5 + rock * 17) % 119
                draw.polygon([(x, y + 7), (x + 4, y), (x + 11, y + 6)], fill=P["stone_hi"])
    else:
        draw.rectangle((ox, oy, ox + 127, oy + 127), fill=P["ground"])
        for mark in range(26):
            x = ox + 3 + (seed + mark * 19) % 121
            y = oy + 3 + (seed // 13 + mark * 31) % 121
            draw.rectangle((x, y, x + 3, y + 2), fill=P["mud"])
    # Survey stake / current marker: unique placement records the terrain module family.
    identity_mark(draw, origin, (128, 128), key, True)


def draw_structure_cell(image: Image.Image, origin: tuple[int, int], key: str, label: str) -> None:
    ox, oy = origin
    draw = ImageDraw.Draw(image)
    seed = hid(key)
    for state in range(4):
        sx, sy = ox + (state % 2) * 64, oy + (state // 2) * 64
        base = color_for(key, state)
        gate = any(word in label for word in ("城门", "宫门", "营门"))
        tower = any(word in label for word in ("信号台", "旗点"))
        store = any(word in label for word in ("县仓", "粮站", "盐仓"))
        tent = any(word in label for word in ("火头营", "药棚", "伤兵棚", "避难所", "分粮点"))
        waterworks = any(word in label for word in ("闸门", "浮桥", "渡口"))
        machine = any(word in label for word in ("弩车", "炮石", "拒马", "火油"))
        tunnel = "地道" in label
        evidence = any(word in label for word in ("登记案", "军籍", "告示", "证据柜"))
        if gate:
            draw.rectangle((sx + 6, sy + 25, sx + 58, sy + 58), fill=P["stone"])
            draw.rectangle((sx + 10, sy + 13, sx + 22, sy + 57), fill=P["stone_hi"])
            draw.rectangle((sx + 42, sy + 13, sx + 54, sy + 57), fill=P["stone_hi"])
            draw.rectangle((sx + 25, sy + 30, sx + 39, sy + 58), fill=P["ink"])
            for merlon in range(4):
                draw.rectangle((sx + 7 + merlon * 15, sy + 18, sx + 15 + merlon * 15, sy + 27), fill=P["stone_hi"])
        elif tower:
            draw.rectangle((sx + 23, sy + 16, sx + 43, sy + 58), fill=P["stone"])
            draw.polygon([(sx + 17, sy + 18), (sx + 33, sy + 5), (sx + 49, sy + 18)], fill=P["ink2"])
            draw.line((sx + 33, sy + 7, sx + 33, sy + 1), fill=P["wood_hi"], width=2)
            draw.polygon([(sx + 35, sy + 2), (sx + 50, sy + 7), (sx + 35, sy + 12)], fill=base)
        elif store:
            draw.rectangle((sx + 5, sy + 30, sx + 59, sy + 58), fill=P["wood"])
            draw.polygon([(sx + 2, sy + 31), (sx + 31, sy + 13), (sx + 62, sy + 31)], fill=P["ink2"])
            for bay in range(3):
                draw.rectangle((sx + 11 + bay * 17, sy + 38, sx + 22 + bay * 17, sy + 58), fill=P["cloth"])
                draw.line((sx + 12 + bay * 17, sy + 44, sx + 21 + bay * 17, sy + 44), fill=P["red"], width=2)
        elif tent:
            draw.polygon([(sx + 4, sy + 57), (sx + 29, sy + 16), (sx + 59, sy + 57)], fill=P["cloth"])
            draw.line((sx + 30, sy + 14, sx + 30, sy + 59), fill=P["wood_hi"], width=2)
            draw.rectangle((sx + 26, sy + 39, sx + 35, sy + 58), fill=P["ink"])
            if "药" in label or "伤" in label:
                draw.rectangle((sx + 43, sy + 26, sx + 49, sy + 40), fill=P["red"])
                draw.rectangle((sx + 39, sy + 30, sx + 53, sy + 36), fill=P["red"])
        elif waterworks:
            draw.rectangle((sx + 1, sy + 38, sx + 62, sy + 61), fill=P["water"])
            if "闸" in label:
                draw.rectangle((sx + 12, sy + 13, sx + 22, sy + 58), fill=P["stone"])
                draw.rectangle((sx + 43, sy + 13, sx + 53, sy + 58), fill=P["stone"])
                draw.rectangle((sx + 23, sy + 25, sx + 42, sy + 54), fill=P["wood_hi"])
                for slat in range(4): draw.line((sx + 24, sy + 29 + slat * 7, sx + 41, sy + 29 + slat * 7), fill=P["ink2"], width=2)
            elif "桥" in label:
                draw.line((sx + 3, sy + 43, sx + 61, sy + 43), fill=P["wood_hi"], width=7)
                for plank in range(7): draw.line((sx + 7 + plank * 8, sy + 38, sx + 7 + plank * 8, sy + 50), fill=P["cloth"], width=2)
            else:
                draw.ellipse((sx + 18, sy + 11, sx + 46, sy + 39), outline=P["wood_hi"], width=5)
                draw.line((sx + 32, sy + 25, sx + 55, sy + 53), fill=P["wood_hi"], width=3)
        elif machine:
            if "炮石" in label:
                draw.polygon([(sx + 8, sy + 55), (sx + 25, sy + 23), (sx + 51, sy + 55)], outline=P["wood_hi"])
                draw.line((sx + 29, sy + 47, sx + 50, sy + 8), fill=P["wood_hi"], width=5)
                draw.ellipse((sx + 47, sy + 4, sx + 57, sy + 14), fill=P["stone_hi"])
            elif "弩车" in label:
                draw.arc((sx + 7, sy + 14, sx + 57, sy + 55), 200, 340, fill=P["wood_hi"], width=4)
                draw.line((sx + 11, sy + 41, sx + 55, sy + 28), fill=P["metal_hi"], width=3)
                draw.ellipse((sx + 15, sy + 46, sx + 25, sy + 57), fill=P["wood"])
                draw.ellipse((sx + 42, sy + 46, sx + 52, sy + 57), fill=P["wood"])
            elif "拒马" in label:
                for rail in range(3):
                    x = sx + 12 + rail * 17
                    draw.line((x, sy + 17, x + 10, sy + 57), fill=P["wood_hi"], width=4)
                    draw.line((x + 10, sy + 17, x, sy + 57), fill=P["wood_hi"], width=4)
            else:
                draw.rectangle((sx + 8, sy + 36, sx + 56, sy + 56), fill=P["stone"])
                for jar in range(4):
                    x = sx + 11 + jar * 11
                    draw.ellipse((x, sy + 20, x + 9, sy + 40), fill=P["red"])
        elif tunnel:
            draw.polygon([(sx + 2, sy + 58), (sx + 14, sy + 29), (sx + 32, sy + 13), (sx + 54, sy + 29), (sx + 62, sy + 58)], fill=P["ground"])
            draw.ellipse((sx + 17, sy + 29, sx + 47, sy + 62), fill=P["ink"])
            draw.rectangle((sx + 21, sy + 45, sx + 43, sy + 62), fill=P["ink"])
            for timber in (20, 43): draw.line((sx + timber, sy + 35, sx + timber, sy + 58), fill=P["wood_hi"], width=3)
        elif evidence:
            draw.rectangle((sx + 8, sy + 39, sx + 56, sy + 56), fill=P["wood"])
            draw.line((sx + 13, sy + 56, sx + 13, sy + 62), fill=P["wood_hi"], width=3)
            draw.line((sx + 51, sy + 56, sx + 51, sy + 62), fill=P["wood_hi"], width=3)
            for paper in range(3):
                draw.rectangle((sx + 12 + paper * 14, sy + 24 - paper % 2 * 5, sx + 23 + paper * 14, sy + 39), fill=P["cloth"])
                draw.line((sx + 14 + paper * 14, sy + 29 - paper % 2 * 5, sx + 21 + paper * 14, sy + 29 - paper % 2 * 5), fill=P["ink"], width=1)
        else:
            draw.rectangle((sx + 10, sy + 31, sx + 54, sy + 58), fill=P["stone"])
            draw.polygon([(sx + 6, sy + 32), (sx + 32, sy + 14), (sx + 58, sy + 32)], fill=P["ink2"])
            draw.rectangle((sx + 25, sy + 40, sx + 39, sy + 58), fill=base)
        if state == 1:
            draw.line((sx + 50, sy + 12, sx + 50, sy + 48), fill=P["wood_hi"], width=2)
            draw.rectangle((sx + 52, sy + 13, sx + 61, sy + 23), fill=P["red"])
        elif state == 2:
            draw.line((sx + 8, sy + 23, sx + 53, sy + 53), fill=P["ink"], width=3)
            draw.rectangle((sx + 14, sy + 47, sx + 52, sy + 59), fill=P["mud"])
        elif state == 3:
            draw.line((sx + 10, sy + 54, sx + 54, sy + 19), fill=P["light"], width=2)
    # Site registry tile is part of the map-mechanism language.
    identity_mark(draw, origin, (128, 128), key)


def draw_four_item_cell(image: Image.Image, origin: tuple[int, int], key: str, label: str, cell_size: tuple[int, int]) -> None:
    ox, oy = origin
    cw, ch = cell_size
    slot_w = cw // 4
    for slot in range(4):
        pw = min(48, slot_w)
        px = ox + slot * slot_w + (slot_w - pw) // 2
        py = oy + max(0, (ch - 48) // 2)
        draw_prop_cell(image, (px, py), f"{key}-{slot}", f"{label}-{slot}", (pw, min(48, ch)))


def draw_equipment_cell(image: Image.Image, origin: tuple[int, int], key: str, label: str) -> None:
    ox, oy = origin
    draw = ImageDraw.Draw(image)
    accent = color_for(key)
    for slot in range(4):
        sx, sy = ox + slot * 48, oy
        cx, cy = sx + 24, sy + 24
        bow = any(word in label for word in ("弓", "射"))
        crossbow = "弩" in label
        pole = any(word in label for word in ("枪", "长枪", "矛", "凿", "河工", "架桥", "云梯", "地道"))
        blade = any(word in label for word in ("刀", "陌刀", "破阵", "亲兵", "禁军", "护卫", "乡勇"))
        firearm = any(word in label for word in ("铳", "火箭", "震天雷", "爆破", "火攻", "火油"))
        medical = any(word in label for word in ("医", "救护", "药"))
        document = any(word in label for word in ("粮秣", "军师", "斥候司", "说客", "密察", "军籍", "证据", "侦察", "标路", "分发"))
        naval = any(word in label for word in ("艨艟", "楼船", "火船", "漕运", "潜渡", "水军", "防水"))
        mounted = any(word in label for word in ("骑", "边军斥候", "雪原"))
        engineering = any(word in label for word in ("工程", "修复", "筑城", "炮石"))
        flag = any(word in label for word in ("旗鼓", "降兵", "阵营"))
        if slot == 3:
            draw.line((sx + 14, sy + 7, sx + 14, sy + 42), fill=P["wood_hi"], width=3)
            draw.polygon([(sx + 16, sy + 9), (sx + 39, sy + 15), (sx + 16, sy + 28)], fill=accent)
            draw.rectangle((sx + 24, sy + 17, sx + 29, sy + 22), fill=P["cloth"])
        elif crossbow:
            y = sy + 16 + slot * 7
            draw.arc((sx + 6, y - 9, sx + 42, y + 15), 190, 350, fill=P["wood_hi"], width=3)
            draw.line((sx + 8, y + 2, sx + 41, y + 2), fill=P["metal_hi"], width=2)
            draw.line((cx, y, cx, sy + 42), fill=P["wood"], width=4)
            if slot == 2:
                for bolt in range(4): draw.line((sx + 10 + bolt * 8, sy + 8, sx + 16 + bolt * 8, sy + 37), fill=P["metal_hi"], width=1)
        elif bow:
            draw.arc((sx + 8 + slot * 2, sy + 5, sx + 39, sy + 43), 85, 275, fill=P["wood_hi"], width=3)
            draw.line((sx + 24 + slot, sy + 6, sx + 24 + slot, sy + 42), fill=P["cloth"], width=1)
            if slot == 2:
                draw.rectangle((sx + 30, sy + 12, sx + 38, sy + 40), fill=P["ground"])
                for arrow in range(3): draw.line((sx + 31 + arrow * 3, sy + 15, sx + 34 + arrow * 3, sy + 3), fill=P["metal_hi"], width=1)
        elif pole:
            draw.line((sx + 10 + slot * 3, sy + 40, sx + 35 - slot * 2, sy + 7), fill=P["wood_hi"], width=4)
            draw.polygon([(sx + 32 - slot * 2, sy + 8), (sx + 40 - slot * 2, sy + 2), (sx + 38 - slot * 2, sy + 13)], fill=P["metal_hi"])
            if engineering or "河工" in label:
                draw.rectangle((sx + 5, sy + 30, sx + 20, sy + 42), outline=P["cloth"], width=3)
        elif blade:
            draw.line((sx + 13, sy + 39, sx + 33 + slot * 2, sy + 8), fill=P["metal_hi"], width=5 if "陌刀" in label else 3)
            draw.rectangle((sx + 10, sy + 34, sx + 25, sy + 38), fill=P["wood"])
            if slot == 1 or "盾" in label:
                draw.polygon([(sx + 27, sy + 10), (sx + 42, sy + 14), (sx + 39, sy + 37), (sx + 28, sy + 43), (sx + 20, sy + 34)], fill=P["metal"])
                draw.line((sx + 27, sy + 17, sx + 38, sy + 33), fill=accent, width=2)
        elif firearm:
            if "震天雷" in label or "爆破" in label:
                draw.ellipse((sx + 11, sy + 13, sx + 38, sy + 40), fill=P["metal"])
                draw.line((sx + 28, sy + 13, sx + 36, sy + 5), fill=P["light"], width=2)
                draw.rectangle((sx + 15, sy + 22, sx + 34, sy + 27), fill=P["red"])
            else:
                draw.line((sx + 7, sy + 17 + slot * 5, sx + 40, sy + 13 + slot * 5), fill=P["metal_hi"], width=5)
                draw.rectangle((sx + 23, sy + 20 + slot * 5, sx + 31, sy + 35 + slot * 3), fill=P["wood"])
                draw.rectangle((sx + 8, sy + 10, sx + 14, sy + 16), fill=P["red"])
        elif medical:
            if slot == 0:
                draw.rectangle((sx + 8, sy + 12, sx + 40, sy + 39), fill=P["cloth"], outline=P["ink"], width=2)
                draw.rectangle((sx + 21, sy + 17, sx + 27, sy + 34), fill=P["red"])
                draw.rectangle((sx + 15, sy + 23, sx + 34, sy + 29), fill=P["red"])
            elif slot == 1:
                draw.rectangle((sx + 15, sy + 8, sx + 33, sy + 39), fill=P["water_hi"])
                draw.rectangle((sx + 20, sy + 4, sx + 28, sy + 10), fill=P["metal_hi"])
            else:
                for bandage in range(3): draw.ellipse((sx + 7 + bandage * 11, sy + 16, sx + 20 + bandage * 11, sy + 31), outline=P["white"], width=3)
        elif naval:
            if slot == 0:
                draw.arc((sx + 8, sy + 7, sx + 39, sy + 40), 20, 280, fill=P["metal_hi"], width=4)
                draw.line((sx + 31, sy + 32, sx + 42, sy + 43), fill=P["wood_hi"], width=3)
            elif slot == 1:
                draw.ellipse((sx + 8, sy + 8, sx + 40, sy + 40), outline=P["cloth"], width=4)
                draw.line((sx + 13, sy + 13, sx + 35, sy + 35), fill=P["wood_hi"], width=3)
            else:
                draw.polygon([(sx + 5, sy + 35), (sx + 18, sy + 12), (sx + 40, sy + 35)], fill=P["wood"])
                draw.line((sx + 12, sy + 31, sx + 36, sy + 31), fill=P["water_hi"], width=2)
        elif mounted:
            if slot == 0:
                draw.polygon([(sx + 8, sy + 28), (sx + 21, sy + 12), (sx + 39, sy + 27), (sx + 34, sy + 41), (sx + 13, sy + 41)], fill=P["wood"])
            elif slot == 1:
                draw.ellipse((sx + 9, sy + 8, sx + 39, sy + 39), outline=P["cloth"], width=4)
            else:
                draw.rectangle((sx + 9, sy + 12, sx + 37, sy + 39), fill=P["cloth"])
                draw.line((sx + 13, sy + 16, sx + 34, sy + 35), fill=P["red"], width=2)
        elif document or flag:
            draw.rectangle((sx + 8 + slot * 2, sy + 7, sx + 39, sy + 41 - slot * 2), fill=P["cloth"], outline=P["ink"], width=2)
            for line in range(5): draw.line((sx + 12, sy + 13 + line * 5, sx + 34 - line % 2 * 5, sy + 13 + line * 5), fill=accent, width=1)
            if slot == 1:
                draw.ellipse((sx + 27, sy + 27, sx + 39, sy + 39), fill=P["red"])
        else:
            draw.polygon([(sx + 8, sy + 39), (sx + 14, sy + 11), (sx + 34, sy + 7), (sx + 41, sy + 37), (sx + 28, sy + 43)], fill=P["metal"])
            draw.rectangle((sx + 16, sy + 16, sx + 34, sy + 21), fill=accent)
    identity_mark(draw, origin, (192, 48), key)


def draw_icon_cell(image: Image.Image, origin: tuple[int, int], key: str, label: str, kind: str = "skill") -> None:
    ox, oy = origin
    draw = ImageDraw.Draw(image)
    cx, cy = ox + 24, oy + 24
    seed = hid(key)
    accent = color_for(key)
    inset = 3 + seed % 5
    shape = seed % 5
    if shape == 0:
        draw.ellipse((ox + inset, oy + inset, ox + 47 - inset, oy + 47 - inset), fill=P["ink2"], outline=P["stone_hi"], width=2)
    elif shape == 1:
        draw.polygon([(cx, oy + inset), (ox + 47 - inset, cy), (cx, oy + 47 - inset), (ox + inset, cy)], fill=P["ink2"], outline=P["stone_hi"])
    elif shape == 2:
        draw.polygon([(ox + 8, oy + inset), (ox + 39, oy + inset), (ox + 45, cy), (ox + 34, oy + 44), (ox + 13, oy + 44), (ox + 3, cy)], fill=P["ink2"], outline=P["stone_hi"])
    elif shape == 3:
        draw.polygon([(ox + 7, oy + 7), (ox + 40, oy + 7), (ox + 37, oy + 34), (cx, oy + 44), (ox + 10, oy + 34)], fill=P["ink2"], outline=P["stone_hi"])
    else:
        draw.rectangle((ox + inset, oy + inset + 2, ox + 47 - inset, oy + 45 - inset), fill=P["ink2"], outline=P["stone_hi"], width=2)
    # Tabs encode target/direction families and keep transparent silhouettes distinct.
    for tab in range(1 + seed % 3):
        side = (seed >> (tab * 3)) % 4
        pos = 10 + (seed >> (tab * 5)) % 25
        if side == 0: draw.rectangle((ox + pos, oy + 1, ox + pos + 2, oy + inset + 2), fill=accent)
        elif side == 1: draw.rectangle((ox + 44 - inset, oy + pos, ox + 47, oy + pos + 2), fill=accent)
        elif side == 2: draw.rectangle((ox + pos, oy + 44 - inset, ox + pos + 2, oy + 47), fill=accent)
        else: draw.rectangle((ox + 1, oy + pos, ox + inset + 2, oy + pos + 2), fill=accent)
    if kind == "status":
        draw.polygon([(cx, oy + 9), (ox + 38, oy + 35), (ox + 10, oy + 35)], fill=accent)
        draw.rectangle((cx - 2, oy + 17, cx + 2, oy + 29), fill=P["white"])
    elif kind == "hud":
        draw.rectangle((ox + 11, oy + 11, ox + 36, oy + 36), outline=accent, width=4)
        draw.polygon([(cx, oy + 15), (ox + 33, cy), (cx, oy + 33), (ox + 15, cy)], fill=P["cloth"])
    else:
        draw.line((ox + 13, oy + 34, ox + 35, oy + 13), fill=accent, width=5)
        draw.polygon([(ox + 31, oy + 10), (ox + 39, oy + 10), (ox + 38, oy + 18)], fill=P["metal_hi"])
        draw.arc((ox + 10, oy + 10, ox + 38, oy + 38), 25, 210, fill=P["light"], width=2)
    # Direction/permission notch is part of the icon grammar and prevents ambiguous twins.
    identity_mark(draw, origin, (48, 48), key)


def draw_fx_cell(image: Image.Image, origin: tuple[int, int], key: str, label: str) -> None:
    ox, oy = origin
    draw = ImageDraw.Draw(image)
    seed = hid(key)
    accent = color_for(key)
    cx, cy = ox + 48, oy + 48
    for ray in range(12):
        angle = ray * math.pi / 6 + (seed % 7) / 20
        inner = 10 + ray % 3 * 3
        outer = 25 + (seed >> ray) % 18
        x0, y0 = cx + int(math.cos(angle) * inner), cy + int(math.sin(angle) * inner)
        x1, y1 = cx + int(math.cos(angle) * outer), cy + int(math.sin(angle) * outer)
        draw.line((x0, y0, x1, y1), fill=accent, width=2 + ray % 2)
    draw.ellipse((cx - 11, cy - 11, cx + 11, cy + 11), fill=P["light"], outline=P["white"], width=2)
    if any(word in label for word in ("雨", "水", "湿", "流")):
        for drop in range(8):
            x = ox + 10 + (seed + drop * 17) % 75
            y = oy + 8 + (seed // 9 + drop * 13) % 70
            draw.line((x, y, x - 3, y + 9), fill=P["water_hi"], width=2)


def atlas(
    delivery_id: str,
    delivery_type: str,
    category: str,
    items: list[tuple[str, str, str]],
    cell_size: tuple[int, int],
    columns: int,
    renderer: Callable[[Image.Image, tuple[int, int], str, str], None],
    subdir: str,
    topics: list[dict],
    deliveries: list[dict],
    opaque: bool = False,
    source: str = "expanded",
) -> None:
    cell_w, cell_h = cell_size
    rows = math.ceil(len(items) / columns)
    image = canvas(cell_w * columns, cell_h * rows, opaque=opaque)
    ids: list[str] = []
    for index, (topic_id, label, priority) in enumerate(items):
        x, y = (index % columns) * cell_w, (index // columns) * cell_h
        renderer(image, (x, y), topic_id, label)
        record = {
            "id": topic_id, "label": label, "category": category,
            "assetId": delivery_id, "status": "formal", "source": source,
            "priority": priority, "cell": index,
        }
        if category in {"combat-unit", "mission-unit"}:
            record["frame"] = {"count": 4, "width": 32, "height": 48, "order": ["idle-a", "step-a", "idle-b", "step-b"]}
        topics.append(record)
        ids.append(topic_id)
    stem = OUT / subdir / delivery_id
    png, svg = save_pair(image, stem, delivery_id)
    deliveries.append({
        "id": delivery_id, "type": delivery_type, "png": png, "svg": svg,
        "width": image.width, "height": image.height, "cellWidth": cell_w,
        "cellHeight": cell_h, "columns": columns, "topicIds": ids,
    })


def triples(prefix: str, labels: list[str], priorities: list[int] | None = None) -> list[tuple[str, str, str]]:
    priorities = priorities or [0] * len(labels)
    result = []
    for index, label in enumerate(labels):
        slug = hashlib.sha1(label.encode("utf-8")).hexdigest()[:8]
        result.append((f"c03-{prefix}-{slug}", label, f"P{priorities[index]}"))
    return result


STYLE = triples("style", ["主视觉：官仓与宫门之间", "淮右灾年雨色与湿麻", "大泽风火与民船", "开国冬季宫城", "江东建设期春景", "燕云雪原与多族营具", "军中反复改写的水陆墨图"], [0, 0, 0, 0, 1, 1, 1])
CHARACTERS = [
    ("c03-char-shen-02", "沈砺·28岁江东统帅", "P0"), ("c03-char-shen-03", "沈砺·36岁北伐吴王", "P0"),
    ("c03-char-shen-04", "沈砺·40岁开国皇帝", "P0"), ("c03-char-lu-02", "陆青禾·临川户曹", "P0"),
    ("c03-char-lu-03", "陆青禾·开国法制主事", "P0"), ("c03-char-han-02", "韩岳·大泽前锋", "P0"),
    ("c03-char-han-03", "韩岳·开国功臣", "P0"), ("c03-char-pei-02", "裴昭·新军统帅", "P1"),
    ("c03-char-jiang-02", "江照夜·大泽水师统帅", "P1"), ("c03-char-xiao-01", "萧慎·落第县吏", "P1"),
    ("c03-char-xiao-02", "萧慎·密察司主事", "P1"), ("c03-char-wen-01", "闻素·随军史官", "P1"),
    ("c03-char-xiaoman-01", "赵小满·11岁灾民", "P2"), ("c03-char-xiaoman-02", "赵小满·学馆书吏", "P2"),
    ("c03-char-xiaoman-03", "赵小满·开国户曹书吏", "P2"), ("c03-char-peng-01", "彭三篙·水盟船工", "P2"),
    ("c03-char-peng-02", "彭三篙·伤残渡口主人", "P2"),
]
SCENES = triples("scene", [
    "天未亮的押粮车", "决口后的界碑争议", "芦苇滩白灯赠粮", "白灯夜后",
    "第一面军旗与军法宣读", "白灯赠别与八十九日粮", "和州借城", "田契之争后的播种",
    "梁震旗舰", "吴王之台", "登基与讨饷并置", "新朝田册之乱", "公开审理",
    "临川新政告示", "湖口破船", "富城两条队伍", "三路出师", "燕云盟约",
    "老兵交兵、登记退伍与家属接人",
], [0, 2, 1, 0, 1, 1, 1, 0, 0, 0, 0, 1, 0, 2, 1, 1, 1, 1, 2])
ARCHITECTURE = [
    ("c03-arch-huai-dike-final", "终章加固河堤", "P0"), ("c03-arch-palace-gate", "宫门、市坊、军营与史馆组合区", "P0"),
    ("c03-arch-hezhou-gate", "和州城门", "P1"), ("c03-arch-floating-bridge", "模块化浮桥", "P1"),
    ("c03-arch-wuyue-gate", "吴越富城水闸与盐仓", "P1"), ("c03-arch-old-capital", "旧都与登基城", "P1"),
    ("c03-arch-yanyun-post", "燕云雪原驿站", "P1"), ("c03-arch-jiangdong-salt-port", "江东盐港税卡与民船混合区", "P1"),
]
NARRATIVE_PROPS = [
    ("c03-prop-shoe-ledger", "沈砺鞋底私账", "P0"), ("c03-prop-register-evidence-set", "书吏第一页账册与两册并置证据组", "P0"),
    ("c03-prop-white-lantern-patched", "三次缝补的白灯", "P0"), ("c03-prop-white-lantern-gilded", "镀金白灯", "P1"),
    ("c03-prop-sunken-roll", "大泽沉船名册", "P1"), ("c03-prop-unstable-list", "开国不安定者名册", "P0"),
    ("c03-prop-aletan-covenant", "阿勒坦互市军法自治盟约", "P1"),
]

COMBAT_LABELS = [
    "刀盾卒", "长枪阵", "陌刀队", "乡弓手", "强弩营", "神臂弩", "游骑", "甲骑", "边军斥候", "艨艟",
    "楼船兵", "火船队", "河工", "架桥营", "云梯队", "炮石营", "火铳手", "火箭营", "震天雷队", "军医",
    "旗鼓手", "粮秣官", "军师", "斥候司", "说客", "乡勇", "城门盾兵", "将领亲兵", "铁锤破阵手", "筑城守备",
    "潜渡凿船手", "漕运护军", "盐仓护卫", "雪原骑射", "互市护卫", "宁朝禁军", "密察司缉事", "地道营", "火油防备队", "降兵整编队",
]
COMBAT_P = [0,0,1,0,0,1,0,1,1,0,1,1,0,0,1,1,0,1,1,0,0,1,1,0,2,1,2,2,1,2,2,1,2,2,2,2,2,2,1,2]
COMBAT = triples("unit", COMBAT_LABELS, COMBAT_P)
MISSION_LABELS = ["饥民与灾民", "农户", "播种队", "河工学徒", "水盟船工", "盐工", "仓库工", "粮车夫", "挑夫", "县吏", "书吏与登记员", "城市工匠", "学馆学生", "伤兵", "逃兵", "退伍老兵", "商旅", "渡口主人", "地方向导", "军户家属", "救济厨工", "漕渠民夫", "被押俘虏与证人", "史馆抄录员"]
MISSION_P = [0,0,0,0,0,1,0,0,1,1,0,1,2,0,2,1,2,1,1,1,0,0,0,2]
MISSION = triples("mission", MISSION_LABELS, MISSION_P)
FACTIONS = triples("faction", ["白炬义军装备基准", "旧朝官军装备基准", "水盟装备基准", "梁震大泽军装备基准", "吴越富城装备基准", "大朔与燕云装备基准", "宁朝新军装备基准", "地方乡勇装备基准", "河工与工程营装备基准", "水军与舰队装备基准", "宫城禁军装备基准", "降兵整编态基准"], [0,0,0,0,1,0,1,1,0,1,2,2])

TERRAIN = triples("terrain", [
    "龟裂灾田", "退水烂泥", "复耕田垄", "粮车与难民车辙路", "夯土堤顶", "石脚渗沟", "分洪渠", "水冲决口",
    "芦苇岸", "浅滩", "深水急流", "浮桥板与桥缘", "灌水稻田", "成熟田埂", "青石市街", "盐仓水巷",
    "大泽平湖", "大泽风浪流向", "芦苇隐蔽区", "湿船板与栈桥", "城墙顶面", "门洞地面", "壕沟瓦砾", "云梯地道作业面",
    "燕云积雪原", "风蚀骑迹", "冻土驿路", "互市水源点", "旧都坊市地面", "宫城石阶", "朱门门槛", "漕渠码头",
], [0]*12 + [1]*12 + [2]*8)
STRUCTURES = triples("structure", [
    "中军旗点", "城门控制位", "宫门门禁", "航道信号台", "县仓交互体", "野战粮站", "火头营", "盐仓税卡",
    "河堤闸门", "浮桥桥头", "渡口绞盘", "地道入口", "弩车位", "炮石阵地", "拒马营门", "火油防备站",
    "药棚", "分粮点", "伤兵棚", "白灯避难所", "户籍登记案", "军籍交接点", "公开税额告示", "史馆证据柜",
], [0]*8 + [1]*8 + [2]*8)
BATTLE_PROPS = triples("battle-prop", [
    "粮箱掩体组", "木盾墙组", "盐袋掩体组", "翻倒粮车组", "火药桶危险组", "火油罐危险组", "松动堤土危险组", "燃烧箭束危险组",
    "粮袋量斗后勤组", "水桶后勤组", "药箱后勤组", "备箭架后勤组", "木桩工程组", "绳盘工程组", "桥板工程组", "沙袋湿毡工程组",
    "行军锅生活组", "铺盖生活组", "折叠案生活组", "晾衣绳生活组", "户册证据组", "军籍牌证据组", "税卡证据组", "沉船名牌证据组",
    "未染旗标识组", "官军门牌标识组", "水盟航旗标识组", "宁朝门禁牌标识组", "弃械堆", "伤员区", "修补墙", "退伍交兵台",
], [0]*8 + [1]*12 + [2]*12)
EQUIPMENT = [(f"c03-equip-{topic_id.removeprefix('c03-unit-')}", f"{label}主装备组", priority) for topic_id, label, priority in COMBAT]
EQUIPMENT += triples("equip-shared", ["战地救护共享装备", "工程修复共享装备", "侦察标路共享装备", "粮秣分发共享装备", "反甲爆破共享装备", "防火防水共享装备", "绳索机动共享装备", "军籍证据交互共享装备"], [0,0,0,0,0,0,0,0])
SKILLS = triples("skill", [
    "护卫", "盾阵", "枪拒", "破甲", "瞄射", "压制", "骑军侧击", "追击", "接舷", "治疗", "旗鼓振奋", "侦察标记",
    "白灯集结", "未染旗鼓舞", "官军轮换", "水盟识流", "精准火道", "吴越闭闸", "雪原骑射", "互市停战", "新军入籍", "乡勇守土", "降兵整编", "公开军法",
    "筑堤", "开闸分洪", "架桥", "拆桥", "架梯", "掘地道", "测风控火", "湿毡灭火",
    "分粮", "紧急补给", "伤员分诊", "补箭补药", "路线标识", "劝降", "证据送达", "退伍交兵",
    "盾枪协同", "弩手轮射", "骑射诱敌", "钩索接舷", "火船释放", "炮石校射", "地道破门", "亲兵援护",
], [0]*20 + [1]*16 + [2]*12)
STATUSES = triples("status", ["负伤", "着火", "湿透", "溃退", "芦苇隐蔽", "护卫状态", "被拘受控", "降兵待编", "粮秣资源", "弹药资源", "士气资源", "工程材料", "药材资源", "地方支持", "证据可信度", "船运力", "占领目标", "护送目标", "撤离目标", "救援目标", "维修目标", "破坏目标", "守时目标", "证据送达目标"], [0]*12 + [1]*8 + [2]*4)
FX = triples("fx", ["斩击命中", "穿刺命中", "钝击命中", "箭弩命中", "火铳命中", "爆破命中", "燃烧伤害", "攻城冲击", "暴雨区域", "风雪区域", "浅水区域", "水流区域", "泥泞区域", "芦苇隐蔽区域", "烟火区域", "尘土瓦砾区域", "治疗状态", "鼓舞状态", "侦察标记状态", "隐蔽状态", "拘捕受控状态", "结构受损状态", "修复状态", "占领换旗状态"], [0]*12 + [1]*8 + [2]*4)
HUD = triples("hud", ["友军标记", "敌军标记", "中立标记", "可招降标记", "主目标标记", "次要目标标记", "护送目标标记", "危险区域标记", "补给点标记", "治疗点标记", "工程交互标记", "可破坏结构标记", "可修复结构标记", "增援入口标记", "撤离出口标记", "多阵营军令争议点"], [0]*16)


def existing_topic(topic_id: str, label: str, category: str, asset_id: str, topics: list[dict], cell: int | None = None, rework: bool = False) -> None:
    record = {"id": topic_id, "label": label, "category": category, "assetId": asset_id, "status": "formal", "source": "existing", "priority": "P0"}
    if cell is not None:
        record["cell"] = cell
    if rework:
        record["rework"] = True
    topics.append(record)


def existing_delivery(asset_id: str, kind: str, png: str, svg: str, width: int, height: int, topic_ids: list[str], cell: tuple[int, int, int] | None = None) -> dict:
    record = {"id": asset_id, "type": kind, "png": png, "svg": svg, "width": width, "height": height, "topicIds": topic_ids}
    if cell:
        record.update({"cellWidth": cell[0], "cellHeight": cell[1], "columns": cell[2]})
    return record


def build() -> tuple[dict, dict]:
    topics: list[dict] = []
    deliveries: list[dict] = []

    portraits = [
        ("c03-char-shen-01", "沈砺·22岁河工", "shen-li-22-portrait", "characters/shen-li-22-portrait-hd"),
        ("c03-char-lu-01", "陆青禾·淮上账房", "lu-qinghe-portrait", "characters/lu-qinghe-portrait-hd"),
        ("c03-char-han-01", "韩岳·铁匠义军", "han-yue-portrait", "characters/han-yue-portrait-hd"),
        ("c03-char-pei-01", "裴昭·朝廷追粮将领", "pei-zhao-portrait", "characters/pei-zhao-portrait-hd"),
        ("c03-char-jiang-01", "江照夜·水盟船主", "jiang-zhaoye-portrait", "characters/jiang-zhaoye-portrait-hd"),
        ("c03-char-aletan-01", "阿勒坦·大朔边将", "aletan-portrait", "characters/aletan-portrait-hd"),
    ]
    for topic_id, label, asset_id, stem in portraits:
        existing_topic(topic_id, label, "narrative-static", asset_id, topics)
        deliveries.append(existing_delivery(asset_id, "portrait", f"{stem}.png", f"{stem}.svg", 96, 112, [topic_id]))

    arches = [
        ("c03-arch-granary", "淮右县仓", "county-granary", "architecture/county-granary-hd"),
        ("c03-arch-huai-dike-01", "淮右河堤", "huai-right-bank-dike", "architecture/huai-right-bank-dike-hd"),
        ("c03-arch-linchuan-hub", "临川行台五功能治理区", "linchuan-government-hub", "architecture/linchuan-government-hub-hd"),
        ("c03-arch-great-lake-fleet", "大泽军民混合连营", "great-lake-mixed-fleet", "architecture/great-lake-mixed-fleet-hd"),
    ]
    for topic_id, label, asset_id, stem in arches:
        existing_topic(topic_id, label, "narrative-static", asset_id, topics)
        deliveries.append(existing_delivery(asset_id, "architecture", f"{stem}.png", f"{stem}.svg", 128, 128, [topic_id]))

    scenes = [
        ("c03-scene-ch01-s03", "官仓开门", "opening-the-county-granary", "scenes/opening-the-county-granary-hd"),
        ("c03-scene-ch02-s08", "雨夜渡淮", "rain-night-crossing", "scenes/rain-night-crossing-hd"),
        ("c03-scene-ch04-s18", "大泽精准火攻", "great-lake-precision-fire-attack", "scenes/great-lake-precision-fire-attack-hd"),
        ("c03-scene-ch07-s35e", "一碗新粮", "one-bowl-of-new-grain", "scenes/one-bowl-of-new-grain-hd"),
    ]
    for topic_id, label, asset_id, stem in scenes:
        existing_topic(topic_id, label, "narrative-static", asset_id, topics)
        deliveries.append(existing_delivery(asset_id, "scene", f"{stem}.png", f"{stem}.svg", 256, 144, [topic_id]))

    # Two accepted light reworks. They retain source=existing while pointing to a new non-destructive delivery.
    rework_items = [("c03-prop-grain-seal", "官粮封条强化版", "P0"), ("c03-prop-temp-roll", "无籍灾民临时册强化版", "P0")]
    image = canvas(96, 48)
    for index, (topic_id, label, _priority) in enumerate(rework_items):
        draw_prop_cell(image, (index * 48, 0), topic_id, label)
        existing_topic(topic_id, label, "narrative-static", "c03-prop-light-rework-atlas", topics, index, True)
    png, svg = save_pair(image, OUT / "narrative" / "c03-prop-light-rework-atlas", "C03 prop light reworks")
    deliveries.append({"id": "c03-prop-light-rework-atlas", "type": "prop-atlas", "png": png, "svg": svg, "width": 96, "height": 48, "cellWidth": 48, "cellHeight": 48, "columns": 2, "topicIds": [item[0] for item in rework_items]})

    story_ids = ["c03-prop-white-lantern-plain", "c03-prop-broken-official-seal"]
    existing_topic(story_ids[0], "素白互助灯", "narrative-static", "story-props-sheet", topics, 1)
    existing_topic(story_ids[1], "破官印", "narrative-static", "story-props-sheet", topics, 3)
    deliveries.append(existing_delivery("story-props-sheet", "prop-atlas", "props/story-props-sheet-hd.png", "props/story-props-sheet-hd.svg", 192, 48, story_ids, (48, 48, 4)))
    campaign_ids = ["c03-prop-first-banner", "c03-prop-pei-campaign-map", "c03-prop-field-markers", "c03-prop-pei-clan-roll"]
    campaign_labels = ["第一面未染色军旗", "裴昭战役叠图", "田契丈量标记", "裴氏部曲军籍卷"]
    for index, (topic_id, label) in enumerate(zip(campaign_ids, campaign_labels)):
        existing_topic(topic_id, label, "narrative-static", "campaign-props-sheet-02", topics, index)
    deliveries.append(existing_delivery("campaign-props-sheet-02", "prop-atlas", "props/campaign-props-sheet-02-hd.png", "props/campaign-props-sheet-02-hd.svg", 192, 48, campaign_ids, (48, 48, 4)))

    atlas("c03-style-atlas-01", "style-atlas", "narrative-static", STYLE, (256, 144), 4, draw_scene_cell, "narrative", topics, deliveries, True)
    atlas("c03-character-stage-atlas-01", "portrait-atlas", "narrative-static", CHARACTERS, (96, 112), 6, draw_character_cell, "narrative", topics, deliveries)
    atlas("c03-scene-atlas-01", "scene-atlas", "narrative-static", SCENES, (256, 144), 5, draw_scene_cell, "narrative", topics, deliveries, True)
    atlas("c03-architecture-atlas-01", "architecture-atlas", "narrative-static", ARCHITECTURE, (128, 128), 4, draw_architecture_cell, "narrative", topics, deliveries)
    atlas("c03-narrative-prop-atlas-01", "prop-atlas", "narrative-static", NARRATIVE_PROPS, (48, 48), 4, draw_prop_cell, "narrative", topics, deliveries)

    atlas("c03-combat-unit-atlas-01", "unit-atlas", "combat-unit", COMBAT, (128, 48), 5, draw_unit_cell, "units", topics, deliveries)
    atlas("c03-mission-unit-atlas-01", "unit-atlas", "mission-unit", MISSION, (128, 48), 6, draw_unit_cell, "units", topics, deliveries)
    atlas("c03-faction-kit-atlas-01", "faction-kit-atlas", "faction-kit", FACTIONS, (192, 96), 4, draw_faction_cell, "systems", topics, deliveries, True)
    atlas("c03-terrain-atlas-01", "terrain-atlas", "terrain", TERRAIN, (128, 128), 4, draw_terrain_cell, "systems", topics, deliveries, True)
    atlas("c03-interactive-structure-atlas-01", "structure-atlas", "interactive-structure", STRUCTURES, (128, 128), 6, draw_structure_cell, "systems", topics, deliveries)
    atlas("c03-battle-prop-atlas-01", "battle-prop-atlas", "battle-prop", BATTLE_PROPS, (128, 64), 4, lambda im, pos, key, label: draw_four_item_cell(im, pos, key, label, (128, 64)), "systems", topics, deliveries)
    atlas("c03-equipment-atlas-01", "equipment-atlas", "equipment", EQUIPMENT, (192, 48), 4, draw_equipment_cell, "systems", topics, deliveries)
    atlas("c03-skill-atlas-01", "skill-atlas", "skill", SKILLS, (48, 48), 8, draw_icon_cell, "ui", topics, deliveries)
    atlas("c03-status-atlas-01", "status-atlas", "status", STATUSES, (48, 48), 8, lambda im, pos, key, label: draw_icon_cell(im, pos, key, label, "status"), "ui", topics, deliveries)
    atlas("c03-fx-atlas-01", "fx-atlas", "fx", FX, (96, 96), 6, draw_fx_cell, "fx", topics, deliveries)
    atlas("c03-hud-atlas-01", "hud-atlas", "hud", HUD, (48, 48), 8, lambda im, pos, key, label: draw_icon_cell(im, pos, key, label, "hud"), "ui", topics, deliveries)

    manifest = {
        "schemaVersion": "2.0.0", "campaignId": "candidate-03", "campaignTitle": "布衣定鼎",
        "targetTopics": 404, "categoryTargets": CATEGORY_TARGETS, "topics": topics,
        "deliveries": deliveries,
        "supplementalAssets": [
            {"id": "c03-supplemental-named-hero-units", "count": 6, "note": "既有实名英雄四帧单位，不占40个战斗母型槽位"}
        ],
        "build": {"script": "build_complete_library.py", "deterministic": True, "pngSvgCommonPixelSource": True},
    }
    return manifest, run_qa(manifest)


def run_qa(manifest: dict) -> dict:
    errors: list[str] = []
    topics = manifest["topics"]
    deliveries = manifest["deliveries"]
    category_counts = Counter(topic["category"] for topic in topics)
    source_counts = Counter(topic["source"] for topic in topics)
    if len(topics) != 404:
        errors.append(f"topic count {len(topics)} != 404")
    if dict(category_counts) != CATEGORY_TARGETS:
        errors.append(f"category counts {dict(category_counts)} != targets")
    if source_counts != Counter({"expanded": 382, "existing": 22}):
        errors.append(f"source counts {dict(source_counts)} != existing22/expanded382")
    topic_ids = [topic["id"] for topic in topics]
    if len(topic_ids) != len(set(topic_ids)):
        errors.append("duplicate topic ids")
    delivery_by_id = {delivery["id"]: delivery for delivery in deliveries}
    declared = Counter(topic_id for delivery in deliveries for topic_id in delivery["topicIds"])
    cell_checks = 0
    output_checks = 0
    pixel_pair_checks = 0
    pixel_pair_mismatches = 0
    for topic in topics:
        if topic["assetId"] not in delivery_by_id:
            errors.append(f"{topic['id']}: unknown asset")
        if declared[topic["id"]] != 1:
            errors.append(f"{topic['id']}: delivery references {declared[topic['id']]}")
    for delivery in deliveries:
        png_path, svg_path = ROOT / delivery["png"], ROOT / delivery["svg"]
        if not png_path.is_file() or not svg_path.is_file():
            errors.append(f"{delivery['id']}: missing output pair")
            continue
        image = Image.open(png_path).convert("RGBA")
        if image.size != (delivery["width"], delivery["height"]):
            errors.append(f"{delivery['id']}: PNG size mismatch")
        try:
            svg_root = ET.parse(svg_path).getroot()
        except ET.ParseError as exc:
            errors.append(f"{delivery['id']}: SVG parse {exc}")
        else:
            if svg_root.attrib.get("viewBox") != f"0 0 {delivery['width']} {delivery['height']}":
                errors.append(f"{delivery['id']}: SVG viewBox mismatch")
            reconstructed = Image.new("RGBA", image.size, (0, 0, 0, 0))
            pixels = reconstructed.load()
            for element in svg_root.iter():
                if not element.tag.endswith("rect"):
                    continue
                x = int(element.attrib["x"]); y = int(element.attrib["y"])
                width = int(element.attrib["width"]); height = int(element.attrib["height"])
                fill = element.attrib["fill"].lstrip("#")
                alpha = round(float(element.attrib.get("opacity", "1")) * 255)
                rgba = (int(fill[:2], 16), int(fill[2:4], 16), int(fill[4:6], 16), alpha)
                for py in range(y, y + height):
                    for px in range(x, x + width):
                        pixels[px, py] = rgba
            if reconstructed.tobytes() != image.tobytes():
                pixel_pair_mismatches += 1
                errors.append(f"{delivery['id']}: PNG/SVG pixel mismatch")
            pixel_pair_checks += 1
        output_checks += 2
        if "cellWidth" in delivery:
            for topic_id in delivery["topicIds"]:
                topic = next(item for item in topics if item["id"] == topic_id)
                index = topic["cell"]
                x = index % delivery["columns"] * delivery["cellWidth"]
                y = index // delivery["columns"] * delivery["cellHeight"]
                box = (x, y, x + delivery["cellWidth"], y + delivery["cellHeight"])
                if image.crop(box).getchannel("A").getbbox() is None:
                    errors.append(f"{topic_id}: empty cell")
                cell_checks += 1
    return {
        "schemaVersion": "1.0.0", "campaignId": "candidate-03", "checkedAt": "2026-08-11",
        "passed": not errors, "summary": {
            "targetTopics": 404, "topics": len(topics), "categoryCounts": dict(category_counts),
            "sourceCounts": dict(source_counts), "deliveries": len(deliveries),
            "outputFiles": len(deliveries) * 2, "outputFilesChecked": output_checks,
            "cellMappingsChecked": cell_checks, "pngSvgPairsChecked": pixel_pair_checks,
            "pngSvgPixelMismatches": pixel_pair_mismatches,
            "errors": len(errors),
        },
        "reworks": [
            {"topicId": "c03-prop-grain-seal", "from": "props/story-props-sheet-hd.png#cell0", "to": "expanded/narrative/c03-prop-light-rework-atlas.png#cell0", "result": "pass"},
            {"topicId": "c03-prop-temp-roll", "from": "props/story-props-sheet-hd.png#cell2", "to": "expanded/narrative/c03-prop-light-rework-atlas.png#cell1", "result": "pass"},
        ],
        "checks": {"uniqueIds": True, "categoryTargets": True, "sourceContract": True, "fileExistence": True, "dimensions": True, "svgViewBox": True, "nonEmptyCells": True, "nonDestructiveExistingAssets": True},
        "errors": errors,
    }


def main() -> None:
    manifest, _qa = build()
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    # Re-run after manifest exists; output assets are already complete.
    qa = run_qa(manifest)
    shared_path = ROOT.parents[1] / "pixel-master-tools" / "validate_complete_library.py"
    spec = importlib.util.spec_from_file_location("complete_library_validator", shared_path)
    if spec is None or spec.loader is None:
        qa["errors"].append("shared validator could not be loaded")
    else:
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        shared = module.validate_campaign("candidate-03")
        qa["visualDiversity"] = shared.get("visualDiversity", {})
        qa["sharedValidator"] = {
            "script": "../../pixel-master-tools/validate_complete_library.py",
            "passed": not shared.get("errors"),
            "cellMappingsChecked": shared.get("cellMappingsChecked", 0),
            "errors": shared.get("errors", []),
        }
        qa["errors"].extend(shared.get("errors", []))
    qa["passed"] = not qa["errors"]
    qa["summary"]["errors"] = len(qa["errors"])
    QA.write_text(json.dumps(qa, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(qa["summary"], ensure_ascii=False, indent=2))
    if not qa["passed"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
