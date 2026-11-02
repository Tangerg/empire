#!/usr/bin/env python3
"""Generate a read-only status projection from selected resources and original audit.

This file never changes scene selection, collision, support or campaign state.
Historical reports are evidence, not inputs to current selected-instance counts.
"""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2] / "godot"
OUTPUT = ROOT / "docs/art-provenance/c07-layered-v1/selected-current-status.json"
FOUNDATION_BODIES = {
    "joint-granary-foundation": "granary-main",
    "militia-hall-foundation": "militia-barracks-core",
    "council-hall-foundation": "council-hall-main",
    "southern-supply-foundation": "southern-supply-main",
    "west-inner-house-foundation": "west-inner-house-main",
    "west-inner-blue-house-foundation": "west-inner-blue-house-main",
    "west-inner-back-house-foundation": "west-inner-rear-house-main",
}


def load(path):
    return json.loads(path.read_text())


def resource(path):
    if not path.startswith("res://"):
        raise ValueError(f"Expected a project resource path: {path}")
    return ROOT / path[6:]


def report():
    inputs = {}

    def read(path):
        inputs[str(path.relative_to(ROOT))] = hashlib.sha256(path.read_bytes()).hexdigest()
        return load(path)

    audit = read(ROOT / "docs/art-provenance/luojia-geometry.json")
    level = next(item for item in read(ROOT / "data/grayflag-campaign-levels.json") if item["id"] == "c01-07")
    scene = read(ROOT / "data/scenes/c01-07-selected-assembly.json")
    official = read(ROOT / "data/scenes/c01-07.json")
    ground = read(resource(scene["assembledGroundPath"]))
    catalog = read(resource(ground["objectCatalogPath"]))["objects"]
    placements = ground["objects"]
    ids = {item["id"] for item in placements}
    if len(ids) != len(placements) or not set(FOUNDATION_BODIES.values()) <= ids:
        raise ValueError("Selected instances duplicate IDs or explicit foundation review mappings became stale")
    instances = []
    for item in placements:
        asset = catalog[item["assetId"]]
        if "surfaceGeometryPath" in asset:
            owner = asset["surfaceGeometryPath"]
            supports = len(read(resource(owner))["supports"])
            kind = "compiled UV surface supports"
        elif "depthStrips" in asset:
            owner = ground["objectCatalogPath"] + "#" + item["assetId"] + "/depthStrips"
            supports = len(asset["depthStrips"])
            kind = "registered sprite-strip feet"
        else:
            owner = ground["objectCatalogPath"] + "#" + item["assetId"] + "/groundFrontPixels"
            supports = 1
            kind = "registered whole-sprite foot"
        instances.append({"id": item["id"], "assetId": item["assetId"], "placementOwner": scene["assembledGroundPath"],
                          "footOwner": owner, "footKind": kind, "supports": supports,
                          "selectionCell": item.get("selectionCell"),
                          "status": "selected local geometry candidate; whole-area footprint and final art are not implied"})
    sites = []
    for site in audit["sites"]:
        x, y = site["cell"]
        if level["terrain"][y][x] != site["terrain"]:
            raise ValueError("Original site audit no longer matches canonical terrain: " + site["id"])
        owners = [item["id"] for item in placements if item.get("selectionCell") == site["cell"]]
        sites.append({"id": site["id"], "cell": site["cell"], "approach": site["approach"], "selectedSiteOwners": owners,
                      "status": "selected local body; yard/complete foundation still requires separate closure" if owners else "not migrated; native fallback inventory must remain explicit"})
    foundations = [{"id": group["id"], "auditedBlockedCells": len(group["blockedCells"]),
                    "sourceOwner": "docs/art-provenance/luojia-geometry.json#" + group["id"],
                    "selectedRelatedBody": FOUNDATION_BODIES.get(group["id"]), "wholeFoundationClosed": False}
                   for group in audit["blockedPolygons"] if group["id"].endswith("-foundation")]
    return {"scope": "Generated current selected-resource projection, not collision or placement authority; counts are not completion percentages",
            "inputsSha256": inputs, "officialSceneSelected": official.get("assembledGroundPath") == scene["assembledGroundPath"], "actualPlayStatus": "C06/C07 paused; no new actual play in this matrix",
            "selectedInstances": len(instances), "selectedAssets": len(catalog), "supportEntries": sum(item["supports"] for item in instances),
            "selectedSites": sum(bool(item["selectedSiteOwners"]) for item in sites), "canonicalSites": len(sites),
            "instances": instances, "sites": sites, "foundations": foundations,
            "knownUnselectedFailures": ["west-inner left courtyard: selected warm-masonry baseline, trellis/rooted garden and block art incomplete",
                                        "central rear-east planted/seat group: natural structure unresolved",
                                        "northwest and northeast lookouts: solid/void and structural shape unresolved"],
            "finalArtStatus": "not accepted; materials, ground/object integration and contact shadows require bounded whole-neighbourhood passes"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Reject a stale generated status report")
    args = parser.parse_args()
    content = json.dumps(report(), ensure_ascii=False, indent=2) + "\n"
    if args.check:
        if not OUTPUT.exists() or OUTPUT.read_text() != content:
            raise SystemExit("Selected responsibility projection is stale; regenerate from current resources")
    else:
        OUTPUT.write_text(content)
    print("Current selected responsibility projection verified" if args.check else str(OUTPUT))


if __name__ == "__main__":
    main()
