#!/usr/bin/env python3
"""Validate isolated runtime-v2 batch-03 combat closeout packs."""

from __future__ import annotations

import hashlib
import json
import re
from collections import Counter
from pathlib import Path

from PIL import Image

from validate_runtime_v2 import file_pair
from validate_runtime_v2_batches import runtime_category, validate_batch_unit


ROOT = Path(__file__).resolve().parents[1]
CAMPAIGNS = ("candidate-01", "candidate-02", "candidate-03")
BATCH = "b03"


def main() -> None:
    reports = []
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
            errors.append(f"{campaign}/{BATCH}: runtimeReady must remain false")
        if manifest.get("batchId") not in {BATCH, "batch-03", 3}:
            errors.append(f"{campaign}/{BATCH}: batchId must identify batch 03")

        complete = json.loads((asset_root / "manifest-complete.json").read_text(encoding="utf-8"))
        topics = {str(topic["id"]): topic for topic in complete["topics"]}
        all_combat = {topic_id for topic_id, topic in topics.items() if topic.get("category") == "combat-unit"}
        assets = manifest.get("assets")
        if not isinstance(assets, list):
            errors.append(f"{campaign}/{BATCH}: assets must be an array")
            assets = []
        ids = [str(asset.get("id", "")) for asset in assets]
        content_ids = [str(asset.get("contentId", "")) for asset in assets]
        topic_ids = [str(asset.get("topicId", "")) for asset in assets]
        for label, values in (("asset id", ids), ("contentId", content_ids), ("topicId", topic_ids)):
            duplicates = [value for value, count in Counter(values).items() if value and count > 1]
            if "" in values: errors.append(f"{campaign}/{BATCH}: missing {label}")
            if duplicates: errors.append(f"{campaign}/{BATCH}: duplicate {label}s {duplicates}")

        prior_content = set()
        batch02_topics = set()
        for name in ("manifest-runtime-v2.json", "manifest-runtime-v2-b02.json"):
            prior_path = asset_root / name
            if not prior_path.is_file():
                errors.append(f"{campaign}/{BATCH}: missing prior manifest {name}")
                continue
            prior = json.loads(prior_path.read_text(encoding="utf-8"))
            prior_content.update(str(asset.get("contentId", "")) for asset in prior.get("assets", []))
            if name.endswith("b02.json"):
                batch02_topics.update(
                    str(asset.get("topicId")) for asset in prior.get("assets", [])
                    if runtime_category(asset) == "combat-unit"
                )
        collisions = sorted(prior_content & set(content_ids))
        if collisions: errors.append(f"{campaign}/{BATCH}: contentIds collide with earlier packs {collisions}")

        hashes = []
        coverage = Counter()
        for asset in assets:
            asset_id = str(asset.get("id", "<missing-id>"))
            category = runtime_category(asset)
            coverage[category] += 1
            topic_id = str(asset.get("topicId", ""))
            topic = topics.get(topic_id)
            if topic is None:
                errors.append(f"{campaign}/{asset_id}: unknown topicId {topic_id!r}")
            else:
                if topic.get("category") != "combat-unit" or category != "combat-unit":
                    errors.append(f"{campaign}/{asset_id}: batch-03 accepts combat-unit topics only")
                if topic.get("source") != "expanded":
                    errors.append(f"{campaign}/{asset_id}: batch-03 cannot reclaim existing formal topic")
            if topic_id in batch02_topics:
                errors.append(f"{campaign}/{asset_id}: topic already claimed by batch-02")
            for label in ("png", "svg"):
                value = asset.get(label)
                if isinstance(value, str) and not value.startswith("runtime-v2/batch-03/"):
                    errors.append(f"{campaign}/{asset_id}: {label} must stay under runtime-v2/batch-03")
            image = file_pair(campaign, asset_root, asset, errors)
            if image is None: continue
            validate_batch_unit(campaign, asset, image, errors)
            normalized = image.resize((64, 32), Image.Resampling.NEAREST)
            hashes.append((asset_id, hashlib.sha256(normalized.tobytes()).hexdigest()))

        if len(assets) != 28 or coverage != Counter({"combat-unit": 28}):
            errors.append(f"{campaign}/{BATCH}: strict coverage must be exactly 28 combat-unit assets, got {dict(coverage)} / {len(assets)}")
        duplicates = [value for value, count in Counter(value for _, value in hashes).items() if count > 1]
        if duplicates:
            examples = [[asset_id for asset_id, value in hashes if value == repeated] for repeated in duplicates]
            errors.append(f"{campaign}/{BATCH}: exact duplicate combat sheets {examples}")

        lock_value = manifest.get("topicLock")
        if not isinstance(lock_value, str) or not lock_value.startswith("runtime-v2/batch-03/"):
            errors.append(f"{campaign}/{BATCH}: valid isolated topicLock path is required")
        else:
            lock_path = asset_root / lock_value
            if not lock_path.is_file():
                errors.append(f"{campaign}/{BATCH}: missing topic lock")
            else:
                batch03_topics = set(topic_ids)
                if lock_path.suffix.casefold() == ".json":
                    lock = json.loads(lock_path.read_text(encoding="utf-8"))
                    primary_topics = set(lock.get("primaryTopicIds", []))
                    locked_batch02 = set(lock.get("batch02TopicIds", []))
                else:
                    lock_text = lock_path.read_text(encoding="utf-8")
                    mentioned_topics = set(re.findall(r"`([^`]+)`", lock_text)) & all_combat
                    locked_batch02 = set(batch02_topics)
                    primary_topics = all_combat - locked_batch02 - batch03_topics
                    if not (primary_topics | locked_batch02 | batch03_topics) <= mentioned_topics:
                        errors.append(
                            f"{campaign}/{BATCH}: topic lock document does not mention the complete 4+8+28 closure"
                        )
                union = primary_topics | locked_batch02 | batch03_topics
                overlaps = (primary_topics & locked_batch02) | (primary_topics & batch03_topics) | (locked_batch02 & batch03_topics)
                if locked_batch02 != batch02_topics:
                    errors.append(f"{campaign}/{BATCH}: topic lock batch-02 set differs from prior manifest")
                if len(primary_topics) != 4 or len(locked_batch02) != 8 or len(batch03_topics) != 28 or union != all_combat or overlaps:
                    errors.append(
                        f"{campaign}/{BATCH}: cumulative combat closure invalid: primary={len(primary_topics)} b02={len(locked_batch02)} b03={len(batch03_topics)} union={len(union)} overlaps={sorted(overlaps)}"
                    )

        batch_root = asset_root / "runtime-v2" / "batch-03"
        prompts_path = batch_root / "PROMPTS.md"
        if not prompts_path.is_file():
            errors.append(f"{campaign}/{BATCH}: missing production artifact PROMPTS.md")
        production_scripts = [
            path for path in batch_root.rglob("*.py")
            if "__pycache__" not in path.parts and any(token in path.name.casefold() for token in ("build", "process"))
        ]
        if not production_scripts:
            errors.append(f"{campaign}/{BATCH}: missing reproducible build/process script")
        previews = list((batch_root / "previews").glob("*1x.png"))
        if not previews:
            errors.append(f"{campaign}/{BATCH}: missing 1x combat preview")
        qa_path = asset_root / f"qa-runtime-v2-{BATCH}.json"
        if not qa_path.is_file():
            errors.append(f"{campaign}/{BATCH}: missing independent QA report")
        else:
            qa = json.loads(qa_path.read_text(encoding="utf-8"))
            if qa.get("passed") is not True or qa.get("errors") not in ([], None):
                errors.append(f"{campaign}/{BATCH}: independent QA report did not pass")

        reports.append({
            "campaign": campaign, "batch": BATCH,
            "status": "passed-machine-qa" if not errors else "failed",
            "assets": len(assets), "coverage": dict(coverage),
            "cumulativeCombatCoverage": "40/40" if not errors else "invalid",
            "errors": errors,
        })
        all_errors.extend(errors)
    print(json.dumps({"validator": "runtime-v2-b03-combat-closeout", "campaigns": reports, "errors": all_errors}, ensure_ascii=False, indent=2))
    if all_errors: raise SystemExit(1)


if __name__ == "__main__":
    main()
