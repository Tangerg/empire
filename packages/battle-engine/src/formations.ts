import { IllegalActionError } from './domain/errors';
import { dist } from './grid';
import { areAllies } from './state';
import type { ContentCatalog } from './content-pack';
import type { FormationDef, FormationId, GameState, Unit } from './types';

/** A formation only contributes while its spatial invariant is satisfied. */
export function activeFormation(
  state: GameState,
  unit: Unit,
  content: ContentCatalog,
): FormationDef | null {
  if (!unit.formation) return null;
  const definition = content.formations.tryGet(unit.formation);
  if (!definition) return null;
  const adjacentAllies = state.units.filter((candidate) =>
    candidate.id !== unit.id &&
    areAllies(state, candidate.owner, unit.owner) &&
    dist(candidate, unit) === 1).length;
  return adjacentAllies >= definition.minimumAdjacentAllies ? definition : null;
}

export function validateFormationChange(
  state: GameState,
  unit: Unit,
  formation: FormationId | null,
  content: ContentCatalog,
): void {
  if (formation === null) return;
  const allowed = content.units.get(unit.type).formations ?? [];
  if (!allowed.includes(formation)) throw new IllegalActionError(`unit ${unit.id} cannot use formation "${formation}"`);
  const previous = unit.formation;
  unit.formation = formation;
  const active = activeFormation(state, unit, content);
  unit.formation = previous;
  if (!active) throw new IllegalActionError(`formation "${formation}" lacks adjacent allied units`);
}

export function formationMovementDelta(
  state: GameState,
  unit: Unit,
  content: ContentCatalog,
): number {
  return activeFormation(state, unit, content)?.movementDelta ?? 0;
}
