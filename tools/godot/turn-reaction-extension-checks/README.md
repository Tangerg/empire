# Turn-order and reaction extension parity

Run `bash tools/godot/turn-reaction-extension-checks/run.sh --verify-guards` with the existing .NET toolchain. Add `--regenerate` first to execute the original offline TypeScript source and use a fresh oracle. TypeScript is used only for development-time differential fixtures, never by the C# runtime.

The fixture contains 41 lifecycle/turn-policy vectors, 30 reaction vectors and eight actual action vectors. The native runner checks source and native-contract assertions covering complete state, ordered events, event-time and final hashes, query purity, exact default metadata, replacement/upsert semantics, shallow-clone identity and install-time override consumption.

Turn tests include a custom policy whose initial state chooses a different policy: Start must capture the requested policy for both initial-state creation and Begin, while all subsequent queries and Advance select the policy stored in state. Other vectors cover independent Actors/Preview behavior, phase entitlement, exhaustion, round/actor timing, dead initiative actors and custom command authorization.

Reaction tests exercise custom mitigation, interception, retaliation and resource conservation through actual forecast/execution owners. The default support definition declares its 0.6 support-attack modifier and 20 triggered rank progress. New IDs and replacements can change/remove both behaviors; ordinary interceptor definitions do not gain rewards implicitly. The frozen source unknown-stance action is retained as evidence of its invalid commit; the native action must instead refuse atomically with no state, event-count, digest or undo-checkpoint change.

Seventeen compiled semantic mutants must fail their targeted source or native assertions, covering turn-policy selection, command entitlement, consumer identity, reaction/facing admission, internal transition invariants, saved-facing admission, custom tiling directions and registry-owned support selection/modifiers/progress.

Native regressions use real named plugins and engines. They cover every declared direction in square4, square8, hex and a custom renamed-direction tiling; authored/saved rejection; accepted command save/load/replay; automatic facing; and new/replaced support behavior through actual attacks. Application choice/HUD stance lists enumerate the resolved registry, but arbitrary custom presentation and complete default campaign parity remain separate gates.

Native action refusals use explicit named-case `code`/`details` expectations,
including unknown reaction and invalid facing parameters. The eight frozen source
action cases retain their original outcome data; no formatter converts native
refusals back to source prose. Complete case coverage, rollback, event ordering,
state/hash parity and the intentional unknown-stance rejection remain enforced.
