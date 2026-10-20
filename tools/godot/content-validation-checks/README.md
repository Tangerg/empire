# Content admission checks

Run `bash tools/godot/content-validation-checks/run.sh` from the repository root.

The generator runs the original TypeScript content installer and default battle
engine. The native runner composes a fresh `GrayFlagEngine` for every fixture and
compares the exact diagnostic or acceptance, while ensuring input packs are
unchanged. Coverage includes catalog references, account/cost arithmetic, morale,
transport capacity, hit-effect payloads, careers and cycles, formations, tactics,
overlays, damage coverage, and capabilities referenced by currently unused units,
weapons, and unlearned mastery abilities.

Four cases deliberately differ from source admission: an unregistered resource
adapter in an account/recruitment cost/career cost, and an unregistered tactic
effect. They are explicit native fail-closed checks, not source-parity successes.

This is admission testing, not proof of arbitrary extension-plugin execution.
NaN and Infinity fixture mutations are serialized to null only where both are
invalid and have the same source diagnostic. No fixture lookup is used by the
production validator.
