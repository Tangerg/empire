import { defineStructure } from '../../core/content-builders';
import type { StructureDef } from '../../core/types';

export const CANDIDATE_01_STRUCTURES: readonly StructureDef[] = [
  defineStructure({
    id: 'c01.mother-root',
    name: '银林母根',
    maxHp: 500,
    defense: 0.35,
    blocksMovement: false,
    blocksVision: false,
    cover: 'full',
    obstructionHeight: 2,
    repairable: true,
    targetable: true,
    tags: ['building', 'silverwood', 'sacred'],
  }),
];
