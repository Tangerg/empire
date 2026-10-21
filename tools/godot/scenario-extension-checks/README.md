# Scenario and objective extension parity

Run `tools/godot/scenario-extension-checks/run.sh --frozen --verify-guards` with the repository's existing toolchain for Node-free verification. The checked-in `godot/tests/oracle/scenario-extensions.json.gz` is a deterministic gzip snapshot generated from the live TypeScript oracle (zero timestamp, no stored filename). Omit `--frozen` to regenerate the development oracle using the original TypeScript source. `EMPIRE_SOURCE_ROOT` may name the source checkout when checking an isolated C# overlay.

The harness is an external Core consumer, not a source-included test assembly. It generates its oracle from the original TypeScript registries and checks:

- A custom compound condition, custom effect calling a replaced built-in effect, and custom objective through real trigger stabilization and objective refresh. Ordered events, every event-time digest, and the full final state must match.
- Handler-owned references, children, roles, descriptions, progress, and refresh policy, including 196 authored and invalid-edge metadata payloads.
- Exact ordered default registry keys: 16 conditions, 34 effects, and 17 objectives. These counts include `composite`, `moveComposite`, and `neutralizeComposite`; the previous handoff's 35-effect count was incorrect.
- A custom objective with declared children and default `self` refresh does not refresh its children.
- Unknown reference queries return empty metadata; unknown execution and children demands throw.
- Clone isolation, duplicate registration rejection, sealing, built-in replacement, nested condition dispatch, and full-state random-source injection.
- Source `unitHealth` gauge semantics: HP below zero reads 0; HP above max reads 1; nonpositive max reads 1. This corrects the prior native raw division, and the resumed incomplete candidate's incorrect zero-max fallback of 0.

Ten compiled mutants prove the tests reject condition/effect/objective default-registry bypasses, forced child refresh, objective-role bypass, unclamped health, the wrong nonpositive-maximum fallback, destination/selector-zone reference ordering, explicit-null optional thresholds, and ECMAScript whitespace (including BOM versus nonblank U+0085/U+180E). Compilation failure is not counted as a killed mutant.

The existing `scenario-checks` harness remains the default-behavior fence: 466 live source scenario cases, 29 full-engine dispatch/save-resume steps, and 3 non-finite status guards.
