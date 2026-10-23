# Native narrative checks

Run `bash tools/godot/narrative-checks/run.sh --verify-guard`. Every run uses a fresh temporary native project and isolated XDG data/config/cache/runtime directories, the production narrative controls/projection/JSON loader, the native PO resource and catalog-verified native portraits plus source-verified scene art. No TypeScript runtime is needed.

`godot/tests/native/narrative.json` owns the reviewed current native expectations: 106 story projections, 11 choice menus, 48 transition fixtures, 11 lifecycle observations and four supplemental native inputs. It is checked in, not generated from actual output. The historical `godot/tests/oracle/narrative.json.gz` is unchanged and no longer supplies current native wording. See [the fixture migration contract](../../../godot/tests/native/narrative.md) for the date, six-message and native portrait-catalog migrations. Complete story projections compare strictly without historical field adapters. Unknown speakers fail rather than displaying an untranslated ID.

`verify-native.sh` explicitly runs the same `NarrativeChecks` scene against the same native fixture. There is no second expected-value map in C# or the runners.

The native tests cover all 35 story documents and 91 beats, all choices, paging/ending callback ownership, progress, missing art, stale/detached/rebound controls and real resize/scroll layout. Two independent injected catalogs are exercised through actual widgets, without adding a shipped language. Tests reject missing messages, duplicate beat IDs and obsolete literal fields, and prove reordering a beat preserves its semantic wording identity.

`--verify-guard` rejects wrong reinforcement-deadline prose expectations in both projections and widgets, a wrong speaker expectation in the independent 91-beat shared-identity group, and a wrong next-beat key in the temporary production projection. Check groups aggregate failures without preventing unrelated groups from running. The source guard separately checks every narrative message reference and forbids a second literal title owner.

`export-oracle.ts` is retained only as an optional original-browser research tool. Its raw original input shape is not the native authoring schema and must not overwrite native documents or the reviewed native expectations. Production wording is owned solely by `godot/Localization/zh_CN.po`; `tools/godot/export-grayflag.ts` no longer writes story/staging documents or scrapes menu wording.

See `godot/docs/NARRATIVE.md` for ownership and acceptance limits. Headless layout checks are not physical GUI acceptance or a victorious playthrough.
