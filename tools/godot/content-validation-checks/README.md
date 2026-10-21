# Content admission checks

Run `bash tools/godot/content-validation-checks/run.sh` from the repository root.

The generator runs the original TypeScript content installer and default battle
engine. The native runner composes a fresh `BattleEngine` for every fixture and
compares the exact diagnostic or acceptance, while ensuring input packs are
unchanged. Coverage includes catalog references, account/cost arithmetic, morale,
transport capacity, hit-effect payloads, careers and cycles, formations, tactics,
overlays, damage coverage, and capabilities referenced by currently unused units,
weapons, and unlearned mastery abilities.

All 72 cases now compare exact source outcomes. In particular, unregistered resource
IDs stored in accounts, recruitment/career costs, or an unused tactic payload do
not imply that an adapter is being used at catalog admission. These four former
native-only gates are source-backed compatibility corrections, not exceptions to
the comparison. Their historical case names remain searchable in the generator.
Execution still resolves the relevant owner when the rule is actually used; see
`../persistence-reference-checks` for atomic delayed-use failures.

This is admission testing, not proof of arbitrary extension-plugin execution.
NaN and Infinity fixture mutations are serialized to null only where both are
invalid and have the same source diagnostic. No fixture lookup is used by the
production validator.
