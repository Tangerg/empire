# Native narrative checks

Run `bash tools/godot/narrative-checks/run.sh --verify-guard`. Every run uses a fresh temporary native project and `XDG_DATA_HOME`, the production narrative controls/projection/JSON loader, the native PO resource and exact source-verified art. No TypeScript runtime is needed.

`godot/tests/oracle/narrative.json.gz` retains frozen original rendered expectations: 106 story projections, 11 choice menus, 48 transition fixtures and 11 DOM lifecycle observations. Four supplemental inputs now use explicit native schema-2 structure and message references. Unknown speakers intentionally fail rather than displaying an untranslated ID; the other rendered expectations remain unchanged. No production old-schema adapter exists.

The native tests cover all 35 story documents and 91 beats, all choices, paging/ending callback ownership, progress, missing art, stale/detached/rebound controls and real resize/scroll layout. Two independent injected catalogs are exercised through actual widgets, without adding a shipped language. Tests reject missing messages, duplicate beat IDs and obsolete literal fields, and prove reordering a beat preserves its semantic wording identity.

`--verify-guard` changes only the temporary projection's next-beat key to the continue key and requires the frozen rendering comparison to fail. The source guard separately checks every narrative message reference and forbids a second literal title owner. Latest run: 4,940 assertions plus the negative control, zero build warnings/errors.

`export-oracle.ts` is retained only as an optional original-browser research tool. Its raw original input shape is not the native authoring schema and must not overwrite native documents or the intentionally migrated native edge fixtures. Production wording is owned solely by `godot/Localization/zh_CN.po`; `tools/godot/export-grayflag.ts` no longer writes story/staging documents or scrapes menu wording.

See `godot/docs/NARRATIVE.md` for ownership and acceptance limits. Headless layout checks are not physical GUI acceptance or a victorious playthrough.
