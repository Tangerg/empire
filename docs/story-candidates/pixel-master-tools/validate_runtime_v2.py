#!/usr/bin/env python3
"""Validate high-fidelity runtime-v2 candidate packs before game integration."""

from __future__ import annotations

import hashlib
import json
from collections import Counter
from pathlib import Path
from typing import Any
from xml.etree import ElementTree as ET

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
CAMPAIGNS = ("candidate-01", "candidate-02", "candidate-03")
MINIMUM_COVERAGE = {
    "unit-sheet": 4,
    "terrain": 8,
    "structure": 2,
    "icon": 8,
    "fx-sheet": 4,
    "scene": 1,
}


def values(asset: dict[str, Any], *names: str) -> Any:
    for name in names:
        if name in asset:
            return asset[name]
    return None


def asset_type(asset: dict[str, Any]) -> str:
    raw = str(values(asset, "type", "assetType") or "")
    aliases = {
        "combat-unit-sheet": "unit-sheet",
        "combat-unit": "unit-sheet",
        "combat-unit-sprite": "unit-sheet",
        "unit": "unit-sheet",
        "terrain-tile": "terrain",
        "terrain-atlas": "terrain",
        "terrain-variants": "terrain",
        "terrain-connection": "terrain",
        "interactive-structure": "structure",
        "interactive-facility": "structure",
        "structure-sheet": "structure",
        "equipment-icon": "icon",
        "equipment": "icon",
        "skill-icon": "icon",
        "skill": "icon",
        "icon-atlas": "icon",
        "fx": "fx-sheet",
        "scene-background": "scene",
        "story-scene": "scene",
        "narrative-scene": "scene",
    }
    return aliases.get(raw, raw)


def topic_count(asset: dict[str, Any]) -> int:
    topic_ids = values(asset, "topicIds", "topics", "items")
    if isinstance(topic_ids, list):
        return len(topic_ids)
    count = values(asset, "topicCount", "count")
    return count if isinstance(count, int) and count > 0 else 1


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    return image.convert("RGBA").getchannel("A").getbbox()


def file_pair(
    campaign: str,
    asset_root: Path,
    asset: dict[str, Any],
    errors: list[str],
) -> Image.Image | None:
    asset_id = str(asset.get("id", "<missing-id>"))
    png_value = asset.get("png")
    svg_value = asset.get("svg")
    width = asset.get("width")
    height = asset.get("height")
    if not isinstance(width, int) or not isinstance(height, int) or width <= 0 or height <= 0:
        errors.append(f"{campaign}/{asset_id}: invalid declared dimensions")
        return None
    if not isinstance(png_value, str) or not isinstance(svg_value, str):
        errors.append(f"{campaign}/{asset_id}: PNG/SVG paths are required")
        return None
    png_path = asset_root / png_value
    svg_path = asset_root / svg_value
    if not png_path.is_file():
        errors.append(f"{campaign}/{asset_id}: missing PNG {png_path}")
    if not svg_path.is_file():
        errors.append(f"{campaign}/{asset_id}: missing SVG {svg_path}")
    if not png_path.is_file() or not svg_path.is_file():
        return None
    try:
        image = Image.open(png_path).convert("RGBA")
    except OSError as error:
        errors.append(f"{campaign}/{asset_id}: invalid PNG: {error}")
        return None
    if image.size != (width, height):
        errors.append(
            f"{campaign}/{asset_id}: PNG size {image.size} != {(width, height)}"
        )
    if alpha_bbox(image) is None:
        errors.append(f"{campaign}/{asset_id}: entirely transparent PNG")
    try:
        svg_root = ET.parse(svg_path).getroot()
    except (OSError, ET.ParseError) as error:
        errors.append(f"{campaign}/{asset_id}: invalid SVG: {error}")
    else:
        expected = f"0 0 {width} {height}"
        if svg_root.attrib.get("viewBox") != expected:
            errors.append(
                f"{campaign}/{asset_id}: SVG viewBox {svg_root.attrib.get('viewBox')!r} != {expected!r}"
            )
    return image


def validate_unit(
    campaign: str, asset: dict[str, Any], image: Image.Image, errors: list[str]
) -> None:
    asset_id = str(asset.get("id"))
    frame_width = values(asset, "frameWidth", "cellWidth")
    frame_height = values(asset, "frameHeight", "cellHeight")
    frames = values(asset, "frames", "frameCount")
    anchor = asset.get("anchor")
    if (frame_width, frame_height, frames) != (32, 48, 4):
        errors.append(
            f"{campaign}/{asset_id}: unit sheet must be 4 frames of 32x48"
        )
        return
    if image.size != (128, 48):
        errors.append(f"{campaign}/{asset_id}: unit sheet must be 128x48")
    if anchor not in ([16, 47], {"x": 16, "y": 47}):
        errors.append(f"{campaign}/{asset_id}: unit anchor must be (16,47)")
    for frame in range(4):
        cell = image.crop((frame * 32, 0, (frame + 1) * 32, 48))
        if alpha_bbox(cell) is None:
            errors.append(f"{campaign}/{asset_id}: empty unit frame {frame}")
            continue
        bbox = alpha_bbox(cell)
        assert bbox is not None
        if bbox[2] > 32 or bbox[3] > 48:
            errors.append(f"{campaign}/{asset_id}: frame {frame} exceeds its cell")
        bottom = cell.getchannel("A").crop((0, 44, 32, 48))
        if bottom.getbbox() is None:
            errors.append(f"{campaign}/{asset_id}: frame {frame} has no grounded pixels")


def validate_terrain(
    campaign: str, asset: dict[str, Any], image: Image.Image, errors: list[str]
) -> None:
    asset_id = str(asset.get("id"))
    cell_width = values(asset, "cellWidth", "frameWidth", "width")
    cell_height = values(asset, "cellHeight", "frameHeight", "height")
    if (cell_width, cell_height) != (32, 32):
        errors.append(f"{campaign}/{asset_id}: terrain cells must be 32x32")
    tile_mode = values(asset, "tileMode", "autotile")
    if tile_mode in {"connected-4", "nesw-16", "autotile-16", "nesw-mask-16", "nesw-mask"}:
        masks = values(asset, "masks", "maskCount", "maskOrder", "variants", "frames")
        mask_count = len(masks) if isinstance(masks, list) else masks
        if mask_count != 16:
            errors.append(
                f"{campaign}/{asset_id}: connected terrain must provide 16 N/E/S/W masks"
            )
    elif tile_mode in {
        "repeat",
        "cosmetic-variants",
        "seamless-repeat",
        "repeat-variants",
        "coordinate-hash-variants",
    }:
        variants = values(asset, "variants", "variantCount", "variantOrder", "topicCount", "frames")
        variant_count = len(variants) if isinstance(variants, list) else variants
        if not isinstance(variant_count, int) or variant_count < 4:
            errors.append(
                f"{campaign}/{asset_id}: repeating terrain requires at least 4 variants"
            )
    elif tile_mode is None:
        errors.append(f"{campaign}/{asset_id}: terrain tileMode is required")
    else:
        errors.append(f"{campaign}/{asset_id}: unsupported terrain tileMode {tile_mode!r}")


def validate_structure(
    campaign: str, asset: dict[str, Any], _image: Image.Image, errors: list[str]
) -> None:
    asset_id = str(asset.get("id"))
    footprint = asset.get("footprint")
    anchor = asset.get("anchor")
    states = values(asset, "states", "stateRows")
    footprint_values = (
        footprint
        if isinstance(footprint, list)
        else [
            footprint.get("columns", footprint.get("width")),
            footprint.get("rows", footprint.get("height")),
        ]
        if isinstance(footprint, dict)
        else None
    )
    if not (
        isinstance(footprint_values, list)
        and len(footprint_values) == 2
        and all(isinstance(value, int) and value > 0 for value in footprint_values)
    ):
        errors.append(f"{campaign}/{asset_id}: valid [w,h] footprint is required")
    if not (
        isinstance(anchor, (list, dict))
        and (len(anchor) == 2 if isinstance(anchor, list) else {"x", "y"} <= set(anchor))
    ):
        errors.append(f"{campaign}/{asset_id}: structure anchor is required")
    state_count = len(states) if isinstance(states, (list, dict)) else states
    if not isinstance(state_count, int) or state_count < 3:
        errors.append(f"{campaign}/{asset_id}: at least 3 structure states are required")


def validate_icon(
    campaign: str, asset: dict[str, Any], image: Image.Image, errors: list[str]
) -> None:
    asset_id = str(asset.get("id"))
    cell_width = values(asset, "cellWidth", "frameWidth", "width")
    cell_height = values(asset, "cellHeight", "frameHeight", "height")
    if (cell_width, cell_height) != (32, 32):
        errors.append(f"{campaign}/{asset_id}: runtime icons must use 32x32 cells")
    if alpha_bbox(image) is None:
        errors.append(f"{campaign}/{asset_id}: empty icon")


def validate_fx(
    campaign: str, asset: dict[str, Any], _image: Image.Image, errors: list[str]
) -> None:
    asset_id = str(asset.get("id"))
    frame_width = values(asset, "frameWidth", "cellWidth")
    frame_height = values(asset, "frameHeight", "cellHeight")
    frames = values(asset, "frames", "frameCount")
    if frame_width not in {32, 64} or frame_height not in {32, 64}:
        errors.append(f"{campaign}/{asset_id}: FX frames must be 32x32 or 64x64")
    if frames not in {4, 6, 8}:
        errors.append(f"{campaign}/{asset_id}: FX must have 4, 6 or 8 frames")
    if not isinstance(asset.get("fps"), (int, float)) or asset["fps"] <= 0:
        errors.append(f"{campaign}/{asset_id}: positive fps is required")
    if asset.get("blendMode") not in {"normal", "screen", "add", "additive", "alpha", "multiply"}:
        errors.append(f"{campaign}/{asset_id}: invalid blendMode")
    if not isinstance(asset.get("loop"), bool):
        errors.append(f"{campaign}/{asset_id}: loop must be boolean")
    if not isinstance(asset.get("anchor"), (list, dict)):
        errors.append(f"{campaign}/{asset_id}: FX anchor is required")


def validate_scene(
    campaign: str, asset: dict[str, Any], image: Image.Image, errors: list[str]
) -> None:
    asset_id = str(asset.get("id"))
    if image.size != (256, 144):
        errors.append(f"{campaign}/{asset_id}: runtime scene must be 256x144")
    alpha_values = set(image.getchannel("A").get_flattened_data())
    if alpha_values != {255}:
        errors.append(f"{campaign}/{asset_id}: scene must be fully opaque")


def main() -> None:
    reports: list[dict[str, Any]] = []
    all_errors: list[str] = []
    for campaign in CAMPAIGNS:
        asset_root = ROOT / campaign / "assets"
        manifest_path = asset_root / "manifest-runtime-v2.json"
        errors: list[str] = []
        if not manifest_path.is_file():
            reports.append({"campaign": campaign, "status": "pending", "errors": []})
            continue
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        quality_tier = manifest.get("qualityTier")
        runtime_ready = manifest.get("runtimeReady")
        if quality_tier == "runtime-v2-candidate":
            if runtime_ready is not False:
                errors.append(f"{campaign}: candidate packs must remain runtimeReady=false")
        elif quality_tier == "runtime-v2-partial":
            if runtime_ready is not False:
                errors.append(f"{campaign}: partially promoted packs must remain runtimeReady=false")
            integration = manifest.get("gameIntegration")
            if not isinstance(integration, dict):
                errors.append(f"{campaign}: partially promoted pack requires gameIntegration evidence")
            else:
                screenshot = integration.get("screenshot")
                if not isinstance(screenshot, str) or not (asset_root / screenshot).is_file():
                    errors.append(f"{campaign}: partial promotion game screenshot is missing")
                ready_asset_ids = integration.get("runtimeReadyAssetIds")
                if not isinstance(ready_asset_ids, list) or not ready_asset_ids:
                    errors.append(f"{campaign}: partial promotion requires runtimeReadyAssetIds")
                for field in ("fallbackUnits", "fallbackTerrainTiles", "brokenImages"):
                    if integration.get(field) != 0:
                        errors.append(f"{campaign}: partial promotion requires {field}=0")
                if integration.get("movementAnimationVerified") is not True:
                    errors.append(f"{campaign}: partial promotion requires movement animation verification")
        elif quality_tier == "runtime-v2":
            if runtime_ready is not True:
                errors.append(f"{campaign}: promoted runtime-v2 packs must be runtimeReady=true")
            integration = manifest.get("gameIntegration")
            if not isinstance(integration, dict):
                errors.append(f"{campaign}: promoted pack requires gameIntegration evidence")
            else:
                screenshot = integration.get("screenshot")
                if not isinstance(screenshot, str) or not (asset_root / screenshot).is_file():
                    errors.append(f"{campaign}: promoted pack game screenshot is missing")
                for field in ("fallbackUnits", "fallbackTerrainTiles", "brokenImages"):
                    if integration.get(field) != 0:
                        errors.append(f"{campaign}: promoted pack requires {field}=0")
                if integration.get("movementAnimationVerified") is not True:
                    errors.append(f"{campaign}: promoted pack requires movement animation verification")
        else:
            errors.append(f"{campaign}: unsupported qualityTier {quality_tier!r}")
        assets = manifest.get("assets")
        if not isinstance(assets, list):
            errors.append(f"{campaign}: assets must be an array")
            assets = []
        ids = [str(asset.get("id", "")) for asset in assets]
        duplicates = [value for value, count in Counter(ids).items() if count > 1]
        if "" in ids:
            errors.append(f"{campaign}: asset without id")
        if duplicates:
            errors.append(f"{campaign}: duplicate asset ids {duplicates}")
        if quality_tier == "runtime-v2-partial":
            ready_asset_ids = manifest.get("gameIntegration", {}).get("runtimeReadyAssetIds", [])
            unknown_ready_ids = sorted(set(map(str, ready_asset_ids)) - set(ids))
            if unknown_ready_ids:
                errors.append(f"{campaign}: runtimeReadyAssetIds reference unknown assets {unknown_ready_ids}")

        coverage: Counter[str] = Counter()
        visual_hashes: dict[str, list[tuple[str, str]]] = {}
        for asset in assets:
            kind = asset_type(asset)
            coverage[kind] += topic_count(asset)
            image = file_pair(campaign, asset_root, asset, errors)
            if image is None:
                continue
            normalized = image.resize((32, 32), Image.Resampling.NEAREST)
            visual_hashes.setdefault(kind, []).append(
                (str(asset.get("id")), hashlib.sha256(normalized.tobytes()).hexdigest())
            )
            if kind == "unit-sheet":
                validate_unit(campaign, asset, image, errors)
            elif kind == "terrain":
                validate_terrain(campaign, asset, image, errors)
            elif kind == "structure":
                validate_structure(campaign, asset, image, errors)
            elif kind == "icon":
                validate_icon(campaign, asset, image, errors)
            elif kind == "fx-sheet":
                validate_fx(campaign, asset, image, errors)
            elif kind == "scene":
                validate_scene(campaign, asset, image, errors)
            else:
                errors.append(f"{campaign}/{asset.get('id')}: unsupported runtime type {kind!r}")

        for kind, minimum in MINIMUM_COVERAGE.items():
            if coverage[kind] < minimum:
                errors.append(
                    f"{campaign}: {kind} coverage {coverage[kind]} < first-batch minimum {minimum}"
                )
        for kind, entries in visual_hashes.items():
            hashes = Counter(value for _asset_id, value in entries)
            repeated = [value for value, count in hashes.items() if count > 1]
            if repeated:
                examples = [
                    [asset_id for asset_id, value in entries if value == repeated_hash]
                    for repeated_hash in repeated
                ]
                errors.append(f"{campaign}: exact duplicate {kind} files {examples}")

        report = {
            "campaign": campaign,
            "status": "passed-machine-qa" if not errors else "failed",
            "assets": len(assets),
            "coverage": dict(coverage),
            "errors": errors,
        }
        reports.append(report)
        all_errors.extend(errors)

    result = {
        "validator": "runtime-v2",
        "humanGameScreenshotRequired": True,
        "campaigns": reports,
        "errors": all_errors,
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    if all_errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
