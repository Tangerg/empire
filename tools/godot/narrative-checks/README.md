# Native narrative checks

Run `bash tools/godot/narrative-checks/run.sh --verify-guard` from the repository. The installed official Godot.NET/.NET tools are selected by `tools/godot/env.sh`. Each run creates `/tmp/empire-narrative.*`, isolates `XDG_DATA_HOME`, and prints its output path.

`export-oracle.ts` instantiates the original `StoryCampaignController` in JSDOM with the real campaign adapter, content, battle engine and runtime. It extracts the actual rendered DOM, not a rewritten TypeScript expectation function. Original story/choice IDs come from campaign nodes and adapter briefing IDs.

The isolated Godot scene runs the production narrative projection and controls against all 106 story projections, four supplemental edge projections and 11 choice menus. The oracle additionally asserts 11 actual DOM lifecycle fixtures, explicitly labeling its three unmount/remount differences. It checks local single-beat paging, original text/art, null scenes, narration, source progress/navigation, canonical-callback boundaries, ending outcomes, stale/rebound/double/unmounted controls and real native resize/scroll layout. SHA-256 checks compare required exported PNGs with original source bytes. The source transition data contains 48 cases; native callback tests use the 39 reachable beat-zero sequences rather than introducing an API for unreachable nonzero ending cursors.

`--verify-guard` mutates only the temporary native projection's `下一段` label to `继续`, rebuilds, and requires the source comparison to fail. It never modifies production code.

Latest verification: 4,949 native assertions and the negative-control mutation gate passed; combined project build had zero warnings/errors.

See `godot/docs/NARRATIVE.md` for the intentional ending quirk, safety differences and acceptance boundaries. These are controlled presentation/runtime fixtures, not a played or won campaign. JSDOM does not establish browser layout or scroll geometry, and native headless checks do not constitute GUI acceptance.
