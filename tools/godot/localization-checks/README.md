# Localization foundation checks

Run `bash tools/godot/localization-checks/run.sh`; add `--packaged` to verify a native Linux release export. Uses the repository's `tools/godot/env.sh` toolchain and creates fresh temporary projects/data. Does not read or modify player saves.

The source guard validates the project's currently authored singular, context-free PO subset. It is not a runtime translation loader; Godot parses the actual shipped resource. The JSON emitted into the temporary check directory is a disposable assertion projection, never an additional production catalog. Source lint is mandatory because Godot may normalize duplicate messages or ignore fuzzy entries before its resource API can expose them.

Coverage:

- Duplicate keys (including adjacent entries), malformed quoted input, missing values, empty/whitespace translations, fuzzy/obsolete entries, missing/wrong locale, invalid message-key syntax and an empty catalog.
- Native .NET composite formatting, reordered/repeated arguments, escaped braces, invalid formats, exact arity (missing and extra arguments), index gaps, and required-key completeness.
- Two independent injected test catalogs, explicit culture isolation from process culture and later caller mutation.
- Exact equality of every source-catalog entry with Godot's native TranslationServer lookup; resource locale, strict missing-key behavior and one parameterized real Chinese message.
- Native schema-3 story/schema-3 staging reference completeness, declared campaign roster name coverage, strict field ownership, speaker/beat identities, and negative controls for obsolete fields, duplicated/positional beats and orphaned messages.
- Single-owner schema-1 portrait catalog shape and native byte hashes, schema-2 HUD explicit portrait choices, existing portrait-ID references, and rejection of embedded card facts, obsolete schemas, missing choices, unknown IDs, altered bytes and duplicate/escaping resource paths.
- Optional isolated native release packaging, followed by launching only the exported bundle.

These checks deliberately assert failures, not just scan for methods that exist. They are foundation tests. They do not claim the existing application has stopped exposing all hardcoded strings or that domain schema migration is complete. Narrative content-key discovery and real consumers are now integrated. Semantic-code coverage, remaining menu/battle consumers and core locale-independence tests still belong to their separate migrations.
