#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path
from typing import Any

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter, ImageFont, ImageOps, ImageStat


PACK = Path(__file__).resolve().parents[1]
PARENT = PACK.parent
MASTERS = PACK / "masters"
ALPHA = PACK / "intermediate" / "alpha"
RUNTIME = PACK / "runtime"
ATLAS_DIR = RUNTIME / "atlas"
SCENERY_DIR = RUNTIME / "scenery"
PREVIEWS = PACK / "previews"
MANIFEST = PACK / "manifest-environment-builder-v1.json"
QA = PACK / "qa-environment-builder-v1.json"
SEMANTIC_CATALOG = PACK / "SEMANTIC-CATALOG.json"
SCENE = PACK / "scenes" / "c01-01.scene.json"
FORMAL_MANIFEST = PARENT / "manifest-final-fantasy-v1.json"

RESAMPLE = Image.Resampling.LANCZOS
TILE_2X = 64
TILE_1X = 32


def ensure_dirs() -> None:
    for path in (ATLAS_DIR, SCENERY_DIR, PREVIEWS, SCENE.parent):
        path.mkdir(parents=True, exist_ok=True)


def grid_cell(image: Image.Image, columns: int, rows: int, index: int) -> Image.Image:
    x = index % columns
    y = index // columns
    left = round(x * image.width / columns)
    top = round(y * image.height / rows)
    right = round((x + 1) * image.width / columns)
    bottom = round((y + 1) * image.height / rows)
    return image.crop((left, top, right, bottom))


def make_tileable(image: Image.Image, size: int) -> Image.Image:
    image = ImageOps.fit(image.convert("RGB"), (size, size), method=RESAMPLE)
    image = ImageChops.offset(image, size // 2, size // 2)
    blur = image.filter(ImageFilter.GaussianBlur(max(1, size / 48)))
    band = max(4, size // 12)
    mask = Image.new("L", image.size, 0)
    draw = ImageDraw.Draw(mask)
    cx = cy = size // 2
    draw.rectangle((cx - band, 0, cx + band, size), fill=210)
    draw.rectangle((0, cy - band, size, cy + band), fill=210)
    mask = mask.filter(ImageFilter.GaussianBlur(max(2, band / 2)))
    return lock_self_edges(Image.composite(blur, image, mask))


def lock_self_edges(image: Image.Image, border: int = 4) -> Image.Image:
    image = image.convert("RGB")
    pixels = image.load()
    width, height = image.size
    horizontal = [tuple((pixels[x, 0][c] + pixels[x, height - 1][c]) // 2 for c in range(3)) for x in range(width)]
    vertical = [tuple((pixels[0, y][c] + pixels[width - 1, y][c]) // 2 for c in range(3)) for y in range(height)]
    corner = tuple(sum(edge[c] for edge in (horizontal[0], horizontal[-1], vertical[0], vertical[-1])) // 4 for c in range(3))
    horizontal[0] = horizontal[-1] = vertical[0] = vertical[-1] = corner
    for x in range(width):
        pixels[x, 0] = pixels[x, height - 1] = horizontal[x]
    for y in range(height):
        pixels[0, y] = pixels[width - 1, y] = vertical[y]
    return image


def lock_family_edges(cells: list[Image.Image]) -> list[Image.Image]:
    locked = [lock_self_edges(cell) for cell in cells]
    width, height = locked[0].size
    sources = [cell.load() for cell in locked]
    horizontal = [
        tuple(sum(source[x, edge][c] for source in sources for edge in (0, height - 1)) // (len(sources) * 2) for c in range(3))
        for x in range(width)
    ]
    vertical = [
        tuple(sum(source[edge, y][c] for source in sources for edge in (0, width - 1)) // (len(sources) * 2) for c in range(3))
        for y in range(height)
    ]
    corner = tuple(sum(edge[c] for edge in (horizontal[0], horizontal[-1], vertical[0], vertical[-1])) // 4 for c in range(3))
    horizontal[0] = horizontal[-1] = vertical[0] = vertical[-1] = corner
    results = []
    for cell in locked:
        pixels = cell.load()
        for x in range(width):
            pixels[x, 0] = pixels[x, height - 1] = horizontal[x]
        for y in range(height):
            pixels[0, y] = pixels[width - 1, y] = vertical[y]
        results.append(cell)
    return results


def atlas_image(cells: list[Image.Image], columns: int, cell_size: tuple[int, int]) -> Image.Image:
    rows = math.ceil(len(cells) / columns)
    mode = "RGBA" if any(cell.mode == "RGBA" for cell in cells) else "RGB"
    fill = (0, 0, 0, 0) if mode == "RGBA" else (0, 0, 0)
    atlas = Image.new(mode, (columns * cell_size[0], rows * cell_size[1]), fill)
    for index, cell in enumerate(cells):
        x = (index % columns) * cell_size[0]
        y = (index // columns) * cell_size[1]
        if mode == "RGBA":
            cell = cell.convert("RGBA")
            atlas.alpha_composite(cell, (x, y))
        else:
            atlas.paste(cell.convert("RGB"), (x, y))
    return atlas


def save_pair(name: str, cells_2x: list[Image.Image], columns: int, meta: dict[str, Any]) -> dict[str, Any]:
    cell_2x = cells_2x[0].size
    atlas_2x = atlas_image(cells_2x, columns, cell_2x)
    cells_1x = [cell.resize((cell.width // 2, cell.height // 2), RESAMPLE) for cell in cells_2x]
    if meta.get("edgeLocked"):
        cells_1x = lock_family_edges(cells_1x)
    cell_1x = cells_1x[0].size
    atlas_1x = atlas_image(cells_1x, columns, cell_1x)
    path_1x = ATLAS_DIR / f"{name}.png"
    path_2x = ATLAS_DIR / f"{name}@2x.png"
    atlas_1x.save(path_1x, optimize=True)
    atlas_2x.save(path_2x, optimize=True)
    return {
        "id": name,
        "png": path_1x.relative_to(PACK).as_posix(),
        "png2x": path_2x.relative_to(PACK).as_posix(),
        "columns": columns,
        "rows": math.ceil(len(cells_2x) / columns),
        "cellWidth": cell_1x[0],
        "cellHeight": cell_1x[1],
        "cellWidth2x": cell_2x[0],
        "cellHeight2x": cell_2x[1],
        "componentCount": len(cells_2x),
        **meta,
    }


def texture_rows(path: Path) -> list[list[Image.Image]]:
    source = Image.open(path).convert("RGB")
    return [[grid_cell(source, 4, 4, row * 4 + col) for col in range(4)] for row in range(4)]


def source_cells(path: Path, columns: int, rows: int) -> list[Image.Image]:
    source = Image.open(path).convert("RGBA")
    return [grid_cell(source, columns, rows, i) for i in range(columns * rows)]


def fit_sprite(image: Image.Image, size: tuple[int, int], padding: int = 8) -> Image.Image:
    image = image.convert("RGBA")
    bbox = image.getchannel("A").getbbox()
    if bbox:
        image = image.crop(bbox)
    fitted = ImageOps.contain(image, (size[0] - padding * 2, size[1] - padding * 2), method=RESAMPLE)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    x = (size[0] - fitted.width) // 2
    y = size[1] - padding - fitted.height
    canvas.alpha_composite(fitted, (x, y))
    return canvas


def canonical_blob_mask(mask: int) -> int:
    n, ne, e, se, s, sw, w, nw = [(mask >> i) & 1 for i in range(8)]
    ne &= n & e
    se &= e & s
    sw &= s & w
    nw &= w & n
    values = [n, ne, e, se, s, sw, w, nw]
    return sum(value << i for i, value in enumerate(values))


BLOB_47 = sorted({canonical_blob_mask(mask) for mask in range(256)})


def mask_shape(size: int, mask: int, eight_way: bool = False, width_ratio: float = 0.52) -> Image.Image:
    scale = 4
    s = size * scale
    half = s // 2
    radius = int(s * width_ratio / 2)
    m = Image.new("L", (s, s), 0)
    d = ImageDraw.Draw(m)
    d.rounded_rectangle((half - radius, half - radius, half + radius, half + radius), radius=radius // 2, fill=255)
    if eight_way:
        n, ne, e, se, south, sw, w, nw = [(mask >> i) & 1 for i in range(8)]
    else:
        n, e, south, w = [(mask >> i) & 1 for i in range(4)]
        ne = se = sw = nw = 0
    if n:
        d.rectangle((half - radius, 0, half + radius, half), fill=255)
    if e:
        d.rectangle((half, half - radius, s, half + radius), fill=255)
    if south:
        d.rectangle((half - radius, half, half + radius, s), fill=255)
    if w:
        d.rectangle((0, half - radius, half, half + radius), fill=255)
    corner = half - radius
    if ne:
        d.rectangle((half, 0, s, half), fill=255)
        d.ellipse((half - corner, -corner, s + corner, half + corner), fill=255)
    if se:
        d.rectangle((half, half, s, s), fill=255)
        d.ellipse((half - corner, half - corner, s + corner, s + corner), fill=255)
    if sw:
        d.rectangle((0, half, half, s), fill=255)
        d.ellipse((-corner, half - corner, half + corner, s + corner), fill=255)
    if nw:
        d.rectangle((0, 0, half, half), fill=255)
        d.ellipse((-corner, -corner, half + corner, half + corner), fill=255)
    return m.resize((size, size), RESAMPLE)


def colored_overlay(texture: Image.Image, shape: Image.Image, shoulder: tuple[int, int, int] | None = None) -> Image.Image:
    size = shape.width
    tex = make_tileable(texture, size).convert("RGBA")
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    if shoulder:
        outer = shape.filter(ImageFilter.MaxFilter(9))
        ring = ImageChops.subtract(outer, shape.filter(ImageFilter.MaxFilter(3)))
        ring_color = Image.new("RGBA", (size, size), (*shoulder, 220))
        out.alpha_composite(Image.composite(ring_color, Image.new("RGBA", out.size), ring))
    tex.putalpha(shape)
    out.alpha_composite(tex)
    return out


def deterministic_noise_mask(size: int, seed: int, strength: int = 8) -> Image.Image:
    image = Image.new("L", (size, size), 128)
    pixels = image.load()
    for y in range(size):
        for x in range(size):
            value = ((x * 37 + y * 61 + seed * 101 + x * y * 3) ^ (x * 11 + y * 17 + seed * 7)) & 255
            pixels[x, y] = 128 - strength + (value % (strength * 2 + 1))
    return image.filter(ImageFilter.GaussianBlur(1.2))


def warp_connection_shape(shape: Image.Image, seed: int, amplitude: int = 3) -> Image.Image:
    width, height = shape.size
    warped = Image.new("L", shape.size, 0)
    for y in range(height):
        offset = round(math.sin((y + seed * 7) * 0.19) * amplitude + math.sin((y + seed * 13) * 0.071) * 1.5)
        row = shape.crop((0, y, width, y + 1))
        warped.paste(row, (offset, y))
    result = Image.new("L", shape.size, 0)
    for x in range(width):
        offset = round(math.sin((x + seed * 5) * 0.17) * amplitude + math.sin((x + seed * 11) * 0.063) * 1.5)
        column = warped.crop((x, 0, x + 1, height))
        result.paste(column, (x, offset))
    return result


def connection_overlay(texture: Image.Image, mask: int, variant: int, shoulder: tuple[int, int, int], water: bool = False) -> Image.Image:
    base_shape = mask_shape(TILE_2X, mask, False, 0.57 if water else 0.48)
    warped = warp_connection_shape(base_shape, variant + mask * 5, 2 if water else 3)
    noise = deterministic_noise_mask(TILE_2X, variant + mask * 17, 10)
    textured_shape = ImageChops.multiply(warped, noise)
    connected_ports = [(0, TILE_2X // 2), (TILE_2X - 1, TILE_2X // 2), (TILE_2X // 2, TILE_2X - 1), (TILE_2X // 2, 0)]
    draw = ImageDraw.Draw(textured_shape)
    half_width = 17 if water else 14
    if mask & 1:
        draw.rectangle((TILE_2X // 2 - half_width, 0, TILE_2X // 2 + half_width, 4), fill=255)
    if mask & 2:
        draw.rectangle((TILE_2X - 5, TILE_2X // 2 - half_width, TILE_2X - 1, TILE_2X // 2 + half_width), fill=255)
    if mask & 4:
        draw.rectangle((TILE_2X // 2 - half_width, TILE_2X - 5, TILE_2X // 2 + half_width, TILE_2X - 1), fill=255)
    if mask & 8:
        draw.rectangle((0, TILE_2X // 2 - half_width, 4, TILE_2X // 2 + half_width), fill=255)
    del connected_ports
    return colored_overlay(texture, textured_shape, shoulder)


def route_edge_cell(mask: int, variant: int) -> Image.Image:
    shape = warp_connection_shape(mask_shape(TILE_2X, mask, False, 0.52), variant + mask * 3, 3)
    outer = shape.filter(ImageFilter.MaxFilter(11))
    inner = shape.filter(ImageFilter.MinFilter(7))
    ring = ImageChops.subtract(outer, shape)
    rut = ImageChops.subtract(shape, inner)
    canvas = Image.new("RGBA", (TILE_2X, TILE_2X), (0, 0, 0, 0))
    shoulder = Image.new("RGBA", canvas.size, (78, 68, 49, 150))
    shoulder.putalpha(ring.point(lambda value: min(180, value)))
    canvas.alpha_composite(shoulder)
    ruts = Image.new("RGBA", canvas.size, (59, 48, 35, 100))
    ruts.putalpha(rut.point(lambda value: min(110, value)))
    canvas.alpha_composite(ruts)
    draw = ImageDraw.Draw(canvas)
    if variant == 1:
        draw.ellipse((42, 7, 49, 11), fill=(130, 111, 76, 150))
    elif variant == 2:
        draw.line((11, 46, 24, 51), fill=(47, 42, 32, 135), width=2)
    elif variant == 3:
        draw.ellipse((16, 15, 23, 20), fill=(99, 87, 62, 150))
    return canvas


def surface_atlases() -> tuple[list[dict[str, Any]], dict[str, list[Image.Image]]]:
    common = texture_rows(MASTERS / "surface-common-4x4.png")
    regional = texture_rows(MASTERS / "surface-regional-4x4.png")
    families = {
        "meadow": common[0],
        "forest-floor": common[1],
        "earth": common[2],
        "old-stone": common[3],
        "snow": regional[0],
        "wasteland": regional[1],
        "graveyard": regional[2],
        "forge-stone": regional[3],
    }
    atlases = []
    for name, cells in families.items():
        cells_2x = lock_family_edges([make_tileable(cell, TILE_2X) for cell in cells])
        atlases.append(save_pair(f"surface-{name}", cells_2x, 4, {
            "category": "base-surface", "tileMode": "variants", "variants": 4,
            "cellOrder": [f"{name}-{i + 1}" for i in range(4)], "edgeLocked": True,
        }))
    return atlases, families


def transition_atlases(families: dict[str, list[Image.Image]]) -> list[dict[str, Any]]:
    pairs = [
        ("meadow-earth", "meadow", "earth"),
        ("meadow-forest", "meadow", "forest-floor"),
        ("meadow-snow", "meadow", "snow"),
        ("earth-stone", "earth", "old-stone"),
    ]
    result = []
    for name, base, overlay in pairs:
        texture = families[overlay][0]
        cells = [colored_overlay(texture, mask_shape(TILE_2X, mask, True, 0.50)) for mask in BLOB_47]
        result.append(save_pair(f"transition-{name}", cells, 16, {
            "category": "transition", "tileMode": "blob-47", "baseSurface": f"surface-{base}",
            "overlaySurface": f"surface-{overlay}", "maskOrder": BLOB_47,
            "bitOrder": ["N", "NE", "E", "SE", "S", "SW", "W", "NW"],
        }))
    return result


def route_atlases() -> list[dict[str, Any]]:
    rows = texture_rows(MASTERS / "route-materials-4x4.png")
    names = ["dirt-road", "mud-road", "stone-road", "forest-trail"]
    result = []
    for name, row in zip(names, rows):
        cells = []
        for mask in range(16):
            for variant in range(4):
                texture = row[variant]
                if name == "dirt-road":
                    texture = ImageEnhance.Color(texture).enhance(0.82)
                    texture = ImageEnhance.Brightness(texture).enhance(1.28)
                    texture = ImageEnhance.Contrast(texture).enhance(1.06)
                cells.append(connection_overlay(texture, mask, variant, (72, 61, 45)))
        result.append(save_pair(f"route-{name}", cells, 16, {
            "category": "route", "tileMode": "nesw-16-variants", "maskOrder": list(range(16)),
            "variantsPerMask": 4, "cellIndex": "mask * 4 + variant",
            "bitOrder": ["N", "E", "S", "W"], "passable": True,
        }))
        edge_cells = [route_edge_cell(mask, variant) for mask in range(16) for variant in range(4)]
        result.append(save_pair(f"route-edge-{name}", edge_cells, 16, {
            "category": "route-edge", "tileMode": "nesw-16-variants", "maskOrder": list(range(16)),
            "variantsPerMask": 4, "cellIndex": "mask * 4 + variant", "renderAfter": f"route-{name}",
            "bitOrder": ["N", "E", "S", "W"], "passable": True,
        }))
    return result


def water_atlases() -> list[dict[str, Any]]:
    rows = texture_rows(MASTERS / "water-materials-4x4.png")
    names = ["slow-river", "shallow-stream", "marsh-channel", "icy-water"]
    banks = [(74, 67, 53), (98, 88, 67), (55, 62, 48), (125, 132, 129)]
    result = []
    for name, row, bank in zip(names, rows, banks):
        cells = []
        for mask in range(16):
            for variant in range(4):
                cells.append(connection_overlay(row[variant], mask, variant, bank, True))
        result.append(save_pair(f"water-{name}", cells, 16, {
            "category": "water", "tileMode": "nesw-16-variants", "maskOrder": list(range(16)),
            "variantsPerMask": 4, "cellIndex": "mask * 4 + variant",
            "bitOrder": ["N", "E", "S", "W"], "passable": name in {"shallow-stream", "marsh-channel"},
        }))
    return result


def elevation_edge_cell(mask: int, texture: Image.Image, cap: tuple[int, int, int]) -> Image.Image:
    width, height = 64, 96
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    rock = ImageOps.fit(texture.convert("RGB"), (width, height), method=RESAMPLE).convert("RGBA")
    dmask = Image.new("L", (width, height), 0)
    d = ImageDraw.Draw(dmask)
    n, e, south, w = [(mask >> i) & 1 for i in range(4)]
    if south:
        d.polygon([(1, 48), (63, 48), (59, 91), (5, 91)], fill=255)
    if e:
        d.polygon([(51, 4), (63, 7), (63, 64), (55, 57)], fill=235)
    if w:
        d.polygon([(1, 7), (13, 4), (9, 57), (1, 64)], fill=210)
    if n:
        d.polygon([(5, 3), (59, 3), (63, 14), (1, 14)], fill=180)
    rock.putalpha(dmask)
    canvas.alpha_composite(rock)
    draw = ImageDraw.Draw(canvas)
    if south:
        draw.line([(2, 48), (62, 48)], fill=(*cap, 255), width=5)
        draw.line([(5, 91), (59, 91)], fill=(28, 31, 29, 210), width=2)
    if e:
        draw.line([(52, 5), (56, 56)], fill=(*cap, 235), width=4)
    if w:
        draw.line([(12, 5), (8, 56)], fill=(*cap, 220), width=4)
    if n:
        draw.line([(5, 4), (59, 4)], fill=(*cap, 210), width=3)
    return canvas


def module_cells(path: Path, columns: int, rows: int, size: tuple[int, int], padding: int = 8) -> list[Image.Image]:
    return [fit_sprite(cell, size, padding) for cell in source_cells(path, columns, rows)]


def module_atlases() -> list[dict[str, Any]]:
    result = []
    semantic_data = json.loads(SEMANTIC_CATALOG.read_text(encoding="utf-8"))["atlases"]
    temperate = source_cells(ALPHA / "cliffs-temperate-4x4.png", 4, 4)
    highland = source_cells(ALPHA / "cliffs-highland-4x4.png", 4, 4)
    cliff_labels = [
        "straight-south", "end-west", "end-east", "concave-corner", "hill-cap", "ramp-level-1",
        "ramp-level-2", "mountain-pass", "convex-corner", "broken-ledge", "low-ledge", "talus-foot",
        "pillar-wide", "pillar-narrow", "ledge-end", "plateau-cap",
    ]
    for name, source, cap in [
        ("temperate", temperate, (126, 126, 72)),
        ("highland", highland, (176, 184, 177)),
    ]:
        result.append(save_pair(f"elevation-auto-{name}", [elevation_edge_cell(mask, source[0], cap) for mask in range(16)], 16, {
            "category": "elevation-edge", "tileMode": "nesw-16", "maskOrder": list(range(16)),
            "bitOrder": ["N", "E", "S", "W"], "heightDelta": 1, "repeatForHigherDelta": True,
            "obstructionHeight": 1, "passableOnlyWithRamp": True, "anchor": [0, 0],
        }))
        result.append(save_pair(f"cliff-modules-{name}", [fit_sprite(cell, (256, 256), 12) for cell in source], 4, {
            "category": "elevation-module", "tileMode": "modules", "cellOrder": cliff_labels,
            "heightDeltaRange": [1, 2], "anchor": [64, 127], "ySort": "bottom-anchor",
            "rampCells": [5, 6, 7], "blockingByDefault": True,
            "cells": semantic_data.get(f"cliff-modules-{name}", []),
        }))

    module_specs = [
        ("forest-existing", PARENT / "runtime" / "environment" / "twin-hills-forest-atlas-v1.png", 4, 4, (192, 192), "forest-scenery", 10),
        ("forest-temperate", ALPHA / "forest-temperate-4x4.png", 4, 4, (192, 192), "forest-scenery", 10),
        ("scenery-regional", ALPHA / "scenery-regional-4x4.png", 4, 4, (192, 192), "regional-scenery", 10),
        ("crossings-fortifications", ALPHA / "crossings-fortifications-4x4.png", 4, 4, (192, 192), "structure-kit", 10),
        ("camps-foundations", ALPHA / "camps-foundations-4x4.png", 4, 4, (288, 288), "foundation", 10),
        ("landmarks-large", ALPHA / "landmarks-large-4x4.png", 4, 4, (320, 320), "large-landmark", 10),
        ("decals-small", ALPHA / "decals-small-6x4.png", 6, 4, (128, 128), "ground-decal", 8),
    ]
    for name, path, cols, rows, size, category, padding in module_specs:
        cells = module_cells(path, cols, rows, size, padding)
        labels = [f"{name}-{i + 1:02d}" for i in range(len(cells))]
        semantic_cells = semantic_data.get(name, [])
        if semantic_cells:
            labels = [cell["id"] for cell in semantic_cells]
        meta: dict[str, Any] = {
            "category": category, "tileMode": "modules", "cellOrder": labels,
            "anchor": [size[0] // 4, size[1] // 2 - padding // 2], "ySort": "bottom-anchor",
            "cells": semantic_cells,
        }
        if category == "structure-kit":
            meta.update({"bridgeCells": [0, 1, 2, 3, 4, 5], "wallCells": list(range(6, 16)), "obstructionHeight": 1})
        if category == "large-landmark":
            meta.update({"footprintRange": [[3, 3], [4, 4]], "obstructionHeight": 2})
        if category == "foundation":
            meta.update({"footprintRange": [[2, 2], [4, 4]], "renderBelowStructures": True})
        result.append(save_pair(name, cells, cols, meta))
    return result


def rural_life_atlas() -> dict[str, Any]:
    path = ALPHA / "rural-life-4x4.png"
    cells = module_cells(path, 4, 4, (224, 224), 8)
    names = [
        "grain-drying-mat-dark", "grain-drying-mat-linen", "grain-drying-mat-earth", "threshing-floor",
        "wheat-sheaves", "wheat-bundles", "grain-sacks-baskets", "wheat-drying-rack",
        "frontier-granary", "frontier-farmhouse", "stone-well", "livestock-pen-small",
        "farm-fence-straight", "farm-fence-corner", "farm-handcart-tools", "roadside-waystation",
    ]
    footprints = [
        [2, 2], [2, 2], [2, 2], [2, 2],
        [1, 1], [1, 1], [1, 1], [2, 1],
        [2, 2], [3, 2], [2, 2], [3, 2],
        [2, 1], [2, 2], [2, 1], [3, 2],
    ]
    obstruction = [0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 1, 1, 1, 1, 1, 2]
    semantic_cells = [
        {"id": name, "footprint": footprint, "anchor": [56, 108], "obstructionHeight": height, "renderLayer": "foundation" if index < 4 else "under-units"}
        for index, (name, footprint, height) in enumerate(zip(names, footprints, obstruction))
    ]
    return save_pair("rural-life", cells, 4, {
        "category": "rural-life", "tileMode": "modules", "cellOrder": names,
        "anchor": [56, 108], "ySort": "bottom-anchor", "cells": semantic_cells,
    })


def cell_from_atlas(atlas: Image.Image, meta: dict[str, Any], index: int) -> Image.Image:
    cw, ch = meta["cellWidth2x"], meta["cellHeight2x"]
    x = (index % meta["columns"]) * cw
    y = (index // meta["columns"]) * ch
    return atlas.crop((x, y, x + cw, y + ch))


def connected_cell_index(meta: dict[str, Any], mask: int, variant: int = 0) -> int:
    return mask * int(meta.get("variantsPerMask", 1)) + variant % int(meta.get("variantsPerMask", 1))


def mask4(cells: set[tuple[int, int]], x: int, y: int) -> int:
    return (
        int((x, y - 1) in cells)
        | (int((x + 1, y) in cells) << 1)
        | (int((x, y + 1) in cells) << 2)
        | (int((x - 1, y) in cells) << 3)
    )


def load_atlas(meta: dict[str, Any]) -> Image.Image:
    return Image.open(PACK / meta["png2x"]).convert("RGBA")


def composite_bottom(canvas: Image.Image, image: Image.Image, cx: int, base_y: int) -> None:
    canvas.alpha_composite(image, (cx - image.width // 2, base_y - image.height))


def tile_surface(canvas: Image.Image, atlas: Image.Image, meta: dict[str, Any], salt: int = 0) -> None:
    cols = math.ceil(canvas.width / meta["cellWidth2x"])
    rows = math.ceil(canvas.height / meta["cellHeight2x"])
    for y in range(rows):
        for x in range(cols):
            index = (x * 7 + y * 11 + salt) % meta["componentCount"]
            canvas.alpha_composite(
                cell_from_atlas(atlas, meta, index),
                (x * meta["cellWidth2x"], y * meta["cellHeight2x"]),
            )


def preview_label(canvas: Image.Image, text: str, xy: tuple[int, int], fill: tuple[int, int, int] = (236, 228, 205)) -> None:
    font = ImageFont.load_default(size=20)
    x, y = xy
    box = ImageDraw.Draw(canvas).textbbox((x, y), text, font=font)
    ImageDraw.Draw(canvas).rounded_rectangle((box[0] - 10, box[1] - 6, box[2] + 10, box[3] + 6), radius=8, fill=(29, 33, 31, 220))
    ImageDraw.Draw(canvas).text((x, y), text, font=font, fill=fill)


def old_frame(relative: str, frame_width: int, frame_height: int, index: int = 0) -> Image.Image:
    image = Image.open(PARENT / relative).convert("RGBA")
    scale = max(1, image.height // frame_height)
    fw = frame_width * scale
    fh = frame_height * scale
    return image.crop((index * fw, 0, (index + 1) * fw, fh))


def make_compatibility_preview(atlases: list[dict[str, Any]]) -> None:
    by_id = {atlas["id"]: atlas for atlas in atlases}
    cols, rows = 20, 12
    canvas = Image.new("RGBA", (cols * TILE_2X, rows * TILE_2X), (34, 42, 37, 255))
    meadow_meta = by_id["surface-meadow"]
    meadow = load_atlas(meadow_meta)
    earth_meta = by_id["surface-earth"]
    earth = load_atlas(earth_meta)
    for y in range(rows):
        for x in range(cols):
            meta, atlas = (earth_meta, earth) if x > 14 and y < 5 else (meadow_meta, meadow)
            cell = cell_from_atlas(atlas, meta, (x * 7 + y * 11) % 4)
            canvas.alpha_composite(cell, (x * TILE_2X, y * TILE_2X))

    route_meta = by_id["route-dirt-road"]
    route = load_atlas(route_meta)
    water_meta = by_id["water-slow-river"]
    water = load_atlas(water_meta)
    road_cells = {(x, 7) for x in range(1, 19)} | {(5, y) for y in range(2, 8)} | {(15, y) for y in range(4, 10)}
    river_cells = {(10, y) for y in range(rows)} | {(11, y) for y in range(rows)}
    for x, y in river_cells:
        mask = mask4(river_cells, x, y)
        canvas.alpha_composite(cell_from_atlas(water, water_meta, connected_cell_index(water_meta, mask, (x * 7 + y * 11) % 4)), (x * TILE_2X, y * TILE_2X))
    for x, y in road_cells:
        mask = mask4(road_cells, x, y)
        variant = (x * 7 + y * 11) % 4
        canvas.alpha_composite(cell_from_atlas(route, route_meta, connected_cell_index(route_meta, mask, variant)), (x * TILE_2X, y * TILE_2X))
        edge_meta = by_id["route-edge-dirt-road"]
        edge = load_atlas(edge_meta)
        canvas.alpha_composite(cell_from_atlas(edge, edge_meta, connected_cell_index(edge_meta, mask, variant)), (x * TILE_2X, y * TILE_2X))

    cliff_meta = by_id["cliff-modules-temperate"]
    cliffs = load_atlas(cliff_meta)
    for index, x, y in [(4, 4, 4), (5, 7, 4), (6, 13, 5), (7, 16, 5)]:
        composite_bottom(canvas, cell_from_atlas(cliffs, cliff_meta, index), x * TILE_2X, (y + 2) * TILE_2X)

    forest_meta = by_id["forest-temperate"]
    forest = load_atlas(forest_meta)
    for i, (x, y) in enumerate([(1, 3), (2, 4), (3, 2), (17, 2), (18, 4), (1, 11), (18, 11), (8, 2), (13, 11)]):
        composite_bottom(canvas, cell_from_atlas(forest, forest_meta, i % 8), x * TILE_2X, (y + 1) * TILE_2X)

    camps_meta = by_id["camps-foundations"]
    camps = load_atlas(camps_meta)
    composite_bottom(canvas, cell_from_atlas(camps, camps_meta, 0), 4 * TILE_2X, 11 * TILE_2X)
    composite_bottom(canvas, cell_from_atlas(camps, camps_meta, 1), 16 * TILE_2X, 11 * TILE_2X)

    landmark_meta = by_id["landmarks-large"]
    landmarks = load_atlas(landmark_meta)
    composite_bottom(canvas, cell_from_atlas(landmarks, landmark_meta, 2), 17 * TILE_2X, 5 * TILE_2X)

    old_assets = [
        ("runtime-hd/interactive-structure/struct-lorne-keep.png", 64, 64, 3, 9),
        ("runtime-hd/interactive-structure/struct-mercenary-depot.png", 64, 64, 16, 9),
        ("runtime-hd/battle-prop/bprop-logistics-1.png", 64, 64, 7, 8),
    ]
    for path, fw, fh, x, y in old_assets:
        image = old_frame(path, fw, fh)
        composite_bottom(canvas, image, x * TILE_2X, y * TILE_2X)
    unit_assets = [
        ("runtime-hd/combat-unit/unit-banner-guard.png", 32, 48, 4, 8),
        ("runtime-hd/combat-unit/unit-archer.png", 32, 48, 6, 7),
        ("runtime-hd/combat-unit/unit-legion-shield.png", 32, 48, 15, 7),
        ("runtime-hd/combat-unit/unit-swordsman.png", 32, 48, 17, 8),
    ]
    for path, fw, fh, x, y in unit_assets:
        image = old_frame(path, fw, fh)
        composite_bottom(canvas, image, x * TILE_2X, (y + 1) * TILE_2X)
    canvas.convert("RGB").save(PREVIEWS / "compatibility-map-temperate-2x.png", optimize=True)
    canvas.resize((canvas.width // 2, canvas.height // 2), RESAMPLE).convert("RGB").save(PREVIEWS / "compatibility-map-temperate-1x.png", optimize=True)


def regional_surface_variant(x: int, y: int, count: int, seed: int) -> int:
    """Stable broad-area distribution that avoids a visible x/y checker cycle."""
    region_x, region_y = x // 4, y // 3
    broad = (region_x * 5 + region_y * 3 + seed) % count
    local = ((x * 0x1F123BB5) ^ (y * 0x5F356495) ^ (x * y * 97) ^ seed) & 0xFFFFFFFF
    local_value = (local ^ (local >> 13)) & 0xFFFFFFFF
    variant = broad
    if local_value % 7 == 0:
        variant = (variant + 1 + (local_value >> 9) % max(1, count - 1)) % count
    return variant


def formal_asset_by_topic(topic_id: str) -> dict[str, Any]:
    manifest = json.loads(FORMAL_MANIFEST.read_text(encoding="utf-8"))
    for asset in manifest["assets"]:
        if asset.get("topicId") == topic_id:
            return asset
    raise KeyError(topic_id)


def formal_asset_frame(topic_id: str, index: int = 0) -> Image.Image:
    asset = formal_asset_by_topic(topic_id)
    image = Image.open(PARENT / asset["png"].replace("runtime/", "runtime-hd/", 1)).convert("RGBA")
    frame_width = int(asset.get("frameWidth", asset["width"])) * 2
    frame_height = int(asset.get("frameHeight", asset["height"])) * 2
    return image.crop((index * frame_width, 0, (index + 1) * frame_width, frame_height))


def make_twin_hills_scene_preview(atlases: list[dict[str, Any]]) -> None:
    """V1.1 scene-language proof. This does not promote the pack to runtime-ready."""
    by_id = {atlas["id"]: atlas for atlas in atlases}
    scene = json.loads(SCENE.read_text(encoding="utf-8"))
    grid = [
        "TTTTTTTTTTTTTTTTTTTTTTTTTTT",
        "TTT...T......TT......T..TTT",
        "TT...T.^^^^...T........T.TT",
        "Tq-----^^^^h..............T",
        "TT..T.-^^^^h.T..hhh...T..TT",
        "T.....-^^^^.......hh......T",
        "TT..T.-------..T........T.T",
        "T.....-.....-.............T",
        "T-----------v-v-----------T",
        "T..-..........-..........-T",
        "TT.-.hTh......--------...-T",
        "T..-......-.....h^^^^----qT",
        "TT.-...T..-hh.T.h^^^^h.T.TT",
        "T..--------h....h^^^^h....T",
        "TT...T........T..hhhh..T.TT",
        "TTT.....T......T.....T..TTT",
        "TTTTTTTTTTTTTTTTTTTTTTTTTTT",
    ]
    cols, rows = scene["mapSize"]
    scene_scale = 2
    tile = TILE_1X * scene_scale
    canvas = Image.new("RGBA", (cols * tile, rows * tile), (25, 33, 29, 255))
    meadow_meta = by_id["surface-meadow"]
    meadow = load_atlas(meadow_meta)
    forest_floor_meta = by_id["surface-forest-floor"]
    forest_floor = load_atlas(forest_floor_meta)
    for y, row in enumerate(grid):
        for x, code in enumerate(row):
            meta, atlas = (forest_floor_meta, forest_floor) if code == "T" else (meadow_meta, meadow)
            index = regional_surface_variant(x, y, meta["componentCount"], 0xC0101)
            canvas.alpha_composite(cell_from_atlas(atlas, meta, index), (x * tile, y * tile))

    route_cells = {(x, y) for y, row in enumerate(grid) for x, code in enumerate(row) if code == "-"}
    route_meta = by_id["route-dirt-road"]
    route_edge_meta = by_id["route-edge-dirt-road"]
    route = load_atlas(route_meta)
    route_edge = load_atlas(route_edge_meta)
    for x, y in sorted(route_cells, key=lambda point: (point[1], point[0])):
        mask = mask4(route_cells, x, y)
        variant = regional_surface_variant(x, y, 4, 0xD17)
        index = connected_cell_index(route_meta, mask, variant)
        canvas.alpha_composite(cell_from_atlas(route, route_meta, index), (x * tile, y * tile))
        canvas.alpha_composite(cell_from_atlas(route_edge, route_edge_meta, index), (x * tile, y * tile))

    semantic_lookup: dict[str, tuple[dict[str, Any], int]] = {}
    for meta in atlases:
        for index, cell in enumerate(meta.get("cells", [])):
            semantic_lookup[cell["id"]] = (meta, index)

    low_layers = {"foundation": [], "ground-decal": [], "under-units": [], "foreground": []}
    for placement in scene["placements"]:
        low_layers[placement["layer"]].append(placement)

    def place_semantic(placement: dict[str, Any]) -> None:
        meta, index = semantic_lookup[placement["id"]]
        sprite = cell_from_atlas(load_atlas(meta), meta, index)
        composite_bottom(canvas, sprite, round(placement["x"] * tile), round((placement["y"] + 1) * tile))

    for placement in low_layers["foundation"]:
        place_semantic(placement)
    for placement in low_layers["ground-decal"]:
        place_semantic(placement)

    for placement in low_layers["under-units"]:
        if placement.get("id"):
            place_semantic(placement)
        else:
            composite_bottom(
                canvas,
                formal_asset_frame(placement["topicId"]),
                round(placement["x"] * tile),
                round((placement["y"] + 1) * tile),
            )

    units = [
        ("C01-UNIT-BANNER-GUARD", 3.1, 9.0), ("C01-UNIT-ARCHER", 5.0, 6.0),
        ("C01-UNIT-SWORDSMAN", 6.0, 12.0), ("C01-MISSION-BORDER-FARMER", 13.4, 9.1),
        ("C01-MISSION-BORDER-FARMER", 15.0, 7.2), ("C01-UNIT-LEGION-SHIELD", 20.2, 9.0),
        ("C01-UNIT-AXE-BREAKER", 23.3, 8.0), ("C01-UNIT-ARCHER", 18.0, 13.0),
    ]
    for topic_id, x, y in units:
        composite_bottom(canvas, formal_asset_frame(topic_id), round(x * tile), round((y + 1) * tile))

    forest_meta = by_id["forest-temperate"]
    forest = load_atlas(forest_meta)
    border_points = [
        (x, y) for y, row in enumerate(grid) for x, code in enumerate(row)
        if code == "T" and ((x * 17 + y * 31) % 4 != 0)
    ]
    for index, (x, y) in enumerate(border_points):
        sprite = cell_from_atlas(forest, forest_meta, index % 5)
        sprite = ImageOps.contain(sprite, (144, 144), method=RESAMPLE)
        composite_bottom(canvas, sprite, x * tile + tile // 2, (y + 1) * tile + 10)

    for placement in low_layers["foreground"]:
        place_semantic(placement)

    preview_label(canvas, "TWIN HILLS · V1.1 SCENE LANGUAGE PROOF", (18, 16))
    canvas.convert("RGB").save(PREVIEWS / "twin-hills-v1.1-scene-2x.png", optimize=True)
    canvas.resize((canvas.width // 2, canvas.height // 2), RESAMPLE).convert("RGB").save(
        PREVIEWS / "twin-hills-v1.1-scene-1x.png", optimize=True
    )


def make_elevation_preview(atlases: list[dict[str, Any]]) -> None:
    by_id = {atlas["id"]: atlas for atlas in atlases}
    canvas = Image.new("RGBA", (1280, 768), (32, 39, 35, 255))
    meadow_meta = by_id["surface-meadow"]
    meadow = load_atlas(meadow_meta)
    tile_surface(canvas, meadow, meadow_meta, 2)

    route_meta = by_id["route-dirt-road"]
    route = load_atlas(route_meta)
    for x in range(20):
        mask = (2 if x < 19 else 0) | (8 if x > 0 else 0)
        canvas.alpha_composite(cell_from_atlas(route, route_meta, connected_cell_index(route_meta, mask, x % 4)), (x * 64, 10 * 64))

    cliffs_meta = by_id["cliff-modules-temperate"]
    cliffs = load_atlas(cliffs_meta)
    cliff_placements = [
        (4, 260, 490),
        (0, 420, 480),
        (5, 535, 590),
        (6, 745, 490),
        (15, 930, 465),
        (7, 1090, 575),
    ]
    for index, cx, base_y in cliff_placements:
        composite_bottom(canvas, cell_from_atlas(cliffs, cliffs_meta, index), cx, base_y)

    high_meta = by_id["cliff-modules-highland"]
    high = load_atlas(high_meta)
    composite_bottom(canvas, cell_from_atlas(high, high_meta, 6), 870, 325)

    forest_meta = by_id["forest-temperate"]
    forest = load_atlas(forest_meta)
    for index, cx, base_y in [(0, 75, 340), (1, 165, 345), (3, 1165, 350), (5, 1125, 735), (10, 90, 735)]:
        composite_bottom(canvas, cell_from_atlas(forest, forest_meta, index), cx, base_y)

    units = [
        ("runtime-hd/combat-unit/unit-swordsman.png", 32, 48, 210, 640),
        ("runtime-hd/combat-unit/unit-archer.png", 32, 48, 405, 325),
        ("runtime-hd/combat-unit/unit-legion-shield.png", 32, 48, 805, 300),
        ("runtime-hd/combat-unit/unit-banner-guard.png", 32, 48, 1030, 405),
    ]
    for path, fw, fh, cx, base_y in units:
        composite_bottom(canvas, old_frame(path, fw, fh), cx, base_y)

    preview_label(canvas, "ELEVATION 0 · GROUND", (32, 700))
    preview_label(canvas, "ELEVATION 1 · HIGH GROUND", (330, 165), (224, 215, 164))
    preview_label(canvas, "ELEVATION 2 · CLIFF / RIDGE", (735, 72), (212, 225, 232))
    preview_label(canvas, "RAMP · PASSABLE", (500, 590), (196, 220, 176))
    preview_label(canvas, "CLIFF · BLOCKING", (935, 500), (231, 173, 151))
    canvas.convert("RGB").save(PREVIEWS / "elevation-system-2x.png", optimize=True)
    canvas.resize((640, 384), RESAMPLE).convert("RGB").save(PREVIEWS / "elevation-system-1x.png", optimize=True)


def make_biome_preview(atlases: list[dict[str, Any]]) -> None:
    by_id = {atlas["id"]: atlas for atlas in atlases}
    panel_w, panel_h = 640, 384
    canvas = Image.new("RGBA", (panel_w * 2, panel_h * 2), (26, 30, 29, 255))
    panel_specs = [
        ("surface-snow", "SNOW FRONTIER", 0),
        ("surface-wasteland", "WAR-TORN WASTE", 1),
        ("surface-graveyard", "OLD GRAVEYARD", 2),
        ("surface-forge-stone", "FORGE DOMAIN", 3),
    ]
    scenery_meta = by_id["scenery-regional"]
    scenery = load_atlas(scenery_meta)
    road_meta = by_id["route-stone-road"]
    road = load_atlas(road_meta)
    landmarks_meta = by_id["landmarks-large"]
    landmarks = load_atlas(landmarks_meta)
    for panel_index, (surface_id, label, scenery_row) in enumerate(panel_specs):
        ox = (panel_index % 2) * panel_w
        oy = (panel_index // 2) * panel_h
        panel = Image.new("RGBA", (panel_w, panel_h), (0, 0, 0, 255))
        surface_meta = by_id[surface_id]
        surface = load_atlas(surface_meta)
        tile_surface(panel, surface, surface_meta, panel_index)
        for x in range(10):
            mask = (2 if x < 9 else 0) | (8 if x > 0 else 0)
            panel.alpha_composite(cell_from_atlas(road, road_meta, connected_cell_index(road_meta, mask, (x + panel_index) % 4)), (x * 64, 5 * 64))
        for i, (cx, base_y) in enumerate([(70, 180), (180, 145), (475, 155), (560, 275)]):
            cell_index = scenery_row * 4 + i
            composite_bottom(panel, cell_from_atlas(scenery, scenery_meta, cell_index), cx, base_y)
        landmark_index = [3, 0, 12, 15][panel_index]
        composite_bottom(panel, cell_from_atlas(landmarks, landmarks_meta, landmark_index), 340, 310)
        preview_label(panel, label, (18, 18))
        canvas.alpha_composite(panel, (ox, oy))
    canvas.convert("RGB").save(PREVIEWS / "biome-coverage-2x.png", optimize=True)
    canvas.resize((640, 384), RESAMPLE).convert("RGB").save(PREVIEWS / "biome-coverage-1x.png", optimize=True)


def contact_sheet(atlases: list[dict[str, Any]]) -> None:
    cards = []
    font = ImageFont.load_default()
    for meta in atlases:
        atlas = load_atlas(meta)
        thumb = ImageOps.contain(atlas, (300, 150), method=RESAMPLE)
        card = Image.new("RGB", (320, 190), (47, 50, 48))
        checker = Image.new("RGB", thumb.size, (76, 80, 77))
        check_draw = ImageDraw.Draw(checker)
        for cy in range(0, thumb.height, 12):
            for cx in range(0, thumb.width, 12):
                if (cx // 12 + cy // 12) % 2:
                    check_draw.rectangle((cx, cy, cx + 11, cy + 11), fill=(59, 63, 61))
        if thumb.mode == "RGBA":
            checker.paste(thumb, (0, 0), thumb)
        else:
            checker.paste(thumb.convert("RGB"), (0, 0))
        card.paste(checker, ((320 - thumb.width) // 2, 6))
        draw = ImageDraw.Draw(card)
        draw.text((8, 160), meta["id"], font=font, fill=(232, 224, 202))
        draw.text((8, 176), f"{meta['category']} · {meta['componentCount']} components", font=font, fill=(160, 176, 157))
        cards.append(card)
    sheet = atlas_image([card.convert("RGB") for card in cards], 3, (320, 190))
    sheet.save(PREVIEWS / "environment-atlases-overview.png", optimize=True)


def alpha_magenta_count(image: Image.Image) -> int:
    count = 0
    for r, g, b, a in image.convert("RGBA").get_flattened_data():
        if a > 200 and r > 225 and b > 185 and g < 70:
            count += 1
    return count


def edge_diff(a: Image.Image, b: Image.Image, side_a: str, side_b: str) -> int:
    a = a.convert("RGBA")
    b = b.convert("RGBA")
    if side_a == "E":
        edge_a = a.crop((a.width - 1, 0, a.width, a.height))
    elif side_a == "S":
        edge_a = a.crop((0, a.height - 1, a.width, a.height))
    else:
        raise ValueError(side_a)
    if side_b == "W":
        edge_b = b.crop((0, 0, 1, b.height))
    elif side_b == "N":
        edge_b = b.crop((0, 0, b.width, 1))
    else:
        raise ValueError(side_b)
    if edge_a.size != edge_b.size:
        return 255
    extrema = ImageChops.difference(edge_a, edge_b).getextrema()
    return max(channel[1] for channel in extrema)


def validate(atlases: list[dict[str, Any]]) -> dict[str, Any]:
    errors: list[str] = []
    warnings: list[str] = []
    component_count = sum(atlas["componentCount"] for atlas in atlases)
    if component_count < 500:
        errors.append(f"component count {component_count} < 500")
    duplicate_cells: list[dict[str, Any]] = []
    magenta = 0
    port_errors: list[str] = []
    seam_errors: list[str] = []
    for meta in atlases:
        for scale_key, cw_key, ch_key in [("png", "cellWidth", "cellHeight"), ("png2x", "cellWidth2x", "cellHeight2x")]:
            path = PACK / meta[scale_key]
            if not path.exists():
                errors.append(f"missing {path}")
                continue
            image = Image.open(path)
            expected = (meta["columns"] * meta[cw_key], meta["rows"] * meta[ch_key])
            if image.size != expected:
                errors.append(f"size mismatch {path}: {image.size} != {expected}")
        image = load_atlas(meta)
        magenta += alpha_magenta_count(image)
        hashes: dict[str, list[int]] = {}
        for i in range(meta["componentCount"]):
            cell = cell_from_atlas(image, meta, i)
            digest = hashlib.sha256(cell.tobytes()).hexdigest()
            hashes.setdefault(digest, []).append(i)
        groups = [group for group in hashes.values() if len(group) > 1]
        if groups:
            duplicate_cells.append({"atlas": meta["id"], "groups": groups})
        if meta.get("tileMode") in {"nesw-16", "nesw-16-variants"} and meta["category"] in {"route", "water"}:
            for mask in range(16):
                for variant in range(int(meta.get("variantsPerMask", 1))):
                    cell = cell_from_atlas(image, meta, connected_cell_index(meta, mask, variant)).getchannel("A")
                    cx, cy = cell.width // 2, cell.height // 2
                    samples = [cell.getpixel((cx, 0)), cell.getpixel((cell.width - 1, cy)), cell.getpixel((cx, cell.height - 1)), cell.getpixel((0, cy))]
                    for bit, value in enumerate(samples):
                        connected = bool(mask & (1 << bit))
                        if connected != (value > 128):
                            port_errors.append(f"{meta['id']} mask {mask} variant {variant} bit {bit}")
        if meta.get("edgeLocked"):
            cells = [cell_from_atlas(image, meta, i) for i in range(meta["componentCount"])]
            for i, a in enumerate(cells):
                for j, b in enumerate(cells):
                    if edge_diff(a, b, "E", "W") or edge_diff(a, b, "S", "N"):
                        seam_errors.append(f"{meta['id']} {i}->{j}")
    if magenta:
        errors.append(f"opaque magenta pixels: {magenta}")
    if duplicate_cells:
        errors.append(f"duplicate atlas cells: {len(duplicate_cells)} atlases")
    if port_errors:
        errors.append(f"port errors: {len(port_errors)}")
    if seam_errors:
        errors.append(f"seam errors: {len(seam_errors)}")
    atlas_by_id = {meta["id"]: meta for meta in atlases}
    meadow = load_atlas(atlas_by_id["surface-meadow"])
    dirt_road = load_atlas(atlas_by_id["route-dirt-road"])
    meadow_luma = ImageStat.Stat(meadow.convert("L")).mean[0]
    dirt_road_luma = ImageStat.Stat(dirt_road.convert("L"), mask=dirt_road.getchannel("A")).mean[0]
    road_luma_delta = dirt_road_luma - meadow_luma
    if road_luma_delta < 10:
        errors.append(f"dirt road luma delta {road_luma_delta:.2f} < 10")
    distribution = [0, 0, 0, 0]
    for y in range(17):
        for x in range(27):
            distribution[regional_surface_variant(x, y, 4, 0xC0101)] += 1
    if min(distribution) == 0 or max(distribution) / min(distribution) > 2:
        errors.append(f"unbalanced regional surface distribution: {distribution}")
    preview_expectations = {
        "twin-hills-v1.1-scene-1x.png": (864, 544),
        "twin-hills-v1.1-scene-2x.png": (1728, 1088),
    }
    preview_errors: list[str] = []
    for filename, expected in preview_expectations.items():
        path = PREVIEWS / filename
        if not path.exists():
            preview_errors.append(f"missing {filename}")
        elif Image.open(path).size != expected:
            preview_errors.append(f"{filename} size {Image.open(path).size} != {expected}")
    if preview_errors:
        errors.append(f"scene preview errors: {len(preview_errors)}")
    categories: dict[str, int] = {}
    for meta in atlases:
        categories[meta["category"]] = categories.get(meta["category"], 0) + meta["componentCount"]
    semantic_errors: list[str] = []
    semantic_ids: set[str] = set()
    semantic_sources: dict[str, str] = {}
    for meta in atlases:
        cells = meta.get("cells")
        if not cells:
            continue
        if len(cells) != meta["componentCount"]:
            semantic_errors.append(f"{meta['id']} semantic count {len(cells)} != {meta['componentCount']}")
            continue
        for index, cell in enumerate(cells):
            cid = cell.get("id")
            if not isinstance(cid, str) or not cid:
                semantic_errors.append(f"{meta['id']} cell {index} missing id")
            elif cid in semantic_ids:
                semantic_errors.append(f"duplicate semantic id {cid}")
            else:
                semantic_ids.add(cid)
                semantic_sources[cid] = meta["id"]
            footprint = cell.get("footprint")
            anchor = cell.get("anchor")
            if not isinstance(footprint, list) or len(footprint) != 2 or min(footprint) < 1:
                semantic_errors.append(f"{meta['id']} cell {index} invalid footprint")
            if not isinstance(anchor, list) or len(anchor) != 2:
                semantic_errors.append(f"{meta['id']} cell {index} invalid anchor")
            if not isinstance(cell.get("obstructionHeight"), int):
                semantic_errors.append(f"{meta['id']} cell {index} invalid obstructionHeight")
    if semantic_errors:
        errors.append(f"semantic errors: {len(semantic_errors)}")
    formal_manifest = json.loads(FORMAL_MANIFEST.read_text(encoding="utf-8"))
    formal_topic_ids = {
        asset.get("topicId")
        for asset in formal_manifest.get("assets", [])
        if isinstance(asset.get("topicId"), str)
    }
    scene_errors: list[str] = []
    if not SCENE.exists():
        scene_errors.append("missing c01-01 scene config")
    else:
        scene = json.loads(SCENE.read_text(encoding="utf-8"))
        atlas_ids = {meta["id"] for meta in atlases}
        allow = scene.get("selection", {}).get("allowAtlases", [])
        deny = scene.get("selection", {}).get("denyAtlases", [])
        if len(allow) > 10:
            scene_errors.append(f"c01-01 allowlist has {len(allow)} atlases; expected <= 10")
        if len(allow) != len(set(allow)) or len(deny) != len(set(deny)):
            scene_errors.append("c01-01 duplicate atlas ids")
        if set(allow) & set(deny):
            scene_errors.append("c01-01 allow/deny overlap")
        for atlas_id in allow + deny:
            if atlas_id not in atlas_ids:
                scene_errors.append(f"c01-01 unknown atlas {atlas_id}")
        known_semantic = semantic_ids
        for placement in scene.get("placements", []):
            placement_id = placement.get("id")
            if placement_id and placement_id not in known_semantic:
                scene_errors.append(f"c01-01 unknown placement {placement_id}")
            elif placement_id and semantic_sources[placement_id] not in allow:
                scene_errors.append(
                    f"c01-01 placement {placement_id} belongs to disallowed atlas {semantic_sources[placement_id]}"
                )
            topic_id = placement.get("topicId")
            if topic_id and topic_id not in formal_topic_ids:
                scene_errors.append(f"c01-01 unknown formal topic {topic_id}")
            x = placement.get("x")
            y = placement.get("y")
            map_size = scene.get("mapSize", [])
            if not isinstance(x, (int, float)) or not isinstance(y, (int, float)):
                scene_errors.append("c01-01 placement missing numeric coordinates")
            elif len(map_size) != 2 or not (0 <= x < map_size[0] and 0 <= y < map_size[1]):
                scene_errors.append(f"c01-01 placement outside map: {placement_id or topic_id}")
            if placement.get("layer") not in {"foundation", "ground-decal", "under-units", "foreground"}:
                scene_errors.append(f"c01-01 invalid scene layer: {placement.get('layer')}")
        if scene.get("runtimeReady") is not False:
            scene_errors.append("c01-01 scene must remain runtimeReady=false before screenshot QA")
    if scene_errors:
        errors.append(f"scene errors: {len(scene_errors)}")
    return {
        "passed": not errors,
        "atlasCount": len(atlases),
        "componentCount": component_count,
        "categoryCoverage": categories,
        "blob47MaskCount": len(BLOB_47),
        "opaqueMagentaPixels": magenta,
        "duplicateCells": duplicate_cells,
        "connectionPortErrors": port_errors,
        "edgeSeamErrors": seam_errors,
        "edgeSeamComparisons": sum(meta["componentCount"] ** 2 * 2 for meta in atlases if meta.get("edgeLocked")),
        "visualMetrics": {
            "meadowMeanLuma": round(meadow_luma, 2),
            "dirtRoadMeanLuma": round(dirt_road_luma, 2),
            "dirtRoadLumaDelta": round(road_luma_delta, 2),
            "regionalSurfaceDistribution21x13": distribution,
            "scenePreviewSizes": {name: list(size) for name, size in preview_expectations.items()},
            "scenePreviewErrors": preview_errors,
        },
        "semanticCellCount": len(semantic_ids),
        "semanticErrors": semantic_errors,
        "sceneErrors": scene_errors,
        "warnings": warnings,
        "errors": errors,
    }


def main() -> None:
    ensure_dirs()
    atlases, families = surface_atlases()
    atlases += transition_atlases(families)
    atlases += route_atlases()
    atlases += water_atlases()
    atlases += module_atlases()
    atlases += [rural_life_atlas()]
    manifest = {
        "schemaVersion": "environment-builder-v1.1",
        "campaignId": "candidate-01",
        "packId": "environment-builder-v1.1",
        "title": "剧本一 · 通用西幻 SRPG 地图建造包 V1.1",
        "runtimeReady": False,
        "qualityTier": "environment-builder-v1.1-candidate",
        "logicalTileSize": 32,
        "runtimeScales": [1, 2],
        "componentCount": sum(atlas["componentCount"] for atlas in atlases),
        "styleCompatibility": {
            "existingPack": "../manifest-final-fantasy-v1.json",
            "reference": "../../../art-assets/reference/art-direction-map.png",
            "camera": "high-oblique tactical map; existing units remain side-front readable",
            "palette": ["gray-blue", "moss green", "rain brown", "old stone", "muted burgundy", "restrained amber"],
            "lighting": "diffuse overcast upper-left",
            "material": "matte two-step cartoon shading with dark brown-black outlines",
        },
        "assetReview": {
            "directUseCandidate": [
                "cliff-modules-temperate", "forest-temperate", "forest-existing", "camps-foundations",
                "decals-small", "crossings-fortifications", "rural-life",
            ],
            "controlledUseCandidate": [
                "surface-*", "transition-*", "route-*", "route-edge-*", "landmarks-large",
            ],
            "laterBiomeCandidate": [
                "water-*", "cliff-modules-highland", "scenery-regional",
            ],
            "holdout": ["elevation-auto-temperate", "elevation-auto-highland"],
            "note": "All tiers remain runtimeReady=false until a real level screenshot is approved.",
        },
        "renderLayers": [
            "base-surface", "transition", "route", "route-edge", "water", "elevation-edge", "foundation",
            "ground-decal", "rural-life", "structure-kit", "large-landmark", "forest-scenery", "regional-scenery", "units", "foreground",
        ],
        "sceneLayerOrder": ["foundation", "ground-decal", "under-units", "units", "foreground"],
        "elevationContract": {
            "source": "GameMap.elevation integer layer",
            "supportedVisualLevels": [0, 1, 2],
            "higherLevels": "repeat one-level face by delta",
            "automaticEdgeMode": "N/E/S/W 16-mask",
            "movement": "engine maxClimb/maxDrop/uphillCostPerLevel remains authoritative",
            "lineOfSight": "engine elevation and obstructionHeight remain authoritative",
            "ramps": "only rampCells are visually passable; editor cliff edges remain authoritative",
        },
        "atlases": atlases,
    }
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    make_compatibility_preview(atlases)
    make_twin_hills_scene_preview(atlases)
    make_elevation_preview(atlases)
    make_biome_preview(atlases)
    contact_sheet(atlases)
    qa = validate(atlases)
    QA.write_text(json.dumps(qa, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(qa, ensure_ascii=False, indent=2))
    if not qa["passed"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
