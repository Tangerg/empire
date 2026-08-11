#!/usr/bin/env python3
"""Generate a deterministic runtime-v2 production coverage status report.

This reporter deliberately separates runtime files from exact topic coverage.
An asset in the primary manifest without ``topicId`` is an output, but it is not
an exact 404-topic delivery.  This distinction is especially important for the
four primary combat-unit sheets in each campaign.
"""

from __future__ import annotations

import argparse
import json
import re
import struct
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any


STORY_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_JSON = STORY_ROOT / "RUNTIME-V2-STATUS.json"
DEFAULT_MARKDOWN = STORY_ROOT / "RUNTIME-V2-STATUS.md"
CAMPAIGNS = ("candidate-01", "candidate-02", "candidate-03")
CATEGORY_ORDER = (
    "narrative-static",
    "combat-unit",
    "mission-unit",
    "faction-kit",
    "terrain",
    "interactive-structure",
    "battle-prop",
    "equipment",
    "skill",
    "status",
    "fx",
    "hud",
)
CATEGORY_LABELS = {
    "narrative-static": "叙事静态图",
    "combat-unit": "战斗单位",
    "mission-unit": "任务单位",
    "faction-kit": "阵营套件",
    "terrain": "地形",
    "interactive-structure": "交互设施",
    "battle-prop": "战场物件",
    "equipment": "装备",
    "skill": "技能",
    "status": "状态",
    "fx": "FX",
    "hud": "HUD",
}


@dataclass(frozen=True)
class RuntimeAsset:
    manifest: Path
    batch: str
    runtime_ready: bool
    asset_id: str
    content_id: str
    topic_id: str
    category: str
    exact_topic: bool


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--check-only",
        action="store_true",
        help="validate and print a compact summary without writing status files",
    )
    parser.add_argument("--json-output", type=Path, default=DEFAULT_JSON)
    parser.add_argument("--markdown-output", type=Path, default=DEFAULT_MARKDOWN)
    return parser.parse_args()


def read_json(path: Path, errors: list[str]) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        errors.append(f"{relative(path)}: cannot read JSON: {exc}")
        return {}
    if not isinstance(value, dict):
        errors.append(f"{relative(path)}: top level must be an object")
        return {}
    return value


def relative(path: Path) -> str:
    try:
        return path.resolve().relative_to(STORY_ROOT.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def png_dimensions(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("invalid PNG signature or truncated IHDR")
    return struct.unpack(">II", header[16:24])


def normalize_category(asset_type: object) -> str:
    value = str(asset_type or "").strip().casefold()
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
    if value in CATEGORY_ORDER:
        return value
    return aliases.get(value, "")


def manifest_batch(path: Path, manifest: dict[str, Any]) -> str:
    if path.name == "manifest-runtime-v2.json":
        return "primary"
    raw = str(manifest.get("batchId") or "")
    match = re.search(r"(?:^|[-_])b(?:atch[-_]?)?(\d+)$", raw, re.IGNORECASE)
    if not match:
        match = re.search(r"-b(\d+)\.json$", path.name, re.IGNORECASE)
    return f"b{int(match.group(1)):02d}" if match else path.stem


def manifest_sort_key(path: Path) -> tuple[int, int, str]:
    if path.name == "manifest-runtime-v2.json":
        return (0, 0, path.name)
    match = re.search(r"-b(\d+)\.json$", path.name, re.IGNORECASE)
    return (1, int(match.group(1)) if match else 9999, path.name)


def validate_png(
    campaign: str,
    asset_root: Path,
    manifest_path: Path,
    asset: dict[str, Any],
    errors: list[str],
) -> None:
    asset_id = str(asset.get("id") or "<missing-id>")
    png_value = asset.get("png")
    if not isinstance(png_value, str) or not png_value:
        errors.append(f"{campaign}/{manifest_path.name}/{asset_id}: missing PNG path")
        return
    png_path = (asset_root / png_value).resolve()
    try:
        png_path.relative_to(asset_root.resolve())
    except ValueError:
        errors.append(f"{campaign}/{manifest_path.name}/{asset_id}: PNG escapes asset root")
        return
    if not png_path.is_file():
        errors.append(f"{campaign}/{manifest_path.name}/{asset_id}: missing PNG {png_value}")
        return
    try:
        actual = png_dimensions(png_path)
    except (OSError, ValueError) as exc:
        errors.append(f"{campaign}/{manifest_path.name}/{asset_id}: unreadable PNG: {exc}")
        return
    try:
        declared = (int(asset["width"]), int(asset["height"]))
    except (KeyError, TypeError, ValueError):
        errors.append(f"{campaign}/{manifest_path.name}/{asset_id}: invalid declared width/height")
        return
    if actual != declared:
        errors.append(
            f"{campaign}/{manifest_path.name}/{asset_id}: PNG size {actual} != declared {declared}"
        )


def count_row(target: int, items: list[RuntimeAsset]) -> dict[str, int]:
    exact_topics = {item.topic_id for item in items if item.exact_topic}
    ready = sum(item.runtime_ready for item in items)
    return {
        "targetCount": target,
        "outputCount": len(items),
        "exactTopicCount": len(exact_topics),
        "runtimeReadySourceCount": ready,
        "candidateCount": len(items) - ready,
    }


def build_campaign(campaign: str, errors: list[str]) -> dict[str, Any]:
    asset_root = STORY_ROOT / campaign / "assets"
    complete_path = asset_root / "manifest-complete.json"
    complete = read_json(complete_path, errors)
    targets_value = complete.get("categoryTargets", {})
    if not isinstance(targets_value, dict):
        errors.append(f"{campaign}: manifest-complete categoryTargets must be an object")
        targets_value = {}
    targets: dict[str, int] = {}
    for category in CATEGORY_ORDER:
        try:
            targets[category] = int(targets_value.get(category, 0))
        except (TypeError, ValueError):
            errors.append(f"{campaign}: invalid target for {category}")
            targets[category] = 0
    unexpected_targets = sorted(set(targets_value) - set(CATEGORY_ORDER))
    if unexpected_targets:
        errors.append(f"{campaign}: unexpected target categories {unexpected_targets}")

    topics_value = complete.get("topics", [])
    if not isinstance(topics_value, list):
        errors.append(f"{campaign}: manifest-complete topics must be an array")
        topics_value = []
    topics: dict[str, dict[str, Any]] = {}
    for index, topic in enumerate(topics_value):
        if not isinstance(topic, dict) or not topic.get("id"):
            errors.append(f"{campaign}: invalid complete topic at index {index}")
            continue
        topic_id = str(topic["id"])
        if topic_id in topics:
            errors.append(f"{campaign}: duplicate topic in manifest-complete: {topic_id}")
        topics[topic_id] = topic

    topic_category_counts = Counter(str(topic.get("category") or "") for topic in topics.values())
    for category, target in targets.items():
        if topic_category_counts[category] != target:
            errors.append(
                f"{campaign}: complete {category} topics {topic_category_counts[category]} != target {target}"
            )
    unknown_topic_categories = sorted(set(topic_category_counts) - set(targets) - {""})
    if unknown_topic_categories:
        errors.append(f"{campaign}: unknown complete topic categories {unknown_topic_categories}")

    declared_total = complete.get("targetTopics")
    target_total = sum(targets.values())
    if declared_total is not None:
        try:
            if int(declared_total) != target_total:
                errors.append(
                    f"{campaign}: targetTopics {declared_total} != category target sum {target_total}"
                )
        except (TypeError, ValueError):
            errors.append(f"{campaign}: targetTopics must be an integer")
    if len(topics) != target_total:
        errors.append(f"{campaign}: complete topic count {len(topics)} != target sum {target_total}")

    manifests: list[dict[str, Any]] = []
    runtime_assets: list[RuntimeAsset] = []
    content_occurrences: defaultdict[str, list[str]] = defaultdict(list)
    topic_occurrences: defaultdict[str, list[str]] = defaultdict(list)
    manifest_paths = sorted(asset_root.glob("manifest-runtime-v2*.json"), key=manifest_sort_key)
    if not manifest_paths:
        errors.append(f"{campaign}: no runtime-v2 manifests")

    for manifest_path in manifest_paths:
        manifest = read_json(manifest_path, errors)
        batch = manifest_batch(manifest_path, manifest)
        declared_runtime_ready = manifest.get("runtimeReady") is True
        assets_value = manifest.get("assets", [])
        if not isinstance(assets_value, list):
            errors.append(f"{campaign}/{manifest_path.name}: assets must be an array")
            assets_value = []
        declared_asset_count = manifest.get("assetCount")
        if declared_asset_count is not None:
            try:
                if int(declared_asset_count) != len(assets_value):
                    errors.append(
                        f"{campaign}/{manifest_path.name}: assetCount {declared_asset_count} != {len(assets_value)}"
                    )
            except (TypeError, ValueError):
                errors.append(f"{campaign}/{manifest_path.name}: invalid assetCount")

        asset_ids = [
            str(asset.get("id") or "")
            for asset in assets_value
            if isinstance(asset, dict)
        ]
        duplicated_asset_ids = sorted(
            asset_id for asset_id, count in Counter(asset_ids).items() if asset_id and count > 1
        )
        if duplicated_asset_ids:
            errors.append(
                f"{campaign}/{manifest_path.name}: duplicate asset ids {duplicated_asset_ids}"
            )

        game_integration = manifest.get("gameIntegration")
        has_asset_ready_list = (
            isinstance(game_integration, dict) and "runtimeReadyAssetIds" in game_integration
        )
        ready_asset_ids: set[str] = set()
        if has_asset_ready_list:
            ready_value = game_integration.get("runtimeReadyAssetIds")
            if not isinstance(ready_value, list) or not all(
                isinstance(value, str) and value for value in ready_value
            ):
                errors.append(
                    f"{campaign}/{manifest_path.name}: gameIntegration.runtimeReadyAssetIds "
                    "must be an array of non-empty asset ids"
                )
                ready_value = []
            if len(ready_value) != len(set(ready_value)):
                errors.append(
                    f"{campaign}/{manifest_path.name}: duplicate gameIntegration.runtimeReadyAssetIds"
                )
            asset_id_set = {asset_id for asset_id in asset_ids if asset_id}
            unknown_ready_ids = sorted(set(ready_value) - asset_id_set)
            if unknown_ready_ids:
                errors.append(
                    f"{campaign}/{manifest_path.name}: runtimeReadyAssetIds are not an assets subset: "
                    f"{unknown_ready_ids}"
                )
            ready_asset_ids = set(ready_value) & asset_id_set

        manifest_exact = 0
        manifest_ready = 0
        for index, raw_asset in enumerate(assets_value):
            if not isinstance(raw_asset, dict):
                errors.append(f"{campaign}/{manifest_path.name}: asset {index} is not an object")
                continue
            asset_id = str(raw_asset.get("id") or "")
            content_id = str(raw_asset.get("contentId") or "")
            topic_id = str(raw_asset.get("topicId") or "")
            asset_runtime_ready = (
                asset_id in ready_asset_ids if has_asset_ready_list else declared_runtime_ready
            )
            if not asset_id:
                errors.append(f"{campaign}/{manifest_path.name}: asset {index} missing id")
            if not content_id:
                errors.append(f"{campaign}/{manifest_path.name}/{asset_id or index}: missing contentId")
            else:
                content_occurrences[content_id].append(f"{manifest_path.name}:{asset_id}")

            category = normalize_category(raw_asset.get("type"))
            if not category:
                errors.append(
                    f"{campaign}/{manifest_path.name}/{asset_id or index}: unknown runtime type {raw_asset.get('type')!r}"
                )
            elif category not in targets:
                errors.append(
                    f"{campaign}/{manifest_path.name}/{asset_id or index}: category {category} has no target"
                )

            exact_topic = False
            if topic_id:
                topic_occurrences[topic_id].append(f"{manifest_path.name}:{asset_id}")
                topic = topics.get(topic_id)
                if topic is None:
                    errors.append(
                        f"{campaign}/{manifest_path.name}/{asset_id or index}: unknown topicId {topic_id}"
                    )
                elif str(topic.get("category")) != category:
                    errors.append(
                        f"{campaign}/{manifest_path.name}/{asset_id or index}: topic {topic_id} category "
                        f"{topic.get('category')!r} != runtime category {category!r}"
                    )
                else:
                    exact_topic = True
                    manifest_exact += 1

            validate_png(campaign, asset_root, manifest_path, raw_asset, errors)
            if category:
                runtime_assets.append(
                    RuntimeAsset(
                        manifest=manifest_path,
                        batch=batch,
                        runtime_ready=asset_runtime_ready,
                        asset_id=asset_id,
                        content_id=content_id,
                        topic_id=topic_id,
                        category=category,
                        exact_topic=exact_topic,
                    )
                )
                manifest_ready += int(asset_runtime_ready)

        effective_ready_state = (
            "complete"
            if assets_value and manifest_ready == len(assets_value)
            else "partial"
            if manifest_ready
            else "none"
        )

        manifests.append(
            {
                "path": relative(manifest_path),
                "batch": batch,
                "qualityTier": str(manifest.get("qualityTier") or "unknown"),
                "declaredRuntimeReady": declared_runtime_ready,
                "runtimeReady": effective_ready_state == "complete",
                "effectiveReadyState": effective_ready_state,
                "readyPolicy": (
                    "gameIntegration.runtimeReadyAssetIds"
                    if has_asset_ready_list
                    else "manifest.runtimeReady"
                ),
                "outputCount": len(assets_value),
                "exactTopicCount": manifest_exact,
                "runtimeReadySourceCount": manifest_ready,
                "candidateCount": len(assets_value) - manifest_ready,
            }
        )

    for content_id, locations in sorted(content_occurrences.items()):
        if len(locations) > 1:
            errors.append(f"{campaign}: duplicate contentId {content_id}: {locations}")
    for topic_id, locations in sorted(topic_occurrences.items()):
        if len(locations) > 1:
            errors.append(f"{campaign}: duplicate runtime topicId {topic_id}: {locations}")

    by_category: defaultdict[str, list[RuntimeAsset]] = defaultdict(list)
    for item in runtime_assets:
        by_category[item.category].append(item)
    categories = [
        {
            "category": category,
            "label": CATEGORY_LABELS[category],
            **count_row(targets[category], by_category[category]),
        }
        for category in CATEGORY_ORDER
    ]
    totals = count_row(target_total, runtime_assets)

    combat = by_category["combat-unit"]
    primary = [item for item in combat if item.batch == "primary"]
    batch02 = [item for item in combat if item.batch == "b02"]
    batch03 = [item for item in combat if item.batch == "b03"]
    primary_semantic = [item for item in primary if item.content_id and not item.topic_id]
    combat_exact_topics = {item.topic_id for item in combat if item.exact_topic}
    production_coverage = len(primary_semantic) + len(combat_exact_topics)
    combat_target = targets["combat-unit"]
    combat_report = {
        "targetCount": combat_target,
        "primary": {
            "outputCount": len(primary),
            "primarySemanticMappingCount": len(primary_semantic),
            "exactTopicCount": len({item.topic_id for item in primary if item.exact_topic}),
            "note": "Primary semantic mappings are named outputs only; without topicId they are not exact topic coverage.",
        },
        "batch02": {
            "outputCount": len(batch02),
            "exactTopicCount": len({item.topic_id for item in batch02 if item.exact_topic}),
        },
        "batch03": {
            "outputCount": len(batch03),
            "exactTopicCount": len({item.topic_id for item in batch03 if item.exact_topic}),
        },
        "cumulative": {
            "outputCount": len(combat),
            "exactTopicCount": len(combat_exact_topics),
            "semanticOnlyCount": len(primary_semantic),
            "productionCoverageCount": production_coverage,
            "targetCount": combat_target,
            "productionCoverageStatus": f"{production_coverage}/{combat_target}",
            "exactTopicStatus": f"{len(combat_exact_topics)}/{combat_target}",
            "productionComplete": production_coverage == combat_target,
            "exactTopicComplete": len(combat_exact_topics) == combat_target,
        },
    }

    return {
        "campaignId": campaign,
        "title": str(complete.get("campaignTitle") or complete.get("title") or campaign),
        "completeManifest": relative(complete_path),
        "targetTopics": target_total,
        "runtimeManifests": manifests,
        "totals": totals,
        "categories": categories,
        "combatCoverage": combat_report,
        "_identities": [
            {
                "contentId": item.content_id,
                "topicId": item.topic_id,
                "location": f"{campaign}/{item.manifest.name}:{item.asset_id}",
            }
            for item in runtime_assets
        ],
    }


def render_markdown(report: dict[str, Any]) -> str:
    lines = [
        "# Runtime V2 覆盖状态",
        "",
        "> 自动生成；请运行 `python3 pixel-master-tools/generate_runtime_v2_status.py` 更新。",
        "",
        "口径：`outputCount` 是运行时文件输出；`exactTopicCount` 只统计存在于对应 404 清单、类别匹配且不重复的 `topicId`。没有 `topicId` 的 primary 资产只算输出，不算精确题材覆盖。若 manifest 提供 `gameIntegration.runtimeReadyAssetIds`，`runtimeReadySourceCount` 只统计列表内资产；否则才回退到整包 `runtimeReady`。其余输出计入 `candidateCount`。",
        "",
    ]
    for campaign in report["campaigns"]:
        totals = campaign["totals"]
        lines.extend(
            [
                f"## {campaign['title']}（{campaign['campaignId']}）",
                "",
                f"目标 {totals['targetCount']}；运行时输出 {totals['outputCount']}；精确 topic {totals['exactTopicCount']}；实机来源 {totals['runtimeReadySourceCount']}；候选 {totals['candidateCount']}。",
                "",
                "| 类别 | 目标 | 输出 | 精确 topic | 实机来源 | 候选 |",
                "| --- | ---: | ---: | ---: | ---: | ---: |",
            ]
        )
        for row in campaign["categories"]:
            lines.append(
                f"| {row['label']} (`{row['category']}`) | {row['targetCount']} | "
                f"{row['outputCount']} | {row['exactTopicCount']} | "
                f"{row['runtimeReadySourceCount']} | {row['candidateCount']} |"
            )
        combat = campaign["combatCoverage"]
        cumulative = combat["cumulative"]
        lines.extend(
            [
                "",
                "### 战斗单位 40 母型",
                "",
                "| 阶段 | 输出 | 精确 topic | 说明 |",
                "| --- | ---: | ---: | --- |",
                f"| Primary | {combat['primary']['outputCount']} | {combat['primary']['exactTopicCount']} | 语义映射 {combat['primary']['primarySemanticMappingCount']}；无 topicId 不算精确覆盖 |",
                f"| Batch-02 | {combat['batch02']['outputCount']} | {combat['batch02']['exactTopicCount']} | 精确 topicId |",
                f"| Batch-03 | {combat['batch03']['outputCount']} | {combat['batch03']['exactTopicCount']} | 精确 topicId |",
                f"| 累计 | {cumulative['outputCount']} | {cumulative['exactTopicCount']} | 生产覆盖 {cumulative['productionCoverageStatus']}；精确题材 {cumulative['exactTopicStatus']} |",
                "",
                "来源 manifest：",
                "",
            ]
        )
        for manifest in campaign["runtimeManifests"]:
            status = {
                "complete": "runtimeReady",
                "partial": "partial",
                "none": "candidate",
            }[manifest["effectiveReadyState"]]
            lines.append(
                f"- `{manifest['batch']}` · {status} · {manifest['outputCount']} outputs · "
                f"{manifest['exactTopicCount']} exact topics · "
                f"{manifest['runtimeReadySourceCount']} ready / {manifest['candidateCount']} candidate · "
                f"policy `{manifest['readyPolicy']}` · `{manifest['path']}`"
            )
        lines.append("")

    validation = report["validation"]
    lines.extend(
        [
            "## 校验",
            "",
            f"状态：**{'通过' if validation['passed'] else '失败'}**；错误 {validation['errorCount']}。",
            "",
        ]
    )
    if validation["errors"]:
        lines.extend(f"- {error}" for error in validation["errors"])
        lines.append("")
    return "\n".join(lines)


def main() -> int:
    args = parse_args()
    errors: list[str] = []
    campaigns = [build_campaign(campaign, errors) for campaign in CAMPAIGNS]
    global_content_ids: defaultdict[str, list[str]] = defaultdict(list)
    global_topic_ids: defaultdict[str, list[str]] = defaultdict(list)
    for campaign in campaigns:
        for identity in campaign.pop("_identities"):
            if identity["contentId"]:
                global_content_ids[identity["contentId"]].append(identity["location"])
            if identity["topicId"]:
                global_topic_ids[identity["topicId"]].append(identity["location"])
    for label, occurrences in (
        ("contentId", global_content_ids),
        ("topicId", global_topic_ids),
    ):
        for value, locations in sorted(occurrences.items()):
            campaigns_present = {location.split("/", 1)[0] for location in locations}
            if len(campaigns_present) > 1:
                errors.append(f"cross-campaign duplicate {label} {value}: {locations}")

    report = {
        "schemaVersion": "1.0.0",
        "generator": "pixel-master-tools/generate_runtime_v2_status.py",
        "definitions": {
            "outputCount": "Runtime assets declared by runtime-v2 manifests.",
            "exactTopicCount": "Unique topicIds that exist in the campaign complete manifest and match the runtime category.",
            "runtimeReadySourceCount": "Outputs listed by gameIntegration.runtimeReadyAssetIds when present; otherwise outputs from a manifest whose top-level runtimeReady is true.",
            "candidateCount": "Outputs not counted by the effective asset-level runtime-ready policy.",
            "primarySemanticMappingCount": "Named primary combat outputs without topicId; excluded from exactTopicCount.",
        },
        "campaigns": campaigns,
        "validation": {
            "passed": not errors,
            "errorCount": len(errors),
            "errors": errors,
            "checks": [
                "duplicate contentId within campaigns and across campaigns",
                "duplicate exact topicId within campaigns and across campaigns",
                "topicId exists in manifest-complete and category matches",
                "PNG path stays under campaign assets and file exists",
                "PNG signature and dimensions match manifest",
                "gameIntegration.runtimeReadyAssetIds is an assets id subset when present",
            ],
        },
    }

    if args.check_only:
        compact = {
            "status": "passed" if not errors else "failed",
            "campaigns": [
                {
                    "campaignId": campaign["campaignId"],
                    "outputs": campaign["totals"]["outputCount"],
                    "exactTopics": campaign["totals"]["exactTopicCount"],
                    "combatProduction": campaign["combatCoverage"]["cumulative"]["productionCoverageStatus"],
                    "combatExact": campaign["combatCoverage"]["cumulative"]["exactTopicStatus"],
                }
                for campaign in campaigns
            ],
            "errorCount": len(errors),
            "errors": errors,
        }
        print(json.dumps(compact, ensure_ascii=False, indent=2))
        return 0 if not errors else 1

    args.json_output.parent.mkdir(parents=True, exist_ok=True)
    args.markdown_output.parent.mkdir(parents=True, exist_ok=True)
    args.json_output.write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    args.markdown_output.write_text(render_markdown(report), encoding="utf-8")
    print(
        json.dumps(
            {
                "status": "passed" if not errors else "failed",
                "json": relative(args.json_output),
                "markdown": relative(args.markdown_output),
                "errorCount": len(errors),
            },
            ensure_ascii=False,
            indent=2,
        )
    )
    return 0 if not errors else 1


if __name__ == "__main__":
    sys.exit(main())
