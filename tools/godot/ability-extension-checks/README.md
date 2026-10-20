# Open ability command checks

`bash tools/godot/ability-extension-checks/run.sh --verify-guard`

Add `--frozen` to use the checked-in gzip fixture without Node or TypeScript:
`bash tools/godot/ability-extension-checks/run.sh --frozen --verify-guard`.
Both flags can appear in either order; omitting `--frozen` regenerates the live oracle.

Generates an offline oracle by invoking the original TypeScript ability definitions,
command menu and command action handler. The native runner executes the same command
semantics through `AbilityCommands` and the existing `Battle` transaction writer.
Failures also execute the original engine dispatch to establish its rollback result.
The checked-in `godot/tests/oracle/ability-extensions.json.gz` is the same generated
fixture; native execution needs no TypeScript runtime.

The 55 cases compare exact menu keys, labels, hints and stable order; rejection types
and messages; ordered events; complete post-command states and digests; and native
undo. Coverage includes a second weapon-using ability, every selected weapon, custom
weapon availability, tag/status gates, replacement attack tags, engagement kinds and
player scoping, target validation, side effects, rollback, deaths during movement or
ability execution, captured-actor replacement, capture, healing/rank awards, ordinary attacks and delayed casts.
Forty duplicate weapon keys protect stable equal-priority ordering. Ten deliberate
mutants must compile and then fail their intended behavioral assertions.

The comparator preserves numeric priority (including fractional values), then ordinal
UTF-16 command key order. For a consistent comparator it preserves equal-key input
order. As in the source registry contract, mixed NaN priorities can make the comparator
non-transitive; cross-runtime sorting results for that invalid ordering relation are
not claimed to match.

This is command-slice evidence, not plugin composition, custom AI, admission, save or
arbitrary extension parity. The current engine uses the registry for menus/execution
and derives ability admission names from its keys. Injecting a custom registry through
the one capability composition root remains a separate integration milestone. The
harness does not introduce a second production construction path.
