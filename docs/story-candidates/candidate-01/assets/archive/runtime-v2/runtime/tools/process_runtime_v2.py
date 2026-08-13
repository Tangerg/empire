#!/usr/bin/env python3
"""Build C01 runtime-v2 game assets from ImageGen source masters."""

from __future__ import annotations

import argparse
import hashlib
import json
import random
import subprocess
from pathlib import Path

from PIL import Image, ImageChops, ImageEnhance, ImageFilter, ImageOps, ImageStat


RUNTIME_ROOT = Path(__file__).resolve().parents[1]
ASSET_ROOT = RUNTIME_ROOT.parent
MASTER_ROOT = RUNTIME_ROOT / "masters"
ALPHA_ROOT = RUNTIME_ROOT / "intermediate" / "alpha"
MANIFEST_PATH = ASSET_ROOT / "manifest-runtime-v2.json"
QA_PATH = ASSET_ROOT / "qa-runtime-v2.json"
RESAMPLE = Image.Resampling.LANCZOS
NEAREST = Image.Resampling.NEAREST

UNIT_SOURCES = {
    "soldier": ("gray-banner-soldier", "gray-banner-soldier-master.png"),
    "archer": ("silverwood-archer", "silverwood-archer-master.png"),
    "knight": ("burgundy-knight", "burgundy-knight-master.png"),
    "cleric": ("forge-cleric", "forge-cleric-master.png"),
}
TERRAIN_CONTENT = (
    "plain",
    "road",
    "bridge",
    "forest",
    "hill",
    "mountain",
    "water",
    "wall",
)
ICON_SPECS = (
    ("equipment", "ash-spear", "ash-spear"),
    ("equipment", "burgundy-shield", "burgundy-shield"),
    ("equipment", "silverwood-longbow", "silverwood-longbow"),
    ("equipment", "forge-oath-hammer", "forge-oath-hammer"),
    ("skill", "healing-lantern", "healing-lantern"),
    ("skill", "gray-banner-rally", "gray-banner-rally"),
    ("skill", "bridge-field-repair", "bridge-field-repair"),
    ("skill", "broken-oath-dispel", "broken-oath-dispel"),
)
FX_SPECS = (
    ("blade-hit", 14, "additive", False),
    ("oath-flame", 10, "additive", True),
    ("lantern-heal", 12, "additive", False),
    ("masonry-impact", 12, "normal", False),
)
MAP_STRUCTURE_SOURCES = (
    ("village", "village-states-master.png"),
    ("barracks", "barracks-states-master.png"),
    ("castle", "castle-states-master.png"),
)
TERRAIN_DENOISE = {
    "plain": 0.62,
    "road": 0.52,
    "bridge": 0.30,
    "forest": 0.56,
    "hill": 0.50,
    "mountain": 0.44,
    "water": 0.34,
    "wall": 0.30,
}


def refresh_chroma() -> None:
    helper = Path.home() / ".codex/skills/.system/imagegen/scripts/remove_chroma_key.py"
    if not helper.is_file():
        raise FileNotFoundError(f"找不到 ImageGen 色键工具：{helper}")
    ALPHA_ROOT.mkdir(parents=True, exist_ok=True)
    sources = sorted((MASTER_ROOT / "units").glob("*.png"))
    sources += sorted((MASTER_ROOT / "structures").glob("*.png"))
    sources += sorted((MASTER_ROOT / "icons").glob("*.png"))
    sources += sorted((MASTER_ROOT / "fx").glob("*.png"))
    for source in sources:
        subprocess.run(
            [
                "python3",
                str(helper),
                "--input",
                str(source),
                "--out",
                str(ALPHA_ROOT / source.name),
                "--auto-key",
                "border",
                "--soft-matte",
                "--transparent-threshold",
                "12",
                "--opaque-threshold",
                "220",
                "--despill",
                "--force",
            ],
            check=True,
        )


def grid_cell(image: Image.Image, cols: int, rows: int, col: int, row: int, inset: int = 0) -> Image.Image:
    x0 = round(col * image.width / cols) + inset
    x1 = round((col + 1) * image.width / cols) - inset
    y0 = round(row * image.height / rows) + inset
    y1 = round((row + 1) * image.height / rows) - inset
    return image.crop((x0, y0, x1, y1))


def tight_crop(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    bbox = alpha.point(lambda value: 255 if value >= 18 else 0).getbbox()
    if bbox is None:
        raise ValueError("透明单元为空")
    return rgba.crop(bbox)


def quantize_rgba(image: Image.Image, colors: int, binary_alpha: bool = True) -> Image.Image:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    rgb = rgba.convert("RGB").quantize(colors=colors, method=Image.Quantize.MEDIANCUT).convert("RGB")
    if binary_alpha:
        alpha = alpha.point(lambda value: 255 if value >= 96 else 0)
    rgb.putalpha(alpha)
    return rgb


def render_group(
    crops: list[Image.Image],
    frame_size: tuple[int, int],
    padding: tuple[int, int],
    *,
    bottom_align: bool,
    colors: int,
) -> list[Image.Image]:
    frame_w, frame_h = frame_size
    pad_x, pad_y = padding
    tight = [tight_crop(crop) for crop in crops]
    scale = min(
        (frame_w - 2 * pad_x) / max(crop.width for crop in tight),
        (frame_h - 2 * pad_y) / max(crop.height for crop in tight),
    )
    rendered: list[Image.Image] = []
    for crop in tight:
        size = (max(1, round(crop.width * scale)), max(1, round(crop.height * scale)))
        resized = quantize_rgba(crop.resize(size, RESAMPLE), colors)
        canvas = Image.new("RGBA", frame_size, (0, 0, 0, 0))
        x = (frame_w - resized.width) // 2
        y = frame_h - pad_y - resized.height if bottom_align else (frame_h - resized.height) // 2
        canvas.alpha_composite(resized, (x, y))
        rendered.append(canvas)
    return rendered


def horizontal_sheet(frames: list[Image.Image]) -> Image.Image:
    sheet = Image.new("RGBA", (sum(frame.width for frame in frames), max(frame.height for frame in frames)), (0, 0, 0, 0))
    x = 0
    for frame in frames:
        sheet.alpha_composite(frame, (x, 0))
        x += frame.width
    return sheet


def vertical_sheet(frames: list[Image.Image]) -> Image.Image:
    sheet = Image.new("RGBA", (max(frame.width for frame in frames), sum(frame.height for frame in frames)), (0, 0, 0, 0))
    y = 0
    for frame in frames:
        sheet.alpha_composite(frame, (0, y))
        y += frame.height
    return sheet


def save_png_and_svg(image: Image.Image, relative_png: str) -> tuple[str, str]:
    png_path = ASSET_ROOT / relative_png
    png_path.parent.mkdir(parents=True, exist_ok=True)
    image.save(png_path, optimize=True)
    svg_path = png_path.with_suffix(".svg")
    svg_path.write_text(
        "\n".join(
            [
                '<svg xmlns="http://www.w3.org/2000/svg" '
                f'xmlns:xlink="http://www.w3.org/1999/xlink" width="{image.width}" height="{image.height}" '
                f'viewBox="0 0 {image.width} {image.height}" shape-rendering="crispEdges">',
                f'  <image href="{png_path.name}" xlink:href="{png_path.name}" width="{image.width}" height="{image.height}" style="image-rendering:pixelated"/>',
                "</svg>",
                "",
            ]
        ),
        encoding="utf-8",
    )
    return relative_png, svg_path.relative_to(ASSET_ROOT).as_posix()


def common_edges(tiles: list[Image.Image]) -> list[Image.Image]:
    base = tiles[0].convert("RGB")
    top = [base.getpixel((x, 0)) for x in range(32)]
    side = [base.getpixel((0, y)) for y in range(32)]
    corner = top[0]
    result: list[Image.Image] = []
    for tile in tiles:
        fixed = tile.convert("RGB")
        for x, color in enumerate(top):
            fixed.putpixel((x, 0), color)
            fixed.putpixel((x, 31), color)
        for y, color in enumerate(side):
            fixed.putpixel((0, y), color)
            fixed.putpixel((31, y), color)
        for point in ((0, 0), (31, 0), (0, 31), (31, 31)):
            fixed.putpixel(point, corner)
        result.append(fixed)
    return result


def runtime_terrain_tone(tile: Image.Image, kind: str) -> Image.Image:
    """Lift C01 midtones and suppress 1 px noise without softening pixel edges."""
    rgb = tile.convert("RGB")
    median = rgb.filter(ImageFilter.MedianFilter(3))
    rgb = Image.blend(rgb, median, TERRAIN_DENOISE[kind])
    gamma = 0.78
    lut = [min(255, round(255 * ((value / 255) ** gamma))) for value in range(256)]
    rgb = rgb.point(lut * 3)
    return rgb.quantize(colors=28, method=Image.Quantize.MEDIANCUT).convert("RGB")


def material_variants(source: Image.Image, kind: str) -> list[Image.Image]:
    source = source.convert("RGB")
    crop_size = round(min(source.size) * 0.56)
    centers = ((0.34, 0.34), (0.66, 0.34), (0.34, 0.66), (0.66, 0.66))
    tiles: list[Image.Image] = []
    for cx, cy in centers:
        x0 = max(0, min(source.width - crop_size, round(cx * source.width - crop_size / 2)))
        y0 = max(0, min(source.height - crop_size, round(cy * source.height - crop_size / 2)))
        seed = source.crop((x0, y0, x0 + crop_size, y0 + crop_size)).resize((16, 16), RESAMPLE)
        top = Image.new("RGB", (32, 16))
        top.paste(seed, (0, 0))
        top.paste(ImageOps.mirror(seed), (16, 0))
        tile = Image.new("RGB", (32, 32))
        tile.paste(top, (0, 0))
        tile.paste(ImageOps.flip(top), (0, 16))
        tile = tile.quantize(colors=36, method=Image.Quantize.MEDIANCUT).convert("RGB")
        tile = runtime_terrain_tone(tile, kind)
        tiles.append(tile)
    return common_edges(tiles)


def connection_mask(bits: int) -> Image.Image:
    mask = Image.new("L", (32, 32), 0)
    px = mask.load()
    # Irregular compact hub; small deterministic edge changes prevent sterile geometry.
    for y in range(9, 23):
        jitter = (y * 7 + bits * 3) % 3 - 1
        for x in range(9 - jitter, 23 + jitter):
            px[x, y] = 255
    if bits & 1:  # north
        for y in range(0, 16):
            half = 5 + ((y + bits) % 3 == 0)
            for x in range(16 - half, 16 + half + 1):
                px[x, y] = 255
    if bits & 2:  # east
        for x in range(16, 32):
            half = 5 + ((x + bits) % 3 == 0)
            for y in range(16 - half, 16 + half + 1):
                px[x, y] = 255
    if bits & 4:  # south
        for y in range(16, 32):
            half = 5 + ((y + bits) % 3 == 0)
            for x in range(16 - half, 16 + half + 1):
                px[x, y] = 255
    if bits & 8:  # west
        for x in range(0, 16):
            half = 5 + ((x + bits) % 3 == 0)
            for y in range(16 - half, 16 + half + 1):
                px[x, y] = 255
    return mask


def connected_tiles(background: Image.Image, material: Image.Image, kind: str) -> list[Image.Image]:
    background = background.convert("RGB")
    material = material.convert("RGB")
    if kind == "road":
        # Preserve the ImageGen rut/stone detail while separating the road value
        # from the similarly brown plain at the final 32 px game scale.
        material = ImageEnhance.Contrast(ImageEnhance.Brightness(material).enhance(1.10)).enhance(1.08)
    edge_material = ImageEnhance.Brightness(material).enhance(0.58 if kind == "water" else 0.68)
    tiles: list[Image.Image] = []
    for bits in range(16):
        inner = connection_mask(bits)
        outer = inner.filter(ImageFilter.MaxFilter(5))
        tile = Image.composite(edge_material, background, outer)
        tile = Image.composite(material, tile, inner)
        tiles.append(tile)

    bg_top = [background.getpixel((x, 0)) for x in range(32)]
    bg_side = [background.getpixel((0, y)) for y in range(32)]
    mat_top = [material.getpixel((x, 0)) for x in range(32)]
    mat_side = [material.getpixel((0, y)) for y in range(32)]
    edge_top = [edge_material.getpixel((x, 0)) for x in range(32)]
    edge_side = [edge_material.getpixel((0, y)) for y in range(32)]
    connected_h = list(bg_top)
    connected_v = list(bg_side)
    for i in range(8, 24):
        connected_h[i] = mat_top[i] if 10 <= i <= 22 else edge_top[i]
        connected_v[i] = mat_side[i] if 10 <= i <= 22 else edge_side[i]

    for bits, tile in enumerate(tiles):
        top = connected_h if bits & 1 else bg_top
        right = connected_v if bits & 2 else bg_side
        bottom = connected_h if bits & 4 else bg_top
        left = connected_v if bits & 8 else bg_side
        for x, color in enumerate(top):
            tile.putpixel((x, 0), color)
        for x, color in enumerate(bottom):
            tile.putpixel((x, 31), color)
        for y, color in enumerate(left):
            tile.putpixel((0, y), color)
        for y, color in enumerate(right):
            tile.putpixel((31, y), color)
    return tiles


def build_units(assets: list[dict]) -> dict[str, Image.Image]:
    outputs: dict[str, Image.Image] = {}
    for content_id, (slug, source_name) in UNIT_SOURCES.items():
        source = Image.open(ALPHA_ROOT / source_name).convert("RGBA")
        cells = [grid_cell(source, 4, 1, frame, 0, inset=4) for frame in range(4)]
        frames = render_group(cells, (32, 48), (2, 1), bottom_align=True, colors=42)
        sheet = horizontal_sheet(frames)
        png, svg = save_png_and_svg(sheet, f"runtime-v2/units/{slug}-walk.png")
        assets.append(
            {
                "id": f"c01-v2-unit-{content_id}",
                "contentId": content_id,
                "type": "combat-unit",
                "png": png,
                "svg": svg,
                "width": 128,
                "height": 48,
                "frameWidth": 32,
                "frameHeight": 48,
                "frames": 4,
                "frameOrder": ["idle-a", "step-a", "idle-b", "step-b"],
                "anchor": {"x": 16, "y": 47},
                "footprint": {"width": 1, "height": 1},
                "facing": "right",
                "zOrder": 10,
                "alphaMode": "binary",
                "sourceMaster": f"runtime-v2/masters/units/{source_name}",
            }
        )
        outputs[content_id] = sheet
    return outputs


def build_terrain(assets: list[dict]) -> dict[str, list[Image.Image]]:
    source = Image.open(MASTER_ROOT / "terrain" / "c01-terrain-material-board-master.png").convert("RGB")
    materials = {
        content_id: material_variants(
            grid_cell(source, 4, 2, index % 4, index // 4, inset=7),
            content_id,
        )
        for index, content_id in enumerate(TERRAIN_CONTENT)
    }
    materials["road"] = connected_tiles(materials["plain"][0], materials["road"][0], "road")
    materials["water"] = connected_tiles(materials["plain"][0], materials["water"][0], "water")
    for content_id in TERRAIN_CONTENT:
        tiles = materials[content_id]
        sheet = horizontal_sheet([tile.convert("RGBA") for tile in tiles])
        png, svg = save_png_and_svg(sheet, f"runtime-v2/terrain/{content_id}.png")
        connected = content_id in {"road", "water"}
        item = {
            "id": f"c01-v2-terrain-{content_id}",
            "contentId": content_id,
            "type": "terrain",
            "png": png,
            "svg": svg,
            "width": sheet.width,
            "height": 32,
            "frameWidth": 32,
            "frameHeight": 32,
            "frames": len(tiles),
            "tileMode": "nesw-mask" if connected else "coordinate-hash-variants",
            "alphaMode": "opaque",
            "sourceMaster": "runtime-v2/masters/terrain/c01-terrain-material-board-master.png",
        }
        if connected:
            item.update({"maskBits": ["N", "E", "S", "W"], "maskOrder": list(range(16))})
        else:
            item["variantOrder"] = [0, 1, 2, 3]
        assets.append(item)
    return materials


def build_structures(assets: list[dict]) -> dict[str, Image.Image]:
    specs = (
        ("redstone-oath-tower", "redstone-oath-tower-states-master.png", {"width": 2, "height": 2}),
        ("gray-banner-supply-depot", "gray-banner-depot-states-master.png", {"width": 3, "height": 2}),
    )
    outputs: dict[str, Image.Image] = {}
    for content_id, source_name, footprint in specs:
        source = Image.open(ALPHA_ROOT / source_name).convert("RGBA")
        cells = [grid_cell(source, 3, 1, state, 0, inset=4) for state in range(3)]
        states = render_group(cells, (128, 128), (4, 1), bottom_align=True, colors=64)
        sheet = vertical_sheet(states)
        png, svg = save_png_and_svg(sheet, f"runtime-v2/structures/{content_id}-states.png")
        assets.append(
            {
                "id": f"c01-v2-structure-{content_id}",
                "contentId": content_id,
                "type": "interactive-structure",
                "png": png,
                "svg": svg,
                "width": 128,
                "height": 384,
                "frameWidth": 128,
                "frameHeight": 128,
                "frames": 3,
                "stateRows": ["normal", "damaged", "captured"],
                "anchor": {"x": 64, "y": 127},
                "footprint": footprint,
                "collision": {"x": 32, "y": 64, "width": 64, "height": 64},
                "interactionHotspot": {"x": 48, "y": 76, "width": 32, "height": 48},
                "zOrder": 20,
                "alphaMode": "binary",
                "sourceMaster": f"runtime-v2/masters/structures/{source_name}",
            }
        )
        outputs[content_id] = sheet
    return outputs


def build_map_structures(assets: list[dict]) -> dict[str, Image.Image]:
    """Build the three legacy BoardView 1×1 structure replacements."""
    outputs: dict[str, Image.Image] = {}
    for content_id, source_name in MAP_STRUCTURE_SOURCES:
        source = Image.open(ALPHA_ROOT / source_name).convert("RGBA")
        cells = [grid_cell(source, 3, 1, state, 0, inset=4) for state in range(3)]
        states = render_group(cells, (32, 64), (1, 1), bottom_align=True, colors=38)
        sheet = vertical_sheet(states)
        png, svg = save_png_and_svg(sheet, f"runtime-v2/structures/{content_id}-states.png")
        assets.append(
            {
                "id": f"c01-v2-structure-{content_id}",
                "contentId": content_id,
                "type": "interactive-structure",
                "png": png,
                "svg": svg,
                "width": 32,
                "height": 192,
                "frameWidth": 32,
                "frameHeight": 64,
                "frames": 3,
                "stateRows": ["normal", "damaged", "captured"],
                "anchor": {"x": 16, "y": 63},
                "footprint": {"width": 1, "height": 1},
                "collision": {"x": 0, "y": 32, "width": 32, "height": 32},
                "interactionHotspot": {"x": 4, "y": 28, "width": 24, "height": 36},
                "zOrder": 18,
                "alphaMode": "binary",
                "neutralBody": True,
                "capturedBannerState": "captured",
                "sourceMaster": f"runtime-v2/masters/structures/{source_name}",
            }
        )
        outputs[content_id] = sheet
    return outputs


def build_icons(assets: list[dict]) -> dict[str, Image.Image]:
    source = Image.open(ALPHA_ROOT / "runtime-icons-master.png").convert("RGBA")
    outputs: dict[str, Image.Image] = {}
    for index, (asset_type, content_id, slug) in enumerate(ICON_SPECS):
        cell = grid_cell(source, 4, 2, index % 4, index // 4, inset=3)
        icon = render_group([cell], (32, 32), (2, 2), bottom_align=False, colors=32)[0]
        png, svg = save_png_and_svg(icon, f"runtime-v2/icons/{slug}.png")
        assets.append(
            {
                "id": f"c01-v2-{asset_type}-{content_id}",
                "contentId": content_id,
                "type": asset_type,
                "png": png,
                "svg": svg,
                "width": 32,
                "height": 32,
                "frameWidth": 32,
                "frameHeight": 32,
                "frames": 1,
                "alphaMode": "binary",
                "sourceMaster": "runtime-v2/masters/icons/runtime-icons-master.png",
            }
        )
        outputs[content_id] = icon
    return outputs


def build_fx(assets: list[dict]) -> dict[str, Image.Image]:
    source = Image.open(ALPHA_ROOT / "runtime-fx-master.png").convert("RGBA")
    outputs: dict[str, Image.Image] = {}
    for row, (content_id, fps, blend_mode, loop) in enumerate(FX_SPECS):
        cells = [grid_cell(source, 4, 4, frame, row, inset=3) for frame in range(4)]
        frames = render_group(cells, (32, 32), (1, 1), bottom_align=False, colors=32)
        sheet = horizontal_sheet(frames)
        png, svg = save_png_and_svg(sheet, f"runtime-v2/fx/{content_id}.png")
        assets.append(
            {
                "id": f"c01-v2-fx-{content_id}",
                "contentId": content_id,
                "type": "fx",
                "png": png,
                "svg": svg,
                "width": 128,
                "height": 32,
                "frameWidth": 32,
                "frameHeight": 32,
                "frames": 4,
                "anchor": {"x": 16, "y": 16},
                "fps": fps,
                "blendMode": blend_mode,
                "loop": loop,
                "alphaMode": "binary",
                "sourceMaster": "runtime-v2/masters/fx/runtime-fx-master.png",
            }
        )
        outputs[content_id] = sheet
    return outputs


def build_scene(assets: list[dict]) -> Image.Image:
    source = Image.open(MASTER_ROOT / "gray-banner-dawn-council-master.png").convert("RGB")
    target_ratio = 16 / 9
    if source.width / source.height > target_ratio:
        crop_w = round(source.height * target_ratio)
        x0 = (source.width - crop_w) // 2
        source = source.crop((x0, 0, x0 + crop_w, source.height))
    else:
        crop_h = round(source.width / target_ratio)
        y0 = (source.height - crop_h) // 2
        source = source.crop((0, y0, source.width, y0 + crop_h))
    scene = source.resize((256, 144), RESAMPLE).quantize(colors=112, method=Image.Quantize.MEDIANCUT).convert("RGB")
    png, svg = save_png_and_svg(scene, "runtime-v2/scenes/gray-banner-dawn-council.png")
    assets.append(
        {
            "id": "c01-v2-scene-gray-banner-dawn-council",
            "contentId": "gray-banner-dawn-council",
            "type": "narrative-scene",
            "png": png,
            "svg": svg,
            "width": 256,
            "height": 144,
            "alphaMode": "opaque",
            "dialogueSafeZone": {"x": 0, "y": 0, "width": 256, "height": 112},
            "sourceMaster": "runtime-v2/masters/gray-banner-dawn-council-master.png",
        }
    )
    return scene


def make_previews(
    units: dict[str, Image.Image],
    terrain: dict[str, list[Image.Image]],
    structures: dict[str, Image.Image],
    map_structures: dict[str, Image.Image],
    icons: dict[str, Image.Image],
    fx: dict[str, Image.Image],
    scene: Image.Image,
) -> None:
    preview_root = RUNTIME_ROOT / "previews"
    preview_root.mkdir(parents=True, exist_ok=True)
    unit_strip = Image.new("RGBA", (128, 48 * 4), (20, 27, 29, 255))
    for row, content_id in enumerate(UNIT_SOURCES):
        unit_strip.alpha_composite(units[content_id], (0, row * 48))
    unit_strip.save(preview_root / "units-1x.png")
    unit_strip.resize((256, 384), NEAREST).save(preview_root / "units-2x.png")

    terrain_board = Image.new("RGB", (32 * 12, 32 * 8), (15, 20, 22))
    rng = random.Random(101)
    non_connected = [name for name in TERRAIN_CONTENT if name not in {"road", "water"}]
    for index, name in enumerate(non_connected):
        ox = (index % 3) * 128
        oy = (index // 3) * 128
        for y in range(4):
            for x in range(4):
                terrain_board.paste(rng.choice(terrain[name]), (ox + x * 32, oy + y * 32))
    terrain_board.save(preview_root / "terrain-random-12x8-1x.png")
    terrain_board.resize((768, 512), NEAREST).save(preview_root / "terrain-random-12x8-2x.png")

    connection_board = Image.new("RGB", (256, 128), (15, 20, 22))
    for group, name in enumerate(("road", "water")):
        for bits, tile in enumerate(terrain[name]):
            x = group * 128 + (bits % 4) * 32
            y = (bits // 4) * 32
            connection_board.paste(tile, (x, y))
    connection_board.save(preview_root / "terrain-connections-1x.png")
    connection_board.resize((512, 256), NEAREST).save(preview_root / "terrain-connections-2x.png")

    map_structure_board = Image.new("RGBA", (96, 192), (15, 20, 22, 255))
    for row, (name, _) in enumerate(MAP_STRUCTURE_SOURCES):
        sheet = map_structures[name]
        for state in range(3):
            cell = sheet.crop((0, state * 64, 32, state * 64 + 64))
            map_structure_board.alpha_composite(cell, (state * 32, row * 64))
    map_structure_board.save(preview_root / "map-structures-1x.png")
    map_structure_board.resize((192, 384), NEAREST).save(preview_root / "map-structures-2x.png")

    overview = Image.new("RGB", (656, 536), (15, 20, 22))
    overview.paste(scene, (8, 8))
    overview.paste(unit_strip.convert("RGB"), (280, 8))
    icon_strip = horizontal_sheet([icons[item[1]] for item in ICON_SPECS]).convert("RGB")
    overview.paste(icon_strip, (8, 164))
    overview.paste(terrain_board.resize((384, 256), NEAREST), (8, 200))
    structure_preview = horizontal_sheet([structures[name].crop((0, 0, 128, 128)) for name in structures]).convert("RGB")
    overview.paste(structure_preview, (392, 200))
    overview.paste(map_structure_board.convert("RGB"), (528, 336))
    fx_preview = vertical_sheet([fx[name] for name, *_ in FX_SPECS]).convert("RGB")
    overview.paste(fx_preview, (512, 8))
    overview.save(preview_root / "runtime-v2-overview-1x.png")
    overview.resize((1312, 1072), NEAREST).save(preview_root / "runtime-v2-overview-2x.png")


def image_hash(image: Image.Image) -> str:
    return hashlib.sha256(image.tobytes()).hexdigest()


def run_qa(assets: list[dict]) -> dict:
    errors: list[str] = []
    warnings: list[str] = []
    metrics = {
        "assets": len(assets),
        "pngFiles": 0,
        "svgFiles": 0,
        "unitFramesChecked": 0,
        "terrainTilesChecked": 0,
        "terrainEdgeComparisons": 0,
        "terrainMeanLuma": {},
        "buildingStatesChecked": 0,
        "iconCellsChecked": 0,
        "fxFramesChecked": 0,
        "sceneSafeZoneHeight": 112,
    }

    for asset in assets:
        for key in ("png", "svg"):
            path = ASSET_ROOT / asset[key]
            if not path.is_file():
                errors.append(f"{asset['id']}: missing {key} {path}")
            else:
                metrics[f"{key}Files"] += 1
        png_path = ASSET_ROOT / asset["png"]
        if not png_path.is_file():
            continue
        image = Image.open(png_path)
        if image.size != (asset["width"], asset["height"]):
            errors.append(f"{asset['id']}: size {image.size} != {(asset['width'], asset['height'])}")

        if asset["type"] == "combat-unit":
            hashes = []
            ground_lines = []
            for frame in range(4):
                cell = image.crop((frame * 32, 0, frame * 32 + 32, 48)).convert("RGBA")
                alpha = cell.getchannel("A")
                hashes.append(image_hash(cell))
                bbox = alpha.getbbox()
                if bbox is None:
                    errors.append(f"{asset['id']}: empty unit frame {frame}")
                    continue
                ground_lines.append(bbox[3] - 1)
                if bbox[0] == 0 or bbox[1] == 0 or bbox[2] == 32:
                    errors.append(f"{asset['id']}: cropped head/weapon at frame {frame}, bbox={bbox}")
                if any(alpha.getpixel(point) for point in ((0, 0), (31, 0), (0, 47), (31, 47))):
                    errors.append(f"{asset['id']}: nontransparent frame corner {frame}")
                if any(alpha.histogram()[1:255]):
                    errors.append(f"{asset['id']}: nonbinary alpha in frame {frame}")
                metrics["unitFramesChecked"] += 1
            if len(set(ground_lines)) != 1 or ground_lines != [46] * len(ground_lines):
                errors.append(f"{asset['id']}: drifting ground lines {ground_lines}, expected 46")
            if len(set(hashes)) < 3:
                warnings.append(f"{asset['id']}: only {len(set(hashes))}/4 unique frames")

        elif asset["type"] == "terrain":
            frames = asset["frames"]
            cells = [image.crop((index * 32, 0, index * 32 + 32, 32)).convert("RGB") for index in range(frames)]
            metrics["terrainTilesChecked"] += frames
            mean_luma = round(ImageStat.Stat(image.convert("L")).mean[0], 2)
            metrics["terrainMeanLuma"][asset["contentId"]] = mean_luma
            if mean_luma < 54:
                errors.append(f"{asset['id']}: terrain mean luma {mean_luma} is too dark for the game board")
            if asset["tileMode"] == "coordinate-hash-variants":
                for other in cells[1:]:
                    for direction, box in (
                        ("top", (0, 0, 32, 1)),
                        ("bottom", (0, 31, 32, 32)),
                        ("left", (0, 0, 1, 32)),
                        ("right", (31, 0, 32, 32)),
                    ):
                        metrics["terrainEdgeComparisons"] += 1
                        reference_box = (0, 0, 32, 1) if direction in {"top", "bottom"} else (0, 0, 1, 32)
                        if image_hash(other.crop(box)) != image_hash(cells[0].crop(reference_box)):
                            errors.append(f"{asset['id']}: variant {direction} edge mismatch")
            else:
                for bits, cell in enumerate(cells):
                    for flag, source_box, target_box in (
                        (1, (0, 0, 32, 1), (0, 31, 32, 32)),
                        (2, (31, 0, 32, 32), (0, 0, 1, 32)),
                        (4, (0, 31, 32, 32), (0, 0, 32, 1)),
                        (8, (0, 0, 1, 32), (31, 0, 32, 32)),
                    ):
                        for other_bits, other in enumerate(cells):
                            opposite = {1: 4, 2: 8, 4: 1, 8: 2}[flag]
                            if bool(bits & flag) != bool(other_bits & opposite):
                                continue
                            metrics["terrainEdgeComparisons"] += 1
                            if image_hash(cell.crop(source_box)) != image_hash(other.crop(target_box)):
                                errors.append(f"{asset['id']}: mask edge mismatch {bits}/{other_bits}/{flag}")
                                break

        elif asset["type"] == "interactive-structure":
            hashes = []
            frame_width = asset["frameWidth"]
            frame_height = asset["frameHeight"]
            for state in range(3):
                cell = image.crop((0, state * frame_height, frame_width, state * frame_height + frame_height)).convert("RGBA")
                hashes.append(image_hash(cell))
                alpha = cell.getchannel("A")
                bbox = alpha.getbbox()
                if bbox is None or bbox[3] != frame_height - 1:
                    errors.append(f"{asset['id']}: state {state} anchor/bbox {bbox}")
                corners = ((0, 0), (frame_width - 1, 0), (0, frame_height - 1), (frame_width - 1, frame_height - 1))
                if any(alpha.getpixel(point) for point in corners):
                    errors.append(f"{asset['id']}: state {state} touches transparent canvas corner")
                if any(alpha.histogram()[1:255]):
                    errors.append(f"{asset['id']}: state {state} has nonbinary alpha")
                metrics["buildingStatesChecked"] += 1
            if len(set(hashes)) != 3:
                errors.append(f"{asset['id']}: building states are not unique")

        elif asset["type"] in {"equipment", "skill"}:
            alpha = image.convert("RGBA").getchannel("A")
            if alpha.getbbox() is None:
                errors.append(f"{asset['id']}: empty icon")
            if any(alpha.getpixel(point) for point in ((0, 0), (31, 0), (0, 31), (31, 31))):
                errors.append(f"{asset['id']}: icon touches corner")
            metrics["iconCellsChecked"] += 1

        elif asset["type"] == "fx":
            hashes = []
            for frame in range(4):
                cell = image.crop((frame * 32, 0, frame * 32 + 32, 32)).convert("RGBA")
                hashes.append(image_hash(cell))
                if cell.getchannel("A").getbbox() is None:
                    errors.append(f"{asset['id']}: empty FX frame {frame}")
                metrics["fxFramesChecked"] += 1
            if len(set(hashes)) < 3:
                errors.append(f"{asset['id']}: insufficient FX progression")

        elif asset["type"] == "narrative-scene":
            if image.mode not in {"RGB", "P"} and image.convert("RGBA").getchannel("A").getextrema() != (255, 255):
                errors.append(f"{asset['id']}: scene must be opaque")

    return {
        "schemaVersion": "1.0.0",
        "campaignId": "candidate-01",
        "qualityTier": "runtime-v2-candidate",
        "runtimeReady": False,
        "scope": "first-playable-batch-not-404-complete",
        "checks": metrics,
        "visualReview": {
            "oneX": "runtime-v2/previews/runtime-v2-overview-1x.png",
            "twoX": "runtime-v2/previews/runtime-v2-overview-2x.png",
            "unitsOneX": "runtime-v2/previews/units-1x.png",
            "unitsTwoX": "runtime-v2/previews/units-2x.png",
            "terrainRandomOneX": "runtime-v2/previews/terrain-random-12x8-1x.png",
            "terrainRandomTwoX": "runtime-v2/previews/terrain-random-12x8-2x.png",
            "terrainConnectionsOneX": "runtime-v2/previews/terrain-connections-1x.png",
            "terrainConnectionsTwoX": "runtime-v2/previews/terrain-connections-2x.png",
            "mapStructuresOneX": "runtime-v2/previews/map-structures-1x.png",
            "mapStructuresTwoX": "runtime-v2/previews/map-structures-2x.png",
        },
        "errors": errors,
        "warnings": warnings,
        "passed": not errors,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--refresh-chroma", action="store_true", help="重新运行 ImageGen 色键清除工具")
    args = parser.parse_args()
    if args.refresh_chroma:
        refresh_chroma()

    required_alpha = [source for _, source in UNIT_SOURCES.values()]
    required_alpha += ["redstone-oath-tower-states-master.png", "gray-banner-depot-states-master.png", "runtime-icons-master.png", "runtime-fx-master.png"]
    required_alpha += [source for _, source in MAP_STRUCTURE_SOURCES]
    missing = [name for name in required_alpha if not (ALPHA_ROOT / name).is_file()]
    if missing:
        raise SystemExit(f"缺少色键清除母图：{missing}；请运行 --refresh-chroma")

    assets: list[dict] = []
    units = build_units(assets)
    terrain = build_terrain(assets)
    structures = build_structures(assets)
    map_structures = build_map_structures(assets)
    icons = build_icons(assets)
    fx = build_fx(assets)
    scene = build_scene(assets)
    make_previews(units, terrain, structures, map_structures, icons, fx, scene)

    manifest = {
        "schemaVersion": "1.0.0",
        "campaignId": "candidate-01",
        "title": "断冠之誓·游戏运行时 V2 首批",
        "qualityTier": "runtime-v2-candidate",
        "runtimeReady": False,
        "scope": "first-playable-batch-not-404-complete",
        "tileSize": 32,
        "assetCount": len(assets),
        "prompts": "runtime-v2/PROMPTS.md",
        "assets": assets,
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    qa = run_qa(assets)
    QA_PATH.write_text(json.dumps(qa, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(qa, ensure_ascii=False, indent=2))
    if qa["errors"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
