# Canonical UTF-16 JSON boundary checks

Run `bash tools/godot/utf16-boundary-checks/run.sh` from the repository. It uses the existing official .NET toolchain and installed Vite runner; no software installation is performed. Generated oracle output stays in the printed temporary directory.

The oracle calls the original TypeScript RNG and digest, not a second test implementation. It covers every lone UTF-16 surrogate unit, mixed lone strings, valid astral pairs, controls and escapes, property keys, duplicate keys, JavaScript property enumeration and finite binary64 number formatting. The native checks exercise parse, `DeepClone`, eight RNG draws and counter-key writes, save text, parse, digest, immutable `JsonElement` transfer, exact equality and actual engine save/load.

The scenario suite independently exercises real chance-trigger dispatch followed by save, canonical text roundtrip, reload and continued dispatch against the TypeScript engine. Run `bash tools/godot/scenario-checks/run.sh` for that integration coverage.

## Required application boundary

- Read original JSON text using `EcmaJson.Parse`, or use the engine's `LoadBattleJson` convenience API. Write JSON text using `EcmaJson.Stringify` / `SaveBattleJson`. Use `EcmaJson.DeepEquals` for canonical state comparisons.
- Parsed strings and object keys are eager CLR UTF-16 strings. They retain lone code units through normal `JsonNode.DeepClone`, lookup and counter writes. `EcmaJson.ReadString` also handles an accessible lazy scalar from `JsonNode.Parse` without replacement.
- Do not first parse escaped lone-surrogate property names with `JsonNode.Parse`. The resulting lazy `JsonObject` cannot expose those keys or original raw JSON through its public API. `EcmaJson.Normalize` explicitly rejects such inaccessible objects and asks for the original text. It does not repair, replace or normalize code units. Eager CLR-created objects with those same keys are supported.
- Do not use default `JsonNode.ToJsonString`, `JsonSerializer.SerializeToElement`, or `JsonNode.DeepEquals` on canonical state: they can replace distinct lone units with U+FFFD. For immutable JSON transfer use `EcmaJson.ToElement`, then retain its raw text or decode it through the canonical boundary.
- Numeric values use finite binary64, matching the existing state-digest contract. This boundary is not a replacement for domain validation of malformed CLR-created NaN/Infinity state. Let existing field guards classify those invalid inputs before any serialization.

Running the console project without arguments prints isolated .NET behavior probes demonstrating the original failure and why a value-only JSON converter does not fix property names or cloning.
