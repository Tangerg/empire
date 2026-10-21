# Godot source metadata

Script `.uid` files and resource `.import` settings are source companions and are
committed with their source, including renames. Godot's `.godot` directory and .NET
`bin`/`obj` directories are generated caches and remain ignored. Reimporting must
not silently replace stable source identities with new ones.

After adding or renaming a script or imported image/font, run the matching Godot
editor import, review the resulting companion files, and stage them with the
source. Run `python3 tools/godot/check-source-metadata.py` after staging. The check
uses the Git index's path list and verifies existing source companions and unique
UIDs; it does not stage files or delete working files.

The historical root `actual-initial-state.json` and
`tools/godot/probe-campaign-adapter.ts` are local investigation artifacts, not
production migration inputs. They are deliberately preserved outside commits.
Their presence must not be described as a fully clean working directory.

Godot documents that these companions belong in version control:
- https://godotengine.org/article/uid-changes-coming-to-godot-4-4/
- https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/import_process.html
