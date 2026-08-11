#!/usr/bin/env python3
"""Normalize the three campaign manifests into one runtime-facing schema."""

from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "FORMAL-ASSET-MANIFEST.json"

CAMPAIGNS = {
    "candidate-01": "断冠之誓",
    "candidate-02": "群星熄灭之前",
    "candidate-03": "布衣定鼎",
}

TYPE_MAP = {
    "character-portrait": "portrait",
    "portrait": "portrait",
    "unit-sheet": "unit-sheet",
    "unitSheet": "unit-sheet",
    "architecture": "architecture",
    "story-scene": "scene",
    "scene": "scene",
    "prop-sheet": "prop-sheet",
    "propSheet": "prop-sheet",
}


def normalize_asset(asset: dict[str, object], asset_root: Path) -> dict[str, object]:
    dimensions = asset.get("dimensions", {})
    if not isinstance(dimensions, dict):
        dimensions = {}
    formats = asset.get("formats", {})
    if not isinstance(formats, dict):
        formats = {}

    width = dimensions.get("width", asset.get("width"))
    height = dimensions.get("height", asset.get("height"))
    png = formats.get("png", asset.get("png"))
    svg = formats.get("svg", asset.get("svg"))
    if not isinstance(width, int) or not isinstance(height, int):
        raise ValueError(f"Missing dimensions for {asset.get('id')}")
    if not isinstance(png, str) or not isinstance(svg, str):
        raise ValueError(f"Missing PNG/SVG paths for {asset.get('id')}")
    if not (asset_root / png).is_file() or not (asset_root / svg).is_file():
        raise FileNotFoundError(f"Missing output pair for {asset.get('id')}: {png}, {svg}")

    source_type = asset.get("type")
    if source_type not in TYPE_MAP:
        raise ValueError(f"Unknown asset type {source_type!r}")

    return {
        "id": asset["id"],
        "label": asset.get("title", asset.get("label", asset["id"])),
        "type": TYPE_MAP[source_type],
        "width": width,
        "height": height,
        "png": png,
        "svg": svg,
        "narrativeUse": asset.get(
            "narrativeRole", asset.get("narrativeUse", "")
        ),
    }


def main() -> None:
    campaigns: list[dict[str, object]] = []
    total_assets = 0
    for candidate, title in CAMPAIGNS.items():
        asset_root = ROOT / candidate / "assets"
        source_path = asset_root / "manifest-hd.json"
        source = json.loads(source_path.read_text(encoding="utf-8"))
        assets = [normalize_asset(asset, asset_root) for asset in source["assets"]]
        total_assets += len(assets)
        campaigns.append(
            {
                "id": candidate,
                "title": title,
                "assetRoot": f"{candidate}/assets",
                "gallery": "gallery-hd.html",
                "readme": "README.md",
                "qaReport": "qa-report.md",
                "assets": assets,
            }
        )

    result = {
        "schemaVersion": "1.0.0",
        "coordinateOrigin": "top-left",
        "pixelScaling": "nearest-neighbor integer multiples",
        "totalLogicalAssets": total_assets,
        "totalOutputFiles": total_assets * 2,
        "conventions": {
            "portrait": "96x112, binary alpha",
            "unit-sheet": "128x48, four horizontal 32x48 frames, binary alpha",
            "architecture": "128x128, binary alpha",
            "scene": "256x144, fully opaque",
            "prop-sheet": "192x48, four horizontal 48x48 cells, binary alpha",
        },
        "campaigns": campaigns,
    }
    OUTPUT.write_text(
        json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


if __name__ == "__main__":
    main()
