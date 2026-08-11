#!/usr/bin/env python3
"""Validate all formal campaign PNG/SVG assets from the normalized manifest."""

from __future__ import annotations

import json
from html.parser import HTMLParser
from pathlib import Path
from xml.etree import ElementTree as ET

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "FORMAL-ASSET-MANIFEST.json"

RULES = {
    "portrait": {"size": (96, 112), "colors": 49, "alpha": "binary"},
    "unit-sheet": {
        "size": (128, 48),
        "colors": 49,
        "alpha": "binary",
        "cells": 4,
    },
    "architecture": {"size": (128, 128), "colors": 65, "alpha": "binary"},
    "scene": {"size": (256, 144), "colors": 96, "alpha": "opaque"},
    "prop-sheet": {
        "size": (192, 48),
        "colors": 65,
        "alpha": "binary",
        "cells": 4,
    },
}


class GalleryParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.sources: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag != "img":
            return
        source = dict(attrs).get("src")
        if source:
            self.sources.append(source)


def color_from_rect(rect: ET.Element) -> tuple[int, int, int, int]:
    value = rect.attrib["fill"].removeprefix("#")
    alpha = round(float(rect.attrib.get("opacity", "1")) * 255)
    return int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16), alpha


def rasterize_svg(svg_path: Path, size: tuple[int, int]) -> Image.Image:
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    pixels = image.load()
    root = ET.parse(svg_path).getroot()
    if root.attrib.get("viewBox") != f"0 0 {size[0]} {size[1]}":
        raise ValueError(f"viewBox mismatch: {root.attrib.get('viewBox')}")
    for rect in root.iter():
        if not rect.tag.endswith("rect"):
            continue
        x = int(rect.attrib["x"])
        y = int(rect.attrib["y"])
        width = int(rect.attrib["width"])
        height = int(rect.attrib["height"])
        color = color_from_rect(rect)
        for row in range(y, y + height):
            for column in range(x, x + width):
                pixels[column, row] = color
    return image


def validate_asset(asset_root: Path, asset: dict[str, object]) -> list[str]:
    errors: list[str] = []
    asset_id = str(asset["id"])
    asset_type = str(asset["type"])
    rule = RULES[asset_type]
    expected_size = tuple(rule["size"])
    declared_size = int(asset["width"]), int(asset["height"])
    if declared_size != expected_size:
        errors.append(f"{asset_id}: manifest size {declared_size} != {expected_size}")

    png_path = asset_root / str(asset["png"])
    svg_path = asset_root / str(asset["svg"])
    for path in (png_path, svg_path):
        if not path.is_file():
            errors.append(f"{asset_id}: missing {path}")
    if errors:
        return errors

    image = Image.open(png_path).convert("RGBA")
    if image.size != expected_size:
        errors.append(f"{asset_id}: PNG size {image.size} != {expected_size}")
        return errors

    colors = set(image.get_flattened_data())
    if len(colors) > int(rule["colors"]):
        errors.append(f"{asset_id}: {len(colors)} RGBA colors > {rule['colors']}")
    alphas = {pixel[3] for pixel in colors}
    if rule["alpha"] == "opaque":
        if alphas != {255}:
            errors.append(f"{asset_id}: scene alpha levels are {sorted(alphas)}")
    else:
        if not alphas.issubset({0, 255}) or 0 not in alphas or 255 not in alphas:
            errors.append(f"{asset_id}: invalid binary alpha levels {sorted(alphas)}")
        width, height = image.size
        corners = ((0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1))
        if any(image.getpixel(point)[3] for point in corners):
            errors.append(f"{asset_id}: nontransparent corner")

    bad_magenta = {
        pixel
        for pixel in colors
        if pixel[3] > 0 and pixel[0] > 200 and pixel[2] > 170 and pixel[1] < 80
    }
    if bad_magenta:
        errors.append(f"{asset_id}: chroma-key residue {sorted(bad_magenta)[:4]}")

    cells = int(rule.get("cells", 1))
    if cells > 1:
        cell_width = image.width // cells
        for cell in range(cells):
            alpha = image.crop(
                (cell * cell_width, 0, (cell + 1) * cell_width, image.height)
            ).getchannel("A")
            if alpha.getbbox() is None:
                errors.append(f"{asset_id}: empty cell {cell}")

    try:
        rebuilt = rasterize_svg(svg_path, image.size)
    except (ET.ParseError, KeyError, TypeError, ValueError) as error:
        errors.append(f"{asset_id}: invalid SVG: {error}")
    else:
        if list(image.get_flattened_data()) != list(rebuilt.get_flattened_data()):
            different = sum(
                left != right
                for left, right in zip(
                    image.get_flattened_data(), rebuilt.get_flattened_data()
                )
            )
            errors.append(f"{asset_id}: PNG/SVG differ at {different} pixels")
    return errors


def main() -> None:
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    errors: list[str] = []
    checked = 0
    gallery_references = 0
    campaign_counts: dict[str, int] = {}

    for campaign in manifest["campaigns"]:
        campaign_id = campaign["id"]
        asset_root = ROOT / campaign["assetRoot"]
        assets = campaign["assets"]
        campaign_counts[campaign_id] = len(assets)
        for asset in assets:
            errors.extend(validate_asset(asset_root, asset))
            checked += 1

        gallery_path = asset_root / campaign["gallery"]
        parser = GalleryParser()
        parser.feed(gallery_path.read_text(encoding="utf-8"))
        if len(parser.sources) != len(assets):
            errors.append(
                f"{campaign_id}: gallery has {len(parser.sources)} images, manifest has {len(assets)}"
            )
        for source in parser.sources:
            if "draft-v1" in source or "masters/" in source:
                errors.append(f"{campaign_id}: gallery references nonformal {source}")
            if not (asset_root / source).is_file():
                errors.append(f"{campaign_id}: gallery missing {source}")
            gallery_references += 1

    result = {
        "logicalAssets": checked,
        "outputFiles": checked * 2,
        "galleryReferences": gallery_references,
        "campaignAssets": campaign_counts,
        "errors": errors,
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
