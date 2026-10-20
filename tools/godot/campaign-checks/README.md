# Native campaign source parity

Run `bash tools/godot/campaign-checks/run.sh` after exporting the Candidate 01 data and installing the repository toolchain.

The source TypeScript generates every expected value at test time. Tests cover:
- 296 runtime operations along both authored choice routes through all 16 battles in the first three chapters, including rejection rollback and completed-campaign guards.
- 71 exact authored context/result policy projections.
- 40 exact roster-to-level and battle-to-roster bridge cases, including embarked units, casualty markers, unknown departure kinds, absent roster members, and ratios.
- 90 exact save/validation results and error/cause comparisons, plus nested clone isolation and typed CLR numeric values.
- 27 exact registry results and error name/message comparisons across campaign conditions, effects, and nodes: ordered registration, query/demand lookup, replacement/upsert, sealing, and independent mutable clones.
- Custom condition/effect/node execution and frozen-runtime clone isolation, including replacement of a node validator and exact custom validation failures.

The rules oracle also emits standalone condition/effect cases, synthetic runtime cases, and default registry seal flags. This runner compares only its registry and custom-extension sections; generating the other sections does not establish their native parity.

These are campaign-orchestration checks. Bridge result tests consume actual TypeScript battle snapshots; they do not claim all 16 battles already have complete native execution support. Battle rules remain owned by the battle engine.

Campaign saves require schema 1, matching campaign identity/version and exact content-pack versions. Timestamp validation shares the persistence reader: standard ISO timestamps are covered, not exhaustive JavaScript legacy Date.parse spellings.

All generated fixtures stay under the runner's printed temporary directory. Production campaign code never reads a test fixture.
