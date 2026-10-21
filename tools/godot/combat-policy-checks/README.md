# Combat policy source parity

Run live original TypeScript differential checks:

```sh
bash tools/godot/combat-policy-checks/run.sh --verify-guard
```

Run the same frozen original-source oracle without Node:

```sh
bash tools/godot/combat-policy-checks/run.sh --frozen --verify-guard
```

The runner builds native code into a temporary directory. Both paths compare 38
pipeline cases, 17 registry observations, and 25 actual native combat consumer
cases (189 assertions). Fourteen independently compiled mutants must fail a parity
assertion, including keeping an injected policy visible but ignoring it at actual
execution. No TS process or bridge is involved in native execution.

Coverage includes ordered power/add/multiply contributions, mitigation caps
(including zero/negative/above-one source behavior), ordered affine final phases,
ECMAScript rounding/minimum damage, UTF-16 and nonfinite-priority ties, registry
replacement/clone/seal/snapshot contracts, and shared context projections. The
consumer cases exercise replacement and extended providers, custom/replaced hit
handlers, descriptions, direct application, primary/area/counter/support strikes,
status effects, forced movement/collision, canonical secondary damage settlement,
nonlethal damage, morale departure, a stale forecast whose target now dies, and
source-ordered rejection after an earlier effect. They compare complete plans,
ordered events, every event-time state digest, complete states, and typed errors.
Structure damage deliberately bypasses the unit modifier pipeline, as in source.

`oracle.ts` imports the original source modules, not a rewritten numeric oracle.
Frozen fixture: `godot/tests/oracle/combat-policies.json.gz`. Regenerate by running
the live oracle to a temporary JSON file and gzip-compressing with `mtime=0`.

Native `HitEffectAdmissionChecks` additionally composes new and replaced `forcedMove`/`addStatus` handlers whose profile payload deliberately differs from the built-in schema. Selected handlers own structural/reference admission, execution and description. Invalid content/direct effects are refused by that owner; valid attacks survive named-plugin save/load/replay. Authored/direct/dispatch invalid-duration cases exercise the shared status owner and transaction rollback. Compiled bypasses must expose omitted content, handler or execution validation.

These tests establish policy and canonical-consumer parity plus the bounded native
admission contracts above. They do not claim arbitrary plugin or presentation correctness. Unknown effect rejection is deliberately
a direct-registry case: the source engine rejects unknown weapon effect IDs at
configuration time, so this test does not bypass that admission contract.
