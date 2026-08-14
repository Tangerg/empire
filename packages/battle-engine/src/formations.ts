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

/** A formation only contributes while its spatial invariant is satisfied. */
export function activeFormation(
  rules: FormationRules,
  state: GameState,
  unit: Unit,
): FormationDef | null {
  if (!unit.formation) return null;
  const definition = rules.content.formations.tryGet(unit.formation);
  if (!definition) return null;
  const board = boardOf(rules, state);
  const adjacentAllies = state.units.filter((candidate) =>
    candidate.id !== unit.id &&
    areAllies(state, candidate.owner, unit.owner) &&
    board.distance(candidate, unit) === 1).length;
  return adjacentAllies >= definition.minimumAdjacentAllies ? definition : null;
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
  const previous = unit.formation;
  unit.formation = formation;
  const active = activeFormation(rules, state, unit);
  unit.formation = previous;
  if (!active) throw new IllegalActionError(`formation "${formation}" lacks adjacent allied units`);
}

export function formationMovementDelta(
  rules: FormationRules,
  state: GameState,
  unit: Unit,
): number {
  return activeFormation(rules, state, unit)?.movementDelta ?? 0;
}
