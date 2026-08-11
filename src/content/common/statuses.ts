import { defineStatus } from '../../core/content-builders';
import type { StatusDef } from '../../core/types';

/** Semantic status ids shared by fantasy, stellar and historical presentation. */
export const COMMON_STATUSES: readonly StatusDef[] = [
  defineStatus({
    id: 'poisoned',
    name: '中毒',
    periodic: { timing: 'ownerTurnStart', maxHpFraction: 0.05, nonlethal: true },
    tags: ['damage-over-time'],
  }),
  defineStatus({
    id: 'armor_down',
    name: '破甲',
    modifiers: { defenseDelta: -0.2 },
    tags: ['debuff'],
  }),
  defineStatus({
    id: 'shaken',
    name: '动摇',
    modifiers: { movementDelta: -1, attackMultiplier: 0.9, cannotCapture: true },
    tags: ['morale', 'debuff'],
  }),
  defineStatus({
    id: 'inspired',
    name: '鼓舞',
    modifiers: { attackMultiplier: 1.1 },
    tags: ['morale', 'buff'],
  }),
  defineStatus({
    id: 'silenced',
    name: '沉默',
    blockedAbilityTags: ['arcane', 'spell'],
    tags: ['control', 'debuff'],
  }),
];
