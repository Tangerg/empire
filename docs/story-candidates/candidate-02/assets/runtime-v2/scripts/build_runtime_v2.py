#!/usr/bin/env python3
"""Build and QA the C02 runtime-v2 candidate pack from preserved ImageGen masters."""

from __future__ import annotations

import hashlib
import json
import math
import random
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable

from PIL import Image, ImageChops, ImageColor, ImageDraw, ImageEnhance, ImageOps


ROOT = Path(__file__).resolve().parents[1]
ASSETS_ROOT = ROOT.parent
MANIFEST_PATH = ASSETS_ROOT / "manifest-runtime-v2.json"
QA_PATH = ASSETS_ROOT / "qa-runtime-v2.json"
RESAMPLE = Image.Resampling.LANCZOS
NEAREST = Image.Resampling.NEAREST


@dataclass(frozen=True)
class Asset:
    id: str
    content_id: str
    type: str
    png: str
    svg: str
    width: int
    height: int
    extra: dict[str, Any]

    def manifest(self) -> dict[str, Any]:
        value = {
            "id": self.id,
            "contentId": self.content_id,
            "type": self.type,
            "png": self.png,
            "svg": self.svg,
            "width": self.width,
            "height": self.height,
        }
        value.update(self.extra)
        return value


def ensure_dirs() -> None:
    for name in ("units", "terrain", "facilities", "icons", "fx", "scenes", "previews"):
        (ROOT / name).mkdir(parents=True, exist_ok=True)


def rel(path: Path) -> str:
    return path.relative_to(ASSETS_ROOT).as_posix()


def alpha_bbox(im: Image.Image, threshold: int = 12) -> tuple[int, int, int, int] | None:
    alpha = im.convert("RGBA").getchannel("A").point(lambda p: 255 if p > threshold else 0)
    return alpha.getbbox()


def panel_crops(im: Image.Image, cols: int, rows: int, inset: float = 0.018) -> list[Image.Image]:
    result: list[Image.Image] = []
    for row in range(rows):
        for col in range(cols):
            x0 = round(im.width * col / cols)
            x1 = round(im.width * (col + 1) / cols)
            y0 = round(im.height * row / rows)
            y1 = round(im.height * (row + 1) / rows)
            dx = max(1, round((x1 - x0) * inset))
            dy = max(1, round((y1 - y0) * inset))
            result.append(im.crop((x0 + dx, y0 + dy, x1 - dx, y1 - dy)))
    return result


def quantize_rgba(im: Image.Image, colors: int = 48, binary_alpha: bool = True) -> Image.Image:
    rgba = im.convert("RGBA")
    alpha = rgba.getchannel("A")
    rgb = rgba.convert("RGB").quantize(colors=max(4, colors), method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB")
    if binary_alpha:
        alpha = alpha.point(lambda p: 255 if p >= 48 else 0)
    return Image.merge("RGBA", (*rgb.split(), alpha))


def fit_alpha_panels(
    panels: list[Image.Image], frame_size: tuple[int, int], padding: tuple[int, int], colors: int,
    *, binary_alpha: bool = True, align_bottom: bool = True,
) -> list[Image.Image]:
    boxes = [alpha_bbox(p) for p in panels]
    if any(b is None for b in boxes):
        raise ValueError("empty chroma-key panel")
    cropped = [p.crop(b) for p, b in zip(panels, boxes) if b is not None]
    fw, fh = frame_size
    px, py = padding
    scale = min(
        (fw - px * 2) / max(p.width for p in cropped),
        (fh - py * 2) / max(p.height for p in cropped),
    )
    out: list[Image.Image] = []
    for source in cropped:
        size = (max(1, round(source.width * scale)), max(1, round(source.height * scale)))
        sprite = source.resize(size, RESAMPLE)
        frame = Image.new("RGBA", frame_size, (0, 0, 0, 0))
        x = (fw - sprite.width) // 2
        y = fh - sprite.height if align_bottom else (fh - sprite.height) // 2
        frame.alpha_composite(sprite, (x, y))
        frame = quantize_rgba(frame, colors, binary_alpha=binary_alpha)
        if align_bottom:
            box = alpha_bbox(frame, 0)
            if box and box[3] < fh:
                moved = Image.new("RGBA", frame_size, (0, 0, 0, 0))
                moved.alpha_composite(frame, (0, fh - box[3]))
                frame = moved
        out.append(frame)
    return out


def horizontal_sheet(frames: list[Image.Image]) -> Image.Image:
    width = sum(frame.width for frame in frames)
    height = max(frame.height for frame in frames)
    sheet = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    x = 0
    for frame in frames:
        sheet.alpha_composite(frame, (x, 0))
        x += frame.width
    return sheet


def vertical_sheet(frames: list[Image.Image]) -> Image.Image:
    width = max(frame.width for frame in frames)
    height = sum(frame.height for frame in frames)
    sheet = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    y = 0
    for frame in frames:
        sheet.alpha_composite(frame, (0, y))
        y += frame.height
    return sheet


def save_svg_pixel_runs(im: Image.Image, path: Path) -> None:
    rgba = im.convert("RGBA")
    parts = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {rgba.width} {rgba.height}" width="{rgba.width}" height="{rgba.height}" shape-rendering="crispEdges">',
    ]
    px = rgba.load()
    for y in range(rgba.height):
        x = 0
        while x < rgba.width:
            color = px[x, y]
            if color[3] == 0:
                x += 1
                continue
            x2 = x + 1
            while x2 < rgba.width and px[x2, y] == color:
                x2 += 1
            opacity = "" if color[3] == 255 else f' fill-opacity="{color[3] / 255:.3f}"'
            parts.append(f'<rect x="{x}" y="{y}" width="{x2 - x}" height="1" fill="#{color[0]:02x}{color[1]:02x}{color[2]:02x}"{opacity}/>')
            x = x2
    parts.append("</svg>")
    path.write_text("\n".join(parts) + "\n", encoding="utf-8")


def save_asset_image(im: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    im.save(path, optimize=True)
    save_svg_pixel_runs(im, path.with_suffix(".svg"))


def master_alpha(name: str) -> Image.Image:
    return Image.open(ROOT / "masters" / f"{name}-alpha.png").convert("RGBA")


def master_rgb(name: str) -> Image.Image:
    return Image.open(ROOT / "masters" / f"{name}-master.png").convert("RGB")


def unit_assets() -> list[Asset]:
    specs = [
        ("star-shield", "stars.unit.star-shield-trooper"),
        ("rail-rifleman", "stars.unit.rail-rifleman"),
        ("repair-engineer", "stars.unit.repair-engineer"),
        ("guard-robot", "stars.unit.public-order-guard-robot"),
    ]
    assets: list[Asset] = []
    for name, content_id in specs:
        source = master_alpha(f"c02-v2-unit-{name}")
        frames = fit_alpha_panels(panel_crops(source, 4, 1, 0.01), (32, 48), (1, 1), 48)
        sheet = horizontal_sheet(frames)
        path = ROOT / "units" / f"c02-v2-unit-{name}.png"
        save_asset_image(sheet, path)
        assets.append(Asset(
            f"c02-v2-unit-{name}", content_id, "combat-unit-sheet", rel(path), rel(path.with_suffix('.svg')), 128, 48,
            {
                "frameWidth": 32, "frameHeight": 48, "frames": 4,
                "frameOrder": ["standA", "stepA", "standB", "stepB"],
                "facing": "right", "anchor": [16, 47], "fps": 6, "loop": True,
                "sourceMaster": rel(ROOT / "masters" / f"c02-v2-unit-{name}-master.png"),
            },
        ))
    return assets


def square_texture(cell: Image.Image, variant: int = 0) -> Image.Image:
    side = min(cell.width, cell.height)
    x0 = (cell.width - side) // 2
    y0 = (cell.height - side) // 2
    crop = cell.crop((x0, y0, x0 + side, y0 + side)).convert("RGB")
    base = crop.resize((40, 40), RESAMPLE).crop((4, 4, 36, 36))
    shifts = ((0, 0), (11, 7), (5, 17), (19, 13))
    dx, dy = shifts[variant % len(shifts)]
    base = ImageChops.offset(base, dx, dy)
    if variant == 2:
        base = ImageOps.mirror(base)
    elif variant == 3:
        base = ImageOps.flip(base)
    base = base.quantize(colors=32, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGBA")
    # Runtime contract requires exact repeatable seams.
    px = base.load()
    for y in range(32):
        px[31, y] = px[0, y]
    for x in range(32):
        px[x, 31] = px[x, 0]
    return base


def draw_arm(draw: ImageDraw.ImageDraw, direction: str, colors: tuple[str, str, str], width: int, *, rails: bool = False) -> None:
    cx, cy = 16, 16
    if direction == "N":
        end = (cx, -1)
    elif direction == "E":
        end = (33, cy)
    elif direction == "S":
        end = (cx, 33)
    else:
        end = (-1, cy)
    if rails:
        if direction in ("N", "S"):
            for offset in (-3, 3):
                draw.line((cx + offset, cy, end[0] + offset, end[1]), fill=colors[0], width=3)
                draw.line((cx + offset, cy, end[0] + offset, end[1]), fill=colors[1], width=1)
        else:
            for offset in (-3, 3):
                draw.line((cx, cy + offset, end[0], end[1] + offset), fill=colors[0], width=3)
                draw.line((cx, cy + offset, end[0], end[1] + offset), fill=colors[1], width=1)
        return
    draw.line((cx, cy, *end), fill=colors[0], width=width + 4)
    draw.line((cx, cy, *end), fill=colors[1], width=width + 2)
    draw.line((cx, cy, *end), fill=colors[2], width=max(1, width // 3))


def connection_tile(background: Image.Image, mask: int, family: str) -> Image.Image:
    tile = background.copy().convert("RGBA")
    draw = ImageDraw.Draw(tile)
    palette = {
        "star-vein": ("#162b32", "#1595a4", "#b8f6ed"),
        "coolant": ("#0b252a", "#086878", "#38d2d0"),
        "rail": ("#1a1715", "#855a3c", "#d6ba84"),
        "conduit": ("#171b1d", "#5b6263", "#c59d6e"),
    }[family]
    width = {"star-vein": 3, "coolant": 7, "rail": 3, "conduit": 7}[family]
    rails = family == "rail"
    for bit, direction in ((1, "N"), (2, "E"), (4, "S"), (8, "W")):
        if mask & bit:
            draw_arm(draw, direction, palette, width, rails=rails)
    if rails:
        draw.rounded_rectangle((10, 10, 22, 22), radius=3, fill=palette[0], outline=palette[2], width=2)
        draw.rectangle((14, 14, 18, 18), fill=palette[1])
    elif family == "star-vein":
        draw.polygon(((16, 9), (23, 16), (16, 23), (9, 16)), fill=palette[0], outline=palette[2])
        draw.polygon(((16, 12), (20, 16), (16, 20), (12, 16)), fill=palette[1])
    elif family == "coolant":
        draw.ellipse((9, 9, 23, 23), fill=palette[0], outline=palette[2], width=2)
        draw.ellipse((13, 13, 19, 19), fill=palette[1])
    else:
        draw.ellipse((8, 8, 24, 24), fill=palette[0], outline=palette[2], width=2)
        draw.ellipse((12, 12, 20, 20), fill=palette[1])
        draw.rectangle((15, 10, 17, 22), fill=palette[2])
    # Canonicalize the exact outer pixel profile. This makes E/W and N/S
    # neighbours byte-identical at the seam, independent of Pillow line caps.
    base_px = background.convert("RGBA").load()
    tile_px = tile.load()
    for i in range(32):
        tile_px[i, 0] = base_px[i, 0]
        tile_px[i, 31] = base_px[i, 31]
        tile_px[0, i] = base_px[0, i]
        tile_px[31, i] = base_px[31, i]

    def stamp_port(direction: str) -> None:
        if rails:
            profile = {12: palette[0], 13: palette[1], 14: palette[0], 18: palette[0], 19: palette[1], 20: palette[0]}
        else:
            outer_half = (width + 3) // 2
            profile = {value: palette[0] for value in range(16 - outer_half, 17 + outer_half)}
            inner_half = (width + 1) // 2
            profile.update({value: palette[1] for value in range(16 - inner_half, 17 + inner_half)})
            profile.update({15: palette[2], 16: palette[2], 17: palette[2]})
        for offset, color in profile.items():
            rgba = ImageColor.getrgb(color) + (255,)
            if direction == "N": tile_px[offset, 0] = rgba
            elif direction == "E": tile_px[31, offset] = rgba
            elif direction == "S": tile_px[offset, 31] = rgba
            else: tile_px[0, offset] = rgba

    for bit, direction in ((1, "N"), (2, "E"), (4, "S"), (8, "W")):
        if mask & bit:
            stamp_port(direction)
    return quantize_rgba(tile, 36, binary_alpha=True)


def terrain_assets() -> list[Asset]:
    source = master_rgb("c02-v2-terrain-board")
    cells = panel_crops(source, 4, 2, 0.012)
    base_specs = [
        ("hesha-red-sand", "stars.terrain.hesha-red-sand"),
        ("soler-blue-ice", "stars.terrain.soler-blue-ice"),
        ("verdant-root-ground", "stars.terrain.verdant-root-ground"),
        ("farlight-cargo-deck", "stars.terrain.farlight-cargo-deck"),
    ]
    connection_specs = [
        ("ancient-star-vein", "stars.terrain.ancient-star-vein", "star-vein"),
        ("industrial-coolant", "stars.terrain.industrial-coolant", "coolant"),
        ("kairon-rail", "stars.terrain.kairon-rail", "rail"),
        ("pressure-conduit", "stars.terrain.pressure-conduit", "conduit"),
    ]
    assets: list[Asset] = []
    for idx, (name, content_id) in enumerate(base_specs):
        frames = [square_texture(cells[idx], variant) for variant in range(4)]
        sheet = horizontal_sheet(frames)
        path = ROOT / "terrain" / f"c02-v2-terrain-{name}.png"
        save_asset_image(sheet, path)
        assets.append(Asset(
            f"c02-v2-terrain-{name}", content_id, "terrain-atlas", rel(path), rel(path.with_suffix('.svg')), 128, 32,
            {
                "frameWidth": 32, "frameHeight": 32, "frames": 4,
                "tileMode": "seamless-repeat", "variantCount": 4, "variantOrder": [0, 1, 2, 3],
                "sourceMaster": rel(ROOT / "masters" / "c02-v2-terrain-board-master.png"),
            },
        ))
    for idx, (name, content_id, family) in enumerate(connection_specs, start=4):
        background = square_texture(cells[idx], 0)
        frames = [connection_tile(background, mask, family) for mask in range(16)]
        sheet = horizontal_sheet(frames)
        path = ROOT / "terrain" / f"c02-v2-terrain-{name}.png"
        save_asset_image(sheet, path)
        assets.append(Asset(
            f"c02-v2-terrain-{name}", content_id, "terrain-atlas", rel(path), rel(path.with_suffix('.svg')), 512, 32,
            {
                "frameWidth": 32, "frameHeight": 32, "frames": 16,
                "tileMode": "nesw-16", "maskCount": 16, "maskBits": {"N": 1, "E": 2, "S": 4, "W": 8},
                "maskOrder": list(range(16)), "edgePortWidth": width_for_family(family),
                "sourceMaster": rel(ROOT / "masters" / "c02-v2-terrain-board-master.png"),
            },
        ))
    return assets


def width_for_family(family: str) -> int:
    return {"star-vein": 3, "coolant": 7, "rail": 6, "conduit": 7}[family]


def facility_assets() -> list[Asset]:
    specs = [
        ("rain-control", "stars.structure.rain-control-node", (64, 96), [2, 2], {"x": 8, "y": 64, "width": 48, "height": 32}, {"x": 16, "y": 40, "width": 32, "height": 48}),
        ("cargo-terminal", "stars.structure.freight-permission-terminal", (64, 64), [2, 1], {"x": 4, "y": 44, "width": 56, "height": 20}, {"x": 16, "y": 16, "width": 32, "height": 36}),
    ]
    assets: list[Asset] = []
    for name, content_id, frame_size, footprint, collision, hotspot in specs:
        source = master_alpha(f"c02-v2-facility-{name}")
        frames = fit_alpha_panels(panel_crops(source, 3, 1, 0.01), frame_size, (2, 1), 64)
        sheet = vertical_sheet(frames)
        path = ROOT / "facilities" / f"c02-v2-facility-{name}.png"
        save_asset_image(sheet, path)
        assets.append(Asset(
            f"c02-v2-facility-{name}", content_id, "interactive-structure", rel(path), rel(path.with_suffix('.svg')), frame_size[0], frame_size[1] * 3,
            {
                "frameWidth": frame_size[0], "frameHeight": frame_size[1], "frames": 3,
                "states": ["normal", "damaged", "captured"],
                "stateRows": {"normal": 0, "damaged": 1, "captured": 2},
                "anchor": [frame_size[0] // 2, frame_size[1] - 1], "footprint": footprint,
                "collision": collision, "interactionHotspot": hotspot,
                "sourceMaster": rel(ROOT / "masters" / f"c02-v2-facility-{name}-master.png"),
            },
        ))
    return assets


def icon_assets() -> list[Asset]:
    source = master_alpha("c02-v2-icon-board")
    cells = panel_crops(source, 4, 2, 0.018)
    specs = [
        ("equip-star-shield", "stars.equipment.star-shield-frame", "equipment-icon"),
        ("equip-rail-magazine", "stars.equipment.rail-calibration-magazine", "equipment-icon"),
        ("equip-repair-pack", "stars.equipment.multi-standard-repair-pack", "equipment-icon"),
        ("equip-permission-key", "stars.equipment.low-permission-key", "equipment-icon"),
        ("skill-deploy-shield", "stars.skill.deploy-star-shield", "skill-icon"),
        ("skill-rail-snipe", "stars.skill.rail-snipe", "skill-icon"),
        ("skill-field-repair", "stars.skill.field-repair", "skill-icon"),
        ("skill-capture-authority", "stars.skill.capture-authority", "skill-icon"),
    ]
    assets: list[Asset] = []
    for cell, (name, content_id, kind) in zip(cells, specs):
        frame = fit_alpha_panels([cell], (32, 32), (2, 2), 40, align_bottom=False)[0]
        path = ROOT / "icons" / f"c02-v2-{name}.png"
        save_asset_image(frame, path)
        assets.append(Asset(
            f"c02-v2-{name}", content_id, kind, rel(path), rel(path.with_suffix('.svg')), 32, 32,
            {"frameWidth": 32, "frameHeight": 32, "frames": 1, "anchor": [16, 16], "sourceMaster": rel(ROOT / "masters" / "c02-v2-icon-board-master.png")},
        ))
    return assets


def fx_assets() -> list[Asset]:
    source = master_alpha("c02-v2-fx-board")
    cells = panel_crops(source, 4, 4, 0.025)
    specs = [
        ("rail-impact", "stars.fx.rail-impact", 12, "add", False, True),
        ("shield-deploy", "stars.fx.shield-deploy", 10, "add", False, False),
        ("repair-sparks", "stars.fx.repair-sparks", 12, "add", False, True),
        ("sand-rain-gust", "stars.fx.sand-rain-gust", 8, "alpha", True, False),
    ]
    assets: list[Asset] = []
    for row, (name, content_id, fps, blend, loop, binary_alpha) in enumerate(specs):
        frames = []
        for cell in cells[row * 4:(row + 1) * 4]:
            frame = fit_alpha_panels([cell], (32, 32), (1, 1), 48, binary_alpha=binary_alpha, align_bottom=False)[0]
            frames.append(frame)
        sheet = horizontal_sheet(frames)
        path = ROOT / "fx" / f"c02-v2-fx-{name}.png"
        save_asset_image(sheet, path)
        assets.append(Asset(
            f"c02-v2-fx-{name}", content_id, "fx-sheet", rel(path), rel(path.with_suffix('.svg')), 128, 32,
            {
                "frameWidth": 32, "frameHeight": 32, "frames": 4, "frameOrder": [0, 1, 2, 3],
                "anchor": [16, 16], "fps": fps, "blendMode": blend, "loop": loop,
                "sourceMaster": rel(ROOT / "masters" / "c02-v2-fx-board-master.png"),
            },
        ))
    return assets


def scene_assets() -> list[Asset]:
    source = master_rgb("c02-v2-scene-rain-tower-repair")
    target_ratio = 16 / 9
    if source.width / source.height > target_ratio:
        width = round(source.height * target_ratio)
        left = (source.width - width) // 2
        source = source.crop((left, 0, left + width, source.height))
    else:
        height = round(source.width / target_ratio)
        top = max(0, (source.height - height) // 2)
        source = source.crop((0, top, source.width, top + height))
    scene = source.resize((256, 144), RESAMPLE).quantize(colors=96, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGBA")
    path = ROOT / "scenes" / "c02-v2-scene-rain-tower-repair.png"
    save_asset_image(scene, path)
    return [Asset(
        "c02-v2-scene-rain-tower-repair", "stars.scene.rain-tower-collective-repair", "story-scene",
        rel(path), rel(path.with_suffix('.svg')), 256, 144,
        {"frameWidth": 256, "frameHeight": 144, "frames": 1, "safeAreaBottom": 32, "opaque": True, "sourceMaster": rel(ROOT / "masters" / "c02-v2-scene-rain-tower-repair-master.png")},
    )]


def frame_hash(frame: Image.Image) -> str:
    return hashlib.sha256(frame.convert("RGBA").tobytes()).hexdigest()[:16]


def split_frames(im: Image.Image, fw: int, fh: int, frames: int, vertical: bool = False) -> list[Image.Image]:
    if vertical:
        return [im.crop((0, i * fh, fw, (i + 1) * fh)) for i in range(frames)]
    return [im.crop((i * fw, 0, (i + 1) * fw, fh)) for i in range(frames)]


def checker(size: tuple[int, int], cell: int = 4) -> Image.Image:
    out = Image.new("RGB", size, "#182029")
    draw = ImageDraw.Draw(out)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2:
                draw.rectangle((x, y, x + cell - 1, y + cell - 1), fill="#27313a")
    return out.convert("RGBA")


def terrain_connection_mosaic(asset: Asset, rng: random.Random, width: int, height: int) -> Image.Image:
    sheet = Image.open(ASSETS_ROOT / asset.png).convert("RGBA")
    frames = split_frames(sheet, 32, 32, 16)
    # Make one globally connected randomized path grid. Mask derives from shared horizontal/vertical edges.
    east = [[rng.random() < 0.48 for _ in range(width - 1)] for _ in range(height)]
    south = [[rng.random() < 0.48 for _ in range(width)] for _ in range(height - 1)]
    out = Image.new("RGBA", (width * 32, height * 32), (0, 0, 0, 255))
    for y in range(height):
        for x in range(width):
            mask = 0
            if y > 0 and south[y - 1][x]: mask |= 1
            if x < width - 1 and east[y][x]: mask |= 2
            if y < height - 1 and south[y][x]: mask |= 4
            if x > 0 and east[y][x - 1]: mask |= 8
            out.alpha_composite(frames[mask], (x * 32, y * 32))
    return out


def make_previews(assets: list[Asset]) -> None:
    by_type: dict[str, list[Asset]] = {}
    for asset in assets:
        by_type.setdefault(asset.type, []).append(asset)
    terrain_repeat = [a for a in assets if a.type == "terrain-atlas" and a.extra["tileMode"] == "seamless-repeat"]
    terrain_connection = [a for a in assets if a.type == "terrain-atlas" and a.extra["tileMode"] == "nesw-16"]
    canvas = checker((768, 464), 8)
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((0, 0, 767, 23), fill="#0b1118")
    # Units: all four frames at native 1x.
    x = 12
    for asset in by_type["combat-unit-sheet"]:
        im = Image.open(ASSETS_ROOT / asset.png).convert("RGBA")
        canvas.alpha_composite(im, (x, 30))
        x += 140
    # Terrain rows.
    x, y = 12, 92
    for asset in terrain_repeat:
        im = Image.open(ASSETS_ROOT / asset.png).convert("RGBA")
        canvas.alpha_composite(im, (x, y))
        x += 140
    x, y = 12, 132
    for asset in terrain_connection:
        mosaic = terrain_connection_mosaic(asset, random.Random(100 + x), 3, 3)
        canvas.alpha_composite(mosaic, (x, y))
        x += 112
    # Facilities rendered state-by-state at native resolution.
    x = 482
    for facility_index, asset in enumerate(by_type["interactive-structure"]):
        im = Image.open(ASSETS_ROOT / asset.png).convert("RGBA")
        frames = split_frames(im, asset.extra["frameWidth"], asset.extra["frameHeight"], 3, vertical=True)
        x = 482
        facility_y = 132 if facility_index == 0 else 234
        for frame in frames:
            canvas.alpha_composite(frame, (x, facility_y + max(0, 96 - frame.height)))
            x += frame.width + 4
    # Icons and first FX frame.
    x, y = 12, 244
    for asset in by_type.get("equipment-icon", []) + by_type.get("skill-icon", []):
        canvas.alpha_composite(Image.open(ASSETS_ROOT / asset.png).convert("RGBA"), (x, y))
        x += 38
    x, y = 12, 284
    for asset in by_type["fx-sheet"]:
        canvas.alpha_composite(Image.open(ASSETS_ROOT / asset.png).convert("RGBA"), (x, y))
        x += 140
    scene = Image.open(ASSETS_ROOT / by_type["story-scene"][0].png).convert("RGBA")
    canvas.alpha_composite(scene, (500, 314))
    preview1 = ROOT / "previews" / "c02-v2-runtime-preview-1x.png"
    preview2 = ROOT / "previews" / "c02-v2-runtime-preview-2x.png"
    canvas.convert("RGB").save(preview1, optimize=True)
    canvas.resize((1536, 928), NEAREST).convert("RGB").save(preview2, optimize=True)

    seam = Image.new("RGB", (768, 512), "#080d12")
    for index, asset in enumerate(terrain_connection):
        mosaic = terrain_connection_mosaic(asset, random.Random(20260811 + index), 12, 8).convert("RGB")
        seam.paste(mosaic, ((index % 2) * 384, (index // 2) * 256))
    seam.save(ROOT / "previews" / "c02-v2-terrain-random-12x8-sample.png", optimize=True)

    grid = Image.new("RGB", (384, 192), "#080d12")
    for index, asset in enumerate(terrain_repeat + terrain_connection):
        if asset.extra["tileMode"] == "seamless-repeat":
            sheet = Image.open(ASSETS_ROOT / asset.png).convert("RGBA")
            frames = split_frames(sheet, 32, 32, 4)
            mosaic = Image.new("RGBA", (96, 96), (0, 0, 0, 255))
            for gy in range(3):
                for gx in range(3):
                    mosaic.alpha_composite(frames[(gx + gy * 3) % 4], (gx * 32, gy * 32))
        else:
            mosaic = terrain_connection_mosaic(asset, random.Random(620 + index), 3, 3)
        grid.paste(mosaic.convert("RGB"), ((index % 4) * 96, (index // 4) * 96))
    grid.save(ROOT / "previews" / "c02-v2-terrain-3x3-grid.png", optimize=True)


def file_checks(assets: list[Asset]) -> tuple[list[dict[str, Any]], list[str]]:
    checks: list[dict[str, Any]] = []
    errors: list[str] = []

    def record(name: str, passed: bool, detail: Any) -> None:
        checks.append({"name": name, "passed": passed, "detail": detail})
        if not passed:
            errors.append(f"{name}: {detail}")

    all_files = all((ASSETS_ROOT / a.png).exists() and (ASSETS_ROOT / a.svg).exists() for a in assets)
    record("all-runtime-files-exist", all_files, {"assets": len(assets), "files": len(assets) * 2})
    bad_dims = []
    for asset in assets:
        with Image.open(ASSETS_ROOT / asset.png) as im:
            if im.size != (asset.width, asset.height):
                bad_dims.append({"id": asset.id, "expected": [asset.width, asset.height], "actual": list(im.size)})
    record("exact-png-dimensions", not bad_dims, bad_dims or "all exact")
    content_ids = [asset.content_id for asset in assets]
    record(
        "stable-content-ids",
        len(set(content_ids)) == len(content_ids) and all(value.startswith("stars.") for value in content_ids),
        {"declared": len(content_ids), "unique": len(set(content_ids)), "prefix": "stars.*"},
    )

    alpha_bad = []
    for asset in assets:
        if asset.type not in {"combat-unit-sheet", "interactive-structure", "equipment-icon", "skill-icon"}:
            continue
        im = Image.open(ASSETS_ROOT / asset.png).convert("RGBA")
        alphas = set(im.getchannel("A").get_flattened_data())
        corners = [im.getpixel((0, 0))[3], im.getpixel((im.width - 1, 0))[3], im.getpixel((0, im.height - 1))[3], im.getpixel((im.width - 1, im.height - 1))[3]]
        if not alphas.issubset({0, 255}) or any(corners):
            alpha_bad.append({"id": asset.id, "alphas": len(alphas), "corners": corners})
    record("binary-alpha-and-transparent-corners", not alpha_bad, alpha_bad or "all binary/corners clear")

    unit_bad = []
    for asset in [a for a in assets if a.type == "combat-unit-sheet"]:
        frames = split_frames(Image.open(ASSETS_ROOT / asset.png).convert("RGBA"), 32, 48, 4)
        boxes = [alpha_bbox(f, 0) for f in frames]
        hashes = [frame_hash(f) for f in frames]
        if any(b is None or b[0] == 0 or b[2] == 32 or b[1] == 0 or b[3] != 48 for b in boxes if b) or len(set(hashes)) != 4:
            unit_bad.append({"id": asset.id, "boxes": boxes, "uniqueFrames": len(set(hashes))})
    record("unit-frame-anchor-and-no-clipping", not unit_bad, unit_bad or "4x4 frames nonempty, distinct, feet y=47")

    facility_bad = []
    facility_metrics = []
    for asset in [a for a in assets if a.type == "interactive-structure"]:
        fw, fh = asset.extra["frameWidth"], asset.extra["frameHeight"]
        frames = split_frames(Image.open(ASSETS_ROOT / asset.png).convert("RGBA"), fw, fh, 3, vertical=True)
        hashes = [frame_hash(f) for f in frames]
        diff_normal_damaged = sum(1 for px in ImageChops.difference(frames[0], frames[1]).get_flattened_data() if px != (0, 0, 0, 0))
        diff_ratio = diff_normal_damaged / (fw * fh)
        core_box = (12, min(20, fh // 3), fw - 12, fh - 8)
        core_diff = ImageChops.difference(frames[0].crop(core_box).convert("RGB"), frames[2].crop(core_box).convert("RGB"))
        capture_core_ratio = sum(1 for px in core_diff.get_flattened_data() if px != (0, 0, 0)) / (core_diff.width * core_diff.height)
        metric = {"id": asset.id, "uniqueStates": len(set(hashes)), "physicalDamageDiffRatio": round(diff_ratio, 3), "capturedCoreDiffRatio": round(capture_core_ratio, 3)}
        facility_metrics.append(metric)
        if len(set(hashes)) != 3 or diff_ratio < 0.18 or capture_core_ratio < 0.15:
            facility_bad.append(metric)
    record("facility-three-physical-states", not facility_bad, facility_bad or facility_metrics)

    repeat_bad = []
    for asset in [a for a in assets if a.type == "terrain-atlas" and a.extra["tileMode"] == "seamless-repeat"]:
        frames = split_frames(Image.open(ASSETS_ROOT / asset.png).convert("RGBA"), 32, 32, 4)
        for idx, frame in enumerate(frames):
            if list(frame.crop((0, 0, 1, 32)).get_flattened_data()) != list(frame.crop((31, 0, 32, 32)).get_flattened_data()) or list(frame.crop((0, 0, 32, 1)).get_flattened_data()) != list(frame.crop((0, 31, 32, 32)).get_flattened_data()):
                repeat_bad.append(f"{asset.id}:{idx}")
    record("terrain-repeat-edges", not repeat_bad, repeat_bad or "all 16 base variants exact-seam")

    port_bad = []
    for asset in [a for a in assets if a.type == "terrain-atlas" and a.extra["tileMode"] == "nesw-16"]:
        frames = split_frames(Image.open(ASSETS_ROOT / asset.png).convert("RGBA"), 32, 32, 16)
        for mask, frame in enumerate(frames):
            # Compare connection edge strips with the same asset's disconnected mask0 edge.
            for bit, edge in ((1, "N"), (2, "E"), (4, "S"), (8, "W")):
                if edge == "N": region = (13, 0, 20, 2)
                elif edge == "E": region = (30, 13, 32, 20)
                elif edge == "S": region = (13, 30, 20, 32)
                else: region = (0, 13, 2, 20)
                changed = ImageChops.difference(frame.crop(region).convert("RGB"), frames[0].crop(region).convert("RGB")).getbbox() is not None
                if changed != bool(mask & bit):
                    port_bad.append(f"{asset.id}:mask{mask}:{edge}")
    record("terrain-nesw-port-masks", not port_bad, port_bad or "all 64 mask-edge declarations match pixels")
    seam_bad = []
    for asset in [a for a in assets if a.type == "terrain-atlas" and a.extra["tileMode"] == "nesw-16"]:
        frames = split_frames(Image.open(ASSETS_ROOT / asset.png).convert("RGBA"), 32, 32, 16)
        east = list(frames[2].crop((31, 0, 32, 32)).get_flattened_data())
        west = list(frames[8].crop((0, 0, 1, 32)).get_flattened_data())
        north = list(frames[1].crop((0, 0, 32, 1)).get_flattened_data())
        south = list(frames[4].crop((0, 31, 32, 32)).get_flattened_data())
        if east != west or north != south:
            seam_bad.append(asset.id)
    record("terrain-connected-edge-seams", not seam_bad, seam_bad or "N/S and E/W port pixels match exactly")

    icon_assets_only = [a for a in assets if a.type in {"equipment-icon", "skill-icon"}]
    icon_hashes = [frame_hash(Image.open(ASSETS_ROOT / a.png).convert("RGBA")) for a in icon_assets_only]
    silhouettes = []
    contrast_bad = []
    for asset in icon_assets_only:
        im = Image.open(ASSETS_ROOT / asset.png).convert("RGBA")
        silhouettes.append(hashlib.sha256(im.getchannel("A").tobytes()).hexdigest())
        visible = [v for v, a in zip(im.convert("L").get_flattened_data(), im.getchannel("A").get_flattened_data()) if a]
        if not visible or max(visible) - min(visible) < 36:
            contrast_bad.append(asset.id)
    record("icons-distinct-at-32px", len(set(icon_hashes)) == 8 and len(set(silhouettes)) == 8 and not contrast_bad, {"uniquePixels": len(set(icon_hashes)), "uniqueSilhouettes": len(set(silhouettes)), "lowContrast": contrast_bad})

    fx_bad = []
    for asset in [a for a in assets if a.type == "fx-sheet"]:
        frames = split_frames(Image.open(ASSETS_ROOT / asset.png).convert("RGBA"), 32, 32, 4)
        hashes = [frame_hash(f) for f in frames]
        if len(set(hashes)) != 4 or any(alpha_bbox(f) is None for f in frames):
            fx_bad.append({"id": asset.id, "uniqueFrames": len(set(hashes))})
    record("fx-frame-diversity", not fx_bad, fx_bad or "4 groups x 4 nonempty distinct frames")

    scene = next(a for a in assets if a.type == "story-scene")
    scene_im = Image.open(ASSETS_ROOT / scene.png).convert("RGBA")
    record("scene-opaque-and-safe-area", scene_im.getchannel("A").getextrema() == (255, 255), {"size": list(scene_im.size), "bottomSafeArea": 32, "alpha": list(scene_im.getchannel('A').getextrema())})
    preview_files = [ROOT / "previews" / "c02-v2-runtime-preview-1x.png", ROOT / "previews" / "c02-v2-runtime-preview-2x.png", ROOT / "previews" / "c02-v2-terrain-random-12x8-sample.png", ROOT / "previews" / "c02-v2-terrain-3x3-grid.png"]
    record("visual-preview-files", all(p.exists() for p in preview_files), [rel(p) for p in preview_files])
    return checks, errors


def write_manifest(assets: list[Asset]) -> None:
    manifest = {
        "schemaVersion": "1.0.0",
        "campaignId": "candidate-02",
        "campaignTitle": "群星熄灭之前",
        "qualityTier": "runtime-v2-candidate",
        "runtimeReady": False,
        "generatedAt": "2026-08-11",
        "scope": {"combatUnits": 4, "terrainTypes": 8, "interactiveFacilities": 2, "icons": 8, "fxGroups": 4, "narrativeScenes": 1, "totalAssets": 27},
        "notes": [
            "First playable-quality runtime slice; this is not the complete 404-topic library.",
            "ImageGen masters remain source-only. Root gameplay validation is required before runtimeReady can become true.",
            "Existing expansion assets remain untouched and are treated as prototype coverage.",
        ],
        "assets": [a.manifest() for a in assets],
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    ensure_dirs()
    assets = unit_assets() + terrain_assets() + facility_assets() + icon_assets() + fx_assets() + scene_assets()
    make_previews(assets)
    write_manifest(assets)
    checks, errors = file_checks(assets)
    qa = {
        "schemaVersion": "1.0.0",
        "campaignId": "candidate-02",
        "qualityTier": "runtime-v2-candidate",
        "runtimeReady": False,
        "passed": not errors,
        "assetCount": len(assets),
        "runtimeFileCount": len(assets) * 2,
        "masterCount": 10,
        "sharedValidator": {
            "command": "python3 docs/story-candidates/pixel-master-tools/validate_runtime_v2.py",
            "expectedCampaignStatus": "passed-machine-qa",
            "coverage": {"unit-sheet": 4, "terrain": 8, "structure": 2, "icon": 8, "fx-sheet": 4, "scene": 1},
        },
        "checks": checks,
        "errors": errors,
        "visualReview": {
            "status": "candidate-preview-generated",
            "oneX": rel(ROOT / "previews" / "c02-v2-runtime-preview-1x.png"),
            "twoX": rel(ROOT / "previews" / "c02-v2-runtime-preview-2x.png"),
            "terrainSeams": rel(ROOT / "previews" / "c02-v2-terrain-random-12x8-sample.png"),
            "terrainThreeByThree": rel(ROOT / "previews" / "c02-v2-terrain-3x3-grid.png"),
            "requiredNext": "Root agent must validate gameplay readability, pivots, collisions, and tile selection in-engine before marking runtimeReady.",
        },
    }
    QA_PATH.write_text(json.dumps(qa, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if errors:
        raise SystemExit("QA failed:\n" + "\n".join(errors))
    print(f"Built {len(assets)} runtime assets ({len(assets) * 2} files); QA passed")


if __name__ == "__main__":
    main()
