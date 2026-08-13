#!/usr/bin/env python3
"""Build the final 28 C01 runtime-v2 combat units (batch-03)."""

from __future__ import annotations

import hashlib
import json
from collections import Counter
from pathlib import Path

from PIL import Image, ImageDraw


BATCH = Path(__file__).resolve().parents[1]
ASSET_ROOT = BATCH.parents[1]
LOCK_PATH = BATCH / "TOPIC-LOCK.json"
MANIFEST_PATH = ASSET_ROOT / "manifest-runtime-v2-b03.json"
QA_PATH = ASSET_ROOT / "qa-runtime-v2-b03.json"
RESAMPLE = Image.Resampling.LANCZOS
NEAREST = Image.Resampling.NEAREST

SPECS = (
    ("C01-UNIT-SILVER-LONGBOW", "a", 0, 32, 48),
    ("C01-UNIT-RANGER", "a", 1, 32, 48),
    ("C01-UNIT-ASSASSIN", "a", 2, 32, 48),
    ("C01-UNIT-MAGE", "a", 3, 32, 48),
    ("C01-UNIT-BALLISTA", "b", 0, 96, 64),
    ("C01-UNIT-LANCE-CAVALRY", "b", 1, 64, 64),
    ("C01-UNIT-BATTLE-MAGE", "b", 2, 32, 48),
    ("C01-UNIT-EAGLE-SCOUT", "b", 3, 96, 64),
    ("C01-UNIT-WOODLAND-WALKER", "c", 0, 64, 64),
    ("C01-UNIT-DRUID", "c", 1, 32, 48),
    ("C01-UNIT-WHITE-STAG-RIDER", "c", 2, 64, 64),
    ("C01-UNIT-RUNE-SHIELD", "c", 3, 32, 48),
    ("C01-UNIT-AXE-BREAKER", "d", 0, 32, 48),
    ("C01-UNIT-STONE-GOLEM", "d", 1, 64, 64),
    ("C01-UNIT-SHAMAN", "d", 2, 32, 48),
    ("C01-UNIT-JAVELIN-HUNTER", "d", 3, 32, 48),
    ("C01-UNIT-HEAVY-KNIGHT", "e", 0, 32, 48),
    ("C01-UNIT-SPIRIT-FIRE", "e", 1, 64, 64),
    ("C01-UNIT-CANNON-WAGON", "e", 2, 96, 64),
    ("C01-UNIT-TROLL", "e", 3, 64, 64),
    ("C01-UNIT-BERSERKER", "f", 0, 32, 48),
    ("C01-UNIT-TEMPLAR", "f", 1, 32, 48),
    ("C01-UNIT-INQUISITOR", "f", 2, 32, 48),
    ("C01-UNIT-GHOST", "f", 3, 32, 48),
    ("C01-UNIT-CEMETERY-COLOSSUS", "g", 0, 96, 64),
    ("C01-UNIT-IVRA-GROWTH", "g", 1, 32, 48),
    ("C01-UNIT-WYVERN-RIDER", "g", 2, 96, 64),
    ("C01-UNIT-ANCIENT-DRAGON", "g", 3, 96, 64),
)


def rel(path: Path) -> str:
    return path.relative_to(ASSET_ROOT).as_posix()


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def grid_cell(image: Image.Image, col: int, row: int) -> Image.Image:
    x0 = round(col * image.width / 4) + 3
    x1 = round((col + 1) * image.width / 4) - 3
    y0 = round(row * image.height / 4) + 3
    y1 = round((row + 1) * image.height / 4) - 3
    return image.crop((x0, y0, x1, y1))


def tight(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    bbox = rgba.getchannel("A").point(lambda value: 255 if value >= 24 else 0).getbbox()
    if bbox is None:
        raise RuntimeError("empty alpha panel")
    return rgba.crop(bbox)


def quantize(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A").point(lambda value: 255 if value >= 96 else 0)
    rgb = rgba.convert("RGB").quantize(colors=48, method=Image.Quantize.MEDIANCUT).convert("RGB")
    rgb.putalpha(alpha)
    return rgb


def fit_row(cells: list[Image.Image], frame_size: tuple[int, int]) -> list[Image.Image]:
    fw, fh = frame_size
    crops = [tight(cell) for cell in cells]
    scale = min((fw - 2) / max(crop.width for crop in crops), (fh - 2) / max(crop.height for crop in crops))
    frames = []
    for crop in crops:
        sprite = quantize(crop.resize((max(1, round(crop.width * scale)), max(1, round(crop.height * scale))), RESAMPLE))
        sprite = tight(sprite)
        canvas = Image.new("RGBA", (fw, fh), (0, 0, 0, 0))
        canvas.alpha_composite(sprite, ((fw - sprite.width) // 2, fh - sprite.height))
        frames.append(canvas)
    return frames


def horizontal(frames: list[Image.Image]) -> Image.Image:
    sheet = Image.new("RGBA", (sum(frame.width for frame in frames), max(frame.height for frame in frames)), (0, 0, 0, 0))
    x = 0
    for frame in frames:
        sheet.alpha_composite(frame, (x, 0))
        x += frame.width
    return sheet


def save_pair(image: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, optimize=True)
    path.with_suffix(".svg").write_text(
        "\n".join([
            '<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" '
            f'width="{image.width}" height="{image.height}" viewBox="0 0 {image.width} {image.height}" shape-rendering="crispEdges">',
            f'  <image href="{path.name}" xlink:href="{path.name}" width="{image.width}" height="{image.height}" style="image-rendering:pixelated"/>',
            "</svg>", "",
        ]),
        encoding="utf-8",
    )


def build_assets(lock: dict) -> list[dict]:
    topics = {topic["topicId"]: topic for topic in lock["topics"]}
    boards = {
        name: Image.open(BATCH / "intermediate" / f"combat-board-{name}-alpha.png").convert("RGBA")
        for name in "abcdefg"
    }
    assets = []
    for topic_id, board, row, fw, fh in SPECS:
        if topic_id not in topics:
            raise RuntimeError(f"topic not locked: {topic_id}")
        cells = [grid_cell(boards[board], col, row) for col in range(4)]
        sheet = horizontal(fit_row(cells, (fw, fh)))
        slug = topic_id.removeprefix("C01-UNIT-").lower()
        path = BATCH / "units" / f"c01-v2-b03-unit-{slug}.png"
        save_pair(sheet, path)
        large = fw > 32 or fh > 48
        if fw == 96:
            footprint = {"columns": 3, "rows": 2}
        elif fh == 64:
            footprint = {"columns": 2, "rows": 2}
        else:
            footprint = {"columns": 1, "rows": 1}
        asset = {
            "id": f"c01-v2-b03-unit-{slug}",
            "topicId": topic_id,
            "label": topics[topic_id]["label"],
            "contentId": topics[topic_id]["contentId"],
            "type": "combat-unit",
            "png": rel(path),
            "svg": rel(path.with_suffix(".svg")),
            "width": sheet.width,
            "height": sheet.height,
            "frameWidth": fw,
            "frameHeight": fh,
            "frames": 4,
            "frameOrder": ["idle-a", "step-a", "idle-b", "step-b"],
            "facing": "right",
            "anchor": {"x": fw // 2, "y": fh - 1},
            "footprint": footprint,
            "zOrder": 14 if large else 10,
            "fps": 6,
            "loop": True,
            "alphaMode": "binary",
            "sourceMaster": rel(BATCH / "masters" / f"combat-board-{board}-master.png"),
        }
        assets.append(asset)
    return assets


def checker(size: tuple[int, int], cell: int = 8) -> Image.Image:
    image = Image.new("RGBA", size, (24, 27, 33, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2:
                draw.rectangle((x, y, x + cell - 1, y + cell - 1), fill=(33, 37, 45, 255))
    return image


def make_preview(assets: list[dict]) -> list[str]:
    width = 1408
    placements = []
    x = y = 12
    row_height = 0
    for asset in assets:
        image = Image.open(ASSET_ROOT / asset["png"]).convert("RGBA")
        if x + image.width > width - 12:
            x, y, row_height = 12, y + row_height + 10, 0
        placements.append((asset, image, x, y))
        x += image.width + 10
        row_height = max(row_height, image.height)
    height = y + row_height + 12
    canvas = checker((width, height))
    drawn = []
    for asset, image, x, y in placements:
        canvas.alpha_composite(image, (x, y))
        drawn.append(asset["id"])
    one = canvas.convert("RGB")
    preview = BATCH / "previews" / "c01-v2-b03-combat-preview-1x.png"
    one.save(preview, optimize=True)
    one.resize((one.width * 2, one.height * 2), NEAREST).save(preview.with_name("c01-v2-b03-combat-preview-2x.png"), optimize=True)
    return drawn


def validate(assets: list[dict], lock: dict, preview_ids: list[str], prior_hashes: dict[str, str]) -> dict:
    errors: list[str] = []
    checks: list[dict] = []

    def check(name: str, passed: bool, detail: object) -> None:
        checks.append({"name": name, "passed": passed, "detail": detail})
        if not passed:
            errors.append(f"{name}: {detail}")

    check("locked-final-28", len(assets) == 28 and {asset["topicId"] for asset in assets} == {topic["topicId"] for topic in lock["topics"]}, {"assets": len(assets), "locked": len(lock["topics"])})
    check("unique-id-topic-content", all(len({asset[key] for asset in assets}) == 28 for key in ("id", "topicId", "contentId")), "28/28 unique")
    missing = [asset["id"] for asset in assets if not (ASSET_ROOT / asset["png"]).is_file() or not (ASSET_ROOT / asset["svg"]).is_file()]
    check("png-svg-exist", not missing, missing or "56 runtime files present")

    unit_bad = []
    alpha_bad = []
    normalized_hashes = []
    silhouette_hashes = []
    size_counts: Counter[str] = Counter()
    for asset in assets:
        image = Image.open(ASSET_ROOT / asset["png"]).convert("RGBA")
        fw, fh = asset["frameWidth"], asset["frameHeight"]
        size_counts[f"{fw}x{fh}"] += 1
        if image.size != (fw * 4, fh):
            unit_bad.append({"id": asset["id"], "size": list(image.size)})
            continue
        alpha_values = set(image.getchannel("A").get_flattened_data())
        corners = [image.getpixel(point)[3] for point in ((0, 0), (image.width - 1, 0), (0, image.height - 1), (image.width - 1, image.height - 1))]
        if not alpha_values.issubset({0, 255}) or any(corners):
            alpha_bad.append(asset["id"])
        frame_hashes = []
        boxes = []
        for frame in range(4):
            cell = image.crop((frame * fw, 0, (frame + 1) * fw, fh))
            boxes.append(cell.getchannel("A").getbbox())
            frame_hashes.append(hashlib.sha256(cell.tobytes()).hexdigest())
        if len(set(frame_hashes)) != 4 or any(box is None or box[3] != fh for box in boxes):
            unit_bad.append({"id": asset["id"], "uniqueFrames": len(set(frame_hashes)), "boxes": boxes})
        normalized = image.resize((64, 32), NEAREST)
        normalized_hashes.append(hashlib.sha256(normalized.tobytes()).hexdigest())
        silhouette_hashes.append(hashlib.sha256(normalized.getchannel("A").tobytes()).hexdigest())
    check("exact-sizes-four-grounded-frames", not unit_bad, unit_bad or dict(size_counts))
    check("binary-alpha-transparent-corners", not alpha_bad, alpha_bad or "all 28 sheets clean")
    check("no-exact-unit-duplicates", len(set(normalized_hashes)) == 28, {"unique": len(set(normalized_hashes)), "total": 28})
    check("unit-silhouette-diversity", len(set(silhouette_hashes)) == 28, {"unique": len(set(silhouette_hashes)), "total": 28})

    primary = set(lock["primaryTopicIds"])
    batch02 = set(lock["batch02TopicIds"])
    batch03 = {asset["topicId"] for asset in assets}
    complete = json.loads((ASSET_ROOT / "manifest-complete.json").read_text(encoding="utf-8"))
    all_combat = {topic["id"] for topic in complete["topics"] if topic["category"] == "combat-unit"}
    union = primary | batch02 | batch03
    overlap = (primary & batch02) | (primary & batch03) | (batch02 & batch03)
    check("cumulative-combat-40-of-40", len(union) == 40 and union == all_combat and not overlap, {"primary": 4, "batch02": 8, "batch03": 28, "union": len(union), "overlap": sorted(overlap)})
    prior_content = set()
    for name in ("manifest-runtime-v2.json", "manifest-runtime-v2-b02.json"):
        prior = json.loads((ASSET_ROOT / name).read_text(encoding="utf-8"))
        prior_content.update(asset["contentId"] for asset in prior["assets"])
    collisions = sorted(prior_content & {asset["contentId"] for asset in assets})
    check("no-prior-contentId-collision", not collisions, collisions or "0 collisions")
    check("preview-draws-all-28", preview_ids == [asset["id"] for asset in assets], {"drawn": len(preview_ids), "ids": preview_ids})
    current_hashes = {name: sha256(ASSET_ROOT / name) for name in prior_hashes}
    check("prior-manifests-unchanged", current_hashes == prior_hashes, current_hashes)
    return {
        "schemaVersion": "1.0.0",
        "campaignId": "candidate-01",
        "batchId": "b03",
        "qualityTier": "runtime-v2-candidate",
        "runtimeReady": False,
        "passed": not errors,
        "assetCount": 28,
        "runtimeFileCount": 56,
        "masterCount": 7,
        "coverage": {"combat-unit": 28},
        "cumulativeCombatCoverage": "40/40",
        "checks": checks,
        "previewAssetIds": preview_ids,
        "errors": errors,
        "visualReview": {
            "oneX": rel(BATCH / "previews" / "c01-v2-b03-combat-preview-1x.png"),
            "twoX": rel(BATCH / "previews" / "c01-v2-b03-combat-preview-2x.png"),
            "requiredNext": "In-engine footprint, collision, anchor and z-order validation is required before promotion.",
        },
    }


def main() -> None:
    prior_hashes = {name: sha256(ASSET_ROOT / name) for name in ("manifest-runtime-v2.json", "manifest-runtime-v2-b02.json")}
    lock = json.loads(LOCK_PATH.read_text(encoding="utf-8"))
    assets = build_assets(lock)
    manifest = {
        "schemaVersion": "1.0.0",
        "campaignId": "candidate-01",
        "campaignTitle": "断冠之誓",
        "batchId": "b03",
        "qualityTier": "runtime-v2-candidate",
        "runtimeReady": False,
        "extends": ["manifest-runtime-v2.json", "manifest-runtime-v2-b02.json"],
        "topicLock": rel(LOCK_PATH),
        "prompts": rel(BATCH / "PROMPTS.md"),
        "assetCount": 28,
        "coverage": {"combat-unit": 28},
        "cumulativeCoverage": {"combat-unit": 40, "target": 40},
        "notes": [
            "Final combat closeout batch; primary 4 and batch-02 8 remain untouched.",
            "Large siege, mounted, monster and dragon units declare wider frames, footprint and zOrder.",
            "runtimeReady remains false pending in-engine screenshots and pivot/collision validation.",
        ],
        "assets": assets,
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    preview_ids = make_preview(assets)
    qa = validate(assets, lock, preview_ids, prior_hashes)
    QA_PATH.write_text(json.dumps(qa, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"assets": len(assets), "qaPassed": qa["passed"], "errors": qa["errors"]}, ensure_ascii=False, indent=2))
    if not qa["passed"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
