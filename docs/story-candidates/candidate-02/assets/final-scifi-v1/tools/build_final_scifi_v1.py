#!/usr/bin/env python3
"""Build the complete C02 cartoon-science-fiction runtime library."""

from __future__ import annotations

import hashlib
import json
import colorsys
import subprocess
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageOps, ImageStat


PACK = Path(__file__).resolve().parents[1]
ASSETS = PACK.parent
MASTERS = PACK / "masters"
ALPHA = PACK / "intermediate" / "alpha"
RUNTIME = PACK / "runtime"
PREVIEWS = PACK / "previews"
TOPIC_SOURCE = ASSETS / "manifest-complete.json"
MANIFEST = PACK / "manifest-final-scifi-v1.json"
QA = PACK / "qa-final-scifi-v1.json"
RESAMPLE = Image.Resampling.LANCZOS

TARGETS = {
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

COMBAT_BOARDS = [
    f"combat-board-{i:02d}.png" for i in range(1, 11)
]

MISSION_BOARDS = [
    f"mission-board-{i:02d}.png" for i in range(1, 7)
]

NARRATIVE_SOURCES = {
    "character-board-01.png": [0, 1, 2, 3],
    "character-board-02.png": [4, 5, 34, 35],
    "character-board-03.png": [36, 37, 38, 39],
    "character-board-04.png": [40, 41, 42, 43],
    "character-board-05.png": [44, 45, None, None],
    "character-board-06.png": [None, None, None, None],
    "prop-board-01.png": [14, 15, 16, 17],
    "prop-board-02.png": [18, 19, 20, 21],
    "prop-board-03.png": [73, 74, 75, 76],
    "prop-board-04.png": [77, 78, 79, None],
    "scene-board-01.png": [6, 7, 8, 9],
    "scene-board-02.png": [10, 11, 12, 13],
    "scene-board-03.png": [22, 23, 24, 25],
    "scene-board-04.png": [26, 27, 28, 29],
    "scene-board-05.png": [30, 31, 32, 33],
    "scene-board-06.png": [46, 47, 48, 49],
    "scene-board-07.png": [50, 51, 52, 53],
    "scene-board-08.png": [54, 55, 56, 57],
    "scene-board-09.png": [58, 59, 60, 61],
    "scene-board-10.png": [62, 63, 64, 65],
    "scene-board-11.png": [66, 67, 68, 69],
    "scene-board-12.png": [70, 71, 72, None],
}


def topics_by_category() -> dict[str, list[dict]]:
    data = json.loads(TOPIC_SOURCE.read_text(encoding="utf-8"))
    return {cat: [t for t in data["topics"] if t["category"] == cat] for cat in TARGETS}


def grid_cell(image: Image.Image, cols: int, rows: int, col: int, row: int, inset: int = 4) -> Image.Image:
    x0 = round(col * image.width / cols) + inset
    x1 = round((col + 1) * image.width / cols) - inset
    y0 = round(row * image.height / rows) + inset
    y1 = round((row + 1) * image.height / rows) - inset
    cell = image.crop((x0, y0, x1, y1))
    if cell.mode == "RGBA":
        cell = clear_white_edge_gutter(cell)
    return cell


def clear_white_edge_gutter(image: Image.Image) -> Image.Image:
    """Flood-remove near-white grid gutters without erasing isolated white art."""
    result = image.copy().convert("RGBA")
    px = result.load()
    width, height = result.size
    queue = []
    seen = set()
    for x in range(width): queue.extend(((x, 0), (x, height - 1)))
    for y in range(height): queue.extend(((0, y), (width - 1, y)))
    while queue:
        x, y = queue.pop()
        if (x, y) in seen: continue
        seen.add((x, y))
        r, g, b, a = px[x, y]
        if not (a == 0 or (r >= 225 and g >= 225 and b >= 225)):
            continue
        px[x, y] = (r, g, b, 0)
        if x: queue.append((x - 1, y))
        if x + 1 < width: queue.append((x + 1, y))
        if y: queue.append((x, y - 1))
        if y + 1 < height: queue.append((x, y + 1))
    return result


def chroma(source: Path) -> Image.Image:
    relative = source.relative_to(MASTERS)
    out = ALPHA / relative
    if not out.exists() or out.stat().st_mtime < source.stat().st_mtime:
        out.parent.mkdir(parents=True, exist_ok=True)
        helper = Path.home() / ".codex/skills/.system/imagegen/scripts/remove_chroma_key.py"
        subprocess.run([
            "python3", str(helper), "--input", str(source), "--out", str(out),
            "--auto-key", "border", "--soft-matte", "--transparent-threshold", "14",
            "--opaque-threshold", "90", "--despill", "--force",
        ], check=True, stdout=subprocess.DEVNULL)
    image = Image.open(out).convert("RGBA")
    # ImageGen sometimes leaves a saturated magenta fringe or a magenta island
    # behind white cell gutters. Remove it explicitly after the border-key pass.
    pixels = []
    for r, g, b, a in image.get_flattened_data():
        if r > 225 and b > 175 and g < 105 and a > 0:
            pixels.append((r, g, b, 0))
        else:
            pixels.append((r, g, b, a))
    image.putdata(pixels)
    return image


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    alpha = image.getchannel("A").point(lambda v: 255 if v >= 28 else 0)
    box = alpha.getbbox()
    if box is None:
        raise ValueError("empty alpha cell")
    return box


def fit_transparent(image: Image.Image, size: tuple[int, int], pad: int = 2, bottom: bool = False) -> Image.Image:
    crop = image.crop(alpha_bbox(image))
    scale = min((size[0] - pad * 2) / crop.width, (size[1] - pad * 2) / crop.height)
    target = (max(1, round(crop.width * scale)), max(1, round(crop.height * scale)))
    crop = crop.resize(target, RESAMPLE)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    x = (size[0] - crop.width) // 2
    y = size[1] - pad - crop.height if bottom else (size[1] - crop.height) // 2
    canvas.alpha_composite(crop, (x, y))
    return canvas


def fit_opaque(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    return ImageOps.fit(image.convert("RGB"), size, method=RESAMPLE, centering=(0.5, 0.5))


def horizontal(frames: list[Image.Image]) -> Image.Image:
    result = Image.new("RGBA", (sum(i.width for i in frames), max(i.height for i in frames)), (0, 0, 0, 0))
    x = 0
    for frame in frames:
        result.alpha_composite(frame, (x, 0))
        x += frame.width
    return result


def vertical(frames: list[Image.Image]) -> Image.Image:
    result = Image.new("RGBA", (max(i.width for i in frames), sum(i.height for i in frames)), (0, 0, 0, 0))
    y = 0
    for frame in frames:
        result.alpha_composite(frame, (0, y))
        y += frame.height
    return result


def write_asset(image: Image.Image, category: str, topic: dict, meta: dict) -> dict:
    slug = topic["id"].lower().replace("c02-", "").replace("_", "-")
    relative = Path("runtime") / category / f"{slug}.png"
    path = PACK / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    if image.mode == "RGBA":
        cleaned = []
        for r, g, b, a in image.get_flattened_data():
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            chroma_spill = 0.82 <= h <= 0.94 and s >= 0.58 and v >= 0.32 and g < max(r, b) * 0.52
            cleaned.append((r, g, b, 0) if (a > 0 and (r > 230 and b > 190 and g < 60 or chroma_spill)) else (r, g, b, a))
        image.putdata(cleaned)
    image.save(path, optimize=True)
    payload = {
        "assetId": f"C02-SFV1-{topic['id'][4:]}",
        "topicId": topic["id"], "label": topic["label"], "category": category,
        "png": relative.as_posix(), "width": image.width, "height": image.height,
        "runtimeReady": True, "qualityTier": "final-cartoon-scifi-v1",
    }
    payload.update(meta)
    return payload


def unit_size(index: int, combat: bool) -> tuple[int, int]:
    if not combat:
        return (64, 48) if index == 5 else (32, 48)
    if index in {3, 4, 21}: return (64, 48)
    if index == 16: return (96, 64)
    if index in {17, 18, 19, 20, 31, 39}: return (64, 64)
    return (32, 48)


def build_units(topics: dict[str, list[dict]], assets: list[dict]) -> None:
    combat_crops: list[list[Image.Image]] = []
    for name in COMBAT_BOARDS:
        im = chroma(MASTERS / "combat" / name)
        for row in range(4):
            combat_crops.append([grid_cell(im, 4, 4, col, row, 5) for col in range(4)])
    assert len(combat_crops) == 40
    for i, (topic, crops) in enumerate(zip(topics["combat-unit"], combat_crops)):
        fw, fh = unit_size(i, True)
        frames = [fit_transparent(c, (fw, fh), 1, True) for c in crops]
        assets.append(write_asset(horizontal(frames), "combat-unit", topic, {
            "frameWidth": fw, "frameHeight": fh, "frames": 4,
            "frameOrder": ["idle", "walk-a", "attack", "walk-b"],
            "anchor": [fw // 2, fh - 1], "footprint": [2, 2] if fw >= 64 else [1, 1],
        }))

    mission_crops: list[list[Image.Image]] = []
    for name in MISSION_BOARDS:
        im = chroma(MASTERS / "mission" / name)
        for row in range(4):
            mission_crops.append([grid_cell(im, 4, 4, col, row, 5) for col in range(4)])
    assert len(mission_crops) == 24
    for i, (topic, crops) in enumerate(zip(topics["mission-unit"], mission_crops)):
        fw, fh = unit_size(i, False)
        frames = [fit_transparent(c, (fw, fh), 1, True) for c in crops]
        assets.append(write_asset(horizontal(frames), "mission-unit", topic, {
            "frameWidth": fw, "frameHeight": fh, "frames": 4,
            "frameOrder": ["idle", "walk-a", "carry-or-work", "walk-b"],
            "anchor": [fw // 2, fh - 1], "footprint": [2, 1] if fw > 32 else [1, 1],
        }))


def build_grid_category(topics: dict[str, list[dict]], assets: list[dict], category: str,
                        sources: list[tuple[str, int, int]], cell_size: tuple[int, int], alpha: bool = True) -> None:
    cells: list[Image.Image] = []
    for name, cols, rows in sources:
        base = MASTERS / ({
            "faction-kit": "factions", "battle-prop": "props", "equipment": "equipment",
            "skill": "skills", "status": "status-hud", "hud": "status-hud",
        }[category]) / name
        im = chroma(base) if alpha else Image.open(base).convert("RGB")
        for r in range(rows):
            for c in range(cols):
                cells.append(grid_cell(im, cols, rows, c, r, 5))
    cells = cells[:len(topics[category])]
    assert len(cells) == len(topics[category])
    for topic, cell in zip(topics[category], cells):
        image = fit_transparent(cell, cell_size, 2, False) if alpha else fit_opaque(cell, cell_size)
        assets.append(write_asset(image, category, topic, {"anchor": [cell_size[0] // 2, cell_size[1] // 2]}))


def build_terrain(topics: dict[str, list[dict]], assets: list[dict]) -> None:
    sources = [
        ("terrain-board-01.png", 4, 2), ("terrain-board-02.png", 4, 2),
        ("terrain-board-03.png", 4, 2), ("terrain-board-04.png", 4, 2),
    ]
    cells: list[Image.Image] = []
    for name, cols, rows in sources:
        im = Image.open(MASTERS / "terrain" / name).convert("RGB")
        cells.extend(grid_cell(im, cols, rows, c, r, 3) for r in range(rows) for c in range(cols))
    assert len(cells) == 32
    connected = {1, 2, 3, 5, 6, 7, 9, 10, 11, 12, 14, 17, 18, 19, 21, 22, 23, 26, 29, 30, 31}
    for index, (topic, cell) in enumerate(zip(topics["terrain"], cells)):
        variants = []
        for k, (cx, cy) in enumerate(((0.32, 0.32), (0.68, 0.32), (0.32, 0.68), (0.68, 0.68))):
            side = round(min(cell.size) * 0.62)
            x0 = max(0, min(cell.width - side, round(cell.width * cx - side / 2)))
            y0 = max(0, min(cell.height - side, round(cell.height * cy - side / 2)))
            tile = cell.crop((x0, y0, x0 + side, y0 + side)).resize((32, 32), RESAMPLE)
            tile = ImageEnhance.Color(tile).enhance(0.82)
            tile = ImageEnhance.Contrast(tile).enhance(0.94)
            variants.append(tile.convert("RGBA"))
        if index in connected:
            texture = variants[0]
            masks = []
            for bits in range(16):
                mask = Image.new("L", (32, 32), 0)
                draw = ImageDraw.Draw(mask)
                draw.rounded_rectangle((10, 10, 21, 21), radius=4, fill=255)
                if bits & 1: draw.rectangle((12, 0, 19, 16), fill=255)
                if bits & 2: draw.rectangle((16, 12, 31, 19), fill=255)
                if bits & 4: draw.rectangle((12, 16, 19, 31), fill=255)
                if bits & 8: draw.rectangle((0, 12, 16, 19), fill=255)
                tile = texture.copy()
                tile.putalpha(mask)
                masks.append(tile)
            assets.append(write_asset(horizontal(masks), "terrain", topic, {
                "tileWidth": 32, "tileHeight": 32, "connectionMasks": 16,
                "maskBits": ["N", "E", "S", "W"], "tileMode": "nesw-16",
            }))
        else:
            assets.append(write_asset(horizontal(variants), "terrain", topic, {
                "tileWidth": 32, "tileHeight": 32, "variants": 4,
                "variantOrder": ["a", "b", "c", "d"], "tileMode": "variants",
            }))


def build_structures(topics: dict[str, list[dict]], assets: list[dict]) -> None:
    specs = [
        *[(f"structures-board-{i:02d}.png", 3, 4) for i in range(1, 7)],
    ]
    groups: list[list[Image.Image]] = []
    for name, cols, rows in specs:
        im = chroma(MASTERS / "structures" / name)
        for r in range(rows):
            groups.append([grid_cell(im, cols, rows, c, r, 5) for c in range(3)])
    assert len(groups) == 24
    for topic, group in zip(topics["interactive-structure"], groups):
        states = [fit_transparent(c, (64, 64), 2, True) for c in group]
        assets.append(write_asset(horizontal(states), "interactive-structure", topic, {
            "frameWidth": 64, "frameHeight": 64, "frames": 3,
            "stateOrder": ["normal", "damaged", "captured"], "anchor": [32, 63],
            "footprint": [2, 2],
        }))


def build_fx(topics: dict[str, list[dict]], assets: list[dict]) -> None:
    rows: list[list[Image.Image]] = []
    for i in range(1, 7):
        source = MASTERS / "fx" / f"fx-board-{i:02d}.png"
        im = chroma(source)
        for r in range(4): rows.append([grid_cell(im, 4, 4, c, r, 5) for c in range(4)])
    assert len(rows) == 24
    for index, (topic, row) in enumerate(zip(topics["fx"], rows)):
        frames = [fit_transparent(c, (32, 32), 1, False) for c in row]
        assets.append(write_asset(horizontal(frames), "fx", topic, {
            "frameWidth": 32, "frameHeight": 32, "frames": 4,
            "fps": 12, "loop": index >= 8,
            "blendMode": "normal", "anchor": [16, 24],
        }))


def build_narrative(topics: dict[str, list[dict]], assets: list[dict]) -> None:
    assigned: dict[int, Image.Image] = {}
    for name, slots in NARRATIVE_SOURCES.items():
        if name.startswith("character"):
            path = MASTERS / "narrative/characters" / name
        elif name.startswith("prop"):
            path = MASTERS / "narrative/props" / name
        else:
            path = MASTERS / "narrative/scenes" / name
        is_prop = name.startswith("prop")
        im = chroma(path) if is_prop else Image.open(path).convert("RGB")
        for cell_index, slot in enumerate(slots):
            if slot is None or slot in assigned: continue
            assigned[slot] = grid_cell(im, 2, 2, cell_index % 2, cell_index // 2, 5)
    # No placeholder topic is allowed. The source boards must cover the exact 80-topic set.
    missing = [i for i in range(80) if i not in assigned]
    if missing:
        raise ValueError(f"narrative source mapping missing slots: {missing}")
    assert len(assigned) == 80
    for i, topic in enumerate(topics["narrative-static"]):
        cell = assigned[i]
        if i in set(range(0, 6)) | set(range(34, 46)):
            image = fit_opaque(cell, (192, 224)).convert("RGBA")
            kind = "character-card"
        elif i in set(range(14, 22)) | {73, 74, 75, 76, 77, 78, 79}:
            if cell.mode == "RGBA": image = fit_transparent(cell, (128, 128), 4, False)
            else: image = fit_opaque(cell, (128, 128)).convert("RGBA")
            kind = "key-prop"
        else:
            image = fit_opaque(cell, (256, 144)).convert("RGBA")
            kind = "scene"
        assets.append(write_asset(image, "narrative-static", topic, {"kind": kind}))


def contact_sheet(assets: list[dict], filename: str, category_filter: set[str], thumb=(96, 96), cols=8) -> None:
    chosen = [a for a in assets if a["category"] in category_filter]
    rows = (len(chosen) + cols - 1) // cols
    canvas = Image.new("RGB", (cols * thumb[0], rows * (thumb[1] + 18)), (36, 39, 40))
    draw = ImageDraw.Draw(canvas)
    for i, asset in enumerate(chosen):
        im = Image.open(PACK / asset["png"]).convert("RGBA")
        if asset.get("frames"):
            im = im.crop((0, 0, asset.get("frameWidth", im.width), asset.get("frameHeight", im.height)))
        elif asset.get("connectionMasks"):
            fw, fh = asset["tileWidth"], asset["tileHeight"]
            im = im.crop((15 * fw, 0, 16 * fw, fh))
        elif asset.get("variants"):
            im = im.crop((0, 0, asset["tileWidth"], asset["tileHeight"]))
        preview = Image.new("RGBA", thumb, (69, 72, 71, 255))
        fitted = ImageOps.contain(im, (thumb[0] - 6, thumb[1] - 6), method=RESAMPLE)
        preview.alpha_composite(fitted, ((thumb[0] - fitted.width)//2, (thumb[1] - fitted.height)//2))
        x, y = (i % cols) * thumb[0], (i // cols) * (thumb[1] + 18)
        canvas.paste(preview.convert("RGB"), (x, y))
        draw.text((x + 3, y + thumb[1] + 2), asset["topicId"].replace("c02-", "")[:16], fill=(220, 216, 201))
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    canvas.save(PREVIEWS / filename, optimize=True)


def validate(assets: list[dict], topics: dict[str, list[dict]]) -> dict:
    errors: list[str] = []
    if len(assets) != 404: errors.append(f"asset count {len(assets)} != 404")
    ids = [a["topicId"] for a in assets]
    expected = [t["id"] for cat in TARGETS for t in topics[cat]]
    if len(set(ids)) != len(ids): errors.append("duplicate topicId")
    if set(ids) != set(expected): errors.append("topic coverage mismatch")
    coverage = {}
    magenta = 0
    frame_unique_failures = []
    asset_hashes: dict[str, list[str]] = {}
    structure_state_failures = []
    for cat, target in TARGETS.items():
        count = sum(a["category"] == cat for a in assets)
        coverage[cat] = {"target": target, "actual": count}
        if count != target: errors.append(f"{cat}: {count} != {target}")
    for asset in assets:
        path = PACK / asset["png"]
        if not path.exists(): errors.append(f"missing {path}"); continue
        im = Image.open(path).convert("RGBA")
        asset_hashes.setdefault(hashlib.sha256(im.tobytes()).hexdigest(), []).append(asset["topicId"])
        if im.size != (asset["width"], asset["height"]): errors.append(f"size mismatch {path}")
        for r, g, b, a in im.get_flattened_data():
            if a > 200 and r > 230 and b > 190 and g < 60: magenta += 1
        if asset.get("frames") == 4:
            fw, fh = asset["frameWidth"], asset["frameHeight"]
            hashes = [hashlib.sha256(im.crop((i*fw, 0, (i+1)*fw, fh)).tobytes()).hexdigest() for i in range(4)]
            if len(set(hashes)) < 2: frame_unique_failures.append(asset["topicId"])
        if asset["category"] == "interactive-structure":
            fw, fh = asset["frameWidth"], asset["frameHeight"]
            hashes = [hashlib.sha256(im.crop((i*fw, 0, (i+1)*fw, fh)).tobytes()).hexdigest() for i in range(3)]
            if len(set(hashes)) < 3: structure_state_failures.append(asset["topicId"])
    duplicate_assets = [ids for ids in asset_hashes.values() if len(ids) > 1]
    if magenta: errors.append(f"opaque magenta pixels: {magenta}")
    if frame_unique_failures: errors.append(f"static four-frame sheets: {len(frame_unique_failures)}")
    if structure_state_failures: errors.append(f"static structure states: {len(structure_state_failures)}")
    if duplicate_assets: errors.append(f"exact duplicate runtime assets: {len(duplicate_assets)}")
    return {
        "passed": not errors, "assetCount": len(assets), "coverage": coverage,
        "opaqueMagentaPixels": magenta, "frameUniqueFailures": frame_unique_failures,
        "structureStateFailures": structure_state_failures, "duplicateRuntimeAssets": duplicate_assets,
        "errors": errors,
    }


def main() -> None:
    topics = topics_by_category()
    for cat, target in TARGETS.items():
        assert len(topics[cat]) == target, (cat, len(topics[cat]), target)
    assets: list[dict] = []
    build_units(topics, assets)
    build_grid_category(topics, assets, "faction-kit", [("faction-kits-board.png", 3, 4)], (128, 128))
    build_terrain(topics, assets)
    build_structures(topics, assets)
    build_grid_category(topics, assets, "battle-prop", [(f"props-board-{i:02d}.png", 4, 2) for i in range(1, 5)], (64, 64))
    build_grid_category(topics, assets, "equipment", [
        ("equipment-board-01.png", 4, 4), ("equipment-board-02.png", 4, 4),
        ("equipment-board-03.png", 4, 4),
    ], (48, 48))
    build_grid_category(topics, assets, "skill", [(f"skills-board-{i:02d}.png", 4, 4) for i in range(1, 4)], (48, 48))
    build_grid_category(topics, assets, "status", [("status-board-01.png", 4, 4), ("status-board-02.png", 4, 2)], (32, 32))
    build_fx(topics, assets)
    build_grid_category(topics, assets, "hud", [("hud-board-01.png", 4, 4)], (32, 32))
    build_narrative(topics, assets)
    order = {k: i for i, k in enumerate(TARGETS)}
    assets.sort(key=lambda a: (order[a["category"]], a["topicId"]))
    manifest = {
        "schemaVersion": "final-scifi-v1.0", "campaignId": "candidate-02",
        "title": "候选剧本二 · 卡通科幻完整游戏素材包",
        "artDirection": {
            "genre": "original five-world grounded science fiction",
            "style": "cartoon tactical game; oversized heads; compact bodies; thick ink; matte restrained palette; two-step cel shading",
            "reference": "../../art-assets/reference/art-direction-map.png",
            "prohibitions": ["generic armor recolors", "medieval fantasy", "photorealism", "glossy 3D", "named-franchise copying"],
        },
        "runtimeReady": True, "qualityTier": "final-cartoon-scifi-v1",
        "targetTopics": 404, "categoryTargets": TARGETS, "assetCount": len(assets), "assets": assets,
    }
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    qa = validate(assets, topics)
    QA.write_text(json.dumps(qa, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    contact_sheet(assets, "units-1x.png", {"combat-unit", "mission-unit"}, cols=8)
    contact_sheet(assets, "map-assets-1x.png", {"faction-kit", "terrain", "interactive-structure", "battle-prop"}, cols=8)
    contact_sheet(assets, "icons-fx-1x.png", {"equipment", "skill", "status", "fx", "hud"}, cols=10)
    contact_sheet(assets, "narrative-1x.png", {"narrative-static"}, thumb=(128, 96), cols=6)
    print(json.dumps(qa, ensure_ascii=False, indent=2))
    if not qa["passed"]: raise SystemExit(1)


if __name__ == "__main__":
    main()
