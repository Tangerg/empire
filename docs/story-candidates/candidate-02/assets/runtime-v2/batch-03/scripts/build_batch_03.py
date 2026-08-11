#!/usr/bin/env python3
"""Build the final 28 C02 runtime-v2 combat units (batch-03)."""

from __future__ import annotations

import hashlib
import importlib.util
import json
import sys
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from PIL import Image


BATCH = Path(__file__).resolve().parents[1]
RUNTIME = BATCH.parent
ASSET_ROOT = RUNTIME.parent
MANIFEST_PATH = ASSET_ROOT / "manifest-runtime-v2-b03.json"
QA_PATH = ASSET_ROOT / "qa-runtime-v2-b03.json"
LOCK_PATH = BATCH / "TOPIC-LOCK.json"

base_spec = importlib.util.spec_from_file_location("c02_runtime_v2_base_b03", RUNTIME / "scripts" / "build_runtime_v2.py")
if base_spec is None or base_spec.loader is None:
    raise RuntimeError("cannot load C02 runtime-v2 processing primitives")
base = importlib.util.module_from_spec(base_spec)
sys.modules[base_spec.name] = base
base_spec.loader.exec_module(base)


@dataclass(frozen=True)
class Asset:
    id: str
    topic_id: str
    content_id: str
    png: str
    svg: str
    width: int
    height: int
    extra: dict[str, Any]

    def manifest(self) -> dict[str, Any]:
        value = {
            "id": self.id, "topicId": self.topic_id, "contentId": self.content_id,
            "type": "combat-unit-sheet", "png": self.png, "svg": self.svg,
            "width": self.width, "height": self.height,
        }
        value.update(self.extra)
        return value


LOCK = json.loads(LOCK_PATH.read_text(encoding="utf-8"))
TOPICS = {row["topicId"]: row for row in LOCK["topics"]}

SPECS = [
    ("c02-unit-tide-priest", "a", 0, 32, 48),
    ("c02-unit-memory-monk", "a", 1, 32, 48),
    ("c02-unit-field-medic", "a", 2, 32, 48),
    ("c02-unit-protocol-analyst", "a", 3, 32, 48),
    ("c02-unit-turret-vehicle", "b", 0, 96, 64),
    ("c02-unit-light-mech", "b", 1, 64, 64),
    ("c02-unit-antler-knight", "b", 2, 32, 48),
    ("c02-unit-forest-beast-handler", "b", 3, 64, 48),
    ("c02-unit-eco-maintainer", "c", 0, 32, 48),
    ("c02-unit-boarding-team", "c", 1, 64, 48),
    ("c02-unit-vacuum-trooper", "c", 2, 32, 48),
    ("c02-unit-stargate-guard", "c", 3, 32, 48),
    ("c02-unit-rain-tower-engineer", "d", 0, 32, 48),
    ("c02-unit-wind-sail-skirmisher", "d", 1, 32, 48),
    ("c02-unit-heat-lamp-shield", "d", 2, 32, 48),
    ("c02-unit-ice-hook-hunter", "d", 3, 32, 48),
    ("c02-unit-floating-city-guard", "e", 0, 32, 48),
    ("c02-unit-tide-anchor-engineer", "e", 1, 32, 48),
    ("c02-unit-fungus-bed-medic", "e", 2, 32, 48),
    ("c02-unit-defoliator-pilot", "e", 3, 64, 64),
    ("c02-unit-ring-rail-guard", "f", 0, 32, 48),
    ("c02-unit-gravity-rescuer", "f", 1, 32, 48),
    ("c02-unit-physical-archive-keeper", "f", 2, 32, 48),
    ("c02-unit-echo-tracker", "f", 3, 32, 48),
    ("c02-unit-reset-guard", "g", 0, 32, 48),
    ("c02-unit-protocol-adjudicator", "g", 1, 32, 48),
    ("c02-unit-starbreaker-sapper", "g", 2, 32, 48),
    ("c02-unit-rain-shepherd", "g", 3, 64, 64),
]


def rel(path: Path) -> str:
    return path.relative_to(ASSET_ROOT).as_posix()


def alpha_board(name: str) -> list[Image.Image]:
    image = Image.open(BATCH / "masters" / f"c02-v2-b03-combat-board-{name}-alpha.png").convert("RGBA")
    return base.panel_crops(image, 4, 4, 0.008)


def build_assets() -> list[Asset]:
    boards = {name: alpha_board(name) for name in "abcdefg"}
    assets: list[Asset] = []
    for topic_id, board_name, row_index, frame_width, frame_height in SPECS:
        if topic_id not in TOPICS:
            raise RuntimeError(f"topic not locked: {topic_id}")
        cells = boards[board_name][row_index * 4:(row_index + 1) * 4]
        frames = base.fit_alpha_panels(cells, (frame_width, frame_height), (1, 1), 48)
        sheet = base.horizontal_sheet(frames)
        slug = topic_id.removeprefix("c02-unit-")
        path = BATCH / "units" / f"c02-v2-b03-unit-{slug}.png"
        base.save_asset_image(sheet, path)
        extra: dict[str, Any] = {
            "frameWidth": frame_width, "frameHeight": frame_height, "frames": 4,
            "frameOrder": ["standA", "stepA", "standB", "stepB"],
            "facing": "right", "anchor": [frame_width // 2, frame_height - 1],
            "fps": 6, "loop": True,
            "sourceMaster": rel(BATCH / "masters" / f"c02-v2-b03-combat-board-{board_name}-master.png"),
        }
        if frame_width > 32 or frame_height > 48:
            if frame_width == 96: footprint = [3, 2]
            elif frame_height == 64: footprint = [2, 2]
            else: footprint = [2, 1]
            extra.update({"footprint": footprint, "zOrder": 2 if frame_height == 64 else 1})
        locked = TOPICS[topic_id]
        assets.append(Asset(
            f"c02-v2-b03-unit-{slug}", topic_id, locked["contentId"], rel(path), rel(path.with_suffix(".svg")),
            sheet.width, sheet.height, extra,
        ))
    return assets


def make_preview(assets: list[Asset]) -> None:
    canvas = base.checker((1408, 768), 8)
    x, y, row_height = 12, 12, 0
    for asset in assets:
        image = Image.open(ASSET_ROOT / asset.png).convert("RGBA")
        if x + image.width > 1396:
            x, y, row_height = 12, y + row_height + 10, 0
        canvas.alpha_composite(image, (x, y))
        x += image.width + 10
        row_height = max(row_height, image.height)
    used_height = y + row_height + 12
    one = canvas.crop((0, 0, canvas.width, used_height)).convert("RGB")
    one.save(BATCH / "previews" / "c02-v2-b03-combat-preview-1x.png", optimize=True)
    one.resize((one.width * 2, one.height * 2), Image.Resampling.NEAREST).save(BATCH / "previews" / "c02-v2-b03-combat-preview-2x.png", optimize=True)


def frame_hash(image: Image.Image) -> str:
    return hashlib.sha256(image.convert("RGBA").tobytes()).hexdigest()


def qa_assets(assets: list[Asset]) -> tuple[list[dict[str, Any]], list[str]]:
    checks: list[dict[str, Any]] = []
    errors: list[str] = []

    def check(name: str, passed: bool, detail: Any) -> None:
        checks.append({"name": name, "passed": passed, "detail": detail})
        if not passed: errors.append(f"{name}: {detail}")

    check("locked-final-28", len(assets) == 28 and {a.topic_id for a in assets} == set(TOPICS), {"assets": len(assets), "locked": len(TOPICS)})
    check("unique-id-topic-content", len({a.id for a in assets}) == 28 and len({a.topic_id for a in assets}) == 28 and len({a.content_id for a in assets}) == 28, "28/28 unique")
    missing = [a.id for a in assets if not (ASSET_ROOT / a.png).is_file() or not (ASSET_ROOT / a.svg).is_file()]
    check("png-svg-exist", not missing, missing or "56 runtime files present")

    unit_bad = []
    alpha_bad = []
    normalized_hashes = []
    silhouette_hashes = []
    size_counts: Counter[str] = Counter()
    for asset in assets:
        image = Image.open(ASSET_ROOT / asset.png).convert("RGBA")
        fw, fh = asset.extra["frameWidth"], asset.extra["frameHeight"]
        size_counts[f"{fw}x{fh}"] += 1
        if image.size != (fw * 4, fh) or (asset.width, asset.height) != image.size:
            unit_bad.append({"id": asset.id, "size": list(image.size), "declared": [asset.width, asset.height]})
            continue
        alphas = set(image.getchannel("A").get_flattened_data())
        corners = [image.getpixel((0, 0))[3], image.getpixel((image.width - 1, 0))[3], image.getpixel((0, image.height - 1))[3], image.getpixel((image.width - 1, image.height - 1))[3]]
        if not alphas.issubset({0, 255}) or any(corners): alpha_bad.append(asset.id)
        frames = base.split_frames(image, fw, fh, 4)
        boxes = [base.alpha_bbox(frame, 0) for frame in frames]
        hashes = [frame_hash(frame) for frame in frames]
        if len(set(hashes)) != 4 or any(box is None or box[3] != fh for box in boxes):
            unit_bad.append({"id": asset.id, "uniqueFrames": len(set(hashes)), "boxes": boxes})
        if fw > 32 or fh > 48:
            if "footprint" not in asset.extra or "zOrder" not in asset.extra:
                unit_bad.append({"id": asset.id, "largeMetadata": False})
        normalized = image.resize((64, 32), Image.Resampling.NEAREST)
        normalized_hashes.append(frame_hash(normalized))
        silhouette_hashes.append(hashlib.sha256(normalized.getchannel("A").tobytes()).hexdigest())
    check("exact-sizes-four-grounded-frames", not unit_bad, unit_bad or dict(size_counts))
    check("binary-alpha-transparent-corners", not alpha_bad, alpha_bad or "all 28 sheets clean")
    check("no-exact-unit-duplicates", len(set(normalized_hashes)) == 28, {"unique": len(set(normalized_hashes)), "total": 28})
    check("unit-silhouette-diversity", len(set(silhouette_hashes)) == 28, {"unique": len(set(silhouette_hashes)), "total": 28})

    primary_topics = set(LOCK["primaryTopicIds"])
    batch02_topics = set(LOCK["batch02TopicIds"])
    batch03_topics = {a.topic_id for a in assets}
    complete = json.loads((ASSET_ROOT / "manifest-complete.json").read_text(encoding="utf-8"))
    all_combat = {topic["id"] for topic in complete["topics"] if topic["category"] == "combat-unit"}
    union = primary_topics | batch02_topics | batch03_topics
    overlap = (primary_topics & batch02_topics) | (primary_topics & batch03_topics) | (batch02_topics & batch03_topics)
    check("cumulative-combat-40-of-40", len(union) == 40 and union == all_combat and not overlap, {"primary": 4, "batch02": 8, "batch03": 28, "union": len(union), "overlap": sorted(overlap)})

    previous_content = set()
    for name in ("manifest-runtime-v2.json", "manifest-runtime-v2-b02.json"):
        manifest = json.loads((ASSET_ROOT / name).read_text(encoding="utf-8"))
        previous_content.update(asset["contentId"] for asset in manifest["assets"])
    collisions = sorted(previous_content & {a.content_id for a in assets})
    check("no-prior-contentId-collision", not collisions, collisions or "0 collisions")
    previews = [BATCH / "previews" / "c02-v2-b03-combat-preview-1x.png", BATCH / "previews" / "c02-v2-b03-combat-preview-2x.png"]
    check("preview-files", all(path.is_file() for path in previews), [rel(path) for path in previews])
    return checks, errors


def main() -> None:
    assets = build_assets()
    make_preview(assets)
    manifest = {
        "schemaVersion": "1.0.0", "campaignId": "candidate-02", "campaignTitle": "群星熄灭之前",
        "batchId": "b03", "qualityTier": "runtime-v2-candidate", "runtimeReady": False,
        "generatedAt": "2026-08-12", "topicLock": rel(LOCK_PATH),
        "assetCount": 28, "coverage": {"combat-unit": 28},
        "cumulativeCoverage": {"combat-unit": 40, "target": 40},
        "notes": [
            "Final combat-unit closeout batch; primary 4 and batch-02 8 remain untouched.",
            "Large vehicles, paired squads and beast/walker units use wider declared runtime frames with footprint and zOrder.",
            "runtimeReady remains false pending in-engine screenshots and collision/pivot validation.",
        ],
        "assets": [asset.manifest() for asset in assets],
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    checks, errors = qa_assets(assets)
    qa = {
        "schemaVersion": "1.0.0", "campaignId": "candidate-02", "batchId": "b03",
        "qualityTier": "runtime-v2-candidate", "runtimeReady": False,
        "passed": not errors, "assetCount": 28, "runtimeFileCount": 56, "masterCount": 7,
        "coverage": {"combat-unit": 28}, "cumulativeCombatCoverage": "40/40",
        "sharedValidator": {
            "command": "python3 docs/story-candidates/pixel-master-tools/validate_runtime_v2_b03.py",
            "expectedCampaignStatus": "passed-machine-qa",
        },
        "checks": checks, "errors": errors,
        "visualReview": {
            "oneX": rel(BATCH / "previews" / "c02-v2-b03-combat-preview-1x.png"),
            "twoX": rel(BATCH / "previews" / "c02-v2-b03-combat-preview-2x.png"),
            "requiredNext": "Root agent must validate combat readability, collision footprints, anchors and z-order in engine before promotion.",
        },
    }
    QA_PATH.write_text(json.dumps(qa, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if errors: raise SystemExit("QA failed:\n" + "\n".join(errors))
    print("Built C02 batch-03: 28 combat units / 56 runtime files; cumulative combat 40/40; QA passed")


if __name__ == "__main__":
    main()
