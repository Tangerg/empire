#!/usr/bin/env python3
"""Lock the final 28 C02 combat topics after primary and batch-02 coverage."""

from __future__ import annotations

import json
from pathlib import Path


BATCH = Path(__file__).resolve().parents[1]
ASSET_ROOT = BATCH.parents[1]
OUTPUT = BATCH / "TOPIC-LOCK.json"

PRIMARY_CONTENT_TO_TOPIC = {
    "stars.unit.star-shield-trooper": "c02-unit-star-shield",
    "stars.unit.rail-rifleman": "c02-unit-rail-rifleman",
    "stars.unit.repair-engineer": "c02-unit-repair-engineer",
    "stars.unit.public-order-guard-robot": "c02-unit-guard-robot",
}


def main() -> None:
    complete = json.loads((ASSET_ROOT / "manifest-complete.json").read_text(encoding="utf-8"))
    primary = json.loads((ASSET_ROOT / "manifest-runtime-v2.json").read_text(encoding="utf-8"))
    batch_02 = json.loads((ASSET_ROOT / "manifest-runtime-v2-b02.json").read_text(encoding="utf-8"))
    combat = [topic for topic in complete["topics"] if topic["category"] == "combat-unit"]
    combat_by_id = {topic["id"]: topic for topic in combat}
    primary_content = {asset["contentId"] for asset in primary["assets"]}
    missing_primary = sorted(set(PRIMARY_CONTENT_TO_TOPIC) - primary_content)
    if missing_primary:
        raise SystemExit(f"primary semantic contentIds missing: {missing_primary}")
    primary_topics = set(PRIMARY_CONTENT_TO_TOPIC.values())
    batch_02_topics = {
        asset["topicId"] for asset in batch_02["assets"]
        if asset["type"] in {"combat-unit-sheet", "combat-unit-sprite", "unit-sheet"}
    }
    if len(primary_topics) != 4 or len(batch_02_topics) != 8 or primary_topics & batch_02_topics:
        raise SystemExit("expected disjoint primary 4 + batch-02 8 combat topics")
    used = primary_topics | batch_02_topics
    remaining = [topic for topic in combat if topic["id"] not in used]
    if len(combat) != 40 or len(remaining) != 28:
        raise SystemExit(f"expected 40 total / 28 remaining, got {len(combat)} / {len(remaining)}")
    rows = [{
        "topicId": topic["id"],
        "label": topic["label"],
        "category": "combat-unit",
        "contentId": "stars.unit." + topic["id"].removeprefix("c02-unit-"),
    } for topic in remaining]
    if len({row["contentId"] for row in rows}) != 28:
        raise SystemExit("duplicate batch-03 contentId")
    document = {
        "schemaVersion": "1.0.0",
        "campaignId": "candidate-02",
        "batchId": "b03",
        "runtimeReady": False,
        "sourceManifest": "manifest-complete.json",
        "primarySemanticMapping": PRIMARY_CONTENT_TO_TOPIC,
        "primaryTopicIds": sorted(primary_topics),
        "batch02TopicIds": sorted(batch_02_topics),
        "previousCombatCoverage": 12,
        "batchCombatCoverage": 28,
        "cumulativeCombatCoverage": 40,
        "topics": rows,
    }
    OUTPUT.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Locked final {len(rows)} combat topics -> {OUTPUT}")


if __name__ == "__main__":
    main()
