#!/usr/bin/env python3
"""Build C03 runtime-v2 Batch 03: the final 28 combat-unit topics.

Inputs are preserved built-in ImageGen mothers in ``masters/``. Outputs are
additive and isolated to Batch 03; Primary and Batch 02 are read for QA only.
"""

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
UNITS = HERE / "units"
PREVIEWS = HERE / "previews"
MANIFEST_PATH = ASSETS / "manifest-runtime-v2-b03.json"
QA_PATH = ASSETS / "qa-runtime-v2-b03.json"
TOPIC_LOCK_PATH = HERE / "TOPIC-LOCK.json"
COMPLETE_PATH = ASSETS / "manifest-complete.json"
PRIMARY_PATH = ASSETS / "manifest-runtime-v2.json"
B02_PATH = ASSETS / "manifest-runtime-v2-b02.json"

LANCZOS = Image.Resampling.LANCZOS
NEAREST = Image.Resampling.NEAREST
NO_DITHER = Image.Dither.NONE
FRAME_ORDER = ["idle-a", "step-a", "idle-b", "step-b"]
PRIMARY_TOPIC_IDS = {
    "c03-unit-4c02a584",  # 刀盾卒 -> grain.unit.saber-shield
    "c03-unit-9ad7ca0f",  # 长枪阵 -> grain.unit.spear-line
    "c03-unit-915fc486",  # 乡弓手 -> grain.unit.rural-archer
    "c03-unit-79f06a6f",  # 河工 -> grain.unit.river-engineer
}


@dataclass(frozen=True)
class UnitSpec:
    topic_id: str
    label: str
    content_id: str
    stem: str
    master: str
    row: int
    frame_width: int = 32
    frame_height: int = 48
    footprint_columns: int = 1
    footprint_rows: int = 1
    z_order: str = "unit"

    @property
    def asset_id(self) -> str:
        return f"c03-b03-unit-{self.stem}"

    @property
    def png(self) -> Path:
        return UNITS / f"{self.stem}.png"

    @property
    def svg(self) -> Path:
        return UNITS / f"{self.stem}.svg"


SPECS = [
    UnitSpec("c03-unit-fd4c21da", "火船队", "grain.unit.fire-ship", "fire-ship", "units-b03-01-engineering-master.png", 0, 96, 64, 3, 2, "vessel"),
    UnitSpec("c03-unit-2be16bd2", "架桥营", "grain.unit.bridge-corps", "bridge-corps", "units-b03-01-engineering-master.png", 1, 64, 48, 2, 1, "engineering-large"),
    UnitSpec("c03-unit-4dad36ef", "云梯队", "grain.unit.ladder-corps", "ladder-corps", "units-b03-01-engineering-master.png", 2, 64, 48, 2, 1, "engineering-large"),
    UnitSpec("c03-unit-0609e6e6", "炮石营", "grain.unit.catapult-crew", "catapult-crew", "units-b03-01-engineering-master.png", 3, 64, 48, 2, 1, "siege-engine"),
    UnitSpec("c03-unit-3d86046f", "火铳手", "grain.unit.musketeer", "musketeer", "units-b03-02-gunpowder-support-master.png", 0),
    UnitSpec("c03-unit-5e64c3a6", "火箭营", "grain.unit.fire-arrow-corps", "fire-arrow-corps", "units-b03-02-gunpowder-support-master.png", 1),
    UnitSpec("c03-unit-9881f2a3", "震天雷队", "grain.unit.thunder-bomb-corps", "thunder-bomb-corps", "units-b03-02-gunpowder-support-master.png", 2),
    UnitSpec("c03-unit-4566788b", "军医", "grain.unit.field-medic", "field-medic", "units-b03-02-gunpowder-support-master.png", 3),
    UnitSpec("c03-unit-b59c3f96", "旗鼓手", "grain.unit.drummer-standard", "drummer-standard", "units-b03-03-command-intel-master.png", 0),
    UnitSpec("c03-unit-a53b725f", "粮秣官", "grain.unit.quartermaster", "quartermaster", "units-b03-03-command-intel-master.png", 1),
    UnitSpec("c03-unit-8f49e6ef", "军师", "grain.unit.strategist", "strategist", "units-b03-03-command-intel-master.png", 2),
    UnitSpec("c03-unit-8842d111", "斥候司", "grain.unit.scout-office-agent", "scout-office-agent", "units-b03-03-command-intel-master.png", 3),
    UnitSpec("c03-unit-75ee37ba", "说客", "grain.unit.envoy", "envoy", "units-b03-04-garrison-master.png", 0),
    UnitSpec("c03-unit-f0a4ddaa", "乡勇", "grain.unit.village-militia", "village-militia", "units-b03-04-garrison-master.png", 1),
    UnitSpec("c03-unit-76cd6822", "城门盾兵", "grain.unit.gate-shield-guard", "gate-shield-guard", "units-b03-04-garrison-master.png", 2),
    UnitSpec("c03-unit-73d2bcb4", "将领亲兵", "grain.unit.commander-retinue", "commander-retinue", "units-b03-04-garrison-master.png", 3),
    UnitSpec("c03-unit-6a62b51e", "铁锤破阵手", "grain.unit.warhammer-breaker", "warhammer-breaker", "units-b03-05-assault-defense-master.png", 0),
    UnitSpec("c03-unit-4377f3e4", "筑城守备", "grain.unit.fortification-guard", "fortification-guard", "units-b03-05-assault-defense-master.png", 1),
    UnitSpec("c03-unit-3f6fea2f", "潜渡凿船手", "grain.unit.covert-hull-saboteur", "covert-hull-saboteur", "units-b03-05-assault-defense-master.png", 2),
    UnitSpec("c03-unit-6d03b909", "漕运护军", "grain.unit.canal-escort", "canal-escort", "units-b03-05-assault-defense-master.png", 3),
    UnitSpec("c03-unit-c30bf360", "盐仓护卫", "grain.unit.salt-store-guard", "salt-store-guard", "units-b03-06-border-elite-master.png", 0),
    UnitSpec("c03-unit-747800db", "雪原骑射", "grain.unit.snowfield-horse-archer", "snowfield-horse-archer", "units-b03-06-border-elite-master.png", 1, 64, 48, 2, 1, "mounted"),
    UnitSpec("c03-unit-118c7423", "互市护卫", "grain.unit.frontier-market-guard", "frontier-market-guard", "units-b03-06-border-elite-master.png", 2),
    UnitSpec("c03-unit-4619e546", "宁朝禁军", "grain.unit.ning-imperial-guard", "ning-imperial-guard", "units-b03-06-border-elite-master.png", 3),
    UnitSpec("c03-unit-3f4a6e1d", "密察司缉事", "grain.unit.secret-inspector", "secret-inspector", "units-b03-07-covert-reintegration-master.png", 0),
    UnitSpec("c03-unit-0c42f6d3", "地道营", "grain.unit.tunnel-corps", "tunnel-corps", "units-b03-07-covert-reintegration-master.png", 1),
    UnitSpec("c03-unit-2bc4ddf7", "火油防备队", "grain.unit.fire-oil-defense", "fire-oil-defense", "units-b03-07-covert-reintegration-master.png", 2),
    UnitSpec("c03-unit-cecc448d", "降兵整编队", "grain.unit.reorganized-surrendered", "reorganized-surrendered", "units-b03-07-covert-reintegration-master.png", 3),
]


def setup() -> None:
    for directory in (INTERMEDIATE, UNITS, PREVIEWS):
        directory.mkdir(parents=True, exist_ok=True)


def chroma_helper() -> Path:
    codex_root = Path(os.environ.get("CODEX_HOME", Path.home() / ".codex"))
    helper = codex_root / "skills/.system/imagegen/scripts/remove_chroma_key.py"
    if not helper.is_file():
        raise FileNotFoundError(helper)
    return helper


def remove_keys() -> None:
    helper = chroma_helper()
    for source in sorted({spec.master for spec in SPECS}):
        target = INTERMEDIATE / source.replace("-master.png", "-alpha.png")
        subprocess.run(
            [
                sys.executable,
                str(helper),
                "--input",
                str(MASTERS / source),
                "--out",
                str(target),
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
    """Keep the centered connected subject and reject neighboring-cell bleed."""
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
        center_weight = 1.0 if mask.width * 0.12 <= mean_x <= mask.width * 0.88 else 0.15
        return count * center_weight

    _, selected = max(groups, key=score)
    keep = Image.new("L", rgba.size, 0)
    keep_pixels = keep.load()
    for x, y in selected:
        keep_pixels[x, y] = 255
    keep = keep.filter(ImageFilter.MaxFilter(3))
    rgba.putalpha(ImageChops.multiply(rgba.getchannel("A"), keep))
    return rgba


def quant_rgba(image: Image.Image, colors: int = 64) -> Image.Image:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A").point(lambda value: 255 if value >= 64 else 0)
    rgb = Image.new("RGB", rgba.size)
    rgb.paste(rgba.convert("RGB"), mask=alpha)
    rgb = rgb.quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=NO_DITHER).convert("RGB")
    result = rgb.convert("RGBA")
    result.putalpha(alpha)
    return result


def ground(image: Image.Image) -> Image.Image:
    box = alpha_bbox(image)
    if box is None:
        raise ValueError("cannot ground empty image")
    if box[3] == image.height:
        return image
    shifted = Image.new("RGBA", image.size)
    shifted.alpha_composite(image, (0, image.height - box[3]))
    return shifted


def fit_frame(source: Image.Image, width: int, height: int) -> Image.Image:
    source = crop_alpha(isolate_primary(source))
    source = ImageOps.mirror(source)  # mothers face left; runtime source faces right
    limit_width = width - 2
    limit_height = height - 2
    scale = min(limit_width / source.width, limit_height / source.height)
    resized = source.resize((max(1, round(source.width * scale)), max(1, round(source.height * scale))), LANCZOS)
    canvas = Image.new("RGBA", (width, height))
    canvas.alpha_composite(resized, ((width - resized.width) // 2, height - resized.height))
    return ground(quant_rgba(canvas, 48 if width == 32 else 72))


def save_pair(image: Image.Image, spec: UnitSpec) -> None:
    image.save(spec.png, optimize=True)
    encoded = base64.b64encode(spec.png.read_bytes()).decode("ascii")
    spec.svg.write_text(
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{image.width}" height="{image.height}" viewBox="0 0 {image.width} {image.height}">\n'
        f'  <image width="{image.width}" height="{image.height}" style="image-rendering:pixelated" href="data:image/png;base64,{encoded}"/>\n'
        "</svg>\n",
        encoding="utf-8",
    )


def build_units() -> None:
    opened: dict[str, Image.Image] = {}
    for spec in SPECS:
        alpha_name = spec.master.replace("-master.png", "-alpha.png")
        master = opened.setdefault(alpha_name, Image.open(INTERMEDIATE / alpha_name).convert("RGBA"))
        frames = [fit_frame(cell(master, 4, 4, col, spec.row), spec.frame_width, spec.frame_height) for col in range(4)]
        sheet = Image.new("RGBA", (spec.frame_width * 4, spec.frame_height))
        for index, frame in enumerate(frames):
            sheet.alpha_composite(frame, (index * spec.frame_width, 0))
        save_pair(sheet, spec)


def rel(path: Path) -> str:
    return path.relative_to(ASSETS).as_posix()


def manifest_asset(spec: UnitSpec) -> dict[str, Any]:
    return {
        "id": spec.asset_id,
        "topicId": spec.topic_id,
        "label": spec.label,
        "contentId": spec.content_id,
        "type": "combat-unit",
        "png": rel(spec.png),
        "svg": rel(spec.svg),
        "width": spec.frame_width * 4,
        "height": spec.frame_height,
        "sourceMaster": f"runtime-v2/batch-03/masters/{spec.master}",
        "frameWidth": spec.frame_width,
        "frameHeight": spec.frame_height,
        "frames": 4,
        "frameOrder": FRAME_ORDER,
        "anchor": {"x": spec.frame_width // 2, "y": spec.frame_height - 1},
        "footprint": {"columns": spec.footprint_columns, "rows": spec.footprint_rows},
        "sourceFacing": "right",
        "zOrder": spec.z_order,
    }


def write_manifest() -> None:
    manifest = {
        "schemaVersion": "1.0.0",
        "campaignId": "candidate-03",
        "campaignTitle": "布衣定鼎",
        "batchId": "b03",
        "qualityTier": "runtime-v2-candidate",
        "runtimeReady": False,
        "extends": "manifest-runtime-v2-b02.json",
        "source": "built-in-imagegen+official-chroma-key+deterministic-postprocess",
        "topicLock": "runtime-v2/batch-03/TOPICS.md",
        "notes": "Additive combat-unit closure batch. Primary and Batch 02 remain unchanged; cumulative topic coverage is 40/40, pending in-engine acceptance.",
        "assetCount": len(SPECS),
        "assets": [manifest_asset(spec) for spec in SPECS],
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_topic_lock() -> None:
    batch_02 = json.loads(B02_PATH.read_text(encoding="utf-8"))
    batch_02_topics = sorted(
        asset["topicId"] for asset in batch_02["assets"]
        if asset.get("type") == "combat-unit"
    )
    lock = {
        "schemaVersion": "1.0.0",
        "campaignId": "candidate-03",
        "batchId": "b03",
        "runtimeReady": False,
        "sourceManifest": "manifest-complete.json",
        "primarySemanticMapping": {
            "grain.unit.saber-shield": "c03-unit-4c02a584",
            "grain.unit.spear-line": "c03-unit-9ad7ca0f",
            "grain.unit.rural-archer": "c03-unit-915fc486",
            "grain.unit.river-engineer": "c03-unit-79f06a6f",
        },
        "primaryTopicIds": sorted(PRIMARY_TOPIC_IDS),
        "batch02TopicIds": batch_02_topics,
        "previousCombatCoverage": 12,
        "batchCombatCoverage": 28,
        "cumulativeCombatCoverage": 40,
        "topics": [
            {
                "topicId": spec.topic_id,
                "label": spec.label,
                "category": "combat-unit",
                "contentId": spec.content_id,
                "frameWidth": spec.frame_width,
                "frameHeight": spec.frame_height,
            }
            for spec in SPECS
        ],
    }
    TOPIC_LOCK_PATH.write_text(json.dumps(lock, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def checker(size: tuple[int, int], unit: int = 4) -> Image.Image:
    image = Image.new("RGB", size, "#26313a")
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], unit):
        for x in range(0, size[0], unit):
            if (x // unit + y // unit) % 2:
                draw.rectangle((x, y, x + unit - 1, y + unit - 1), fill="#33424c")
    return image


def load_font(size: int) -> ImageFont.ImageFont:
    candidates = [
        Path("/System/Library/Fonts/PingFang.ttc"),
        Path("/System/Library/Fonts/STHeiti Medium.ttc"),
        Path("/Library/Fonts/Arial Unicode.ttf"),
    ]
    for path in candidates:
        if path.is_file():
            try:
                return ImageFont.truetype(str(path), size)
            except OSError:
                pass
    return ImageFont.load_default()


def make_previews() -> None:
    width, height = 1120, 1060
    canvas = checker((width, height), 4)
    draw = ImageDraw.Draw(canvas)
    title_font, label_font = load_font(16), load_font(12)
    draw.text((12, 8), "C03 Runtime V2 · Batch 03 · Combat 28 · 1×", fill="#f0dfba", font=title_font)
    columns = [12, 570]
    y_positions = [38, 38]
    for index, spec in enumerate(SPECS):
        column = 0 if index < 14 else 1
        x, y = columns[column], y_positions[column]
        image = Image.open(spec.png).convert("RGBA")
        canvas.paste(image, (x, y), image)
        draw.text((x + 396, y + max(0, spec.frame_height // 2 - 7)), f"{spec.label}  {spec.frame_width}×{spec.frame_height}", fill="#f0dfba", font=label_font)
        y_positions[column] += max(64, spec.frame_height + 10)
    canvas.save(PREVIEWS / "combat-units-1x.png", optimize=True)
    canvas.save(PREVIEWS / "c03-v2-b03-combat-preview-1x.png", optimize=True)
    canvas.resize((width * 2, height * 2), NEAREST).save(PREVIEWS / "combat-units-2x.png", optimize=True)
    canvas.resize((width * 2, height * 2), NEAREST).save(PREVIEWS / "c03-v2-b03-combat-preview-2x.png", optimize=True)


def normalized_cell_hash(frame: Image.Image) -> str:
    rgba = frame.convert("RGBA")
    box = alpha_bbox(rgba)
    if box is None:
        return "empty"
    crop = rgba.crop(box)
    target = Image.new("RGBA", (64, 64))
    scale = min(62 / crop.width, 62 / crop.height)
    resized = crop.resize((max(1, round(crop.width * scale)), max(1, round(crop.height * scale))), NEAREST)
    target.alpha_composite(resized, ((64 - resized.width) // 2, 64 - resized.height))
    return hashlib.sha256(target.tobytes()).hexdigest()


def qa() -> dict[str, Any]:
    errors: list[str] = []
    checks: list[dict[str, Any]] = []
    complete = json.loads(COMPLETE_PATH.read_text(encoding="utf-8"))
    topics = {topic["id"]: topic for topic in complete["topics"]}
    combat_topics = {topic["id"] for topic in complete["topics"] if topic["category"] == "combat-unit"}
    b02 = json.loads(B02_PATH.read_text(encoding="utf-8"))
    b02_topics = {asset["topicId"] for asset in b02["assets"] if asset["type"] == "combat-unit"}
    b02_content = {asset["contentId"] for asset in b02["assets"]}
    primary = json.loads(PRIMARY_PATH.read_text(encoding="utf-8"))
    primary_content = {asset["contentId"] for asset in primary["assets"]}
    batch_topics = {spec.topic_id for spec in SPECS}
    batch_content = {spec.content_id for spec in SPECS}

    topic_mapping_ok = all(
        spec.topic_id in topics
        and topics[spec.topic_id]["category"] == "combat-unit"
        and topics[spec.topic_id]["source"] == "expanded"
        and topics[spec.topic_id]["label"] == spec.label
        for spec in SPECS
    )
    checks.append({"id": "manifest.topic-category-label-expanded", "passed": topic_mapping_ok, "count": len(SPECS)})
    if not topic_mapping_ok:
        errors.append("topic/category/label/source mapping failed")

    unique_ok = len(SPECS) == 28 == len({spec.asset_id for spec in SPECS}) == len(batch_topics) == len(batch_content)
    overlap_ok = not (batch_topics & (PRIMARY_TOPIC_IDS | b02_topics)) and not (batch_content & (primary_content | b02_content))
    checks.append({"id": "manifest.unique-id-topic-content", "passed": unique_ok})
    checks.append({"id": "manifest.no-primary-b02-overlap", "passed": overlap_ok})
    if not unique_ok:
        errors.append("Batch 03 id/topic/content uniqueness failed")
    if not overlap_ok:
        errors.append("Batch 03 overlaps Primary or Batch 02")

    cumulative = PRIMARY_TOPIC_IDS | b02_topics | batch_topics
    cumulative_ok = len(combat_topics) == 40 and cumulative == combat_topics and len(PRIMARY_TOPIC_IDS) == 4 and len(b02_topics) == 8 and len(batch_topics) == 28
    checks.append(
        {
            "id": "coverage.combat-unit-40-of-40",
            "passed": cumulative_ok,
            "target": 40,
            "primarySemanticMappings": 4,
            "batch02": 8,
            "batch03": 28,
            "covered": len(cumulative),
            "missingTopicIds": sorted(combat_topics - cumulative),
            "unexpectedTopicIds": sorted(cumulative - combat_topics),
        }
    )
    if not cumulative_ok:
        errors.append("cumulative combat topic coverage is not exact 40/40")

    files_ok = True
    unit_rows: list[dict[str, Any]] = []
    exact_frame_hashes: list[str] = []
    normalized_hashes: list[str] = []
    silhouette_hashes: list[str] = []
    for spec in SPECS:
        expected_size = (spec.frame_width * 4, spec.frame_height)
        if not spec.png.is_file() or not spec.svg.is_file():
            files_ok = False
            errors.append(f"missing PNG/SVG: {spec.asset_id}")
            continue
        image = Image.open(spec.png).convert("RGBA")
        if image.size != expected_size:
            files_ok = False
            errors.append(f"wrong sheet size: {spec.asset_id}")
            continue
        frame_boxes = []
        frame_hashes = []
        alpha_binary = True
        corners_clear = True
        for frame_index in range(4):
            frame = image.crop((frame_index * spec.frame_width, 0, (frame_index + 1) * spec.frame_width, spec.frame_height))
            box = alpha_bbox(frame)
            frame_boxes.append(box)
            frame_hash = hashlib.sha256(frame.tobytes()).hexdigest()
            frame_hashes.append(frame_hash)
            exact_frame_hashes.append(frame_hash)
            normalized_hashes.append(normalized_cell_hash(frame))
            alpha = frame.getchannel("A")
            alpha_binary = alpha_binary and set(alpha.getdata()).issubset({0, 255})
            silhouette_hashes.append(hashlib.sha256(alpha.tobytes()).hexdigest())
            corners_clear = corners_clear and all(
                alpha.getpixel(point) == 0
                for point in [(0, 0), (spec.frame_width - 1, 0), (0, spec.frame_height - 1), (spec.frame_width - 1, spec.frame_height - 1)]
            )
        grounded = all(box is not None and box[3] == spec.frame_height for box in frame_boxes)
        distinct = len(set(frame_hashes)) == 4
        passed = grounded and distinct and alpha_binary and corners_clear
        unit_rows.append(
            {
                "id": spec.asset_id,
                "topicId": spec.topic_id,
                "passed": passed,
                "frameBoxes": frame_boxes,
                "fourDistinctFrames": distinct,
                "binaryAlpha": alpha_binary,
                "transparentCorners": corners_clear,
            }
        )
        if not passed:
            errors.append(f"frames/anchor/alpha failed: {spec.asset_id}")
    checks.append({"id": "files.png-svg-dimensions", "passed": files_ok, "count": len(SPECS)})
    checks.append({"id": "units.four-frames-grounded-alpha", "passed": files_ok and all(row["passed"] for row in unit_rows), "assets": unit_rows})

    exact_duplicates = len(exact_frame_hashes) - len(set(exact_frame_hashes))
    normalized_duplicates = len(normalized_hashes) - len(set(normalized_hashes))
    unique_silhouettes = len(set(silhouette_hashes))
    diversity_ok = exact_duplicates == 0 and normalized_duplicates == 0 and unique_silhouettes >= 56
    checks.append(
        {
            "id": "visualDiversity.no-duplicate-cells",
            "passed": diversity_ok,
            "cellCount": len(exact_frame_hashes),
            "exactDuplicateCells": exact_duplicates,
            "normalizedDuplicateCells": normalized_duplicates,
            "uniqueAlphaSilhouettes": unique_silhouettes,
            "minimumUniqueAlphaSilhouettes": 56,
        }
    )
    if not diversity_ok:
        errors.append("visual diversity threshold failed")

    preview_files = [PREVIEWS / "combat-units-1x.png", PREVIEWS / "combat-units-2x.png"]
    previews_ok = all(path.is_file() for path in preview_files)
    checks.append({"id": "previews.one-and-two-x", "passed": previews_ok, "files": [rel(path) for path in preview_files]})
    if not previews_ok:
        errors.append("preview missing")

    report = {
        "schemaVersion": "1.0.0",
        "campaignId": "candidate-03",
        "batchId": "b03",
        "qualityTier": "runtime-v2-candidate",
        "runtimeReady": False,
        "passed": not errors,
        "summary": {
            "assetCount": len(SPECS),
            "coverage": {"combat-unit": len(SPECS)},
            "cumulativeCombatTopics": len(cumulative),
            "combatTarget": len(combat_topics),
            "machineErrors": len(errors),
        },
        "checks": checks,
        "errors": errors,
        "manualReview": {
            "required": True,
            "completed": ["1x/2x contact preview", "weapon and occupational silhouette review", "large-unit framing review", "four-frame baseline review"],
            "pendingBeforeRuntimeReady": ["in-engine contentId registration", "game-board screenshot", "large-unit footprint and z-order test", "animation timing acceptance"],
        },
        "completionClaim": "C03 combat-unit topic coverage is 40/40 across Primary 4 + Batch 02 8 + Batch 03 28; Batch 03 remains runtime-v2-candidate until in-engine acceptance.",
    }
    QA_PATH.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return report


def main() -> None:
    setup()
    remove_keys()
    build_units()
    write_topic_lock()
    write_manifest()
    make_previews()
    report = qa()
    print(json.dumps(report["summary"], ensure_ascii=False, indent=2))
    if not report["passed"]:
        for error in report["errors"]:
            print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)


if __name__ == "__main__":
    main()
