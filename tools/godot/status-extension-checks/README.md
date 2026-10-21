# Status extension differential checks

Run `bash tools/godot/status-extension-checks/run.sh` against the original TypeScript oracle, or add `--frozen` for the checked-in gzip fixture without Node. Add `--verify-guard` to compile and reject ten deliberate semantic bypasses. Tests write diagnostics to a temporary directory, not the source fixture directory.

The 26 cases compare exact errors, ordered events, complete states and state hashes (104 assertions). They cover periodic damage before the owner-turn hook, actual/nonlethal damage return, departure short-circuiting, copied status iteration, status replacement/removal, duration expiry by identifier, same-identifier unit replacement, unknown optional hooks, and exact catalog identity. Each case runs both the standalone status lifecycle and the real end-turn action transaction. These are extension semantics, not visual acceptance.

The core owns damage and status transitions. Hooks receive the same resolved read-only content owner and mutate the canonical transaction draft. Unknown optional hook handlers are inert; unknown status definitions still fail at content resolution. The hook does not create a second rule engine.

Verified in the isolated status candidate: fresh source output byte-matched the fixture; frozen runner passed 26 cases / 104 assertions; ten compiled semantic mutants were rejected; default Core suite passed 14 / 0. The integrated end-turn case uses the required BattleLifecycleRules dependency record and preserves the explicitly injected status owner. Final exact-tree aggregate evidence is recorded in godot/docs/NATIVE_CHECKPOINT.md.
