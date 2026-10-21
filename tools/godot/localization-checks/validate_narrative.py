#!/usr/bin/env python3
"""Validate native narrative structure and its complete PO references, without rewriting either."""
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

    shape(story, ["schema", "speakerNameKeys", "stories", "choices"])
    shape(staging, ["schema", "titleKey", "completionLabelKey", "menu", "menuArt", "chapters", "storyScenes",
                    "battleTotal", "joinAfter", "relationLabels", "resourceLabels", "portraits", "levels"])
    if story["schema"] != 2 or staging["schema"] != 2:
        raise CatalogError("Native narrative documents require schema 2")
    identities(story["stories"] + story["choices"], "narrative")
    for key in story["speakerNameKeys"].values():
        message(key)
    for row in story["stories"]:
        shape(row, ["id", "chapter", "kickerKey", "titleKey", "dateKey", "locationKey", "summaryKey", "beats"], ["scene", "mission"])
        if not row["beats"]:
            raise CatalogError(f"Story without beats: {row['id']}")
        identities(row["beats"], "beat")
        for beat in row["beats"]:
            shape(beat, ["id", "speaker", "textKey"])
            if not re.fullmatch(r"[a-z][a-z-]+", beat["id"]):
                raise CatalogError(f"Beat needs an authored word-based ID, not a numeric position: {beat['id']}")
            if beat["speaker"] not in story["speakerNameKeys"]:
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
