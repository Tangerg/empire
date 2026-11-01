#!/usr/bin/env python3
"""C06 unbound preview: frozen lost-rock witnesses, not full wall certification."""
import json
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
GODOT = ROOT / "godot"
SCENES = GODOT / "data/scenes"
POSITIVES = [(22, 614), (24, 638), (24, 646), (24, 657)]
NEGATIVES = [(53, 678), (36, 716), (100, 716), (132, 688)]
INSTANCE = "southwest-left-continuous-cliff"


def sample(document, catalog, points):
    instances = [obj for obj in document["objects"] if obj["id"] == INSTANCE]
    if not instances:
        return [0 for _ in points]
    assert len(instances) == 1
    obj = instances[0]
    asset = catalog["objects"][obj["assetId"]]
    assert obj["pointer"] == "pass-through" and obj["selectionCell"] is None
    assert not obj["ownsMapStructureBody"]
    image = Image.open(GODOT / asset["resourcePath"].removeprefix("res://")).convert("RGBA")
    assert list(image.size) == asset["pixelSize"]
    scale = obj["scale"] / document["worldScale"]
    cx, cy = obj["contact"]
    ax, ay = asset["anchorPixels"]
    ox, oy = document["originPixels"]
    ux, uy = document["cellXPixels"]
    vx, vy = document["cellYPixels"]
    tx = ox + cx * ux + cy * vx - ax * scale
    ty = oy + cx * uy + cy * vy - ay * scale
    result = []
    for px, py in points:
        x, y = round((px - tx) / scale), round((py - ty) / scale)
        result.append(image.getpixel((x, y))[3] if 0 <= x < image.width and 0 <= y < image.height else 0)
    return result


def main():
    document = json.loads((SCENES / "c01-06-assembly-preview-ground.json").read_text())
    catalog = json.loads((SCENES / "c01-06-assembly-preview-objects.json").read_text())
    positives = sample(document, catalog, POSITIVES)
    negatives = sample(document, catalog, NEGATIVES)
    assert all(value >= 200 for value in positives), positives
    assert negatives == [0] * len(NEGATIVES), negatives
    absent = dict(document, objects=[obj for obj in document["objects"] if obj["id"] != INSTANCE])
    assert sample(absent, catalog, POSITIVES) == [0] * len(POSITIVES)
    preview = json.loads((SCENES / "c01-06-assembly-preview.json").read_text())
    assert preview["runtimeReady"] is False
    production = json.loads((SCENES / "c01-06.json").read_text())
    assert production.get("assembledGroundPath") is None
    print(json.dumps({"positiveAlpha": positives, "negativeAlpha": negatives,
                      "removedInstanceNegativeControl": "fails all four restoration witnesses",
                      "scope": "unbound high-rock restoration only; not all 12 wall cells or gameplay"}))


if __name__ == "__main__":
    main()
