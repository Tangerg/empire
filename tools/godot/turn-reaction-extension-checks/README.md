# Turn-order and reaction extension parity

Run `bash tools/godot/turn-reaction-extension-checks/run.sh --verify-guards` with the existing .NET toolchain. Add `--regenerate` first to execute the original offline TypeScript source and use a fresh oracle. TypeScript is used only for development-time differential fixtures, never by the C# runtime.

The fixture contains 41 lifecycle/turn-policy vectors, 30 reaction vectors and eight actual action vectors. The native runner checks 481 assertions covering complete state, ordered events, event-time and final hashes, query purity, exact default metadata, replacement/upsert semantics, shallow-clone identity and install-time override consumption.

Turn tests include a custom policy whose initial state chooses a different policy: Start must capture the requested policy for both initial-state creation and Begin, while all subsequent queries and Advance select the policy stored in state. Other vectors cover independent Actors/Preview behavior, phase entitlement, exhaustion, round/actor timing, dead initiative actors and custom command authorization.

Reaction tests exercise custom mitigation, interception, retaliation and resource conservation through actual forecast/execution owners. They preserve the source's literal support-only reward rather than promoting it to every interceptor. Source reaction actions accept an unknown stance string without resolving it; lookup is deferred until a consumer needs the behavior. The unknown-stance action vector guards this deliberate parity correction.

Ten compiled semantic mutants must fail their specific differential assertions: initial-policy capture, state-policy selection, phase gate, delegated Actors, exhaustion, registry capture, custom interception, resource conservation, premature unknown-stance validation and support identity.

Application choice and HUD stance lists enumerate the resolved reaction registry. Full plugin-root admission, persistence and application UI integration remain the composition integration gate, not a claim made by this focused harness.
