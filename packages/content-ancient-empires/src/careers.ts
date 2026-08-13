import { defineCareer, type CareerDef } from '@empire/battle-engine';

/** Demonstration tree; story packs may replace names and graph without changing the engine. */
export const ANCIENT_EMPIRES_CAREERS: readonly CareerDef[] = [
  defineCareer({ id: 'militia', name: '民兵', unitType: 'soldier', masteryThreshold: 120 }),
  defineCareer({
    id: 'ranger', name: '游侠', unitType: 'archer', tier: 1, from: ['militia'],
    minimumRank: 1, minimumMastery: 120, masteryThreshold: 220,
  }),
  defineCareer({
    id: 'shadow', name: '影刃', unitType: 'rogue', tier: 1, from: ['militia'],
    minimumRank: 1, minimumMastery: 120, masteryThreshold: 220,
  }),
  defineCareer({
    id: 'acolyte', name: '侍祭', unitType: 'cleric', tier: 1, from: ['militia'],
    minimumRank: 1, minimumMastery: 120, masteryThreshold: 220, masteryAbilities: ['heal'],
  }),
  defineCareer({
    id: 'knight-order', name: '骑士', unitType: 'knight', tier: 2, from: ['militia', 'ranger'],
    minimumRank: 2, minimumMastery: 220, masteryThreshold: 320,
  }),
  defineCareer({
    id: 'war-mage', name: '战斗法师', unitType: 'mage', tier: 2, from: ['acolyte'],
    minimumRank: 2, minimumMastery: 220, masteryThreshold: 320,
  }),
  defineCareer({
    id: 'siege-engineer', name: '攻城技师', unitType: 'ballista', tier: 2, from: ['ranger'],
    minimumRank: 2, minimumMastery: 220, masteryThreshold: 320,
  }),
  defineCareer({ id: 'ogre-warrior', name: '巨魔战士', unitType: 'ogre', masteryThreshold: 180 }),
  defineCareer({
    id: 'dragon-lord', name: '龙骑领主', unitType: 'dragon', tier: 2, from: ['ogre-warrior'],
    minimumRank: 2, minimumMastery: 180, masteryThreshold: 400,
  }),
];
