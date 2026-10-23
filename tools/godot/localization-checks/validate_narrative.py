#!/usr/bin/env python3
"""Validate native narrative structure, shared identity names and PO references without rewriting sources."""
import hashlib
import json
from pathlib import Path
import re
import sys
from validate_catalog import CatalogError, read_catalog


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise CatalogError(f"Duplicate JSON field: {key}")
        result[key] = value
    return result


def validate(root):
    data = root / "godot/data"
    messages = read_catalog((root / "godot/Localization/zh_CN.po").read_text(encoding="utf-8"))
    story = json.loads((data / "grayflag-story.json").read_text(encoding="utf-8"), object_pairs_hook=unique_object)
    staging = json.loads((data / "grayflag-staging.json").read_text(encoding="utf-8"), object_pairs_hook=unique_object)
    portraits = json.loads((data / "portrait-catalog.json").read_text(encoding="utf-8"), object_pairs_hook=unique_object)
    hud = json.loads((data / "tactical-hud-art.json").read_text(encoding="utf-8"), object_pairs_hook=unique_object)
    used = set()

    def shape(value, required, optional=()):
        if not isinstance(value, dict) or not set(required) <= value.keys() or value.keys() - set(required) - set(optional):
            raise CatalogError(f"Invalid native narrative fields: {value}")

    def message(key):
        if not isinstance(key, str) or key not in messages:
            raise CatalogError(f"Missing narrative localization key: {key}")
        used.add(key)

    def identities(rows, name):
        ids = [row["id"] for row in rows]
        if any(not isinstance(key, str) or not key.strip() for key in ids) or len(set(ids)) != len(ids):
            raise CatalogError(f"Missing or duplicate {name} ID")

    shape(story, ["schema", "identityNameKeys", "stories", "choices"])
    shape(staging, ["schema", "titleKey", "completionLabelKey", "menu", "menuArt", "chapters", "storyScenes",
                    "battleTotal", "joinAfter", "relationLabelKeys", "resourceLabelKeys", "portraits", "levels"])
    if story["schema"] != 3 or staging["schema"] != 3:
        raise CatalogError("Native story and staging require schema 3")
    shape(portraits, ["schema", "portraits"])
    if type(portraits["schema"]) is not int or portraits["schema"] != 1 or not isinstance(portraits["portraits"], dict) or not portraits["portraits"]:
        raise CatalogError("Native portraits require a nonempty schema 1 catalog")
    resources = set()
    for identity, asset in portraits["portraits"].items():
        shape(asset, ["resourcePath", "sha256"])
        resource, digest = asset["resourcePath"], asset["sha256"]
        if not re.fullmatch(r"[a-z][a-z0-9-]*", identity) or not isinstance(resource, str) or not re.fullmatch(
                r"res://assets/runtime/narrative-static/char-[a-z0-9-]+\.png", resource):
            raise CatalogError(f"Invalid native portrait resource: {identity}")
        if resource in resources or not isinstance(digest, str) or not re.fullmatch(r"[a-f0-9]{64}", digest):
            raise CatalogError(f"Invalid or duplicate native portrait declaration: {identity}")
        resources.add(resource)
        source = root / "godot" / resource.removeprefix("res://")
        if not source.is_file() or hashlib.sha256(source.read_bytes()).hexdigest() != digest:
            raise CatalogError(f"Native portrait hash mismatch: {identity}")

    def portrait_id(value, context):
        if not isinstance(value, str) or value not in portraits["portraits"]:
            raise CatalogError(f"Unknown native portrait ID in {context}: {value}")

    if not isinstance(staging["portraits"], dict):
        raise CatalogError("Staging portraits must map identities to portrait IDs")
    for identity, value in staging["portraits"].items():
        if identity not in story["identityNameKeys"]:
            raise CatalogError(f"Unknown staging portrait identity: {identity}")
        portrait_id(value, f"staging identity {identity}")
    shape(hud, ["schema", "portraits", "icons", "sourceIconKeys"])
    if hud["schema"] != 2 or not isinstance(hud["portraits"], dict):
        raise CatalogError("Native tactical HUD requires schema 2 portrait bindings")
    for key, binding in hud["portraits"].items():
        shape(binding, ["unitType", "actorKey", "portraitId"])
        if not isinstance(binding["unitType"], str) or not binding["unitType"] or not (
                binding["actorKey"] is None or isinstance(binding["actorKey"], str) and binding["actorKey"]):
            raise CatalogError(f"Invalid native HUD portrait binding: {key}")
        if key != (binding["actorKey"] or binding["unitType"]):
            raise CatalogError(f"Native HUD portrait binding key mismatch: {key}")
        if binding["portraitId"] is not None:
            portrait_id(binding["portraitId"], f"HUD binding {key}")
    identities(story["stories"] + story["choices"], "narrative")
    campaign = json.loads((data / "grayflag-campaign.json").read_text(encoding="utf-8"), object_pairs_hook=unique_object)
    for unit in campaign["initial"]["roster"]:
        if unit["id"] not in story["identityNameKeys"]:
            raise CatalogError(f"Missing campaign identity name: {unit['id']}")
    for key in story["identityNameKeys"].values():
        message(key)
    content_text = json.loads((data / "grayflag-content-text.json").read_text(encoding="utf-8"), object_pairs_hook=unique_object)
    for identity in ["laiya", "roderick", "cain", "bran", "mirelle", "tasha", "ivra"]:
        unit_id = f"c01.{identity}"
        name_key = story["identityNameKeys"].get(identity)
        if not name_key or content_text["content"]["units"][unit_id]["name"] != name_key:
            raise CatalogError(f"Named unit must reference shared identity name: {unit_id}")
        obsolete_key = f"content.units.{unit_id}.name"
        if obsolete_key in messages:
            raise CatalogError(f"Duplicate named unit wording: {obsolete_key}")
    for row in story["stories"]:
        shape(row, ["id", "chapter", "kickerKey", "titleKey", "dateKey", "locationKey", "summaryKey", "beats"], ["scene", "mission"])
        if not row["beats"]:
            raise CatalogError(f"Story without beats: {row['id']}")
        identities(row["beats"], "beat")
        for beat in row["beats"]:
            shape(beat, ["id", "speaker", "textKey"])
            if not re.fullmatch(r"[a-z][a-z-]+", beat["id"]):
                raise CatalogError(f"Beat needs an authored word-based ID, not a numeric position: {beat['id']}")
            if beat["speaker"] not in story["identityNameKeys"]:
                raise CatalogError(f"Unknown narrative speaker: {beat['speaker']}")
        if row.get("mission") is not None:
            shape(row["mission"], ["objectiveKey", "dangerKey", "lessonKey"])
    for row in story["choices"]:
        shape(row, ["id", "chapter", "promptKey", "contextKey", "options"])
        identities(row["options"], "choice option")
        for option in row["options"]:
            shape(option, ["id", "labelKey", "detailKey", "consequenceKey"])

    def references(value):
        if isinstance(value, dict):
            for key, child in value.items():
                if key.endswith("Key"):
                    message(child)
                else:
                    references(child)
        elif isinstance(value, list):
            for child in value:
                references(child)
    references(story)
    message(staging["titleKey"])
    message(staging["completionLabelKey"])
    shape(staging["menu"], ["titleKey", "kickerKey", "descriptionKey", "artAltKey", "continueLabelKey", "restartLabelKey", "newLabelKey", "replacePromptKey"])
    references(staging["menu"])
    for field in ["resourceLabelKeys", "relationLabelKeys"]:
        for key in staging[field].values():
            message(key)
    for chapter in staging["chapters"].values():
        shape(chapter, ["titleKey", "art"])
        message(chapter["titleKey"])
    for level in staging["levels"].values():
        shape(level, ["chapter", "order", "briefingId", "chapterArt", "briefingArt"])
        if str(level["chapter"]) not in staging["chapters"]:
            raise CatalogError("Level chapter labels must reference the single chapter owner")
    obsolete = {"title", "completionLabel"} & staging.keys()
    if obsolete:
        raise CatalogError(f"Obsolete literal campaign labels: {obsolete}")
    unused = {key for key in messages if key.startswith(("story.", "choice.", "speaker.", "campaign.grayflag."))} - used
    if unused:
        raise CatalogError(f"Orphaned narrative messages: {sorted(unused)}")
    print(f"PASS native narrative references: {len(used)} content keys, {len(story['stories'])} stories, {sum(len(row['beats']) for row in story['stories'])} semantic beats, {len(story['choices'])} choices.")


if __name__ == "__main__":
    validate(Path(sys.argv[1]).resolve())
