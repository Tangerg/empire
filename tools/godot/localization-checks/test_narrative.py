import contextlib
import io
import json
from pathlib import Path
import shutil
import tempfile
import unittest

from validate_catalog import CatalogError
from validate_narrative import validate


class NativeNarrativeContract(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="empire-narrative-contract.")
        self.root = Path(self.temporary.name)
        source = Path(__file__).resolve().parents[3]
        for name in ["godot/data/grayflag-campaign.json", "godot/data/grayflag-story.json", "godot/data/grayflag-staging.json",
                     "godot/data/grayflag-content-text.json", "godot/data/portrait-catalog.json",
                     "godot/data/tactical-hud-art.json", "godot/Localization/zh_CN.po"]:
            target = self.root / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source / name, target)
        portraits = json.loads((source / "godot/data/portrait-catalog.json").read_text())["portraits"]
        for asset in portraits.values():
            relative = Path("godot") / asset["resourcePath"].removeprefix("res://")
            (self.root / relative).parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source / relative, self.root / relative)

    def tearDown(self):
        self.temporary.cleanup()

    def change(self, name, mutate):
        path = self.root / "godot/data" / name
        value = json.loads(path.read_text())
        mutate(value)
        path.write_text(json.dumps(value, ensure_ascii=False))

    def reject(self):
        with self.assertRaises(CatalogError), contextlib.redirect_stdout(io.StringIO()):
            validate(self.root)

    def test_native_source_is_complete(self):
        with contextlib.redirect_stdout(io.StringIO()):
            validate(self.root)

    def test_missing_key(self):
        self.change("grayflag-story.json", lambda value: value["stories"][0].update(titleKey="story.missing.title"))
        self.reject()

    def test_missing_roster_identity(self):
        self.change("grayflag-story.json", lambda value: value["identityNameKeys"].pop("torren"))
        self.reject()

    def test_missing_roster_name_key(self):
        self.change("grayflag-story.json", lambda value: value["identityNameKeys"].update(elin="speaker.missing.name"))
        self.reject()

    def test_obsolete_speaker_name_owner(self):
        self.change("grayflag-story.json", lambda value: value.update(speakerNameKeys=value.pop("identityNameKeys")))
        self.reject()

    def test_named_unit_must_reference_shared_identity(self):
        self.change("grayflag-content-text.json", lambda value: value["content"]["units"]["c01.laiya"].update(name="content.units.c01.laiya.name"))
        with self.assertRaisesRegex(CatalogError, "Named unit must reference shared identity name: c01.laiya"):
            validate(self.root)

    def test_duplicate_named_unit_wording(self):
        path = self.root / "godot/Localization/zh_CN.po"
        with path.open("a") as target:
            target.write('\nmsgid "content.units.c01.laiya.name"\nmsgstr "莱娅"\n')
        with self.assertRaisesRegex(CatalogError, "Duplicate named unit wording: content.units.c01.laiya.name"):
            validate(self.root)

    def test_literal_prose_is_not_a_second_owner(self):
        self.change("grayflag-story.json", lambda value: value["stories"][0].update(title="another wording owner"))
        self.reject()

    def test_duplicate_beat_identity(self):
        self.change("grayflag-story.json", lambda value: value["stories"][0]["beats"][1].update(id=value["stories"][0]["beats"][0]["id"]))
        self.reject()

    def test_positional_beat_identity(self):
        self.change("grayflag-story.json", lambda value: value["stories"][0]["beats"][0].update(id="beat-0"))
        self.reject()

    def test_unknown_speaker(self):
        self.change("grayflag-story.json", lambda value: value["stories"][0]["beats"][0].update(speaker="unknown-speaker"))
        self.reject()

    def test_duplicate_chapter_label_owner(self):
        self.change("grayflag-staging.json", lambda value: value["levels"]["c01-01"].update(chapterTitle="duplicate wording"))
        self.reject()

    def test_unknown_shared_metadata_field(self):
        self.change("grayflag-staging.json", lambda value: value.update(unknownOwner={}))
        self.reject()

    def test_obsolete_schema(self):
        self.change("grayflag-story.json", lambda value: value.update(schema=2))
        self.reject()

    def test_obsolete_staging_schema(self):
        self.change("grayflag-staging.json", lambda value: value.update(schema=2))
        self.reject()

    def test_obsolete_hud_schema(self):
        self.change("tactical-hud-art.json", lambda value: value.update(schema=1))
        self.reject()

    def test_unknown_staging_portrait(self):
        self.change("grayflag-staging.json", lambda value: value["portraits"].update(mirelle="unknown"))
        self.reject()

    def test_embedded_staging_card_is_not_an_owner(self):
        self.change("grayflag-staging.json", lambda value: value["portraits"].update(mirelle={"resourcePath": "res://other.png"}))
        self.reject()

    def test_unknown_hud_portrait(self):
        self.change("tactical-hud-art.json", lambda value: value["portraits"]["campaign-torren"].update(portraitId="unknown"))
        self.reject()

    def test_missing_hud_portrait_choice_is_not_null(self):
        self.change("tactical-hud-art.json", lambda value: value["portraits"]["c01.swordsman"].pop("portraitId"))
        self.reject()

    def test_embedded_hud_card_is_not_an_owner(self):
        self.change("tactical-hud-art.json", lambda value: value["portraits"]["c01.mirelle"].update(card={}))
        self.reject()

    def test_catalog_rejects_duplicate_source_fact(self):
        self.change("portrait-catalog.json", lambda value: value["portraits"]["mirelle"].update(source="/godot/other.png"))
        self.reject()

    def test_catalog_rejects_hash_mismatch(self):
        self.change("portrait-catalog.json", lambda value: value["portraits"]["mirelle"].update(sha256="0" * 64))
        self.reject()

    def test_catalog_rejects_changed_bytes(self):
        path = self.root / "godot/assets/runtime/narrative-static/char-mirel-01.png"
        path.write_bytes(path.read_bytes() + b"changed")
        self.reject()

    def test_catalog_rejects_duplicate_resource(self):
        self.change("portrait-catalog.json", lambda value: value["portraits"].update(other=value["portraits"]["mirelle"].copy()))
        self.reject()

    def test_catalog_rejects_escaping_resource(self):
        self.change("portrait-catalog.json", lambda value: value["portraits"]["mirelle"].update(resourcePath="res://assets/../other.png"))
        self.reject()

    def test_duplicate_json_field(self):
        path = self.root / "godot/data/grayflag-story.json"
        path.write_text(path.read_text().replace('"schema": 3', '"schema": 3, "schema": 3', 1))
        self.reject()

    def test_orphaned_message(self):
        path = self.root / "godot/Localization/zh_CN.po"
        with path.open("a") as target:
            target.write('\nmsgid "story.deleted.title"\nmsgstr "unused"\n')
        self.reject()


if __name__ == "__main__":
    unittest.main()
