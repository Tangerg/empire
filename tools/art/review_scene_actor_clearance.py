"""Measure raw sprite-alpha occlusion in a complete actual BoardView SVG.

Usage: python tools/art/review_scene_actor_clearance.py CAPTURE_DIRECTORY OUTPUT_DIRECTORY
Each real drawing is rasterized once with integer world-aligned crop origins.
Actors contribute their source sprite only; later scenery, structures, actors and
outer frame contribute their alpha. This is an offline overlap measure, not a
perceptual score or live GPU verification. The existing offline helper omits CSS
shadow filters. Fail rather than measure a clipped crop.
"""
from copy import deepcopy
from pathlib import Path
import argparse
import json
import math
import re
import xml.etree.ElementTree as ET

import numpy as np
from PIL import Image

from render_ground_review import rasterize
from svg_viewports import NS

N = "{" + NS + "}"
SIZE = 256
COLUMNS = 16


def later_overlaps(index, order, masks):
    actor = masks[index]
    area = actor.sum()
    if area <= 0:
        raise ValueError(f"empty actor sprite: {order[index]['id']}")
    remaining = np.ones(actor.shape)
    covering = []
    ax, ay = order[index]["cropOrigin"]
    height, width = actor.shape
    for j in range(index + 1, len(order)):
        mask = masks[j]
        bx, by = order[j]["cropOrigin"]
        left, right = max(ax, bx), min(ax + width, bx + mask.shape[1])
        top, bottom = max(ay, by), min(ay + height, by + mask.shape[0])
        if left >= right or top >= bottom:
            continue
        a = actor[top - ay:bottom - ay, left - ax:right - ax]
        b = mask[top - by:bottom - by, left - bx:right - bx]
        overlap = float((a * b).sum() / area)
        remaining[top - ay:bottom - ay, left - ax:right - ax] *= 1 - b
        if overlap >= .001:
            covering.append({**order[j], "pairwiseOverlap": round(overlap, 6)})
    return {
        **order[index],
        "spriteArea": round(float(area), 4),
        "occludedFraction": round(float(1 - (actor * remaining).sum() / area), 6),
        "covering": sorted(covering, key=lambda piece: -piece["pairwiseOverlap"]),
    }


def actual_drawings(source, provenance):
    world = source.find(N + 'g[@class="board-world"]')
    ox, oy = map(float, re.findall(r"-?\d+(?:\.\d+)?", world.get("transform"))[:2])
    drawings = list(world.find(N + 'g[@class="layer layer-units"]'))
    order = deepcopy(provenance["renders"][0]["standingDrawOrder"])
    if len(drawings) != len(order):
        raise ValueError("actual standing layer and recorded order disagree")
    named = {piece["sourceId"]: piece for piece in provenance["renders"][0]["namedPieces"]}
    for piece in order:
        metadata = named.get(piece["id"], {})
        piece.update({key: metadata[key] for key in ["cellId", "atlas"] if key in metadata})
    foreground = next((g for g in source.iter(N + "g")
                       if g.get("class") == "candidate-scene-foreground"), None)
    if foreground is not None:
        for i, child in enumerate(foreground):
            if child.tag != N + "g":
                continue
            contact = list(map(float, re.findall(r"-?\d+(?:\.\d+)?", child.get("transform", ""))[:2]))
            if len(contact) != 2:
                raise ValueError("frame child without contact")
            wrapper = ET.Element(N + "g", {"transform": f"translate({-ox} {-oy})"})
            wrapper.append(deepcopy(child))
            drawings.append(wrapper)
            order.append({"id": f"frame:{i}", "displayIndex": len(order),
                          "worldFoot": [contact[0] - ox, contact[1] - oy], "frame": True})
    return drawings, order


def capture(source_dir, output):
    output.mkdir(parents=True, exist_ok=True)
    provenance = json.loads((source_dir / "runtime-scene-provenance.json").read_text())
    source = ET.parse(source_dir / "runtime-standing-full.svg").getroot()
    drawings, order = actual_drawings(source, provenance)
    width, height = SIZE * COLUMNS, SIZE * math.ceil(len(drawings) / COLUMNS)
    atlas = ET.Element(N + "svg", {"width": str(width), "height": str(height),
                                  "viewBox": f"0 0 {width} {height}"})
    for style in source.iter(N + "style"):
        atlas.append(deepcopy(style))
    for i, (drawing, metadata) in enumerate(zip(drawings, order)):
        foot = metadata["worldFoot"]
        x, y = math.floor(foot[0] - 128), math.floor(foot[1] - 192)
        metadata["cropOrigin"] = [x, y]
        panel = ET.SubElement(atlas, N + "svg", {
            "x": str(i % COLUMNS * SIZE), "y": str(i // COLUMNS * SIZE),
            "width": str(SIZE), "height": str(SIZE),
            "viewBox": f"{x} {y} {SIZE} {SIZE}", "overflow": "hidden",
        })
        picture = deepcopy(drawing)
        if "actorId" in metadata:
            for child in list(picture):
                if child.get("class") != "figure":
                    picture.remove(child)
                else:
                    for part in list(child):
                        if part.get("class") != "board-strip":
                            child.remove(part)
        panel.append(picture)
    for image in atlas.iter(N + "image"):
        href = image.get("href")
        if href and not href.startswith("data:"):
            image.set("href", str((source_dir / href).resolve()))
    ET.ElementTree(atlas).write(output / "pieces.svg", encoding="unicode")
    (output / "pieces.json").write_text(json.dumps(order, indent=2) + "\n")
    rasterize(output, densities=(1,))
    alpha = np.asarray(Image.open(output / "pieces@1x.png").convert("RGBA"))[:, :, 3].astype(float) / 255
    masks = [alpha[i // COLUMNS * SIZE:(i // COLUMNS + 1) * SIZE,
                   i % COLUMNS * SIZE:(i % COLUMNS + 1) * SIZE] for i in range(len(order))]
    for metadata, mask in zip(order, masks):
        if any(np.any(edge) for edge in [mask[0], mask[-1], mask[:, 0], mask[:, -1]]):
            raise AssertionError("truncated " + metadata["id"])
    actors = [later_overlaps(i, order, masks) for i, metadata in enumerate(order) if "actorId" in metadata]
    result = {
        "kind": "actual individual scene drawings at world-aligned native pixels; source actor sprite only; all later standing and frame drawings",
        "limitations": ["offline alpha overlap, not visual recognition or live GPU", "CSS shadow filters omitted"],
        "actorCount": len(actors), "drawingCount": len(drawings), "actors": actors,
    }
    (output / "occlusion.json").write_text(json.dumps(result, indent=2) + "\n")
    for actor in actors:
        print(actor.get("key"), actor.get("cell"), f"{actor['occludedFraction']:.1%}")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture_directory", type=Path)
    parser.add_argument("output_directory", type=Path)
    args = parser.parse_args()
    capture(args.capture_directory.resolve(), args.output_directory.resolve())
