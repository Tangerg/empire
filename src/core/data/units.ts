import { Registry } from '../registry';
import type { ArmorClass, DamageType, UnitDef } from '../types';

export const UnitTypes = new Registry<UnitDef>('unit');

function unit(def: Partial<UnitDef> & { id: string; name: string }): UnitDef {
  return {
    cost: 100,
    maxHp: 100,
    attack: 35,
    defense: 0,
    movement: 3,
    movementClass: 'foot',
    damageType: 'slash',
    armorClass: 'light',
    minRange: 1,
    maxRange: 1,
    attackAfterMove: true,
    vision: 3,
    abilities: ['attack', 'capture', 'wait'],
    tags: [],
    blurb: '',
    ...def,
  };
}

/**
 * The Ancient Empires roster. Only "people" can occupy a town, so mounts,
 * constructs and beasts have no `capture` ability — that is the single rule
 * that makes cheap infantry matter all game long.
 */
UnitTypes.defineAll([
  unit({
    id: 'soldier',
    name: '剑士',
    cost: 100,
    maxHp: 100,
    attack: 38,
    defense: 0.1,
    movement: 3,
    damageType: 'slash',
    armorClass: 'light',
    blurb: '廉价的战线核心，能占领城镇。',
    tags: ['infantry'],
  }),
  unit({
    id: 'archer',
    name: '弓箭手',
    cost: 150,
    maxHp: 80,
    attack: 34,
    defense: 0,
    movement: 3,
    damageType: 'pierce',
    armorClass: 'unarmored',
    minRange: 1,
    maxRange: 2,
    blurb: '隔一格射击不会被近战反击，克制飞行单位。',
    tags: ['infantry', 'ranged'],
  }),
  unit({
    id: 'rogue',
    name: '刺客',
    cost: 200,
    maxHp: 80,
    attack: 46,
    defense: 0,
    movement: 5,
    damageType: 'pierce',
    armorClass: 'unarmored',
    blurb: '机动极高，专杀无甲的法师与弓手。',
    tags: ['infantry'],
  }),
  unit({
    id: 'cleric',
    name: '牧师',
    cost: 250,
    maxHp: 70,
    attack: 18,
    defense: 0,
    movement: 3,
    damageType: 'blunt',
    armorClass: 'unarmored',
    abilities: ['heal', 'attack', 'capture', 'wait'],
    blurb: '治疗相邻友军 30 点生命，自身脆弱。',
    tags: ['infantry', 'support'],
  }),
  unit({
    id: 'mage',
    name: '法师',
    cost: 300,
    maxHp: 70,
    attack: 42,
    defense: 0,
    movement: 3,
    damageType: 'magic',
    armorClass: 'unarmored',
    minRange: 1,
    maxRange: 2,
    blurb: '魔法伤害无视重甲，是攻城的答案。',
    tags: ['infantry', 'ranged'],
  }),
  unit({
    id: 'knight',
    name: '骑士',
    cost: 350,
    maxHp: 110,
    attack: 48,
    defense: 0.15,
    movement: 6,
    movementClass: 'mounted',
    damageType: 'slash',
    armorClass: 'heavy',
    abilities: ['attack', 'wait'],
    blurb: '六格突进的重锤，无法占领城镇，怕魔法。',
    tags: ['cavalry'],
  }),
  unit({
    id: 'ogre',
    name: '巨魔',
    cost: 400,
    maxHp: 150,
    attack: 52,
    defense: 0.25,
    movement: 3,
    movementClass: 'heavy',
    damageType: 'blunt',
    armorClass: 'heavy',
    abilities: ['attack', 'wait'],
    blurb: '血厚甲厚的攻坚肉盾，走不上山地。',
    tags: ['monster'],
  }),
  unit({
    id: 'ballista',
    name: '弩车',
    cost: 350,
    maxHp: 70,
    attack: 58,
    defense: 0,
    movement: 2,
    movementClass: 'heavy',
    damageType: 'pierce',
    armorClass: 'light',
    minRange: 2,
    maxRange: 3,
    attackAfterMove: false,
    abilities: ['attack', 'wait'],
    blurb: '攻城器械：必须静止开火，绝不会被反击。',
    tags: ['siege', 'ranged'],
  }),
  unit({
    id: 'dragon',
    name: '巨龙',
    cost: 700,
    maxHp: 160,
    attack: 62,
    defense: 0.2,
    movement: 5,
    movementClass: 'flying',
    damageType: 'magic',
    armorClass: 'flying',
    abilities: ['attack', 'wait'],
    vision: 4,
    blurb: '无视地形飞行，唯一弱点是弓箭。',
    tags: ['monster', 'flying'],
  }),
]);

export const unitDef = (id: string): UnitDef => UnitTypes.get(id);

/* ------------------------------------------------------- damage type chart */

/**
 * Damage-type vs armour-class multipliers. This 4x4 table is the whole
 * rock-paper-scissors layer — tune it here, nowhere else.
 *
 *              unarmored  light  heavy  flying
 *   slash          1.2      1.0    0.7     0.9
 *   pierce         1.0      1.1    0.8     1.4
 *   blunt          1.0      0.9    1.3     0.7
 *   magic          1.1      1.0    1.4     1.2
 */
export const EFFECTIVENESS: Record<DamageType, Record<ArmorClass, number>> = {
  slash: { unarmored: 1.2, light: 1.0, heavy: 0.7, flying: 0.9 },
  pierce: { unarmored: 1.0, light: 1.1, heavy: 0.8, flying: 1.4 },
  blunt: { unarmored: 1.0, light: 0.9, heavy: 1.3, flying: 0.7 },
  magic: { unarmored: 1.1, light: 1.0, heavy: 1.4, flying: 1.2 },
};

export const DAMAGE_TYPE_LABEL: Record<DamageType, string> = {
  slash: '斩击',
  pierce: '穿刺',
  blunt: '钝击',
  magic: '魔法',
};

export const ARMOR_LABEL: Record<ArmorClass, string> = {
  unarmored: '无甲',
  light: '轻甲',
  heavy: '重甲',
  flying: '飞行',
};

export const MOVEMENT_LABEL: Record<string, string> = {
  foot: '步行',
  mounted: '骑乘',
  heavy: '重装',
  flying: '飞行',
};
