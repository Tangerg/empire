# Native unit health transitions

From the repository root, with the SDK already configured through `tools/godot/env.sh`:

```sh
bash tools/godot/health-transition-checks/run.sh --verify-guard
```

The runner copies Core source and this external-consumer project into an isolated
temporary directory, compiles with a project reference (not source inclusion),
and reads only the checked-in content/initial-state fixtures. It uses no user
saves or external packages. Builds and executions have bounded timeouts. Temporary
builds are removed on completion; no production source is mutated.

## Contract and ownership

`UnitHealth` owns live-unit health over its existing `JsonObject`; no second state
representation or compatibility API is introduced. It validates requested damage
and healing before rounding/clamping, validates current health against canonical
content, and owns all four numerical transitions: damage, healing, restore, and
health-preserving type change. `BattleDamage` keeps multi-entity aftermath and
ordered event ownership. `TacticalCell.Heal` remains a battlefield read fact.

An explicit restore strength must be finite, positive, and at most the canonical
maximum before rounding. Positive fractions use the game's minimum-one rounding,
without exceeding fractional maxima. Omitted strength preserves a valid living
snapshot exactly. A corpse requires an explicit revival policy; scenario revival
supplies maximum health × its policy ratio. Withdrawn restoration preserves the
snapshot. Invalid health must fail before return or career cost mutations/events.

The test executable exercises the public entity API plus real aggregate damage,
registered healing ability, lifecycle regeneration, scenario revival/withdrawn
return, career cost preflight, and transactional rollback. Inputs include
negative values, NaN, both infinities, zero, fractional rounding, maximum finite
doubles, fractional maxima, dead snapshots, unknown types, and over-maximum HP.

## Negative controls

`--verify-guard` compiles and then deliberately breaks four semantics independently:

1. Clamp negative aggregate damage before the entity can validate it.
2. Admit non-finite healing inputs.
3. Clamp away invalid explicit restore bounds.
4. Spend career costs before health validation.

Each mutant must fail its named semantic assertion. Build failure, timeout, and
unrelated failure are not accepted as evidence that a guard works.

## Intentional differences from the source

The original source silently admits some invalid health inputs. Those vectors
remain in the differential suites as explicit native invariant assertions, not
skipped tests or normalized gameplay results:

- Damage: two negative-request cases now reject atomically; 31 valid cases remain
  exact source comparisons.
- Progression: dead health, over-maximum health and invalid target maximum reject
  before costs/events/state; 172 other cases remain exact comparisons.
- Return: ten negative/over-maximum strength cases reject atomically; 71 direct
  cases plus dispatcher/save-resume checks retain source gameplay parity.

Run the affected `damage-checks`, `lifecycle-checks`, `progression-checks`,
`transport-checks --verify-guard`, and `scenario-checks` scripts as well. The
aggregate verifier should include this runner with `--verify-guard`; full shipped
level replay remains a separate integration gate.
