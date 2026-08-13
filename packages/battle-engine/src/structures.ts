import { StructureEntity } from './domain/structure-entity';
import type { GameEvent, GameState, StructureId, StructureState } from './types';
import { GlobalContentCatalog, type ContentCatalog } from './content-pack';

export function structureById(state: GameState, id: StructureId): StructureState | undefined {
  return state.structures.find((structure) => structure.id === id);
}

export function requireStructure(state: GameState, id: StructureId): StructureState {
  const structure = structureById(state, id);
  if (!structure) throw new Error(`unknown structure "${id}"`);
  return structure;
}

export function structureAt(state: GameState, x: number, y: number): StructureState | undefined {
  return state.structures.find((structure) => structure.x === x && structure.y === y && structure.hp > 0);
}

export function damageStructure(
  state: GameState,
  id: StructureId,
  rawAmount: number,
  emit?: (event: GameEvent) => void,
  content: ContentCatalog = GlobalContentCatalog,
): number {
  const structure = requireStructure(state, id);
  if (structure.hp <= 0) return 0;
  const result = new StructureEntity(structure, content.structures.get(structure.type)).takeRawDamage(rawAmount);
  emit?.({ type: 'structureDamaged', structure: id, amount: result.amount, hpAfter: result.hpAfter });
  if (result.destroyed) {
    emit?.({ type: 'structureDestroyed', structure: id, at: { x: structure.x, y: structure.y } });
  }
  return result.amount;
}

export function repairStructure(
  state: GameState,
  id: StructureId,
  requested: number,
  emit?: (event: GameEvent) => void,
  content: ContentCatalog = GlobalContentCatalog,
): number {
  const structure = requireStructure(state, id);
  const amount = new StructureEntity(structure, content.structures.get(structure.type)).repair(requested);
  if (amount <= 0) return 0;
  emit?.({ type: 'structureRepaired', structure: id, amount, hpAfter: structure.hp });
  return amount;
}
