import { defineStructure, type StructureDef } from '@empire/battle-engine';

/** Function-oriented structure primitives; each theme may rename their presentation. */
export const COMMON_STRUCTURES: readonly StructureDef[] = [
  defineStructure({ id: 'gate', name: '城门', maxHp: 140, defense: 0.2, blocksVision: true, cover: 'full', obstructionHeight: 3, tags: ['building', 'gate'] }),
  defineStructure({
    id: 'command_node',
    name: '指挥节点',
    maxHp: 100,
    blocksMovement: false,
    cover: 'half',
    obstructionHeight: 1,
    tags: ['building', 'node'],
  }),
  defineStructure({
    id: 'depot',
    name: '补给设施',
    maxHp: 90,
    blocksMovement: false,
    cover: 'half',
    obstructionHeight: 1,
    tags: ['building', 'supply'],
  }),
  defineStructure({ id: 'bulkhead', name: '隔断', maxHp: 120, defense: 0.15, blocksVision: true, cover: 'full', obstructionHeight: 3, tags: ['blocking'] }),
  defineStructure({
    id: 'boss_part',
    name: '大型目标部位',
    maxHp: 180,
    defense: 0.2,
    blocksMovement: false,
    cover: 'half',
    obstructionHeight: 2,
    repairable: false,
    tags: ['boss', 'part'],
  }),
];
