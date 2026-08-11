#!/usr/bin/env python3
"""Lock C02 runtime-v2 batch-02 topics against the complete 404-topic manifest."""

from __future__ import annotations

import json
from pathlib import Path


BATCH = Path(__file__).resolve().parents[1]
ASSET_ROOT = BATCH.parents[1]
COMPLETE = ASSET_ROOT / "manifest-complete.json"
OUTPUT = BATCH / "TOPIC-LOCK.json"

SELECTIONS = {
    "combat-unit": [
        "c02-unit-power-swordsman", "c02-unit-symbiotic-guard", "c02-unit-sand-rider",
        "c02-unit-hover-lancer", "c02-unit-tide-fin-scout", "c02-unit-long-beam-sniper",
        "c02-unit-spore-shooter", "c02-unit-star-vein-mage",
    ],
    "mission-unit": [
        "c02-mission-water-maintainer", "c02-mission-water-collector", "c02-mission-red-sand-child",
        "c02-mission-cargo-crew", "c02-mission-refugee", "c02-mission-patient",
        "c02-mission-dock-builder", "c02-mission-heat-lamp-monk",
    ],
    "terrain": [
        "c02-terrain-hesha-glass-sea", "c02-terrain-hesha-sealed-waterway",
        "c02-terrain-soler-brittle-ice", "c02-terrain-soler-heated-walkway",
        "c02-terrain-nereia-reverse-current", "c02-terrain-verdant-fungus-bed",
        "c02-terrain-kairon-train-platform", "c02-terrain-farlight-living-deck",
    ],
    "interactive-structure": [
        "c02-structure-star-vein-node", "c02-structure-tide-anchor-console",
        "c02-structure-gravity-switch", "c02-structure-water-filter-station",
    ],
    "battle-prop": [
        "c02-battle-prop-ceramic-water-tanks", "c02-battle-prop-cargo-crates",
        "c02-battle-prop-platform-shields", "c02-battle-prop-giant-roots",
        "c02-battle-prop-overload-nodes", "c02-battle-prop-fuel-oxygen-tanks",
        "c02-battle-prop-ice-cracks", "c02-battle-prop-spore-sacs",
    ],
    "equipment": [
        "c02-equipment-power-swordsman", "c02-equipment-symbiotic-guard",
        "c02-equipment-sand-rider", "c02-equipment-hover-lancer",
        "c02-equipment-tide-fin-scout", "c02-equipment-long-beam-sniper",
        "c02-equipment-spore-shooter", "c02-equipment-star-vein-mage",
    ],
    "skill": [
        "c02-skill-assault", "c02-skill-rescue-dash", "c02-skill-snipe",
        "c02-skill-spore-shot", "c02-skill-vein-downgrade", "c02-skill-sand-sail",
        "c02-skill-shift-tide", "c02-skill-symbiotic-regen",
    ],
    "status": [
        "c02-status-controlled", "c02-status-hidden", "c02-status-overheated", "c02-status-hypothermia",
    ],
    "hud": [
        "c02-hud-ally", "c02-hud-enemy", "c02-hud-main-objective", "c02-hud-danger",
    ],
    "fx": [
        "c02-fx-blade-hit", "c02-fx-explosion", "c02-fx-bio-hit", "c02-fx-vacuum-rupture",
    ],
    "narrative-static": [
        "c02-scene-growing-wall", "c02-scene-inverted-city-maintenance",
        "c02-scene-soler-ice-beacon", "c02-scene-nereia-platform-vote",
    ],
}

PREFIX = {
    "combat-unit": "stars.unit.", "mission-unit": "stars.mission.", "terrain": "stars.terrain.",
    "interactive-structure": "stars.structure.", "battle-prop": "stars.prop.",
    "equipment": "stars.equipment.", "skill": "stars.skill.", "status": "stars.status.",
    "hud": "stars.hud.", "fx": "stars.fx.", "narrative-static": "stars.scene.",
}


def slug(topic_id: str, category: str) -> str:
    stems = {
        "combat-unit": "c02-unit-", "mission-unit": "c02-mission-", "terrain": "c02-terrain-",
        "interactive-structure": "c02-structure-", "battle-prop": "c02-battle-prop-",
        "equipment": "c02-equipment-", "skill": "c02-skill-", "status": "c02-status-",
        "hud": "c02-hud-", "fx": "c02-fx-", "narrative-static": "c02-scene-",
    }
    return topic_id.removeprefix(stems[category])


def main() -> None:
    complete = json.loads(COMPLETE.read_text(encoding="utf-8"))
    topics = {topic["id"]: topic for topic in complete["topics"]}
    locked = []
    for category, topic_ids in SELECTIONS.items():
        for topic_id in topic_ids:
            topic = topics.get(topic_id)
            if topic is None:
                raise SystemExit(f"missing complete topic: {topic_id}")
            if topic["category"] != category:
                raise SystemExit(f"category mismatch: {topic_id}")
            locked.append({
                "topicId": topic_id,
                "label": topic["label"],
                "category": category,
                "contentId": PREFIX[category] + slug(topic_id, category),
            })
    if len(locked) != 68 or len({row["topicId"] for row in locked}) != 68:
        raise SystemExit("batch-02 must lock exactly 68 unique topics")
    document = {
        "schemaVersion": "1.0.0",
        "campaignId": "candidate-02",
        "batchId": "runtime-v2-b02",
        "runtimeReady": False,
        "sourceManifest": "manifest-complete.json",
        "counts": {category: len(values) for category, values in SELECTIONS.items()},
        "total": 68,
        "topics": locked,
    }
    OUTPUT.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Locked {len(locked)} unique C02 topics -> {OUTPUT}")


if __name__ == "__main__":
    main()
