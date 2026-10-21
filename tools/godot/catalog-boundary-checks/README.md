# External catalog boundary checks

Run from the repository root:

    bash tools/godot/catalog-boundary-checks/run.sh
    bash tools/godot/catalog-boundary-checks/run.sh --verify-guard
    bash tools/godot/catalog-boundary-checks/run.sh --benchmark

This project references `Empire.Core.csproj`; it does not compile the core sources
into its own assembly or use friend-assembly/reflection access. It protects the real
external consumer contract described in [native catalog ownership](../../../godot/docs/CATALOG-OWNERSHIP.md).

The executable checks:

- Authoring-input isolation and immutable catalog definition collections.
- Every weapon query, ability weapon choice, reactive strike and modifier callback.
- Nested planned hit effects and effect-handler callbacks, across two engines sharing
  one canonical catalog.
- Immutable terrain/overlay definitions and detached active formation definitions.
- Exact lone-surrogate UTF-16 preservation through immutable and detached values.
- Identical save bytes after queries; 12 command event streams and state hashes;
  post-query save/load and deterministic replay against an untouched engine.
- Preservation of every catalog family, not just the sampled weapon.

`--verify-guard` copies sources into a temporary workspace, replaces the single catalog
copy boundary with a live-node return, builds the separate consumer project, and requires
its subsequent-forecast assertion to fail. Build errors do not count as a detected defect.
The original working tree is never modified by the negative control.

## Representative measurements

`--benchmark` reports elapsed time and thread allocation for engine composition,
movement fields, combat forecasts, AI selection and a 12-action replay from the shipped
19-unit first campaign level. It verifies the replay and prints initial/final state
hashes. Compare the same benchmark source and runtime against a baseline core assembly;
wall times on a shared executor are diagnostic rather than a hardware-independent gate.

The first candidate detached every internal JSON definition read. It was rejected after
measurement: AI allocation rose from about 41 MB to 91 MB per choice. The retained
implementation uses one uniform immutable `JsonElement` read surface for measured
movement/terrain/unit queries and detached JSON only where consumers need a mutable
projection. It does not expose raw mutable nodes to recover performance.
