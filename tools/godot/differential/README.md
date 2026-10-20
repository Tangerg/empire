# Deterministic primitive differential checks

This development-only verifier compares the compiled C# core against Node's actual binary64 number formatting and the TypeScript engine's canonicalization/RNG algorithms. It is not a runtime bridge. No generated vectors or binaries belong in source control.

Requirements: .NET SDK 8 and Node on PATH. Build the core, then run from any directory:

```sh
dotnet build godot/Empire.Core/Empire.Core.csproj
tools/godot/differential/run.sh
```

Set `EMPIRE_CORE_ASSEMBLY` to test another compiled core DLL. The script uses a fresh temporary project, has no NuGet package dependencies, and removes its own generated files afterward. It never rebuilds the referenced core assembly.

## Verified reconstruction result: 2026-10-20

Using the reconstructed source based on main `bfe9334a29630351ba4f46fd4c195cb3a72c36d9`, the separate .NET 8 verifier built with zero warnings/errors and passed:

- 257,460 finite binary64 values: exact ECMAScript decimal text and both constructed/parsed JSON state hashes. Includes deterministic bit-pattern sampling, scientific-notation thresholds, subnormal numbers, extreme finite values and adjacent representable numbers.
- 65,548 UTF-16 string/canonical-path vectors, including every escaped code unit, lone surrogates, supplementary characters, control escaping, sorted keys and exact exclusions.
- Nonfinite digest values rejected explicitly, intentionally stricter than `JSON.stringify` mapping them to null.
- 360 RNG seed/counter boundary vectors.
- RNG bound validation, chance endpoints, exact-source refusal, independent cloned counters and state JSON round trips.

This establishes primitive parity for the checked cases, not complete battle-rule or gameplay parity. It is reconstruction verification, not recovery of the deleted earlier commit.
