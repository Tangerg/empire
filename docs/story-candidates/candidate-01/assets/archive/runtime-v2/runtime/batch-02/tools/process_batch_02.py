#!/usr/bin/env python3
"""Build and QA C01 runtime-v2 batch 02 from the checked-in ImageGen masters."""

from __future__ import annotations

import hashlib
import json
from collections import Counter
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageOps, ImageStat


BATCH_ROOT = Path(__file__).resolve().parents[1]
ASSET_ROOT = BATCH_ROOT.parents[1]
ALPHA_ROOT = BATCH_ROOT / "intermediate" / "alpha"
MASTER_ROOT = BATCH_ROOT / "masters"
LOCK_PATH = BATCH_ROOT / "TOPIC-LOCK.json"
COMPLETE_PATH = ASSET_ROOT / "manifest-complete.json"
MAIN_MANIFEST = ASSET_ROOT / "manifest-runtime-v2.json"
MANIFEST_PATH = ASSET_ROOT / "manifest-runtime-v2-b02.json"
QA_PATH = ASSET_ROOT / "qa-runtime-v2-b02.json"
EXPECTED_MAIN_SHA256 = "776e0d0967e116f43a0981ceaef62386d942d836e2232b20f2284012422d9d8f"
RESAMPLE = Image.Resampling.LANCZOS
NEAREST = Image.Resampling.NEAREST

UNIT_SPECS = (
    ("C01-UNIT-SWORDSMAN", "swordsman", "units/swordsman-master.png", 32, 48),
    ("C01-UNIT-ENGINEER", "engineer", "units/engineer-master.png", 32, 48),
    ("C01-UNIT-BANNER-GUARD", "banner-guard", "units/banner-guard-master.png", 32, 48),
    ("C01-UNIT-LEGION-SHIELD", "legion-shield", "units/legion-shield-master.png", 32, 48),
    ("C01-UNIT-RUNE-ARTIFICER", "rune-artificer", "units/rune-artificer-master.png", 32, 48),
    ("C01-UNIT-WOLF-RIDER", "wolf-rider", "units/wolf-rider-master.png", 64, 64),
    ("C01-UNIT-GRAVEKEEPER", "gravekeeper", "units/gravekeeper-master.png", 32, 48),
    ("C01-UNIT-SKELETON-GUARD", "skeleton-guard", "units/skeleton-guard-master.png", 32, 48),
)

MISSION_SPECS = (
    ("C01-MISSION-BORDER-FARMER", "border-farmer", "border-farmer-master.png", None),
    ("C01-MISSION-REFUGEE-ADULT", "refugee-adult", "refugee-adult-master.png", None),
    ("C01-MISSION-REFUGEE-CHILD", "refugee-child", "refugee-child-master.png", None),
    ("C01-MISSION-EVACUATION-DRIVER", "evacuation-driver", "mission-board-05-master.png", 0),
    ("C01-MISSION-BAKER", "baker", "mission-board-05-master.png", 1),
    ("C01-MISSION-MINER", "miner", "mission-board-05-master.png", 2),
    ("C01-MISSION-FORGE-ARTISAN", "forge-artisan", "mission-board-05-master.png", 3),
    ("C01-MISSION-BRIDGE-LABORER", "bridge-laborer", "mission-board-05-master.png", 4),
)

TERRAIN_SPECS = (
    ("C01-TERRAIN-BORDER-3", "scorched-farmland", 0, 0, False),
    ("C01-TERRAIN-RIVER-1", "riverbank", 1, 0, False),
    ("C01-TERRAIN-CAPITAL-2", "capital-street", 2, 0, False),
    ("C01-TERRAIN-SILVERWOOD-2", "mother-root", 3, 0, False),
    ("C01-TERRAIN-FORGE-1", "forge-stone", 0, 1, False),
    ("C01-TERRAIN-GRAVEYARD-1", "graveyard", 1, 1, False),
    ("C01-TERRAIN-FORGE-2", "molten-channel", 2, 1, True),
    ("C01-TERRAIN-OATHLIGHT-2", "controlled-oath", 3, 1, True),
)

STRUCTURE_SPECS = (
    ("C01-STRUCT-GRAY-FLAG-POINT", "gray-flag-point", 0),
    ("C01-STRUCT-JOINT-GRANARY", "joint-granary", 1),
    ("C01-STRUCT-FORGE-WORKSHOP", "forge-workshop", 2),
    ("C01-STRUCT-FIELD-HOSPITAL", "field-hospital", 3),
)

PROP_SPECS = (
    ("C01-BPROP-COVER-1", "crate-stack", 0, 0),
    ("C01-BPROP-COVER-2", "shield-wall", 1, 0),
    ("C01-BPROP-COVER-3", "wind-rock", 2, 0),
    ("C01-BPROP-COVER-4", "overturned-grain-cart", 3, 0),
    ("C01-BPROP-HAZARD-1", "oil-brazier", 0, 1),
    ("C01-BPROP-HAZARD-2", "siege-powder", 1, 1),
    ("C01-BPROP-HAZARD-3", "wild-oath-stone", 2, 1),
    ("C01-BPROP-HAZARD-4", "dead-memory-fragment", 3, 1),
)

EQUIPMENT_SPECS = (
    ("C01-EQUIP-SWORDSMAN", "swordsman", 0, 0),
    ("C01-EQUIP-ENGINEER", "engineer", 1, 0),
    ("C01-EQUIP-BANNER-GUARD", "banner-guard", 2, 0),
    ("C01-EQUIP-LEGION-SHIELD", "legion-shield", 3, 0),
    ("C01-EQUIP-RUNE-ARTIFICER", "rune-artificer", 0, 1),
    ("C01-EQUIP-WOLF-RIDER", "wolf-rider", 1, 1),
    ("C01-EQUIP-GRAVEKEEPER", "gravekeeper", 2, 1),
    ("C01-EQUIP-SKELETON-GUARD", "skeleton-guard", 3, 1),
)

SKILL_SPECS = (
    ("C01-SKILL-01", "capture", 0, 0),
    ("C01-SKILL-02", "guard", 1, 0),
    ("C01-SKILL-03", "anti-cavalry", 2, 0),
    ("C01-SKILL-05", "scout", 3, 0),
    ("C01-SKILL-06", "backstab", 0, 1),
    ("C01-SKILL-09", "armor-break", 1, 1),
    ("C01-SKILL-10", "charge", 2, 1),
    ("C01-SKILL-12", "command-reach", 3, 1),
)

STATUS_SPECS = (
    ("C01-STATUS-01", "poisoned", 0, 0),
    ("C01-STATUS-02", "silenced", 1, 0),
    ("C01-STATUS-03", "guarded", 2, 0),
    ("C01-STATUS-04", "oath-controlled", 3, 0),
)

HUD_SPECS = (
    ("C01-HUD-01", "ally", 0, 1),
    ("C01-HUD-02", "enemy", 1, 1),
    ("C01-HUD-03", "neutral", 2, 1),
    ("C01-HUD-04", "recruitable", 3, 1),
)

FX_SPECS = (
    ("C01-FX-02", "pierce", 0, 14, "additive", False),
    ("C01-FX-03", "arrow-hit", 1, 14, "normal", False),
    ("C01-FX-04", "blunt-hit", 2, 12, "additive", False),
    ("C01-FX-09", "autumn-rain", 3, 10, "normal", True),
)

SCENE_SPECS = (
    ("C01-CH01-S02", "three-bridge-ceasefire", "three-bridge-ceasefire-master.png"),
    ("C01-CH02-S09", "ivra-breaks-reins", "ivra-breaks-reins-master.png"),
    ("C01-CH03-S14", "forge-repair", "forge-repair-master.png"),
    ("C01-SCENE-PUBLIC-RATIONS", "public-rations", "public-rations-master.png"),
)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def grid_cell(image: Image.Image, cols: int, rows: int, col: int, row: int, inset: int = 0) -> Image.Image:
    x0 = round(col * image.width / cols) + inset
    x1 = round((col + 1) * image.width / cols) - inset
    y0 = round(row * image.height / rows) + inset
    y1 = round((row + 1) * image.height / rows) - inset
    return image.crop((x0, y0, x1, y1))


def tight_crop(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    bbox = rgba.getchannel("A").point(lambda v: 255 if v >= 24 else 0).getbbox()
    if bbox is None:
        raise ValueError("transparent source cell is empty")
    return rgba.crop(bbox)


def quantize_rgba(image: Image.Image, colors: int = 48, binary: bool = True) -> Image.Image:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    rgb = rgba.convert("RGB").quantize(colors=colors, method=Image.Quantize.MEDIANCUT).convert("RGB")
    if binary:
        alpha = alpha.point(lambda v: 255 if v >= 96 else 0)
    rgb.putalpha(alpha)
    return rgb


def render_group(cells: list[Image.Image], frame_size: tuple[int, int], *, scale_factor: float = 1.0) -> list[Image.Image]:
    frame_w, frame_h = frame_size
    tight = [tight_crop(cell) for cell in cells]
    common = min(
        (frame_w - 2) / max(cell.width for cell in tight),
        (frame_h - 2) / max(cell.height for cell in tight),
    ) * scale_factor
    frames: list[Image.Image] = []
    for cell in tight:
        size = (max(1, round(cell.width * common)), max(1, round(cell.height * common)))
        sprite = tight_crop(quantize_rgba(cell.resize(size, RESAMPLE), 48, True))
        canvas = Image.new("RGBA", frame_size, (0, 0, 0, 0))
        x = (frame_w - sprite.width) // 2
        y = frame_h - sprite.height
        canvas.alpha_composite(sprite, (x, y))
        frames.append(canvas)
    return frames


def render_icon(cell: Image.Image, size: int, colors: int = 40) -> Image.Image:
    crop = tight_crop(cell)
    scale = min((size - 2) / crop.width, (size - 2) / crop.height)
    sprite = tight_crop(quantize_rgba(
        crop.resize((max(1, round(crop.width * scale)), max(1, round(crop.height * scale))), RESAMPLE),
        colors,
        True,
    ))
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    canvas.alpha_composite(sprite, ((size - sprite.width) // 2, (size - sprite.height) // 2))
    return canvas


def horizontal_sheet(frames: list[Image.Image]) -> Image.Image:
    sheet = Image.new("RGBA", (sum(frame.width for frame in frames), max(frame.height for frame in frames)), (0, 0, 0, 0))
    x = 0
    for frame in frames:
        sheet.alpha_composite(frame, (x, 0))
        x += frame.width
    return sheet


def save_pair(image: Image.Image, relative_png: str) -> tuple[str, str]:
    png_path = ASSET_ROOT / relative_png
    png_path.parent.mkdir(parents=True, exist_ok=True)
    image.save(png_path, optimize=True)
    svg_path = png_path.with_suffix(".svg")
    svg_path.write_text(
        "\n".join(
            [
                '<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" '
                f'width="{image.width}" height="{image.height}" viewBox="0 0 {image.width} {image.height}" shape-rendering="crispEdges">',
                f'  <image href="{png_path.name}" xlink:href="{png_path.name}" width="{image.width}" height="{image.height}" style="image-rendering:pixelated"/>',
                "</svg>",
                "",
            ]
        ),
        encoding="utf-8",
    )
    return relative_png, svg_path.relative_to(ASSET_ROOT).as_posix()


def terrain_tone(image: Image.Image) -> Image.Image:
    rgb = ImageOps.fit(image.convert("RGB"), (64, 64), method=RESAMPLE)
    median = rgb.filter(ImageFilter.MedianFilter(3))
    rgb = Image.blend(rgb, median, 0.42)
    gamma = 0.70
    lut = [min(255, round(255 * ((v / 255) ** gamma))) for v in range(256)]
    rgb = rgb.point(lut * 3)
    rgb = ImageEnhance.Contrast(rgb).enhance(1.06)
    rgb = rgb.resize((32, 32), RESAMPLE)
    return rgb.quantize(colors=28, method=Image.Quantize.MEDIANCUT).convert("RGB")


def seal_edges(tiles: list[Image.Image]) -> list[Image.Image]:
    exemplar = tiles[0].convert("RGB")
    h = [exemplar.getpixel((x, 1)) for x in range(32)]
    v = [exemplar.getpixel((1, y)) for y in range(32)]
    corner = exemplar.getpixel((1, 1))
    result: list[Image.Image] = []
    for source in tiles:
        tile = source.convert("RGB")
        for x, color in enumerate(h):
            tile.putpixel((x, 0), color)
            tile.putpixel((x, 31), color)
        for y, color in enumerate(v):
            tile.putpixel((0, y), color)
            tile.putpixel((31, y), color)
        for point in ((0, 0), (31, 0), (0, 31), (31, 31)):
            tile.putpixel(point, corner)
        result.append(tile)
    return result


def material_variants(cell: Image.Image) -> list[Image.Image]:
    square = ImageOps.fit(cell.convert("RGB"), (96, 96), method=RESAMPLE)
    sources = [
        square,
        ImageOps.mirror(square),
        ImageOps.flip(square),
        square.rotate(180),
    ]
    return seal_edges([terrain_tone(source) for source in sources])


def connected_masks(cell: Image.Image, family: str) -> list[Image.Image]:
    background = seal_edges([terrain_tone(cell)])[0]
    if family == "molten-channel":
        border, core, node = (37, 26, 23), (193, 68, 22), (242, 130, 35)
    else:
        border, core, node = (19, 38, 49), (29, 145, 164), (91, 222, 222)
    ends = {1: (16, 0), 2: (31, 16), 4: (16, 31), 8: (0, 16)}
    tiles: list[Image.Image] = []
    for mask in range(16):
        tile = background.copy()
        draw = ImageDraw.Draw(tile)
        if mask == 0:
            draw.rectangle((13, 13, 18, 18), fill=border)
            draw.rectangle((15, 15, 16, 16), fill=node)
        for bit, end in ends.items():
            if mask & bit:
                draw.line((16, 16, end[0], end[1]), fill=border, width=7)
                draw.line((16, 16, end[0], end[1]), fill=core, width=3)
        if mask:
            draw.rectangle((13, 13, 18, 18), fill=border)
            draw.rectangle((15, 15, 16, 16), fill=node)
        tiles.append(tile)
    return tiles


def record(topic_map: dict[str, dict], topic_id: str, **fields: object) -> dict:
    topic = topic_map[topic_id]
    return {
        "id": f"c01-b02-{fields.pop('slug')}",
        "topicId": topic_id,
        "label": topic["label"],
        "contentId": topic["contentId"],
        **fields,
    }


def build() -> tuple[list[dict], dict[str, int]]:
    lock = json.loads(LOCK_PATH.read_text(encoding="utf-8"))
    topic_map = {topic["topicId"]: topic for topic in lock["topics"]}
    assets: list[dict] = []

    # Combat units.
    for topic_id, slug, source_name, fw, fh in UNIT_SPECS:
        source = Image.open(ALPHA_ROOT / Path(source_name).name).convert("RGBA")
        cells = [grid_cell(source, 4, 1, col, 0, 4) for col in range(4)]
        frames = render_group(cells, (fw, fh))
        sheet = horizontal_sheet(frames)
        png, svg = save_pair(sheet, f"runtime-v2/batch-02/units/{slug}.png")
        large = fw > 32
        assets.append(record(
            topic_map, topic_id, slug=f"unit-{slug}", type="combat-unit", png=png, svg=svg,
            width=sheet.width, height=sheet.height, frameWidth=fw, frameHeight=fh, frames=4,
            frameOrder=["idle-a", "step-a", "idle-b", "step-b"], anchor={"x": fw // 2, "y": fh - 1},
            footprint={"columns": 2 if large else 1, "rows": 1}, sourceFacing="right",
            zOrder=12 if large else 10, alphaMode="binary",
            sourceMaster=f"runtime-v2/batch-02/masters/{source_name}",
        ))

    # Mission units.
    for topic_id, slug, source_name, board_row in MISSION_SPECS:
        source = Image.open(ALPHA_ROOT / source_name).convert("RGBA")
        if board_row is None:
            cells = [grid_cell(source, 4, 1, col, 0, 4) for col in range(4)]
        else:
            cells = [grid_cell(source, 4, 5, col, board_row, 3) for col in range(4)]
        frames = render_group(cells, (32, 48), scale_factor=0.78 if slug == "refugee-child" else 1.0)
        sheet = horizontal_sheet(frames)
        png, svg = save_pair(sheet, f"runtime-v2/batch-02/mission-units/{slug}.png")
        assets.append(record(
            topic_map, topic_id, slug=f"mission-{slug}", type="mission-unit", png=png, svg=svg,
            width=128, height=48, frameWidth=32, frameHeight=48, frames=4,
            frameOrder=["idle-a", "step-a", "idle-b", "step-b"], anchor={"x": 16, "y": 47},
            footprint={"columns": 1, "rows": 1}, sourceFacing="right", zOrder=9, alphaMode="binary",
            sourceMaster=f"runtime-v2/batch-02/masters/mission-units/{source_name}",
        ))

    # Terrain: six four-variant families plus two 16-mask families = 56 cells.
    terrain_master = Image.open(MASTER_ROOT / "terrain" / "terrain-board-master.png").convert("RGB")
    for topic_id, slug, col, row, connected in TERRAIN_SPECS:
        cell = grid_cell(terrain_master, 4, 2, col, row, 1)
        tiles = connected_masks(cell, slug) if connected else material_variants(cell)
        sheet = horizontal_sheet([tile.convert("RGBA") for tile in tiles])
        png, svg = save_pair(sheet, f"runtime-v2/batch-02/terrain/{slug}.png")
        fields: dict[str, object] = {
            "type": "terrain", "png": png, "svg": svg, "width": sheet.width, "height": 32,
            "frameWidth": 32, "frameHeight": 32, "frames": len(tiles), "alphaMode": "opaque",
            "footprint": {"columns": 1, "rows": 1},
            "sourceMaster": "runtime-v2/batch-02/masters/terrain/terrain-board-master.png",
        }
        if connected:
            fields.update(tileMode="nesw-mask", maskBits={"north": 1, "east": 2, "south": 4, "west": 8}, maskOrder=list(range(16)))
        else:
            fields.update(tileMode="coordinate-hash-variants", variantOrder=[0, 1, 2, 3], variantSelector="stable-coordinate-hash")
        assets.append(record(topic_map, topic_id, slug=f"terrain-{slug}", **fields))

    # Structures, three horizontal states in a stable 96x96 frame.
    structures = Image.open(ALPHA_ROOT / "structures-board-master.png").convert("RGBA")
    for topic_id, slug, row in STRUCTURE_SPECS:
        cells = [grid_cell(structures, 3, 4, col, row, 4) for col in range(3)]
        states = render_group(cells, (96, 96))
        sheet = horizontal_sheet(states)
        png, svg = save_pair(sheet, f"runtime-v2/batch-02/structures/{slug}.png")
        assets.append(record(
            topic_map, topic_id, slug=f"structure-{slug}", type="interactive-structure", png=png, svg=svg,
            width=288, height=96, frameWidth=96, frameHeight=96, frames=3,
            states=["normal", "damaged", "captured"], stateRows=["normal", "damaged", "captured"],
            stateLayout="horizontal", anchor={"x": 48, "y": 95}, footprint={"columns": 2, "rows": 2},
            collision={"x": 16, "y": 64, "width": 64, "height": 32},
            interactionHotspot={"x": 16, "y": 48, "width": 64, "height": 48}, alphaMode="binary",
            sourceMaster="runtime-v2/batch-02/masters/structures/structures-board-master.png",
        ))

    # Map props.
    props = Image.open(ALPHA_ROOT / "props-board-master.png").convert("RGBA")
    for topic_id, slug, col, row in PROP_SPECS:
        icon = render_icon(grid_cell(props, 4, 2, col, row, 4), 32)
        png, svg = save_pair(icon, f"runtime-v2/batch-02/props/{slug}.png")
        assets.append(record(
            topic_map, topic_id, slug=f"prop-{slug}", type="battle-prop", png=png, svg=svg,
            width=32, height=32, anchor={"x": 16, "y": 31}, footprint={"columns": 1, "rows": 1},
            alphaMode="binary", sourceMaster="runtime-v2/batch-02/masters/atlases/props-board-master.png",
        ))

    # Equipment and skills.
    for category, specs, source_name, folder in (
        ("equipment", EQUIPMENT_SPECS, "equipment-board-master.png", "equipment"),
        ("skill", SKILL_SPECS, "skill-board-master.png", "skills"),
    ):
        source = Image.open(ALPHA_ROOT / source_name).convert("RGBA")
        for topic_id, slug, col, row in specs:
            icon = render_icon(grid_cell(source, 4, 2, col, row, 4), 32)
            png, svg = save_pair(icon, f"runtime-v2/batch-02/{folder}/{slug}.png")
            assets.append(record(
                topic_map, topic_id, slug=f"{category}-{slug}", type=category, png=png, svg=svg,
                width=32, height=32, alphaMode="binary",
                sourceMaster=f"runtime-v2/batch-02/masters/atlases/{source_name}",
            ))

    # Status and HUD icons.
    status_hud = Image.open(ALPHA_ROOT / "status-hud-board-master.png").convert("RGBA")
    for category, specs, folder in (("status", STATUS_SPECS, "status"), ("hud", HUD_SPECS, "hud")):
        for topic_id, slug, col, row in specs:
            icon = render_icon(grid_cell(status_hud, 4, 2, col, row, 4), 24, 32)
            png, svg = save_pair(icon, f"runtime-v2/batch-02/{folder}/{slug}.png")
            assets.append(record(
                topic_map, topic_id, slug=f"{category}-{slug}", type=category, png=png, svg=svg,
                width=24, height=24, alphaMode="binary",
                sourceMaster="runtime-v2/batch-02/masters/atlases/status-hud-board-master.png",
            ))

    # FX rows.
    fx_board = Image.open(ALPHA_ROOT / "fx-board-master.png").convert("RGBA")
    for topic_id, slug, row, fps, blend, loop in FX_SPECS:
        cells = [grid_cell(fx_board, 4, 4, col, row, 4) for col in range(4)]
        frames = [render_icon(cell, 32, 40) for cell in cells]
        sheet = horizontal_sheet(frames)
        png, svg = save_pair(sheet, f"runtime-v2/batch-02/fx/{slug}.png")
        assets.append(record(
            topic_map, topic_id, slug=f"fx-{slug}", type="fx", png=png, svg=svg,
            width=128, height=32, frameWidth=32, frameHeight=32, frames=4, fps=fps,
            blendMode=blend, loop=loop, anchor={"x": 16, "y": 16}, alphaMode="binary",
            sourceMaster="runtime-v2/batch-02/masters/atlases/fx-board-master.png",
        ))

    # Opaque 16:9 scenes.
    for topic_id, slug, source_name in SCENE_SPECS:
        source = Image.open(MASTER_ROOT / "scenes" / source_name).convert("RGB")
        scene = ImageOps.fit(source, (256, 144), method=RESAMPLE)
        scene = scene.quantize(colors=96, method=Image.Quantize.MEDIANCUT).convert("RGB").convert("RGBA")
        png, svg = save_pair(scene, f"runtime-v2/batch-02/scenes/{slug}.png")
        assets.append(record(
            topic_map, topic_id, slug=f"scene-{slug}", type="narrative-scene", png=png, svg=svg,
            width=256, height=144, alphaMode="opaque", dialogueSafeArea={"x": 0, "y": 0, "width": 256, "height": 112},
            sourceMaster=f"runtime-v2/batch-02/masters/scenes/{source_name}",
        ))

    return assets, lock["categoryCounts"]


def normalized_hash(image: Image.Image) -> str:
    return hashlib.sha256(image.convert("RGBA").resize((32, 32), NEAREST).tobytes()).hexdigest()


def validate(assets: list[dict], expected_counts: dict[str, int], preview_asset_ids: dict[str, list[str]]) -> dict:
    errors: list[str] = []
    checks: list[dict] = []
    lock = json.loads(LOCK_PATH.read_text(encoding="utf-8"))
    complete = json.loads(COMPLETE_PATH.read_text(encoding="utf-8"))
    full_topics = {topic["id"]: topic for topic in complete["topics"]}
    locked = {topic["topicId"]: topic for topic in lock["topics"]}
    aliases = {"narrative-scene": "narrative-static"}
    coverage = Counter(aliases.get(asset["type"], asset["type"]) for asset in assets)
    expected_preview_ids = {
        "combat-unit": [asset["id"] for asset in assets if asset["type"] == "combat-unit"],
        "mission-unit": [asset["id"] for asset in assets if asset["type"] == "mission-unit"],
        "terrain": [asset["id"] for asset in assets if asset["type"] == "terrain"],
        "interactive-structure": [asset["id"] for asset in assets if asset["type"] == "interactive-structure"],
        "icons-props-fx": [asset["id"] for asset in assets if asset["type"] in {"battle-prop", "equipment", "skill", "status", "hud", "fx"}],
        "narrative-scene": [asset["id"] for asset in assets if asset["type"] == "narrative-scene"],
    }
    if preview_asset_ids != expected_preview_ids:
        errors.append("preview asset IDs do not cover the locked 8 combat + 8 mission units in order")

    ids = [asset["id"] for asset in assets]
    topic_ids = [asset["topicId"] for asset in assets]
    content_ids = [asset["contentId"] for asset in assets]
    if len(assets) != 68:
        errors.append(f"asset count {len(assets)} != 68")
    for name, values in (("id", ids), ("topicId", topic_ids), ("contentId", content_ids)):
        dupes = [value for value, count in Counter(values).items() if count > 1]
        if dupes:
            errors.append(f"duplicate {name}: {dupes}")
    if set(topic_ids) != set(locked):
        errors.append("manifest topicIds do not exactly equal TOPIC-LOCK")
    if dict(coverage) != expected_counts:
        errors.append(f"coverage {dict(coverage)} != {expected_counts}")
    for asset in assets:
        topic_id = asset["topicId"]
        category = aliases.get(asset["type"], asset["type"])
        full = full_topics.get(topic_id)
        if full is None or full.get("category") != category or full.get("source") != "expanded":
            errors.append(f"{asset['id']}: topic/category/source mismatch")
        if asset["contentId"] != locked[topic_id]["contentId"]:
            errors.append(f"{asset['id']}: contentId differs from lock")
    checks.append({"id": "manifest.lock-category-expanded", "passed": not errors, "count": len(assets)})

    category_hashes: dict[str, list[tuple[str, str]]] = {}
    terrain_cells = 0
    seam_comparisons = 0
    for asset in assets:
        png_path = ASSET_ROOT / asset["png"]
        svg_path = ASSET_ROOT / asset["svg"]
        if not png_path.is_file() or not svg_path.is_file():
            errors.append(f"{asset['id']}: missing PNG/SVG")
            continue
        image = Image.open(png_path).convert("RGBA")
        if image.size != (asset["width"], asset["height"]):
            errors.append(f"{asset['id']}: dimensions differ")
        category = aliases.get(asset["type"], asset["type"])
        category_hashes.setdefault(category, []).append((asset["id"], normalized_hash(image)))
        if category in {"combat-unit", "mission-unit"}:
            fw, fh = asset["frameWidth"], asset["frameHeight"]
            frame_hashes = []
            for frame in range(4):
                cell = image.crop((frame * fw, 0, (frame + 1) * fw, fh))
                bbox = cell.getchannel("A").getbbox()
                if bbox is None or cell.getchannel("A").crop((0, fh - 4, fw, fh)).getbbox() is None:
                    errors.append(f"{asset['id']}: empty or floating frame {frame}")
                frame_hashes.append(hashlib.sha256(cell.tobytes()).hexdigest())
            if len(set(frame_hashes)) != 4:
                errors.append(f"{asset['id']}: animation frames not distinct")
        elif category == "terrain":
            fw = asset["frameWidth"]
            cells = [image.crop((i * fw, 0, (i + 1) * fw, 32)).convert("RGB") for i in range(asset["frames"])]
            terrain_cells += len(cells)
            if len({hashlib.sha256(cell.tobytes()).hexdigest() for cell in cells}) != len(cells):
                errors.append(f"{asset['id']}: duplicate terrain cells")
            lumas = [ImageStat.Stat(cell.convert("L")).mean[0] for cell in cells]
            if min(lumas) < 54:
                errors.append(f"{asset['id']}: dark terrain cell min luma {min(lumas):.1f}")
            if asset["tileMode"] == "coordinate-hash-variants":
                for a in cells:
                    for b in cells:
                        seam_comparisons += 2
                        if list(a.crop((31, 0, 32, 32)).get_flattened_data()) != list(b.crop((0, 0, 1, 32)).get_flattened_data()):
                            errors.append(f"{asset['id']}: horizontal seam mismatch")
                            break
                        if list(a.crop((0, 31, 32, 32)).get_flattened_data()) != list(b.crop((0, 0, 32, 1)).get_flattened_data()):
                            errors.append(f"{asset['id']}: vertical seam mismatch")
                            break
            else:
                for mask, a in enumerate(cells):
                    for bit, opposite, reverse in ((1, 4, "vertical"), (2, 8, "horizontal"), (4, 1, "vertical"), (8, 2, "horizontal")):
                        if not mask & bit:
                            continue
                        for other_mask, b in enumerate(cells):
                            if not other_mask & opposite:
                                continue
                            seam_comparisons += 1
                            if reverse == "horizontal":
                                edge_a = list(a.crop((31 if bit == 2 else 0, 0, 32 if bit == 2 else 1, 32)).get_flattened_data())
                                edge_b = list(b.crop((0 if opposite == 8 else 31, 0, 1 if opposite == 8 else 32, 32)).get_flattened_data())
                            else:
                                edge_a = list(a.crop((0, 0 if bit == 1 else 31, 32, 1 if bit == 1 else 32)).get_flattened_data())
                                edge_b = list(b.crop((0, 31 if opposite == 4 else 0, 32, 32 if opposite == 4 else 1)).get_flattened_data())
                            if edge_a != edge_b:
                                errors.append(f"{asset['id']}: connected mask seam mismatch")
                                break
        elif category == "interactive-structure":
            fw = asset["frameWidth"]
            hashes = [hashlib.sha256(image.crop((i * fw, 0, (i + 1) * fw, 96)).tobytes()).hexdigest() for i in range(3)]
            if len(set(hashes)) != 3:
                errors.append(f"{asset['id']}: structure states not distinct")
        elif category == "fx":
            hashes = [hashlib.sha256(image.crop((i * 32, 0, (i + 1) * 32, 32)).tobytes()).hexdigest() for i in range(4)]
            if len(set(hashes)) != 4:
                errors.append(f"{asset['id']}: FX frames not distinct")
        elif category == "narrative-static":
            if set(image.getchannel("A").get_flattened_data()) != {255}:
                errors.append(f"{asset['id']}: scene is not opaque")

    for category, entries in category_hashes.items():
        duplicates = [value for value, count in Counter(value for _, value in entries).items() if count > 1]
        if duplicates:
            examples = [[asset_id for asset_id, value in entries if value == duplicate] for duplicate in duplicates]
            errors.append(f"exact duplicate {category}: {examples}")

    main_sha = sha256(MAIN_MANIFEST)
    if main_sha != EXPECTED_MAIN_SHA256:
        errors.append(f"primary manifest changed: {main_sha}")
    checks.extend([
        {"id": "files.png-svg-size", "passed": not any("missing PNG/SVG" in error or "dimensions differ" in error for error in errors), "count": len(assets)},
        {"id": "units.frames-grounded-distinct", "passed": not any("frame" in error for error in errors), "assets": 16},
        {"id": "terrain.56-cells-luma-seams", "passed": not any("terrain" in error or "seam" in error or "luma" in error for error in errors), "cells": terrain_cells, "seamComparisons": seam_comparisons},
        {"id": "structures.states-anchor", "passed": not any("structure" in error for error in errors), "assets": 4},
        {"id": "category.normalized-no-exact-duplicates", "passed": not any("exact duplicate" in error for error in errors)},
        {"id": "primary-manifest-unchanged", "passed": main_sha == EXPECTED_MAIN_SHA256, "sha256": main_sha},
        {"id": "previews.explicit-unit-coverage", "passed": preview_asset_ids == expected_preview_ids, "previewAssetIds": preview_asset_ids},
    ])
    return {
        "schemaVersion": "1.0.0",
        "campaignId": "candidate-01",
        "batchId": "b02",
        "qualityTier": "runtime-v2-candidate",
        "runtimeReady": False,
        "passed": not errors,
        "summary": {"assetCount": len(assets), "coverage": dict(coverage), "terrainCells": terrain_cells, "errors": len(errors)},
        "checks": checks,
        "previewAssetIds": preview_asset_ids,
        "errors": errors,
    }


def make_previews(assets: list[dict]) -> dict[str, list[str]]:
    previews = BATCH_ROOT / "previews"
    previews.mkdir(parents=True, exist_ok=True)
    groups = [
        ("units-1x.png", [a for a in assets if a["type"] == "combat-unit"], 1200, 72),
        ("mission-units-1x.png", [a for a in assets if a["type"] == "mission-unit"], 1060, 56),
        ("terrain-1x.png", [a for a in assets if a["type"] == "terrain"], 520, 152),
        ("structures-1x.png", [a for a in assets if a["type"] == "interactive-structure"], 592, 204),
        ("icons-props-fx-1x.png", [a for a in assets if a["type"] in {"battle-prop", "equipment", "skill", "status", "hud", "fx"}], 520, 220),
        ("scenes-1x.png", [a for a in assets if a["type"] == "narrative-scene"], 524, 300),
    ]
    built: list[Image.Image] = []
    preview_keys = ("combat-unit", "mission-unit", "terrain", "interactive-structure", "icons-props-fx", "narrative-scene")
    rendered_ids: dict[str, list[str]] = {key: [] for key in preview_keys}
    for preview_key, (filename, group, width, height) in zip(preview_keys, groups):
        canvas = Image.new("RGBA", (width, height), (20, 23, 29, 255))
        x = y = 4
        row_h = 0
        for asset in group:
            image = Image.open(ASSET_ROOT / asset["png"]).convert("RGBA")
            if x + image.width > width - 4:
                x = 4
                y += row_h + 4
                row_h = 0
            if y + image.height > height - 4:
                break
            canvas.alpha_composite(image, (x, y))
            rendered_ids[preview_key].append(asset["id"])
            x += image.width + 4
            row_h = max(row_h, image.height)
        canvas.save(previews / filename, optimize=True)
        canvas.resize((width * 2, height * 2), NEAREST).save(previews / filename.replace("-1x", "-2x"), optimize=True)
        built.append(canvas)
    overview = Image.new("RGBA", (1024, 576), (16, 18, 23, 255))
    positions = ((0, 0), (512, 0), (0, 128), (512, 128), (0, 320), (512, 280))
    sizes = ((512, 112), (512, 112), (512, 176), (512, 176), (512, 256), (512, 296))
    for image, position, size in zip(built, positions, sizes):
        fitted = ImageOps.contain(image, size, NEAREST)
        overview.alpha_composite(fitted, position)
    overview.save(previews / "overview-1x.png", optimize=True)
    overview.resize((2048, 1152), NEAREST).save(previews / "overview-2x.png", optimize=True)
    return rendered_ids


def main() -> None:
    assets, expected_counts = build()
    manifest = {
        "schemaVersion": "1.0.0",
        "campaignId": "candidate-01",
        "campaignTitle": "断冠之誓",
        "batchId": "b02",
        "qualityTier": "runtime-v2-candidate",
        "runtimeReady": False,
        "extends": "manifest-runtime-v2.json",
        "scope": "additive-batch-02-not-404-complete",
        "source": "built-in-imagegen+official-chroma-key+deterministic-postprocess",
        "topicLock": "runtime-v2/batch-02/TOPIC-LOCK.json",
        "prompts": "runtime-v2/batch-02/PROMPTS.md",
        "assetCount": len(assets),
        "assets": assets,
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    preview_asset_ids = make_previews(assets)
    qa = validate(assets, expected_counts, preview_asset_ids)
    QA_PATH.write_text(json.dumps(qa, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"manifest": str(MANIFEST_PATH), "qa": str(QA_PATH), "passed": qa["passed"], "errors": qa["errors"]}, ensure_ascii=False, indent=2))
    if not qa["passed"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
