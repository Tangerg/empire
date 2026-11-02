"""Bounded leaf redesign: observed left/central foot; right edge inset from conflicting new stone."""
from pathlib import Path
import json

root = Path(__file__).resolve().parent
polygon = [[153, 94], [157, 93], [161, 94], [165, 98], [165, 136.5], [161, 134], [153, 130]]
uv = [[736 + (x - 153) * 61 / 12, 827 + (y - 93) * 72 / 43.5] for x, y in polygon]
document = {
    "scope": "Unselected narrow leaf redesign; original left/middle timber foot retained, right side inset to avoid reconstructed stone. Hidden hinge/thickness not original pixel certification.",
    "textureResourcePath": "res://assets/native/c07-layered-v1/southeast-gate-v1-candidate.png",
    "namePrefix": "NorthwestWoodLeaf",
    "supportLine": [[153, 130], [161, 134], [165, 136.5]],
    "members": [{"id": "arched-wooden-leaf", "polygon": polygon, "uv": uv, "tint": "ffffff", "coverage": "opaque"}],
}
# Thin joinery remains strictly inside the already verified leaf silhouette.
for name, y in [("upper-cross-rail", 108.5), ("lower-cross-rail", 121.0)]:
    document["members"].append({
        "id": name,
        "polygon": [[154, y], [164, y + 5.5], [164, y + 6.8], [154, y + 1.3]],
        "uv": [[740, 840], [740, 890], [749, 890], [749, 840]],
        "tint": "c5b291", "coverage": "opaque",
    })
document["members"].append({
    "id": "hinge-side-edge-thickness",
    "polygon": [[164.35, 97.35], [165, 98], [165, 136.5], [164.35, 136.09375]],
    "uv": [[785, 829], [790, 830], [790, 898], [785, 897]],
    "tint": "a99983", "coverage": "opaque",
})
(root / "northwest-leaf-source.json").write_text(json.dumps(document, indent=2) + "\n")
