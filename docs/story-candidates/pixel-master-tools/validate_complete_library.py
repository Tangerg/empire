#!/usr/bin/env python3
"""Validate the three 404-topic campaign asset libraries.

The complete manifests deliberately separate playable topics from physical files:
one atlas may deliver several topics, but every topic must resolve to a real,
non-empty PNG/SVG delivery through a stable asset id.
"""

from __future__ import annotations

import json
import hashlib
import math
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any
from xml.etree import ElementTree as ET

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
CAMPAIGNS = ("candidate-01", "candidate-02", "candidate-03")
CATEGORY_TARGETS = {
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


def fail(errors: list[str], campaign: str, message: str) -> None:
    errors.append(f"{campaign}: {message}")


def integer(value: Any) -> int | None:
    return value if isinstance(value, int) and not isinstance(value, bool) else None


def delivery_paths(asset_root: Path, delivery: dict[str, Any]) -> tuple[Path, Path]:
    return asset_root / str(delivery.get("png", "")), asset_root / str(
        delivery.get("svg", "")
    )


def validate_delivery(
    campaign: str,
    asset_root: Path,
    delivery: dict[str, Any],
    errors: list[str],
) -> Image.Image | None:
    delivery_id = str(delivery.get("id", "<missing-id>"))
    width = integer(delivery.get("width"))
    height = integer(delivery.get("height"))
    if not width or not height or width <= 0 or height <= 0:
        fail(errors, campaign, f"{delivery_id}: invalid declared size")
        return None

    png_path, svg_path = delivery_paths(asset_root, delivery)
    if not delivery.get("png") or not png_path.is_file():
        fail(errors, campaign, f"{delivery_id}: missing PNG {png_path}")
    if not delivery.get("svg") or not svg_path.is_file():
        fail(errors, campaign, f"{delivery_id}: missing SVG {svg_path}")
    if not png_path.is_file() or not svg_path.is_file():
        return None

    try:
        image = Image.open(png_path).convert("RGBA")
    except (OSError, ValueError) as error:
        fail(errors, campaign, f"{delivery_id}: unreadable PNG: {error}")
        return None
    if image.size != (width, height):
        fail(
            errors,
            campaign,
            f"{delivery_id}: PNG size {image.size} != {(width, height)}",
        )
    alpha = image.getchannel("A")
    if alpha.getbbox() is None:
        fail(errors, campaign, f"{delivery_id}: PNG is entirely transparent")

    try:
        root = ET.parse(svg_path).getroot()
    except (ET.ParseError, OSError) as error:
        fail(errors, campaign, f"{delivery_id}: unreadable SVG: {error}")
    else:
        view_box = root.attrib.get("viewBox")
        if view_box != f"0 0 {width} {height}":
            fail(
                errors,
                campaign,
                f"{delivery_id}: SVG viewBox {view_box!r} != '0 0 {width} {height}'",
            )
    return image


def topic_cell_box(
    topic: dict[str, Any], delivery: dict[str, Any]
) -> tuple[int, int, int, int] | None:
    cell = topic.get("cell")
    if isinstance(cell, dict):
        values = tuple(integer(cell.get(key)) for key in ("x", "y", "width", "height"))
        if all(value is not None for value in values):
            x, y, width, height = values
            assert x is not None and y is not None and width is not None and height is not None
            return x, y, x + width, y + height
    if isinstance(cell, int):
        cell_width = integer(delivery.get("cellWidth"))
        cell_height = integer(delivery.get("cellHeight"))
        columns = integer(delivery.get("columns"))
        if cell_width and cell_height and columns:
            column = cell % columns
            row = cell // columns
            return (
                column * cell_width,
                row * cell_height,
                (column + 1) * cell_width,
                (row + 1) * cell_height,
            )
    return None


def validate_campaign(campaign: str) -> dict[str, Any]:
    asset_root = ROOT / campaign / "assets"
    manifest_path = asset_root / "manifest-complete.json"
    errors: list[str] = []
    if not manifest_path.is_file():
        return {"campaign": campaign, "errors": [f"missing {manifest_path}"]}

    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        return {"campaign": campaign, "errors": [f"invalid manifest: {error}"]}

    target = manifest.get("targetTopics")
    if target != 404:
        fail(errors, campaign, f"targetTopics {target!r} != 404")
    declared_targets = manifest.get("categoryTargets")
    if declared_targets != CATEGORY_TARGETS:
        fail(errors, campaign, "categoryTargets do not match the shared 404 contract")

    topics = manifest.get("topics")
    deliveries = manifest.get("deliveries")
    if not isinstance(topics, list):
        fail(errors, campaign, "topics is not an array")
        topics = []
    if not isinstance(deliveries, list):
        fail(errors, campaign, "deliveries is not an array")
        deliveries = []
    if len(topics) != 404:
        fail(errors, campaign, f"topic count {len(topics)} != 404")

    topic_ids = [str(topic.get("id", "")) for topic in topics]
    duplicate_topic_ids = sorted(
        topic_id for topic_id, count in Counter(topic_ids).items() if count > 1
    )
    if "" in topic_ids:
        fail(errors, campaign, "one or more topics have no id")
    if duplicate_topic_ids:
        fail(errors, campaign, f"duplicate topic ids: {duplicate_topic_ids[:8]}")

    category_counts = Counter(str(topic.get("category", "")) for topic in topics)
    if dict(category_counts) != CATEGORY_TARGETS:
        fail(
            errors,
            campaign,
            f"category counts {dict(category_counts)} != {CATEGORY_TARGETS}",
        )

    delivery_ids = [str(delivery.get("id", "")) for delivery in deliveries]
    duplicate_delivery_ids = sorted(
        delivery_id
        for delivery_id, count in Counter(delivery_ids).items()
        if count > 1
    )
    if "" in delivery_ids:
        fail(errors, campaign, "one or more deliveries have no id")
    if duplicate_delivery_ids:
        fail(errors, campaign, f"duplicate delivery ids: {duplicate_delivery_ids[:8]}")
    delivery_by_id = {
        str(delivery.get("id")): delivery
        for delivery in deliveries
        if delivery.get("id")
    }

    images: dict[str, Image.Image] = {}
    for delivery_id, delivery in delivery_by_id.items():
        image = validate_delivery(campaign, asset_root, delivery, errors)
        if image is not None:
            images[delivery_id] = image

    referenced_topics: Counter[str] = Counter()
    for delivery in deliveries:
        for topic_id in delivery.get("topicIds", []):
            referenced_topics[str(topic_id)] += 1

    sources = Counter()
    cell_checked = 0
    cell_visuals: dict[str, list[tuple[str, str]]] = defaultdict(list)
    silhouette_visuals: dict[str, list[tuple[str, str]]] = defaultdict(list)
    for topic in topics:
        topic_id = str(topic.get("id", "<missing-id>"))
        if not str(topic.get("label", "")).strip():
            fail(errors, campaign, f"{topic_id}: missing label")
        source = str(topic.get("source", ""))
        sources[source] += 1
        if source not in {"existing", "expanded"}:
            fail(errors, campaign, f"{topic_id}: source must be existing or expanded")
        if topic.get("status") != "formal":
            fail(errors, campaign, f"{topic_id}: status must be formal")
        asset_id = str(topic.get("assetId", ""))
        delivery = delivery_by_id.get(asset_id)
        if delivery is None:
            fail(errors, campaign, f"{topic_id}: unknown assetId {asset_id!r}")
            continue
        if referenced_topics[topic_id] != 1:
            fail(
                errors,
                campaign,
                f"{topic_id}: appears {referenced_topics[topic_id]} times in delivery topicIds",
            )
        image = images.get(asset_id)
        box = topic_cell_box(topic, delivery)
        if image is not None and box is not None:
            left, top, right, bottom = box
            if not (
                0 <= left < right <= image.width and 0 <= top < bottom <= image.height
            ):
                fail(errors, campaign, f"{topic_id}: cell {box} is outside {image.size}")
            elif image.crop(box).getchannel("A").getbbox() is None:
                fail(errors, campaign, f"{topic_id}: mapped cell is empty")
            else:
                cell_checked += 1
                cell_image = image.crop(box).convert("RGBA")
                normalized = cell_image.resize((32, 32), Image.Resampling.NEAREST)
                visual_hash = hashlib.sha256(normalized.tobytes()).hexdigest()
                cell_visuals[str(topic.get("category", ""))].append(
                    (topic_id, visual_hash)
                )
                alpha = normalized.getchannel("A")
                if alpha.getextrema() == (0, 255):
                    silhouette_hash = hashlib.sha256(alpha.tobytes()).hexdigest()
                    silhouette_visuals[str(topic.get("category", ""))].append(
                        (topic_id, silhouette_hash)
                    )

    orphan_refs = sorted(set(referenced_topics) - set(topic_ids))
    if orphan_refs:
        fail(errors, campaign, f"delivery topicIds not in topics: {orphan_refs[:8]}")
    if sources.get("existing", 0) != 22 or sources.get("expanded", 0) != 382:
        fail(
            errors,
            campaign,
            f"source counts {dict(sources)} != existing 22 / expanded 382",
        )

    diversity: dict[str, dict[str, int]] = {}
    for category, entries in sorted(cell_visuals.items()):
        hashes = Counter(value for _topic_id, value in entries)
        duplicates = [value for value, count in hashes.items() if count > 1]
        if duplicates:
            examples = [
                [topic_id for topic_id, value in entries if value == duplicate]
                for duplicate in duplicates[:4]
            ]
            fail(errors, campaign, f"{category}: exact duplicate cells {examples}")
        silhouettes = silhouette_visuals.get(category, [])
        unique_silhouettes = len({value for _topic_id, value in silhouettes})
        if len(silhouettes) >= 8:
            required_silhouettes = math.ceil(len(silhouettes) * 0.5)
            if unique_silhouettes < required_silhouettes:
                fail(
                    errors,
                    campaign,
                    f"{category}: only {unique_silhouettes}/{len(silhouettes)} "
                    f"unique transparent silhouettes; require {required_silhouettes}",
                )
        diversity[category] = {
            "cells": len(entries),
            "uniqueCells": len(hashes),
            "transparentCells": len(silhouettes),
            "uniqueSilhouettes": unique_silhouettes,
        }

    return {
        "campaign": campaign,
        "topics": len(topics),
        "categoryCounts": dict(category_counts),
        "sourceCounts": dict(sources),
        "deliveries": len(deliveries),
        "outputFiles": len(deliveries) * 2,
        "cellMappingsChecked": cell_checked,
        "visualDiversity": diversity,
        "errors": errors,
    }


def main() -> None:
    results = [validate_campaign(campaign) for campaign in CAMPAIGNS]
    errors = [error for result in results for error in result["errors"]]
    report = {
        "campaigns": results,
        "totals": {
            "topics": sum(int(result.get("topics", 0)) for result in results),
            "targetTopics": 1212,
            "deliveries": sum(int(result.get("deliveries", 0)) for result in results),
            "outputFiles": sum(int(result.get("outputFiles", 0)) for result in results),
            "errors": len(errors),
        },
        "errors": errors,
    }
    print(json.dumps(report, ensure_ascii=False, indent=2))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
