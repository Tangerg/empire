import { IllegalActionError } from './domain/errors';
import { boardOf } from './domain/board';
import { areAllies } from './state';
import type { ContentCatalog } from './content-pack';
import type { FormationDef, FormationId, GameState, Unit } from './types';
import type { GridRules } from './tactical-grid';

/** Port declared by this module; `BattleRuleServices` satisfies it. */
export interface FormationRules extends GridRules {
  readonly content: ContentCatalog;
}

/**
 * Would this formation contribute, standing where this unit stands?
 *
 * Asked of a formation rather than of the unit's current one, because the
 * question the change action needs is hypothetical. It used to be answered by
 * committing: `validateFormationChange` wrote the requested formation onto the
 * live unit, asked `activeFormation`, and wrote the old one back — a query that
 * mutated the battle, and one thrown error away from leaving a rejected order
 * applied.
 */
export function formationInEffect(
  rules: FormationRules,
  state: GameState,
  unit: Unit,
  formation: FormationId | null,
): FormationDef | null {
  if (!formation) return null;
  const definition = rules.content.formations.tryGet(formation);
  if (!definition) return null;
  const board = boardOf(rules, state);
  const adjacentAllies = state.units.filter((candidate) =>
    candidate.id !== unit.id &&
    areAllies(state, candidate.owner, unit.owner) &&
    board.distance(candidate, unit) === 1).length;
  return adjacentAllies >= definition.minimumAdjacentAllies ? definition : null;
}

/** The formation actually in effect for this unit. */
export const activeFormation = (
  rules: FormationRules,
  state: GameState,
  unit: Unit,
): FormationDef | null => formationInEffect(rules, state, unit, unit.formation);

/** One shape this unit may take, and whether it would hold where it stands. */
export interface FormationOption {
  formation: FormationDef;
  /** The shape it is in right now. */
  current: boolean;
  /** Its spatial invariant holds here, so ordering it would take effect. */
  eligible: boolean;
  reasons: string[];
}

/**
 * The formations a unit may be ordered into, in the order its type declares.
 *
 * The menu counterpart of `validateFormationChange`, and the reason it can
 * exist: asking whether a shape would hold no longer requires putting it on.
 * Without this the rules had formations, a dozen shipped unit types declared
 * them, and no interface could reach one.
 */
export function formationOptions(
  rules: FormationRules,
  state: GameState,
  unit: Unit,
): FormationOption[] {
  return (rules.content.units.get(unit.type).formations ?? [])
    .flatMap((id) => {
      const formation = rules.content.formations.tryGet(id);
      if (!formation) return [];
      const eligible = formationInEffect(rules, state, unit, id) !== null;
      return [{
        formation,
        current: unit.formation === id,
        eligible,
        reasons: eligible ? [] : [`需要 ${formation.minimumAdjacentAllies} 名相邻友军`],
      }];
    });
}

export function validateFormationChange(
  rules: FormationRules,
  state: GameState,
  unit: Unit,
  formation: FormationId | null,
): void {
  if (formation === null) return;
  const allowed = rules.content.units.get(unit.type).formations ?? [];
  if (!allowed.includes(formation)) throw new IllegalActionError(`unit ${unit.id} cannot use formation "${formation}"`);
  if (!formationInEffect(rules, state, unit, formation)) {
    throw new IllegalActionError(`formation "${formation}" lacks adjacent allied units`);
  }
}

export function formationMovementDelta(
  rules: FormationRules,
  state: GameState,
  unit: Unit,
): number {
  return activeFormation(rules, state, unit)?.movementDelta ?? 0;
}
