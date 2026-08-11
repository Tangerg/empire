#!/usr/bin/env python3
"""Validate additive runtime-v2 production batches without touching promoted packs."""

from __future__ import annotations

import hashlib
import json
from collections import Counter
from pathlib import Path
from typing import Any

from PIL import Image

from validate_runtime_v2 import (
    alpha_bbox,
    file_pair,
    validate_fx,
    validate_scene,
    validate_structure,
    validate_terrain,
)


ROOT = Path(__file__).resolve().parents[1]
CAMPAIGNS = ("candidate-01", "candidate-02", "candidate-03")
BATCH = "b02"
EXPECTED_COVERAGE = {
    "combat-unit": 8,
    "mission-unit": 8,
    "terrain": 8,
    "interactive-structure": 4,
    "battle-prop": 8,
    "equipment": 8,
    "skill": 8,
    "status": 4,
    "hud": 4,
    "fx": 4,
    "narrative-static": 4,
}


def runtime_category(asset: dict[str, Any]) -> str:
    raw = str(asset.get("type", ""))
    aliases = {
        "combat-unit-sheet": "combat-unit",
        "combat-unit-sprite": "combat-unit",
        "unit-sheet": "combat-unit",
        "mission-unit-sheet": "mission-unit",
        "mission-unit-sprite": "mission-unit",
        "task-unit": "mission-unit",
        "terrain-tile": "terrain",
        "terrain-atlas": "terrain",
        "terrain-variants": "terrain",
        "terrain-connection": "terrain",
        "structure": "interactive-structure",
        "interactive-facility": "interactive-structure",
        "structure-sheet": "interactive-structure",
        "map-object": "battle-prop",
        "object": "battle-prop",
        "equipment-icon": "equipment",
        "skill-icon": "skill",
        "status-icon": "status",
        "hud-icon": "hud",
        "fx-sheet": "fx",
        "story-scene": "narrative-static",
        "narrative-scene": "narrative-static",
        "scene": "narrative-static",
    }
    return aliases.get(raw, raw)


def validate_prop(campaign: str, asset: dict[str, Any], image: Image.Image, errors: list[str]) -> None:
    asset_id = str(asset.get("id"))
    if image.width not in {24, 32, 48, 64, 96} or image.height not in {24, 32, 48, 64, 96}:
        errors.append(f"{campaign}/{asset_id}: battle prop dimensions must be runtime cell multiples")
    if alpha_bbox(image) is None:
        errors.append(f"{campaign}/{asset_id}: empty battle prop")
    if not isinstance(asset.get("anchor"), (list, dict)):
        errors.append(f"{campaign}/{asset_id}: battle prop anchor is required")


def validate_batch_unit(
    campaign: str, asset: dict[str, Any], image: Image.Image, errors: list[str]
) -> None:
    asset_id = str(asset.get("id"))
    frame_width = asset.get("frameWidth")
    frame_height = asset.get("frameHeight")
    frames = asset.get("frames", asset.get("frameCount"))
    allowed = {(32, 48), (64, 48), (64, 64), (96, 64)}
    if (frame_width, frame_height) not in allowed or frames != 4:
        errors.append(
            f"{campaign}/{asset_id}: unit sheet must use four frames in an allowed runtime size"
        )
        return
    if image.size != (frame_width * 4, frame_height):
        errors.append(
            f"{campaign}/{asset_id}: unit sheet size {image.size} != {(frame_width * 4, frame_height)}"
        )
    anchor = asset.get("anchor")
    anchor_values = (
        anchor
        if isinstance(anchor, list)
        else [anchor.get("x"), anchor.get("y")]
        if isinstance(anchor, dict)
        else None
    )
    if anchor_values != [frame_width // 2, frame_height - 1]:
        errors.append(
            f"{campaign}/{asset_id}: unit anchor must be bottom-centre of its declared frame"
        )
    if frame_width > 32:
        footprint = asset.get("footprint")
        if not isinstance(footprint, (list, dict)):
            errors.append(f"{campaign}/{asset_id}: large unit footprint is required")
        if "zOrder" not in asset:
            errors.append(f"{campaign}/{asset_id}: large unit zOrder is required")
    for frame in range(4):
        cell = image.crop((frame * frame_width, 0, (frame + 1) * frame_width, frame_height))
        if alpha_bbox(cell) is None:
            errors.append(f"{campaign}/{asset_id}: empty unit frame {frame}")
            continue
        if cell.getchannel("A").crop((0, frame_height - 4, frame_width, frame_height)).getbbox() is None:
            errors.append(f"{campaign}/{asset_id}: frame {frame} has no grounded pixels")


def validate_small_icon(
    campaign: str,
    category: str,
    asset: dict[str, Any],
    image: Image.Image,
    errors: list[str],
) -> None:
    asset_id = str(asset.get("id"))
    allowed = {(32, 32)} if category in {"equipment", "skill"} else {(24, 24), (32, 32)}
    if image.size not in allowed:
        errors.append(f"{campaign}/{asset_id}: invalid {category} icon size {image.size}")
    if alpha_bbox(image) is None:
        errors.append(f"{campaign}/{asset_id}: empty {category} icon")


def main() -> None:
    reports: list[dict[str, Any]] = []
    all_errors: list[str] = []
    for campaign in CAMPAIGNS:
        asset_root = ROOT / campaign / "assets"
        manifest_path = asset_root / f"manifest-runtime-v2-{BATCH}.json"
        errors: list[str] = []
        if not manifest_path.is_file():
            reports.append({"campaign": campaign, "batch": BATCH, "status": "pending", "errors": []})
            continue

        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        if manifest.get("qualityTier") != "runtime-v2-candidate":
            errors.append(f"{campaign}/{BATCH}: qualityTier must be runtime-v2-candidate")
        if manifest.get("runtimeReady") is not False:
            errors.append(f"{campaign}/{BATCH}: batch must remain runtimeReady=false before integration")
        if manifest.get("batchId") not in {BATCH, "batch-02", 2}:
            errors.append(f"{campaign}/{BATCH}: batchId must identify batch 02")

        prototype = json.loads((asset_root / "manifest-complete.json").read_text(encoding="utf-8"))
        topics = {str(topic["id"]): topic for topic in prototype["topics"]}
        assets = manifest.get("assets")
        if not isinstance(assets, list):
            errors.append(f"{campaign}/{BATCH}: assets must be an array")
            assets = []

        ids = [str(asset.get("id", "")) for asset in assets]
        content_ids = [str(asset.get("contentId", "")) for asset in assets]
        topic_ids = [str(asset.get("topicId", "")) for asset in assets]
        for label, values in (("asset id", ids), ("contentId", content_ids), ("topicId", topic_ids)):
            duplicates = [value for value, count in Counter(values).items() if value and count > 1]
            if "" in values:
                errors.append(f"{campaign}/{BATCH}: missing {label}")
            if duplicates:
                errors.append(f"{campaign}/{BATCH}: duplicate {label}s {duplicates}")

        coverage: Counter[str] = Counter()
        hashes: dict[str, list[tuple[str, str]]] = {}
        for asset in assets:
            asset_id = str(asset.get("id", "<missing-id>"))
            category = runtime_category(asset)
            coverage[category] += 1
            topic_id = str(asset.get("topicId", ""))
            topic = topics.get(topic_id)
            if topic is None:
                errors.append(f"{campaign}/{asset_id}: topicId {topic_id!r} is not in the 404-topic manifest")
            else:
                if topic.get("category") != category:
                    errors.append(
                        f"{campaign}/{asset_id}: topic category {topic.get('category')!r} != {category!r}"
                    )
                if topic.get("source") != "expanded":
                    errors.append(f"{campaign}/{asset_id}: batch-02 must not reclaim an existing formal topic")

            png_path = asset.get("png")
            svg_path = asset.get("svg")
            for value, label in ((png_path, "PNG"), (svg_path, "SVG")):
                if isinstance(value, str) and not value.startswith("runtime-v2/batch-02/"):
                    errors.append(f"{campaign}/{asset_id}: {label} must stay under runtime-v2/batch-02")

            image = file_pair(campaign, asset_root, asset, errors)
            if image is None:
                continue
            normalized = image.resize((32, 32), Image.Resampling.NEAREST)
            hashes.setdefault(category, []).append(
                (asset_id, hashlib.sha256(normalized.tobytes()).hexdigest())
            )
            if category in {"combat-unit", "mission-unit"}:
                validate_batch_unit(campaign, asset, image, errors)
            elif category == "terrain":
                validate_terrain(campaign, asset, image, errors)
            elif category == "interactive-structure":
                validate_structure(campaign, asset, image, errors)
            elif category == "battle-prop":
                validate_prop(campaign, asset, image, errors)
            elif category in {"equipment", "skill", "status", "hud"}:
                validate_small_icon(campaign, category, asset, image, errors)
            elif category == "fx":
                validate_fx(campaign, asset, image, errors)
            elif category == "narrative-static":
                validate_scene(campaign, asset, image, errors)
            else:
                errors.append(f"{campaign}/{asset_id}: unsupported batch category {category!r}")

        for category, expected in EXPECTED_COVERAGE.items():
            if coverage[category] != expected:
                errors.append(
                    f"{campaign}/{BATCH}: {category} coverage {coverage[category]} != {expected}"
                )
        if len(assets) != sum(EXPECTED_COVERAGE.values()):
            errors.append(
                f"{campaign}/{BATCH}: asset count {len(assets)} != {sum(EXPECTED_COVERAGE.values())}"
            )
        for category, entries in hashes.items():
            repeated = [value for value, count in Counter(value for _, value in entries).items() if count > 1]
            if repeated:
                examples = [
                    [asset_id for asset_id, value in entries if value == repeated_hash]
                    for repeated_hash in repeated
                ]
                errors.append(f"{campaign}/{BATCH}: exact duplicate {category} files {examples}")

        qa_path = asset_root / f"qa-runtime-v2-{BATCH}.json"
        if not qa_path.is_file():
            errors.append(f"{campaign}/{BATCH}: missing independent QA report")
        else:
            qa = json.loads(qa_path.read_text(encoding="utf-8"))
            if qa.get("passed") is not True or qa.get("errors") not in ([], None):
                errors.append(f"{campaign}/{BATCH}: independent QA report did not pass")

        reports.append(
            {
                "campaign": campaign,
                "batch": BATCH,
                "status": "passed-machine-qa" if not errors else "failed",
                "assets": len(assets),
                "coverage": dict(coverage),
                "errors": errors,
            }
        )
        all_errors.extend(errors)

    print(json.dumps({"validator": "runtime-v2-batches", "campaigns": reports, "errors": all_errors}, ensure_ascii=False, indent=2))
    if all_errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
