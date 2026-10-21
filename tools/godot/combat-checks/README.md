# Native combat parity checks

Run from any directory after installing the workspace toolchain and npm dependencies:

```sh
tools/godot/combat-checks/run.sh
tools/godot/combat-checks/run.sh --verify-guard
```

This development-only verifier runs the actual TypeScript rule functions to generate fresh expected results, compiles the native combat sources in a separate temporary .NET 8 project, and compares C# execution. It is not a gameplay/runtime bridge and does not rebuild the shared application project. No generated oracle JSON, binaries, or build caches belong in this directory. Successful runs remove their temporary artifacts; a failure reports its retained diagnostic directory.

## Verified on 2026-10-20

259 cases passed with zero compilation warnings/errors. Each case compares the complete forecast or query result, ordered events, state digest at every emitted event, full final state, and any domain/illegal-action failure type and message. The only wire normalization removes the absent optional `protectedUnit` property from C# projections when null. All other null values remain significant.

Covered behaviors:

- Counter, guard, support and conserve stances, spent reactions, interception and support tie-breaks, surviving/defeated primary targets.
- Attacker HP/rank thresholds, facing/flanking, status and elevation contributions, unit and structure splash.
- Single, cross, nine-cell and line coverage on square4, square8 and odd-row hex boards, including clipped board edges and vacant primary tiles.
- Push/pull, fractional distances, capture clearing, occupancy, elevation refusal and fatal collision.
- Parting shots, including exclusion of hit riders/rank rewards and termination after the mover dies.
- Charged attacks: declaration, duplicate rejection, due ordering, not-yet-due retention, caster loss, weapon unavailability, sanctuary refusal, empty area resolution, single-target vacancy and invalid duration.
- Silence and movement legality, limited and unlimited weapon-use resources, and consumption.
- Every one of the 38 installed weapon definitions against three unit archetypes, including all authored hit effects.
- All three status stacking modes and provenance, removal idempotence, every installed career mastery threshold, grouped resource readiness and ordered fractional consumption.
- Structure rounding/clipping, dead structures, invalid amounts and lookup/error precedence.
- Custom area registration/replacement, clipping, ordered deduplication and changed occupant requirements in both forecast and delayed resolution. Independent registry checks cover duplicate rejection, sealing, unsealed clones, key order and ask/demand APIs.

Two source edge differences were corrected while adding the checks: destroyed structures return before raw-damage validation, and invalid cast durations report their declared/resolve values.

The optional negative control copies combat sources into the temporary directory, deliberately changes the guard multiplier from 0.7 to 0.8, recompiles, and requires the forecast comparison to reject it. It never modifies the repository's implementation.

These checks supplement the integrated GrayFlag replay fixtures and the separate damage/departure suite. They establish parity for the named cases, not complete plugin/extension-system migration.

## Combat compatibility gate

The native composition root must supply its own `WeaponAreaRegistry`. `Core()` returns fresh, mutable definitions for `single`, `cross1`, `ring1` and `line`; the root seals the registry after composition. `NeedsOccupant` and coverage belong to that registry, not to string comparisons in forecast, casting or the AI.

The installed candidate-01 weapons currently need no unsupported combat behavior. Supported defaults include `counter`, `guard`, `support` and `conserve` reactions; `addStatus`, `removeStatus` and `forcedMove` hit effects; push/pull collision damage; cooldowns; charged attacks; player/unit/weapon resource transactions; rank and career mastery rewards; and canonical damage/death/morale/commander/cast-cancellation consequences. Status arithmetic has one owner, `CombatEffects`, shared with movement and command usability.

Combat consumers now receive resolved modifier, hit-effect, reaction, rank and departure policies from the single composition root. This focused suite retains the original default combat comparisons; `combat-policy-checks`, `departure-rank-checks`, `turn-reaction-extension-checks` and `engine-composition-checks` verify actual nondefault consumers and named-plugin save/replay separately. Handler-owned admission follows source resolution boundaries rather than imposing a blanket saved-ID whitelist. No universal arbitrary-plugin or visual-acceptance claim follows from these combat cases.
