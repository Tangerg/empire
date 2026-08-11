#!/usr/bin/env python3
"""Build C02 runtime-v2 batch-02 from preserved ImageGen source boards."""

from __future__ import annotations

import hashlib
import importlib.util
import json
import random
import sys
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from PIL import Image, ImageChops, ImageColor, ImageDraw


BATCH = Path(__file__).resolve().parents[1]
RUNTIME = BATCH.parent
ASSET_ROOT = RUNTIME.parent
MANIFEST_PATH = ASSET_ROOT / "manifest-runtime-v2-b02.json"
QA_PATH = ASSET_ROOT / "qa-runtime-v2-b02.json"
LOCK_PATH = BATCH / "TOPIC-LOCK.json"

base_spec = importlib.util.spec_from_file_location("c02_runtime_v2_base", RUNTIME / "scripts" / "build_runtime_v2.py")
if base_spec is None or base_spec.loader is None:
    raise RuntimeError("cannot load runtime-v2 processing primitives")
base = importlib.util.module_from_spec(base_spec)
sys.modules[base_spec.name] = base
base_spec.loader.exec_module(base)


@dataclass(frozen=True)
class Asset:
    id: str
    topic_id: str
    content_id: str
    type: str
    png: str
    svg: str
    width: int
    height: int
    extra: dict[str, Any]

    def manifest(self) -> dict[str, Any]:
        value = {
            "id": self.id, "topicId": self.topic_id, "contentId": self.content_id,
            "type": self.type, "png": self.png, "svg": self.svg,
            "width": self.width, "height": self.height,
        }
        value.update(self.extra)
        return value


LOCK = json.loads(LOCK_PATH.read_text(encoding="utf-8"))
TOPICS = {row["topicId"]: row for row in LOCK["topics"]}


def row(topic_id: str) -> dict[str, str]:
    if topic_id not in TOPICS:
        raise KeyError(f"topic not locked: {topic_id}")
    return TOPICS[topic_id]


def rel(path: Path) -> str:
    return path.relative_to(ASSET_ROOT).as_posix()


def save(path: Path, image: Image.Image) -> tuple[str, str]:
    base.save_asset_image(image, path)
    return rel(path), rel(path.with_suffix(".svg"))


def alpha_master(name: str) -> Image.Image:
    return Image.open(BATCH / "masters" / f"c02-v2-b02-{name}-alpha.png").convert("RGBA")


def rgb_master(name: str) -> Image.Image:
    return Image.open(BATCH / "masters" / f"c02-v2-b02-{name}-master.png").convert("RGB")


def make_asset(topic_id: str, kind: str, path: Path, image: Image.Image, extra: dict[str, Any]) -> Asset:
    locked = row(topic_id)
    png, svg = save(path, image)
    return Asset(
        f"c02-v2-b02-{topic_id.removeprefix('c02-')}", topic_id, locked["contentId"], kind,
        png, svg, image.width, image.height, extra,
    )


def unit_assets() -> list[Asset]:
    boards = {
        "combat-a": base.panel_crops(alpha_master("combat-board-a"), 4, 4, 0.008),
        "combat-b": base.panel_crops(alpha_master("combat-board-b"), 4, 4, 0.008),
        "mission-a": base.panel_crops(alpha_master("mission-board-a"), 4, 4, 0.008),
        "mission-b": base.panel_crops(alpha_master("mission-board-b"), 4, 4, 0.008),
    }
    specs = [
        ("c02-unit-power-swordsman", "combat-unit-sheet", "combat-a", 0, 32),
        ("c02-unit-symbiotic-guard", "combat-unit-sheet", "combat-a", 1, 32),
        ("c02-unit-sand-rider", "combat-unit-sheet", "combat-a", 2, 64),
        ("c02-unit-hover-lancer", "combat-unit-sheet", "combat-a", 3, 64),
        ("c02-unit-tide-fin-scout", "combat-unit-sheet", "combat-b", 0, 32),
        ("c02-unit-long-beam-sniper", "combat-unit-sheet", "combat-b", 1, 32),
        ("c02-unit-spore-shooter", "combat-unit-sheet", "combat-b", 2, 32),
        ("c02-unit-star-vein-mage", "combat-unit-sheet", "combat-b", 3, 32),
        ("c02-mission-water-maintainer", "mission-unit-sheet", "mission-a", 0, 32),
        ("c02-mission-water-collector", "mission-unit-sheet", "mission-a", 1, 32),
        ("c02-mission-red-sand-child", "mission-unit-sheet", "mission-a", 2, 32),
        ("c02-mission-cargo-crew", "mission-unit-sheet", "mission-a", 3, 32),
        ("c02-mission-refugee", "mission-unit-sheet", "mission-b", 0, 32),
        ("c02-mission-patient", "mission-unit-sheet", "mission-b", 1, 64),
        ("c02-mission-dock-builder", "mission-unit-sheet", "mission-b", 2, 32),
        ("c02-mission-heat-lamp-monk", "mission-unit-sheet", "mission-b", 3, 32),
    ]
    assets: list[Asset] = []
    for topic_id, kind, board_name, row_index, frame_width in specs:
        source_frames = boards[board_name][row_index * 4:(row_index + 1) * 4]
        frames = base.fit_alpha_panels(source_frames, (frame_width, 48), (1, 1), 48)
        sheet = base.horizontal_sheet(frames)
        path = BATCH / "units" / f"c02-v2-b02-{topic_id.removeprefix('c02-')}.png"
        extra: dict[str, Any] = {
            "frameWidth": frame_width, "frameHeight": 48, "frames": 4,
            "frameOrder": ["standA", "stepA", "standB", "stepB"],
            "anchor": [frame_width // 2, 47], "facing": "right", "fps": 6, "loop": True,
            "sourceMaster": rel(BATCH / "masters" / f"c02-v2-b02-{board_name.replace('combat-a','combat-board-a').replace('combat-b','combat-board-b').replace('mission-a','mission-board-a').replace('mission-b','mission-board-b')}-master.png"),
        }
        if frame_width > 32:
            extra.update({"footprint": [2, 1], "zOrder": 1})
        assets.append(make_asset(topic_id, kind, path, sheet, extra))
    return assets


CONNECTION_STYLE = {
    "waterway": (("#15272d", "#11768a", "#8fe8e5"), 7, False),
    "heated": (("#21150f", "#9b481d", "#ffb24b"), 5, False),
    "current": (("#08252d", "#087c8d", "#76e5da"), 7, False),
    "rail": (("#191512", "#7e5237", "#e0c58c"), 3, True),
}


def connection_tile(background: Image.Image, mask: int, family: str) -> Image.Image:
    palette, width, rails = CONNECTION_STYLE[family]
    tile = background.convert("RGBA").copy()
    draw = ImageDraw.Draw(tile)
    cx = cy = 16
    endpoints = {"N": (16, -1), "E": (33, 16), "S": (16, 33), "W": (-1, 16)}
    for bit, direction in ((1, "N"), (2, "E"), (4, "S"), (8, "W")):
        if not mask & bit:
            continue
        ex, ey = endpoints[direction]
        if rails:
            if direction in ("N", "S"):
                for offset in (-3, 3):
                    draw.line((cx + offset, cy, ex + offset, ey), fill=palette[0], width=3)
                    draw.line((cx + offset, cy, ex + offset, ey), fill=palette[2], width=1)
            else:
                for offset in (-3, 3):
                    draw.line((cx, cy + offset, ex, ey + offset), fill=palette[0], width=3)
                    draw.line((cx, cy + offset, ex, ey + offset), fill=palette[2], width=1)
        else:
            draw.line((cx, cy, ex, ey), fill=palette[0], width=width + 4)
            draw.line((cx, cy, ex, ey), fill=palette[1], width=width + 2)
            draw.line((cx, cy, ex, ey), fill=palette[2], width=max(1, width // 3))
    if family == "waterway":
        draw.rounded_rectangle((8, 8, 24, 24), radius=4, fill=palette[0], outline=palette[2], width=2)
        draw.rectangle((13, 13, 19, 19), fill=palette[1])
    elif family == "heated":
        draw.rectangle((10, 10, 22, 22), fill=palette[0], outline=palette[2], width=2)
        draw.ellipse((13, 13, 19, 19), fill=palette[2])
    elif family == "current":
        draw.ellipse((8, 8, 24, 24), fill=palette[0], outline=palette[2], width=2)
        draw.arc((11, 11, 21, 21), 30, 280, fill=palette[2], width=2)
        draw.polygon(((20, 12), (23, 15), (19, 16)), fill=palette[2])
    else:
        draw.rounded_rectangle((9, 9, 23, 23), radius=3, fill=palette[0], outline=palette[2], width=2)
        draw.rectangle((13, 13, 19, 19), fill=palette[1])

    base_pixels = background.convert("RGBA").load()
    pixels = tile.load()
    for i in range(32):
        pixels[i, 0] = base_pixels[i, 0]
        pixels[i, 31] = base_pixels[i, 31]
        pixels[0, i] = base_pixels[0, i]
        pixels[31, i] = base_pixels[31, i]

    def stamp(direction: str) -> None:
        if rails:
            profile = {12: palette[0], 13: palette[2], 14: palette[0], 18: palette[0], 19: palette[2], 20: palette[0]}
        else:
            half = (width + 3) // 2
            profile = {i: palette[0] for i in range(16 - half, 17 + half)}
            profile.update({i: palette[1] for i in range(16 - width // 2, 17 + width // 2)})
            profile.update({15: palette[2], 16: palette[2], 17: palette[2]})
        for offset, value in profile.items():
            color = ImageColor.getrgb(value) + (255,)
            if direction == "N": pixels[offset, 0] = color
            elif direction == "E": pixels[31, offset] = color
            elif direction == "S": pixels[offset, 31] = color
            else: pixels[0, offset] = color

    for bit, direction in ((1, "N"), (2, "E"), (4, "S"), (8, "W")):
        if mask & bit: stamp(direction)
    return base.quantize_rgba(tile, 36, binary_alpha=True)


def terrain_assets() -> list[Asset]:
    cells = base.panel_crops(rgb_master("terrain-board"), 4, 2, 0.012)
    repeat_specs = [
        ("c02-terrain-hesha-glass-sea", 0),
        ("c02-terrain-soler-brittle-ice", 2),
        ("c02-terrain-verdant-fungus-bed", 5),
        ("c02-terrain-farlight-living-deck", 7),
    ]
    connection_specs = [
        ("c02-terrain-hesha-sealed-waterway", 1, "waterway"),
        ("c02-terrain-soler-heated-walkway", 3, "heated"),
        ("c02-terrain-nereia-reverse-current", 4, "current"),
        ("c02-terrain-kairon-train-platform", 6, "rail"),
    ]
    assets: list[Asset] = []
    for topic_id, index in repeat_specs:
        frames = [base.square_texture(cells[index], variant) for variant in range(4)]
        sheet = base.horizontal_sheet(frames)
        path = BATCH / "terrain" / f"c02-v2-b02-{topic_id.removeprefix('c02-')}.png"
        assets.append(make_asset(topic_id, "terrain-atlas", path, sheet, {
            "frameWidth": 32, "frameHeight": 32, "frames": 4,
            "tileMode": "seamless-repeat", "variantCount": 4, "variantOrder": list(range(4)),
            "sourceMaster": rel(BATCH / "masters" / "c02-v2-b02-terrain-board-master.png"),
        }))
    for topic_id, index, family in connection_specs:
        background = base.square_texture(cells[index], 0)
        sheet = base.horizontal_sheet([connection_tile(background, mask, family) for mask in range(16)])
        path = BATCH / "terrain" / f"c02-v2-b02-{topic_id.removeprefix('c02-')}.png"
        assets.append(make_asset(topic_id, "terrain-atlas", path, sheet, {
            "frameWidth": 32, "frameHeight": 32, "frames": 16,
            "tileMode": "nesw-16", "maskCount": 16, "maskOrder": list(range(16)),
            "maskBits": {"N": 1, "E": 2, "S": 4, "W": 8},
            "sourceMaster": rel(BATCH / "masters" / "c02-v2-b02-terrain-board-master.png"),
        }))
    return assets


def structure_assets() -> list[Asset]:
    cells = base.panel_crops(alpha_master("structure-board"), 3, 4, 0.008)
    topics = [
        "c02-structure-star-vein-node", "c02-structure-tide-anchor-console",
        "c02-structure-gravity-switch", "c02-structure-water-filter-station",
    ]
    assets: list[Asset] = []
    for index, topic_id in enumerate(topics):
        frames = base.fit_alpha_panels(cells[index * 3:(index + 1) * 3], (64, 80), (2, 1), 64)
        sheet = base.vertical_sheet(frames)
        path = BATCH / "structures" / f"c02-v2-b02-{topic_id.removeprefix('c02-')}.png"
        assets.append(make_asset(topic_id, "interactive-facility", path, sheet, {
            "frameWidth": 64, "frameHeight": 80, "frames": 3,
            "states": ["normal", "damaged", "captured"],
            "stateRows": {"normal": 0, "damaged": 1, "captured": 2},
            "anchor": [32, 79], "footprint": [2, 2],
            "collision": {"x": 6, "y": 56, "width": 52, "height": 24},
            "interactionHotspot": {"x": 14, "y": 24, "width": 36, "height": 44},
            "sourceMaster": rel(BATCH / "masters" / "c02-v2-b02-structure-board-master.png"),
        }))
    return assets


def board_item_assets() -> list[Asset]:
    assets: list[Asset] = []
    prop_topics = [
        "c02-battle-prop-ceramic-water-tanks", "c02-battle-prop-cargo-crates",
        "c02-battle-prop-platform-shields", "c02-battle-prop-giant-roots",
        "c02-battle-prop-overload-nodes", "c02-battle-prop-fuel-oxygen-tanks",
        "c02-battle-prop-ice-cracks", "c02-battle-prop-spore-sacs",
    ]
    equipment_topics = [
        "c02-equipment-power-swordsman", "c02-equipment-symbiotic-guard",
        "c02-equipment-sand-rider", "c02-equipment-hover-lancer",
        "c02-equipment-tide-fin-scout", "c02-equipment-long-beam-sniper",
        "c02-equipment-spore-shooter", "c02-equipment-star-vein-mage",
    ]
    skill_topics = [
        "c02-skill-assault", "c02-skill-rescue-dash", "c02-skill-snipe", "c02-skill-spore-shot",
        "c02-skill-vein-downgrade", "c02-skill-sand-sail", "c02-skill-shift-tide", "c02-skill-symbiotic-regen",
    ]
    status_topics = ["c02-status-controlled", "c02-status-hidden", "c02-status-overheated", "c02-status-hypothermia"]
    hud_topics = ["c02-hud-ally", "c02-hud-enemy", "c02-hud-main-objective", "c02-hud-danger"]
    groups = [
        ("prop-board", prop_topics, "map-object", "props", (48, 48), 48),
        ("equipment-board", equipment_topics, "equipment-icon", "equipment", (32, 32), 40),
        ("skill-board", skill_topics, "skill-icon", "skills", (32, 32), 40),
    ]
    for board_name, topics, kind, folder, size, colors in groups:
        cells = base.panel_crops(alpha_master(board_name), 4, 2, 0.016)
        for cell, topic_id in zip(cells, topics):
            image = base.fit_alpha_panels([cell], size, (2, 2), colors, align_bottom=kind == "map-object")[0]
            path = BATCH / folder / f"c02-v2-b02-{topic_id.removeprefix('c02-')}.png"
            extra: dict[str, Any] = {"frameWidth": size[0], "frameHeight": size[1], "frames": 1, "anchor": [size[0] // 2, size[1] - 1 if kind == "map-object" else size[1] // 2], "sourceMaster": rel(BATCH / "masters" / f"c02-v2-b02-{board_name}-master.png")}
            if kind == "map-object": extra.update({"footprint": [1, 1], "collision": {"x": 4, "y": 28, "width": 40, "height": 20}})
            assets.append(make_asset(topic_id, kind, path, image, extra))

    status_hud_cells = base.panel_crops(alpha_master("status-hud-board"), 4, 2, 0.016)
    for index, topic_id in enumerate(status_topics + hud_topics):
        kind = "status-icon" if index < 4 else "hud-icon"
        folder = "status" if index < 4 else "hud"
        image = base.fit_alpha_panels([status_hud_cells[index]], (32, 32), (2, 2), 36, align_bottom=False)[0]
        path = BATCH / folder / f"c02-v2-b02-{topic_id.removeprefix('c02-')}.png"
        assets.append(make_asset(topic_id, kind, path, image, {"frameWidth": 32, "frameHeight": 32, "frames": 1, "anchor": [16, 16], "sourceMaster": rel(BATCH / "masters" / "c02-v2-b02-status-hud-board-master.png")}))
    return assets


def fx_assets() -> list[Asset]:
    cells = base.panel_crops(alpha_master("fx-board"), 4, 4, 0.02)
    specs = [
        ("c02-fx-blade-hit", 12, "add", False, True),
        ("c02-fx-explosion", 10, "add", False, False),
        ("c02-fx-bio-hit", 9, "normal", False, True),
        ("c02-fx-vacuum-rupture", 10, "screen", False, False),
    ]
    assets: list[Asset] = []
    for index, (topic_id, fps, blend, loop, binary) in enumerate(specs):
        frames = [base.fit_alpha_panels([cell], (32, 32), (1, 1), 48, binary_alpha=binary, align_bottom=False)[0] for cell in cells[index * 4:(index + 1) * 4]]
        sheet = base.horizontal_sheet(frames)
        path = BATCH / "fx" / f"c02-v2-b02-{topic_id.removeprefix('c02-')}.png"
        assets.append(make_asset(topic_id, "fx-sheet", path, sheet, {
            "frameWidth": 32, "frameHeight": 32, "frames": 4,
            "frameOrder": [0, 1, 2, 3], "anchor": [16, 16],
            "fps": fps, "blendMode": blend, "loop": loop,
            "sourceMaster": rel(BATCH / "masters" / "c02-v2-b02-fx-board-master.png"),
        }))
    return assets


def scene_assets() -> list[Asset]:
    topics = [
        "c02-scene-growing-wall", "c02-scene-inverted-city-maintenance",
        "c02-scene-soler-ice-beacon", "c02-scene-nereia-platform-vote",
    ]
    assets: list[Asset] = []
    for topic_id in topics:
        name = topic_id.removeprefix("c02-scene-")
        source = rgb_master(f"scene-{name}")
        ratio = 16 / 9
        if source.width / source.height > ratio:
            width = round(source.height * ratio)
            left = (source.width - width) // 2
            source = source.crop((left, 0, left + width, source.height))
        else:
            height = round(source.width / ratio)
            top = max(0, (source.height - height) // 2)
            source = source.crop((0, top, source.width, top + height))
        image = source.resize((256, 144), Image.Resampling.LANCZOS).quantize(colors=96, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGBA")
        path = BATCH / "scenes" / f"c02-v2-b02-scene-{name}.png"
        assets.append(make_asset(topic_id, "story-scene", path, image, {"frameWidth": 256, "frameHeight": 144, "frames": 1, "safeAreaBottom": 32, "opaque": True, "sourceMaster": rel(BATCH / "masters" / f"c02-v2-b02-scene-{name}-master.png")}))
    return assets


def split(asset: Asset, vertical: bool = False) -> list[Image.Image]:
    image = Image.open(ASSET_ROOT / asset.png).convert("RGBA")
    return base.split_frames(image, asset.extra["frameWidth"], asset.extra["frameHeight"], asset.extra["frames"], vertical)


def terrain_mosaic(asset: Asset, width: int, height: int, seed: int) -> Image.Image:
    frames = split(asset)
    output = Image.new("RGBA", (width * 32, height * 32), (0, 0, 0, 255))
    if asset.extra["tileMode"] == "seamless-repeat":
        for y in range(height):
            for x in range(width):
                output.alpha_composite(frames[(x * 3 + y * 5) % 4], (x * 32, y * 32))
        return output
    rng = random.Random(seed)
    east = [[rng.random() < 0.46 for _ in range(width - 1)] for _ in range(height)]
    south = [[rng.random() < 0.46 for _ in range(width)] for _ in range(height - 1)]
    for y in range(height):
        for x in range(width):
            mask = 0
            if y > 0 and south[y - 1][x]: mask |= 1
            if x < width - 1 and east[y][x]: mask |= 2
            if y < height - 1 and south[y][x]: mask |= 4
            if x > 0 and east[y][x - 1]: mask |= 8
            output.alpha_composite(frames[mask], (x * 32, y * 32))
    return output


def make_previews(assets: list[Asset]) -> None:
    by_category: dict[str, list[Asset]] = {}
    for asset in assets:
        category = row(asset.topic_id)["category"]
        by_category.setdefault(category, []).append(asset)
    canvas = base.checker((1408, 768), 8)
    x, y = 12, 12
    for category in ("combat-unit", "mission-unit"):
        for asset in by_category[category]:
            image = Image.open(ASSET_ROOT / asset.png).convert("RGBA")
            if x + image.width > 1396:
                x, y = 12, y + 56
            canvas.alpha_composite(image, (x, y))
            x += image.width + 10
        x, y = 12, y + 58
    for asset in by_category["terrain"]:
        mosaic = terrain_mosaic(asset, 3, 3, 500 + x)
        canvas.alpha_composite(mosaic, (x, y))
        x += 104
    x, y = 12, y + 104
    for asset in by_category["interactive-structure"]:
        frames = split(asset, vertical=True)
        for frame in frames:
            canvas.alpha_composite(frame, (x, y))
            x += 68
        x += 10
    x, y = 12, y + 86
    for category in ("battle-prop", "equipment", "skill", "status", "hud"):
        for asset in by_category[category]:
            image = Image.open(ASSET_ROOT / asset.png).convert("RGBA")
            canvas.alpha_composite(image, (x, y))
            x += image.width + 6
        x, y = 12, y + 54 if category == "battle-prop" else y + 38
    for asset in by_category["fx"]:
        canvas.alpha_composite(Image.open(ASSET_ROOT / asset.png).convert("RGBA"), (x, y))
        x += 138
    x, y = 12, y + 40
    for asset in by_category["narrative-static"]:
        canvas.alpha_composite(Image.open(ASSET_ROOT / asset.png).convert("RGBA"), (x, y))
        x += 266
    used_height = min(canvas.height, y + 156)
    one = canvas.crop((0, 0, canvas.width, used_height)).convert("RGB")
    one.save(BATCH / "previews" / "c02-v2-b02-preview-1x.png", optimize=True)
    one.resize((one.width * 2, one.height * 2), Image.Resampling.NEAREST).save(BATCH / "previews" / "c02-v2-b02-preview-2x.png", optimize=True)

    terrain = by_category["terrain"]
    grid = Image.new("RGB", (384, 192), "#080d12")
    for index, asset in enumerate(terrain):
        grid.paste(terrain_mosaic(asset, 3, 3, 800 + index).convert("RGB"), ((index % 4) * 96, (index // 4) * 96))
    grid.save(BATCH / "previews" / "c02-v2-b02-terrain-3x3.png", optimize=True)
    connection = [asset for asset in terrain if asset.extra["tileMode"] == "nesw-16"]
    large = Image.new("RGB", (768, 512), "#080d12")
    for index, asset in enumerate(connection):
        large.paste(terrain_mosaic(asset, 12, 8, 900 + index).convert("RGB"), ((index % 2) * 384, (index // 2) * 256))
    large.save(BATCH / "previews" / "c02-v2-b02-terrain-12x8.png", optimize=True)


def hash_image(image: Image.Image) -> str:
    return hashlib.sha256(image.convert("RGBA").tobytes()).hexdigest()


def qa_assets(assets: list[Asset]) -> tuple[list[dict[str, Any]], list[str]]:
    checks: list[dict[str, Any]] = []
    errors: list[str] = []

    def check(name: str, passed: bool, detail: Any) -> None:
        checks.append({"name": name, "passed": passed, "detail": detail})
        if not passed: errors.append(f"{name}: {detail}")

    check("topic-lock-exact", len(assets) == 68 and {a.topic_id for a in assets} == set(TOPICS), {"assets": len(assets), "locked": len(TOPICS)})
    check("unique-id-content-topic", len({a.id for a in assets}) == 68 and len({a.content_id for a in assets}) == 68 and len({a.topic_id for a in assets}) == 68, "68/68 unique")
    missing = [a.id for a in assets if not (ASSET_ROOT / a.png).is_file() or not (ASSET_ROOT / a.svg).is_file()]
    check("runtime-png-svg-exist", not missing, missing or "136 files present")
    dimensions = []
    for asset in assets:
        image = Image.open(ASSET_ROOT / asset.png)
        if image.size != (asset.width, asset.height): dimensions.append(asset.id)
    check("declared-dimensions", not dimensions, dimensions or "all exact")

    transparent_categories = {"combat-unit", "mission-unit", "interactive-structure", "battle-prop", "equipment", "skill", "status", "hud"}
    alpha_bad = []
    for asset in assets:
        if row(asset.topic_id)["category"] not in transparent_categories: continue
        image = Image.open(ASSET_ROOT / asset.png).convert("RGBA")
        alphas = set(image.getchannel("A").get_flattened_data())
        corners = [image.getpixel((0, 0))[3], image.getpixel((image.width - 1, 0))[3], image.getpixel((0, image.height - 1))[3], image.getpixel((image.width - 1, image.height - 1))[3]]
        if not alphas.issubset({0, 255}) or any(corners): alpha_bad.append(asset.id)
    check("binary-alpha-clear-corners", not alpha_bad, alpha_bad or "all transparent runtime cells clean")

    unit_bad = []
    for asset in [a for a in assets if row(a.topic_id)["category"] in {"combat-unit", "mission-unit"}]:
        frames = split(asset)
        boxes = [base.alpha_bbox(frame, 0) for frame in frames]
        hashes = [hash_image(frame) for frame in frames]
        if len(set(hashes)) != 4 or any(box is None or box[3] != asset.extra["frameHeight"] for box in boxes):
            unit_bad.append({"id": asset.id, "boxes": boxes, "unique": len(set(hashes))})
        if asset.extra["frameWidth"] > 32 and ("footprint" not in asset.extra or "zOrder" not in asset.extra): unit_bad.append({"id": asset.id, "largeMetadata": False})
    check("unit-frames-anchors-large-footprints", not unit_bad, unit_bad or "16 sheets; 64px riders/patient preserve silhouette")

    structure_bad = []
    for asset in [a for a in assets if row(a.topic_id)["category"] == "interactive-structure"]:
        frames = split(asset, vertical=True)
        hashes = [hash_image(frame) for frame in frames]
        core = (10, 18, 54, 72)
        damage = ImageChops.difference(frames[0].crop(core).convert("RGB"), frames[1].crop(core).convert("RGB"))
        captured = ImageChops.difference(frames[0].crop(core).convert("RGB"), frames[2].crop(core).convert("RGB"))
        d_ratio = sum(px != (0, 0, 0) for px in damage.get_flattened_data()) / (damage.width * damage.height)
        c_ratio = sum(px != (0, 0, 0) for px in captured.get_flattened_data()) / (captured.width * captured.height)
        if len(set(hashes)) != 3 or d_ratio < 0.15 or c_ratio < 0.15: structure_bad.append({"id": asset.id, "damage": round(d_ratio, 3), "capturedCore": round(c_ratio, 3)})
    check("structure-physical-three-states", not structure_bad, structure_bad or "4 facilities normal/damaged/captured core differ")

    terrain_bad = []
    edge_bad = []
    for asset in [a for a in assets if row(a.topic_id)["category"] == "terrain"]:
        frames = split(asset)
        if len({hash_image(frame) for frame in frames}) != len(frames): terrain_bad.append(asset.id)
        if asset.extra["tileMode"] == "seamless-repeat":
            for index, frame in enumerate(frames):
                if list(frame.crop((0, 0, 1, 32)).get_flattened_data()) != list(frame.crop((31, 0, 32, 32)).get_flattened_data()) or list(frame.crop((0, 0, 32, 1)).get_flattened_data()) != list(frame.crop((0, 31, 32, 32)).get_flattened_data()): edge_bad.append(f"{asset.id}:{index}")
        else:
            if list(frames[2].crop((31, 0, 32, 32)).get_flattened_data()) != list(frames[8].crop((0, 0, 1, 32)).get_flattened_data()) or list(frames[1].crop((0, 0, 32, 1)).get_flattened_data()) != list(frames[4].crop((0, 31, 32, 32)).get_flattened_data()): edge_bad.append(asset.id)
    check("terrain-variants-unique", not terrain_bad, terrain_bad or "4x4 repeat and 4x16 connection cells unique")
    check("terrain-adjacency", not edge_bad, edge_bad or "repeat and N/E/S/W seams byte-identical")

    category_hashes: dict[str, list[str]] = {}
    silhouettes: dict[str, list[str]] = {}
    for asset in assets:
        category = row(asset.topic_id)["category"]
        image = Image.open(ASSET_ROOT / asset.png).convert("RGBA").resize((32, 32), Image.Resampling.NEAREST)
        category_hashes.setdefault(category, []).append(hash_image(image))
        if category in {"battle-prop", "equipment", "skill", "status", "hud"}:
            silhouettes.setdefault(category, []).append(hashlib.sha256(image.getchannel("A").tobytes()).hexdigest())
    duplicate_detail = {category: len(values) - len(set(values)) for category, values in category_hashes.items() if len(values) != len(set(values))}
    check("no-exact-category-duplicates", not duplicate_detail, duplicate_detail or "all normalized asset hashes unique per category")
    silhouette_detail = {category: [len(set(values)), len(values)] for category, values in silhouettes.items()}
    check("small-asset-silhouettes", all(unique == total for unique, total in silhouette_detail.values()), silhouette_detail)

    fx_bad = []
    for asset in [a for a in assets if row(a.topic_id)["category"] == "fx"]:
        frames = split(asset)
        if len({hash_image(frame) for frame in frames}) != 4 or any(base.alpha_bbox(frame) is None for frame in frames): fx_bad.append(asset.id)
    check("fx-four-phase-diversity", not fx_bad, fx_bad or "4 FX groups x 4 distinct nonempty phases")

    scene_bad = []
    scene_hashes = []
    for asset in [a for a in assets if row(a.topic_id)["category"] == "narrative-static"]:
        image = Image.open(ASSET_ROOT / asset.png).convert("RGBA")
        scene_hashes.append(hash_image(image.resize((64, 36), Image.Resampling.NEAREST)))
        if image.size != (256, 144) or image.getchannel("A").getextrema() != (255, 255): scene_bad.append(asset.id)
    check("scene-size-opacity-composition", not scene_bad and len(set(scene_hashes)) == 4, scene_bad or "4 distinct opaque 256x144 scenes")
    previews = [BATCH / "previews" / name for name in ("c02-v2-b02-preview-1x.png", "c02-v2-b02-preview-2x.png", "c02-v2-b02-terrain-3x3.png", "c02-v2-b02-terrain-12x8.png")]
    check("visual-previews", all(path.is_file() for path in previews), [rel(path) for path in previews])
    return checks, errors


def main() -> None:
    assets = unit_assets() + terrain_assets() + structure_assets() + board_item_assets() + fx_assets() + scene_assets()
    make_previews(assets)
    counts = Counter(row(asset.topic_id)["category"] for asset in assets)
    manifest = {
        "schemaVersion": "1.0.0", "campaignId": "candidate-02", "campaignTitle": "群星熄灭之前",
        "batchId": "b02", "qualityTier": "runtime-v2-candidate", "runtimeReady": False,
        "generatedAt": "2026-08-12", "topicLock": rel(LOCK_PATH), "assetCount": len(assets),
        "coverage": dict(counts),
        "notes": [
            "Additive second production batch; batch-01 remains untouched.",
            "The 8 new combat units bring runtime-v2 combat coverage to 12/40; this batch does not claim 404 completion.",
            "ImageGen masters are source-only; in-engine screenshot validation is still required before runtimeReady can change.",
        ],
        "assets": [asset.manifest() for asset in assets],
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    checks, errors = qa_assets(assets)
    qa = {
        "schemaVersion": "1.0.0", "campaignId": "candidate-02", "batchId": "b02",
        "qualityTier": "runtime-v2-candidate", "runtimeReady": False,
        "passed": not errors, "assetCount": len(assets), "runtimeFileCount": len(assets) * 2,
        "masterCount": 15, "coverage": dict(counts), "checks": checks, "errors": errors,
        "sharedValidator": {
            "command": "python3 docs/story-candidates/pixel-master-tools/validate_runtime_v2_batches.py",
            "expectedCampaignStatus": "passed-machine-qa",
        },
        "visualReview": {
            "oneX": rel(BATCH / "previews" / "c02-v2-b02-preview-1x.png"),
            "twoX": rel(BATCH / "previews" / "c02-v2-b02-preview-2x.png"),
            "terrainThreeByThree": rel(BATCH / "previews" / "c02-v2-b02-terrain-3x3.png"),
            "terrainTwelveByEight": rel(BATCH / "previews" / "c02-v2-b02-terrain-12x8.png"),
            "requiredNext": "Root agent must validate game-scale readability, collisions, pivots, z-order and autotile selection before promotion.",
        },
    }
    QA_PATH.write_text(json.dumps(qa, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if errors: raise SystemExit("QA failed:\n" + "\n".join(errors))
    print(f"Built C02 batch-02: {len(assets)} assets / {len(assets) * 2} runtime files; QA passed")


if __name__ == "__main__":
    main()
