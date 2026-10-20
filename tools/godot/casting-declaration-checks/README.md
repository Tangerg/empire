# Public cast declaration parity

Run from the repository root:

```sh
source tools/godot/env.sh
tools/godot/casting-declaration-checks/run.sh
tools/godot/casting-declaration-checks/run.sh --frozen --verify-guard
```

The live verifier executes the actual TypeScript source functions. The frozen mode reads
`godot/tests/oracle/casting-declarations.json.gz`; it does not require Node at test time.
Builds, copied semantic mutants and diagnostics go into a fresh temporary directory,
not the application project or user save slots. No dependencies are downloaded.

## Public contract and ownership

`BattleCombat.BeginCast(state, CastDeclaration, emit)` is the existing native cast writer,
now public. `CastDeclaration` names caster, arbitrary string ability metadata, weapon,
locked target and locked origin. The method returns the very JSON object stored in
`state.pendingCasts`. `ExecuteAttack` calls this writer with ability `attack`; there is
no second declaration transition and no custom-ability dispatch during resolution.

Source owners:

- `packages/battle-engine/src/casting.ts`: `CastDeclaration`, `beginCast`, cancellation,
  due ordering, refusal precedence and canonical combat-plan resolution.
- `packages/battle-engine/src/domain/spell-cast.ts`: charge-duration arithmetic and
  invalid declared/resolve-time failure.
- `packages/battle-engine/src/battle-save.ts`: save/load ownership and shape admission.
- `packages/battle-engine/src/rule-references.ts`: real ability references belong to
  unit/career declarations and learned abilities, not cast metadata. Pending casts
  do not contribute weapon or ability references here.

A declaration does not perform command eligibility, geometry, resource consumption or
finish-actor work. Instant weapons are caller defects (`DomainInvariantException`),
checked before the duplicate-charge order refusal (`IllegalActionException`). It
clones the target/origin into the saved record and separately into the emitted event.
Positive fractional durations use source `Math.round` semantics with a minimum of one.

The old native `SupportedCastAbilities` / `castAbilities` pseudo-capability is removed.
An ability string is preserved, not registered, trimmed, translated, substituted or
executed. A cast naming an unknown or non-equipped weapon likewise survives save/load;
resolution uses the existing ready-weapon query and emits `weaponUnavailable`.
Actual unit weapon/learned-ability references remain validated.

## Verification coverage

34 source-generated cases, 101 staged operations and 847 exact comparisons cover:

- Registered, unregistered, empty, whitespace and Unicode metadata, including `heal`
  resolving as a combat strike rather than invoking that ability.
- Direct public declaration, return identity, independently owned event targets,
  supplied geometry without legality normalization, and unchanged done/cost state.
- Negative/zero duration, fractional boundaries, duplicate refusal and error precedence.
- Save/load preservation and non-mutation, including unknown/non-equipped weapons;
  genuine unit weapon and learned-ability references still fail.
- Canonical resolution, no early/repeated resolution, oldest-first due ordering,
  frozen origin after relocation, protected origin/target, vacated area/single target,
  cooldown refusal, orphaned caster and immediate departure cancellation.
- Exact typed errors/messages, ordered events, the digest at every event callback,
  returned records, full state and final digest at every stage.

13 deliberate semantic mutants must compile and fail their named assertion: stamped
metadata, instant weapon acceptance, duplicate acceptance, reversed error precedence,
floor rounding, detached return, closed ability admission, both old weapon-admission
sites, metadata-controlled resolution, wrong cancellation reason, live instead of
locked origin, and reversed due ordering. The harness rejects an unexpected failure
or a mutant that merely fails to compile.

Four separate native-only regression guards retain existing structural/timing
rejections (non-string metadata, non-integer caster, non-positive duration and a
non-array cast collection). They are not claimed as source admission parity: the
TypeScript save reader validates less of the pending record. This patch removes only
invented cast reference restrictions, not unrelated native shape or timing checks.

The source casting/save suites and existing 259-case combat differential suite are
also run for this change. This is a focused cast contract slice, not a claim that all
engine extension registries or all malformed-save semantics have migrated.
