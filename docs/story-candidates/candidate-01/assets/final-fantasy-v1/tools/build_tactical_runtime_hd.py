#!/usr/bin/env python3
"""Derive 2x tactical runtime assets from the approved high-resolution masters."""

from __future__ import annotations

import importlib.util
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageOps


PACK = Path(__file__).resolve().parents[1]
OUTPUT = PACK / "runtime-hd"
MANIFEST = PACK / "manifest-tactical-runtime-hd.json"
DENSITY = 2


def load_base_builder():
    source = Path(__file__).with_name("build_final_fantasy_v1.py")
    spec = importlib.util.spec_from_file_location("candidate01_base_builder", source)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot import base asset builder: {source}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


base = load_base_builder()


def slug(topic: dict) -> str:
    return topic["id"].lower().replace("c01-", "").replace("_", "-")


def write_asset(image: Image.Image, category: str, topic: dict, assets: list[dict]) -> None:
    relative = Path("runtime-hd") / category / f"{slug(topic)}.png"
    path = PACK / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, optimize=True)
    assets.append({
        "topicId": topic["id"],
        "png": relative.as_posix(),
        "width": image.width,
        "height": image.height,
        "pixelDensity": DENSITY,
    })


def combat_source_rows() -> list[list[Image.Image]]:
    rows: list[list[Image.Image]] = []
    for name in base.INDEPENDENT_UNITS:
        image = base.chroma(base.MASTERS / "combat-units" / name)
        rows.append([base.grid_cell(image, 4, 1, column, 0, 8) for column in range(4)])
    for name in base.COMBAT_BOARDS:
        image = base.chroma(base.MASTERS / "combat-atlases" / name)
        for row in range(4):
            rows.append([base.grid_cell(image, 4, 4, column, row, 5) for column in range(4)])
    return rows


def build_units(topics: dict[str, list[dict]], assets: list[dict]) -> None:
    rows = combat_source_rows()
    assert len(rows) == len(topics["combat-unit"])
    for index, (topic, crops) in enumerate(zip(topics["combat-unit"], rows)):
        width, height = base.unit_size(index, True)
        frames = [
            base.fit_transparent(crop, (width * DENSITY, height * DENSITY), 2, True)
            for crop in crops
        ]
        write_asset(base.horizontal(frames), "combat-unit", topic, assets)

    rows = []
    for name in base.MISSION_BOARDS:
        image = base.chroma(base.MASTERS / "mission-atlases" / name)
        for row in range(4):
            rows.append([base.grid_cell(image, 4, 4, column, row, 5) for column in range(4)])
    assert len(rows) == len(topics["mission-unit"])
    for topic, crops in zip(topics["mission-unit"], rows):
        frames = [base.fit_transparent(crop, (64, 96), 2, True) for crop in crops]
        write_asset(base.horizontal(frames), "mission-unit", topic, assets)


def build_terrain(topics: dict[str, list[dict]], assets: list[dict]) -> None:
    specs = [
        ("terrain-atlas-border-river.png", 4, 2),
        ("terrain-atlas-capital-silverwood.png", 4, 2),
        ("terrain-atlas-forge-wasteland.png", 4, 2),
        ("terrain-atlas-graveyard-oathlight.png", 4, 2),
    ]
    cells = []
    for name, columns, rows in specs:
        image = Image.open(base.MASTERS / "terrain" / name).convert("RGB")
        cells.extend(
            base.grid_cell(image, columns, rows, column, row, 3)
            for row in range(rows)
            for column in range(columns)
        )
    connected = {1, 3, 4, 5, 6, 7, 8, 9, 13, 17, 21, 23, 29, 30}
    size = 32 * DENSITY
    for index, (topic, cell) in enumerate(zip(topics["terrain"], cells)):
        variants = []
        for center_x, center_y in ((0.32, 0.32), (0.68, 0.32), (0.32, 0.68), (0.68, 0.68)):
            side = round(min(cell.size) * 0.62)
            x0 = max(0, min(cell.width - side, round(cell.width * center_x - side / 2)))
            y0 = max(0, min(cell.height - side, round(cell.height * center_y - side / 2)))
            tile = cell.crop((x0, y0, x0 + side, y0 + side)).resize((size, size), base.RESAMPLE)
            tile = ImageEnhance.Color(tile).enhance(0.82)
            tile = ImageEnhance.Contrast(tile).enhance(0.94)
            variants.append(tile.convert("RGBA"))
        if index not in connected:
            write_asset(base.horizontal(variants), "terrain", topic, assets)
            continue
        masks = []
        for bits in range(16):
            mask = Image.new("L", (size, size), 0)
            draw = ImageDraw.Draw(mask)
            draw.rounded_rectangle((20, 20, 43, 43), radius=8, fill=255)
            if bits & 1:
                draw.rectangle((24, 0, 39, 32), fill=255)
            if bits & 2:
                draw.rectangle((32, 24, 63, 39), fill=255)
            if bits & 4:
                draw.rectangle((24, 32, 39, 63), fill=255)
            if bits & 8:
                draw.rectangle((0, 24, 32, 39), fill=255)
            tile = variants[0].copy()
            tile.putalpha(mask)
            masks.append(tile)
        write_asset(base.horizontal(masks), "terrain", topic, assets)


def build_structures(topics: dict[str, list[dict]], assets: list[dict]) -> None:
    specs = [
        ("structures-atlas-01.png", 3, 4),
        ("structures-atlas-02.png", 3, 4),
        ("structures-atlas-03.png", 3, 4),
        ("structures-atlas-04-source-4col.png", 4, 4),
        ("structures-atlas-05.png", 3, 4),
        ("structures-atlas-06.png", 3, 4),
    ]
    groups = []
    for name, columns, rows in specs:
        image = base.chroma(base.MASTERS / "structures" / name)
        for row in range(rows):
            groups.append([base.grid_cell(image, columns, rows, column, row, 5) for column in range(3)])
    for topic, group in zip(topics["interactive-structure"], groups):
        frames = [base.fit_transparent(cell, (128, 128), 4, True) for cell in group]
        write_asset(base.horizontal(frames), "interactive-structure", topic, assets)


def build_grid_category(
    topics: dict[str, list[dict]],
    assets: list[dict],
    category: str,
    source_folder: str,
    specs: list[tuple[str, int, int]],
) -> None:
    cells = []
    for name, columns, rows in specs:
        image = base.chroma(base.MASTERS / source_folder / name)
        cells.extend(
            base.grid_cell(image, columns, rows, column, row, 5)
            for row in range(rows)
            for column in range(columns)
        )
    for topic, cell in zip(topics[category], cells):
        image = base.fit_transparent(cell, (128, 128), 4, False)
        write_asset(image, category, topic, assets)


def build_fx(topics: dict[str, list[dict]], assets: list[dict]) -> None:
    rows = []
    for index in range(1, 7):
        image = base.chroma(base.MASTERS / "fx" / f"fx-atlas-{index:02d}.png")
        for row in range(4):
            rows.append([base.grid_cell(image, 4, 4, column, row, 5) for column in range(4)])
    for topic, row in zip(topics["fx"], rows):
        frames = [base.fit_transparent(cell, (64, 64), 2, False) for cell in row]
        write_asset(base.horizontal(frames), "fx", topic, assets)


def build_environment(assets: list[dict]) -> None:
    source = Image.open(base.MASTERS / "terrain" / "terrain-atlas-capital-silverwood.png").convert("RGB")
    # The source board has a dark framing gutter; a generous inset prevents a
    # doubled horizontal seam after mirror-tiling.
    forest_floor = base.grid_cell(source, 4, 2, 0, 1, 24).convert("RGB")
    forest_floor = ImageEnhance.Color(forest_floor).enhance(0.78)
    forest_floor = ImageEnhance.Brightness(forest_floor).enhance(1.08)
    forest_floor = ImageEnhance.Contrast(forest_floor).enhance(0.92)
    target = Image.new("RGB", (21 * 32 * DENSITY, 13 * 32 * DENSITY))
    for row, y in enumerate(range(0, target.height, forest_floor.height)):
        for column, x in enumerate(range(0, target.width, forest_floor.width)):
            tile = forest_floor
            if column % 2:
                tile = ImageOps.mirror(tile)
            if row % 2:
                tile = ImageOps.flip(tile)
            target.paste(tile, (x, y))
    # A restrained green veil unifies mirrored patches without erasing ink detail.
    target = Image.blend(target, Image.new("RGB", target.size, (83, 105, 65)), 0.16)
    write_asset(target.convert("RGBA"), "environment", {"id": "C01-ENV-TWIN-HILLS-GROUND"}, assets)


def main() -> None:
    topics = base.topics_by_category()
    assets: list[dict] = []
    build_units(topics, assets)
    build_terrain(topics, assets)
    build_structures(topics, assets)
    build_grid_category(
        topics,
        assets,
        "battle-prop",
        "props",
        [(f"props-atlas-{index:02d}.png", 4, 2) for index in range(1, 5)],
    )
    build_fx(topics, assets)
    build_environment(assets)
    payload = {
        "schemaVersion": "candidate-01-tactical-runtime-hd-v1",
        "pixelDensity": DENSITY,
        "assetCount": len(assets),
        "categories": ["combat-unit", "mission-unit", "terrain", "interactive-structure", "battle-prop", "fx", "environment"],
        "assets": sorted(assets, key=lambda asset: asset["topicId"]),
    }
    MANIFEST.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"manifest": str(MANIFEST), "assetCount": len(assets)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
