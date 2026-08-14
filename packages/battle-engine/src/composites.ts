import { BattleAggregate } from './domain/battle-aggregate';
import { inBounds } from './grid';
import { structureAt } from './structures';
import type { ContentCatalog } from './content-pack';
import type { CompositeState, Coord, GameEvent, GameState } from './types';

export interface CompositeStatus {
  total: number;
  neutralized: number;
  state: 'intact' | 'damaged' | 'neutralized';
}

export function requireComposite(state: GameState, id: string): CompositeState {
  const composite = state.composites.find((candidate) => candidate.id === id);
  if (!composite) throw new Error(`unknown composite "${id}"`);
  return composite;
}

export function compositeStatus(state: GameState, id: string): CompositeStatus {
  const composite = requireComposite(state, id);
  const neutralized = composite.parts.filter((part) => {
    const structure = state.structures.find((candidate) => candidate.id === part);
    return !structure || structure.hp <= 0 || structure.disabled;
  }).length;
  return {
    total: composite.parts.length,
    neutralized,
    state: neutralized >= composite.minimumNeutralized
      ? 'neutralized'
      : neutralized > 0 ? 'damaged' : 'intact',
  };
}

/** Atomically translates every surviving part of a composite battlefield entity. */
export function moveComposite(
  content: ContentCatalog,
  state: GameState,
  id: string,
  delta: Coord,
  emit: (event: GameEvent) => void,
): void {
  const composite = requireComposite(state, id);
  const moving = state.structures.filter((structure) => composite.parts.includes(structure.id) && structure.hp > 0);
  const movingIds = new Set(moving.map((structure) => structure.id));
  const destinations = moving.map((structure) => ({
    structure,
    from: { x: structure.x, y: structure.y },
    to: { x: structure.x + Math.round(delta.x), y: structure.y + Math.round(delta.y) },
  }));
  for (const destination of destinations) {
    if (!inBounds(state.map, destination.to.x, destination.to.y)) {
      throw new Error(`composite "${id}" would move out of bounds`);
    }
    if (state.units.some((unit) => unit.x === destination.to.x && unit.y === destination.to.y)) {
      throw new Error(`composite "${id}" would collide with a unit`);
    }
    const occupied = structureAt(state, destination.to.x, destination.to.y);
    if (occupied && !movingIds.has(occupied.id)) throw new Error(`composite "${id}" would collide with structure "${occupied.id}"`);
  }
  const unique = new Set(destinations.map(({ to }) => `${to.x},${to.y}`));
  if (unique.size !== destinations.length) throw new Error(`composite "${id}" has overlapping destination parts`);
  for (const destination of destinations) {
    new BattleAggregate(state, content).structure(destination.structure.id).moveTo(destination.to);
    emit({ type: 'structureMoved', structure: destination.structure.id, from: destination.from, to: destination.to });
  }
}
