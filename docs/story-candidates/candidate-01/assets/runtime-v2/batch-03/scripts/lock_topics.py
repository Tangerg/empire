#!/usr/bin/env python3
"""Lock the final 28 C01 combat topics after primary and batch-02 coverage."""

from __future__ import annotations

import json
from pathlib import Path


BATCH = Path(__file__).resolve().parents[1]
ASSET_ROOT = BATCH.parents[1]
OUTPUT = BATCH / "TOPIC-LOCK.json"

PRIMARY_CONTENT_TO_TOPIC = {
    "soldier": "C01-UNIT-SPEARMAN",
    "archer": "C01-UNIT-ARCHER",
    "knight": "C01-UNIT-KNIGHT",
    "cleric": "C01-UNIT-CLERIC",
}


def main() -> None:
    complete = json.loads((ASSET_ROOT / "manifest-complete.json").read_text(encoding="utf-8"))
    primary = json.loads((ASSET_ROOT / "manifest-runtime-v2.json").read_text(encoding="utf-8"))
    batch_02 = json.loads((ASSET_ROOT / "manifest-runtime-v2-b02.json").read_text(encoding="utf-8"))
    combat = [topic for topic in complete["topics"] if topic["category"] == "combat-unit"]
    primary_content = {asset["contentId"] for asset in primary["assets"]}
    missing = sorted(set(PRIMARY_CONTENT_TO_TOPIC) - primary_content)
    if missing:
        raise SystemExit(f"primary semantic contentIds missing: {missing}")
    primary_topics = set(PRIMARY_CONTENT_TO_TOPIC.values())
    batch_02_topics = {
        asset["topicId"] for asset in batch_02["assets"]
        if asset["type"] == "combat-unit"
    }
    if len(combat) != 40 or len(primary_topics) != 4 or len(batch_02_topics) != 8:
        raise SystemExit("expected combat total 40, primary 4 and batch-02 8")
    if primary_topics & batch_02_topics:
        raise SystemExit("primary topics overlap batch-02")
    remaining = [topic for topic in combat if topic["id"] not in primary_topics | batch_02_topics]
    if len(remaining) != 28:
        raise SystemExit(f"expected 28 remaining combat topics, got {len(remaining)}")
    rows = [
        {
            "topicId": topic["id"],
            "label": topic["label"],
            "category": "combat-unit",
            "contentId": "crown.unit." + topic["id"].removeprefix("C01-UNIT-").lower(),
        }
        for topic in remaining
    ]
    document = {
        "schemaVersion": "1.0.0",
        "campaignId": "candidate-01",
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
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Locked final {len(rows)} C01 combat topics -> {OUTPUT}")


if __name__ == "__main__":
    main()
