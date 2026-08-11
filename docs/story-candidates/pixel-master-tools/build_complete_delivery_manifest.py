#!/usr/bin/env python3
"""Merge the three campaign-complete manifests into one runtime index."""

from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "COMPLETE-ASSET-MANIFEST.json"
CAMPAIGNS = ("candidate-01", "candidate-02", "candidate-03")
TITLES = {
    "candidate-01": "断冠之誓",
    "candidate-02": "群星熄灭之前",
    "candidate-03": "布衣定鼎",
}


def main() -> None:
    campaigns: list[dict[str, object]] = []
    total_topics = 0
    total_deliveries = 0
    total_outputs = 0
    for campaign_id in CAMPAIGNS:
        relative_manifest = Path(campaign_id) / "assets" / "manifest-complete.json"
        manifest_path = ROOT / relative_manifest
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        topics = manifest["topics"]
        deliveries = manifest["deliveries"]
        total_topics += len(topics)
        total_deliveries += len(deliveries)
        total_outputs += len(deliveries) * 2
        campaigns.append(
            {
                "id": campaign_id,
                "title": manifest.get("title") or TITLES[campaign_id],
                "assetRoot": f"{campaign_id}/assets",
                "manifest": str(relative_manifest),
                "qualityTier": manifest.get("qualityTier", "prototype"),
                "runtimeReady": bool(manifest.get("runtimeReady", False)),
                "topics": len(topics),
                "deliveries": len(deliveries),
                "outputFiles": len(deliveries) * 2,
                "categoryTargets": manifest["categoryTargets"],
            }
        )

    if total_topics != 1212:
        raise ValueError(f"complete topic count {total_topics} != 1212")
    result = {
        "schemaVersion": "2.0.0",
        "qualityTier": "prototype",
        "runtimeReady": False,
        "intendedUse": "topic coverage and engine wiring; not final game art",
        "totalTopics": total_topics,
        "totalDeliveries": total_deliveries,
        "totalOutputFiles": total_outputs,
        "campaigns": campaigns,
    }
    OUTPUT.write_text(
        json.dumps(result, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
